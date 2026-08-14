# Deploying the Zava DIY MCP Server to Azure App Service

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) installed
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) installed
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- An Azure subscription
- The PostgreSQL database already provisioned (connection string required)

## Steps

### 1. Log in to Azure

```bash
azd auth login
```

### 2. Create the `azure.yaml` project file

Create an `azure.yaml` in the repository root to define the azd project and service mapping:

```yaml
name: zava-diy-mcp-server
metadata:
  template: azd-starter
services:
  mcp-server:
    project: ./ZavaDiyMcpServer
    host: appservice
    language: dotnet
```

### 3. Create the Bicep infrastructure templates

Create the `infra/` directory and add the following files:

**`infra/main.bicep`**

```bicep
targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the environment (used for generating resource names)')
param environmentName string

@description('Primary location for all resources')
param location string

@secure()
@description('PostgreSQL connection string')
param postgresConnectionString string

var abbrs = {
  appServicePlan: 'asp-'
  webApp: 'app-'
}

var resourceToken = uniqueString(resourceGroup().id, environmentName)

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${abbrs.appServicePlan}${resourceToken}'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${abbrs.webApp}${resourceToken}'
  location: location
  tags: {
    'azd-service-name': 'mcp-server'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      connectionStrings: [
        {
          name: 'PostgreSQL'
          connectionString: postgresConnectionString
          type: 'Custom'
        }
      ]
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
    }
  }
}

output WEBAPP_NAME string = webApp.name
output WEBAPP_URL string = 'https://${webApp.properties.defaultHostName}'
```

**`infra/main.parameters.json`**

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": {
      "value": "${AZURE_ENV_NAME}"
    },
    "location": {
      "value": "${AZURE_LOCATION}"
    },
    "postgresConnectionString": {
      "value": "$(secretOrEnv POSTGRES_CONNECTION_STRING)"
    }
  }
}
```

### 4. Initialize the environment

```bash
cd WorkshopTest
azd init --environment zava-mcp --location westus --no-prompt
```

### 5. Set required environment variables

```bash
azd env set AZURE_SUBSCRIPTION_ID "<your-subscription-id>"
azd env set AZURE_RESOURCE_GROUP "rg-zava-agent-wks-6b5c"
azd env set POSTGRES_CONNECTION_STRING "Host=<your-server>.postgres.database.azure.com;Database=zava;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

> To find your subscription ID: `az account show --query id -o tsv`

### 6. Provision and deploy

```bash
azd up --no-prompt
```

This single command will:
1. Build and package the ZavaDiyMcpServer project
2. Provision the App Service Plan and Web App via Bicep
3. Deploy the application code

### 7. Verify the deployment

Once deployment completes, azd prints the endpoint URL. Test it with:

```bash
curl -X POST https://<your-app-name>.azurewebsites.net/ \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

A successful response looks like:

```
event: message
data: {"result":{"protocolVersion":"2025-03-26","capabilities":{"logging":{},"tools":{}},"serverInfo":{"name":"ZavaDiyMcpServer","version":"1.0.0.0"}},"id":1,"jsonrpc":"2.0"}
```

## Subsequent deployments

After the first `azd up`, you can redeploy code-only changes without reprovisioning:

```bash
azd deploy
```

## Tearing down resources

To remove all provisioned Azure resources:

```bash
azd down --purge
```
