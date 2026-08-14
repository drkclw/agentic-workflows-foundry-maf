# Creating the order specialist agent in Foundry

Step-by-step instructions to create the Zava order specialist agent in Foundry.

---

## Prerequisites

- Azure subscription

---

## Step 1: Create a resource group in Azure

NOTE: If you already have a resource group, you can skip this step.

## Step 2: Create a Microsoft Foundry resource

1. In the Azure portal, click **Create a resource**.
2. Search for "Microsoft Foundry" and select it from the list.

![Foundry resource](../../images/foundry-specialist/create-foundry-resource.png)

3. Fill out the required fields in the "Basics" tab.
4. Click "Review and Create".
5. Click "Create".
6. After the deployment completes, click "Go to resource".
7. Once the resource page loads, click on "Go to Foundry portal".

## Step 3: Deploy a language model

1. In the home page of the Foundry portal, click on "Build".

![Foundry resource](../../images/foundry-specialist/foundry-build.png)

2. In the "Model" tab, click on "Deploy" and "Deploy a base model".

![Foundry resource](../../images/foundry-specialist/deploy-base-model.png)

3. Search for the name model you want to deploy or use the filters on the page.

![Foundry resource](../../images/foundry-specialist/open-ai-model.png)

4. After selecting a model, click on the "Deploy" and select "Default Settigs" (if you want to customize the settings, you can do so by clicking on "Custom Settings").

![Foundry resource](../../images/foundry-specialist/deploy-default-settings.png)

Your deployed model should now appear in the "Models" tab of the Foundry portal.

## Step 4: Add the Zava MCP server as a tool

1. In the Foundry portal, click on "Tools" in the left navigation menu.

2. Click on the "Connect a tool" button. If this is not your first tool, you will see a "Connect a tool" button in the upper right hand corner of the page.

![Foundry resource](../../images/foundry-specialist/connect-tool.png)

3. Select the "Custom" tab in the tool catalog, select MCP server and click on "Create".

![Foundry resource](../../images/foundry-specialist/create-mcp-tool.png)

4. Enter the server settings (select "Unauthenticated" for authentication for now) and click "Connect".

![Foundry resource](../../images/foundry-specialist/mcp-server-settings.png)

## Step 5: Create the agent

1. In the Foundry portal, click on "Agents" in the left navigation menu.

![Foundry resource](../../images/foundry-specialist/deploy-default-settings.png)

2. Click on "New Agent" and "Build an agent".

![Foundry resource](../../images/foundry-specialist/build-agent.png)

## Step 6: Add tools to the agent

1. In the "Tools" section, remove the "Web search" tool.

![Foundry resource](../../images/foundry-specialist/remove-web-search.png)

2. In the "Tools" section , click on "Add" and "Add tools".

![Foundry resource](../../images/foundry-specialist/add-tools-to-agents.png)

3. In the "Select a tool" tab, scroll down and select the MCP server tool you created in Step 4 and click "Add tool".

![Foundry resource](../../images/foundry-specialist/zava-mcp-server-catalog.png)

## Step 7: Add instructions and test the agent

1. In the "Instructions" section, enter the agent prompt

![Foundry resource](../../images/foundry-specialist/agent-instructions.png)

2. On the right panel, ask the agent a question

3. Depending on the question asked, the agent might ask permission to call a tool, click on "Approve" and selection your preferred option.

![Foundry resource](../../images/foundry-specialist/approve-tool.png)

## Step 8: Explore conversation traces

1. After a few interactions with the agent, click on the "Traces" tab or the "Traces" button under the conversation to look at the steps the agent took to generate a response

![Foundry resource](../../images/foundry-specialist/conversation-trace.png)

## Step 9: Connect application insights resource

1. Select the "Traces" tab and select "Connect".

![Foundry resource](../../images/foundry-specialist/connect-app-insights.png)

2. Create a new application insights resource (or select an existing one if you have one).

![Foundry resource](../../images/foundry-specialist/new-app-insights.png)

3. Enter the resource settings and click "Create".

![Foundry resource](../../images/foundry-specialist/app-insights-settings.png)

4. Interact with the agent again, once you have a few interactions, go back to the "Traces" tab, the "Trace view" and "Conversation view" sections should now be populated with data.

![Foundry resource](../../images/foundry-specialist/trace-view.png)

## Step 9: Create an agent evaluation

1. Go to the "Evaluations" tab and click on "Create".

![Foundry resource](../../images/foundry-specialist/create-evaluation.png)

2. Select "Agent" as the evaluation target.

![Foundry resource](../../images/foundry-specialist/evaluation-target.png)

3. Select "Full conversations" as the evaluation scope.

![Foundry resource](../../images/foundry-specialist/evaluation-scope.png)

4. For "Frequency", select "One time".

5. For "Conversation data", select "Existing conversations" (make sure you have interacted with your agent several times.

6. In the "Field mapping" section, make sure to select a judge model that is different from the one the agent is using, as well as selecting a value under "Tool definitions"

![Foundry resource](../../images/foundry-specialist/evaluation-field-mapping.png)

7. Use the default values under criteria and hit "Next".

8. Provide a value for the evaluation name and click "Submit".



