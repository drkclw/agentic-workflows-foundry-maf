# Create the OrderReturnWorkflow Project

This guide walks through creating a multi-agent workflow console application that processes order returns using Azure OpenAI and an MCP server for tool access.

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource with a deployed model (e.g., `gpt-4o`)
- The ZavaDiyMcpServer project running locally (or deployed)
- Azure CLI logged in (for `DefaultAzureCredential`)

## Step 1: Create the Console Project

```bash
dotnet new console -n OrderReturnWorkflow --framework net10.0
cd OrderReturnWorkflow
```

## Step 2: Add NuGet Packages

```bash
dotnet add package Azure.AI.OpenAI --version 2.9.0-beta.1
dotnet add package Azure.Identity --version 1.21.0
dotnet add package Microsoft.Agents.AI.Workflows --version 1.17.0
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.8.3
dotnet add package ModelContextProtocol --version 2.1.0
```

## Step 3: Replace Program.cs

Replace the contents of `Program.cs` with the following code.

### 3a. Add Usings and Configuration

```csharp
using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

// --- Configuration ---
var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT environment variable.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o";
var mcpServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:5000/mcp";
```

### 3b. Set Up the Azure OpenAI Chat Client

```csharp
// --- 1. Set up the Azure OpenAI chat client ---
IChatClient chatClient = new AzureOpenAIClient(new Uri(azureOpenAiEndpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

### 3c. Connect to the MCP Server and Retrieve Tools

```csharp
// --- 2. Connect to the Zava DIY MCP server and retrieve tools ---
Console.WriteLine($"Connecting to MCP server at {mcpServerUrl}...");

await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        Name = "ZavaDiyMcpServer"
    }));

IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Discovered {mcpTools.Count} tools from MCP server.");

// Partition MCP tools by agent responsibility
var orderTools = mcpTools.Where(t => t.Name is "GetOrders" or "GetProductInfo").ToList();
var inventoryTools = mcpTools.Where(t => t.Name is "CheckInventory").ToList();
```

### 3d. Define the Local Refund Calculation Tool

```csharp
// --- 3. Define local tool for refund calculation ---
static string CalculateRefund(
    [Description("JSON array of items being returned, each with product_name, quantity, unit_price, and discount fields")]
    string returnItems,
    [Description("Whether the return is within the 30-day return window")]
    bool withinReturnWindow)
{
    if (!withinReturnWindow)
    {
        return """{"eligible": false, "reason": "Return is outside the 30-day return window. Only store credit may be offered at manager discretion."}""";
    }

    using var doc = System.Text.Json.JsonDocument.Parse(returnItems);
    var items = doc.RootElement.EnumerateArray();
    decimal totalRefund = 0;
    var breakdown = new List<string>();

    foreach (var item in items)
    {
        var name = item.GetProperty("product_name").GetString()!;
        var qty = item.GetProperty("quantity").GetInt32();
        var price = item.GetProperty("unit_price").GetDecimal();
        var discount = item.TryGetProperty("discount", out var d) ? d.GetDecimal() : 0m;

        var lineRefund = Math.Round(qty * price * (1 - discount), 2);
        totalRefund += lineRefund;
        breakdown.Add($"  {name}: {qty} x ${price} (discount {discount:P0}) = ${lineRefund}");
    }

    return $$"""
        {
          "eligible": true,
          "total_refund": {{totalRefund}},
          "refund_method": "original_payment",
          "breakdown": {{System.Text.Json.JsonSerializer.Serialize(breakdown)}}
        }
        """;
}
```

### 3e. Create Specialized Agents

```csharp
// --- 4. Create specialized agents ---
ChatClientAgent orderRetriever = new(
    chatClient,
    """
    You are the Order Retriever agent for Zava DIY retail stores.
    Your job is to:
    1. Retrieve the customer's recent order using the GetOrders tool
    2. Present the order details (products, quantities, prices, dates)
    3. Validate return eligibility:
       - Items must be within 30 days of purchase
       - Cut lumber and mixed paint are NON-RETURNABLE
    4. Once you have identified the order and confirmed which items are eligible for return,
       hand off to the refund_calculator agent with a summary of eligible items.
    
    If the customer or order cannot be found, inform the user and do NOT hand off.
    """,
    "order_retriever",
    "Retrieves and validates order details for return eligibility",
    [.. orderTools]);

ChatClientAgent refundCalculator = new(
    chatClient,
    """
    You are the Refund Calculator agent for Zava DIY retail stores.
    Your job is to:
    1. Receive eligible return items from the order retriever
    2. Use the CalculateRefund tool to compute the refund amount for each item
    3. Present the refund breakdown to the user including:
       - Per-item refund amounts
       - Total refund
       - Refund method (original payment method for in-window returns, store credit otherwise)
    4. Once the refund calculation is complete, hand off to the inventory_restorer agent
       with the list of items being returned and their quantities.
    
    The store maintains a 33% gross margin. Refunds are based on the price actually paid.
    """,
    "refund_calculator",
    "Calculates refund amounts and determines refund method",
    [AIFunctionFactory.Create(CalculateRefund)]);

ChatClientAgent inventoryRestorer = new(
    chatClient,
    """
    You are the Inventory Restorer agent for Zava DIY retail stores.
    Your job is to:
    1. Receive the list of returned items and their quantities
    2. Use the CheckInventory tool to look up current stock levels at the originating store
    3. Determine whether returned items should be:
       - Restocked at the originating store (if stock is below average)
       - Flagged for redistribution to another store with lower stock
    4. Produce a final return summary including:
       - Items returned
       - Refund amount (from the previous agent's calculation)
       - Restocking decision for each item
    5. Present the complete return authorization to the user.
    
    This is the final step. Do NOT hand off to another agent. Provide the complete summary.
    """,
    "inventory_restorer",
    "Determines restocking decisions for returned items",
    [.. inventoryTools]);
```

### 3f. Build the Handoff Workflow

```csharp
// --- 5. Build the handoff workflow ---
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(orderRetriever)
    .WithHandoffs(orderRetriever, [refundCalculator])
    .WithHandoffs(refundCalculator, [inventoryRestorer])
    .WithHandoffs(inventoryRestorer, [orderRetriever]) // allow cycling back if needed
    .WithAutonomousMode(turnLimit: 10)
    .WithTerminationCondition(conversation =>
        conversation.Any(m => m.Text?.Contains("Return Authorization", StringComparison.OrdinalIgnoreCase) == true))
    .Build();
```

### 3g. Run the Interactive Conversation Loop

```csharp
// --- 6. Run the interactive conversation loop ---
Console.WriteLine();
Console.WriteLine("=== Zava DIY Order Return & Refund Processing ===");
Console.WriteLine("Type your request (e.g., 'Customer #100 wants to return items from their last order')");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine();

List<ChatMessage> messages = [];

while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    messages.Add(new(ChatRole.User, userInput));

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    string? lastAgent = null;
    List<ChatMessage> newMessages = [];

    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is AgentResponseUpdateEvent update)
        {
            if (update.ExecutorId != lastAgent)
            {
                lastAgent = update.ExecutorId;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{lastAgent}]");
                Console.ResetColor();
            }
            Console.Write(update.Update.Text);
        }
        else if (evt is WorkflowOutputEvent outputEvt)
        {
            newMessages = outputEvt.As<List<ChatMessage>>()!;
            break;
        }
    }

    Console.WriteLine();
    Console.WriteLine();
    messages.AddRange(newMessages.Skip(messages.Count));
}

Console.WriteLine("Goodbye!");
```

## Step 4: Set Environment Variables

Before running, set the required environment variables:

```bash
# Required
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/

# Optional (defaults shown)
set AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
set MCP_SERVER_URL=http://localhost:5000/mcp
```

## Step 5: Run the Application

Make sure the ZavaDiyMcpServer is running first, then:

```bash
dotnet run
```

You should see it connect to the MCP server, discover tools, and present an interactive prompt. Try a request like:

```
Customer #100 wants to return items from their last order
```

## How It Works

The workflow uses three specialized agents connected via handoffs:

1. **Order Retriever** — Looks up the customer's order, validates return eligibility (30-day window, no cut lumber/mixed paint), then hands off eligible items to the refund calculator.
2. **Refund Calculator** — Computes the refund amount using a local `CalculateRefund` tool, then hands off to the inventory restorer.
3. **Inventory Restorer** — Checks current stock levels and decides whether to restock locally or redistribute, then presents the final Return Authorization.

The workflow terminates automatically when the response contains "Return Authorization" or after 10 agent turns.
