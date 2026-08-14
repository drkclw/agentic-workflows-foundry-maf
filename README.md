# Zava DIY AI Workshop — What You'll Build

This workshop takes you step by step throughthe process of building an agentic workflow for **Zava DIY**, a fictional hardware chain with 8 stores across Washington State. The sytem has the following components:

- An MCP server built in C# that connects to a PostgreSQL database and exposes retail data as tools that AI agents can discover and call.
- A single foundry agent that connects to the MCP server and answers natural language questions about orders, products, inventory, and sales.
- A multi-agent handoff workflow that processes order returns end-to-end using three specialized agents.

---

## Prerequisites

- .NET 10 SDK
- Azure CLI & Azure Developer CLI (azd)
- Azure subscription with an Azure OpenAI resource
- Access to the workshop PostgreSQL database
- IDE

---

## Part 1: MCP Server — ZavaDiyMcpServer

Build a **.NET MCP (Model Context Protocol) server** that connects to a PostgreSQL database and exposes retail data as tools that AI agents can discover and call.

**Tools you'll create:**

| Tool | Description |
|------|-------------|
| `CheckInventory` | Query store-specific stock levels by store and/or product |
| `GetOrders` | Retrieve orders filtered by customer, store, or date range |
| `GetProductInfo` | Look up product details by ID, SKU, or category |
| `SearchProducts` | Text search across product names and descriptions |

**Key technologies:** ASP.NET Core, ModelContextProtocol.AspNetCore, Npgsql, PostgreSQL

For a step by step guide, see the [MCP Server](./dotnet/mcp-server/CreateMcpServer.md) section.

---

## Part 2: Order Specialist Agent

Create a **single agent** that connects to the MCP server and answers natural language questions about orders, products, inventory, and sales.

a. Follow the [Order Specialist Agent (Foundry)](./foundry/order-specialist/OrderSpecialistAgent.md) guide to build the agent in Foundry.

**Key technologies:** Azure OpenAI, Microsoft Foundry

## Part 3: Order Return Workflow — OrderReturnWorkflow

Build a **multi-agent workflow** that processes order returns end-to-end using three specialized agents:

1. **Order Retriever** — Looks up the customer's order, validates return eligibility (30-day window, no cut lumber or mixed paint), and hands off eligible items.
2. **Refund Calculator** — Computes refund amounts using a local tool, determines refund method, and hands off to inventory.
3. **Inventory Restorer** — Checks current stock levels and decides whether to restock locally or redistribute to another store, then produces a final Return Authorization.

The agents hand off work automatically and the workflow terminates when a "return authorization" is generated or after 10 agent turns.

For a step by step guide, see the [Order Return Workflow](./dotnet/order-return-workflow/OrderReturnWorkflow.md) section.

**Key technologies:** Microsoft Agent Framework, Azure OpenAI, MCP Client