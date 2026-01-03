targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the resource group')
param resourceGroupName string = ''

@description('Name of the App Service Plan')
param appServicePlanName string = ''

@description('Name of the API App Service')
param apiServiceName string = ''

@description('Name of the Static Web App')
param webServiceName string = ''

@description('Name of the Key Vault')
param keyVaultName string = ''

@description('Name of the Log Analytics workspace')
param logAnalyticsName string = ''

@description('Name of the Application Insights instance')
param applicationInsightsName string = ''

@description('Name of the Azure OpenAI service')
param openAiServiceName string = ''

@description('Name of the Storage Account')
param storageAccountName string = ''

@description('OpenAI model deployment name')
param openAiModelDeploymentName string = 'gpt-4o'

@description('OpenAI model name')
param openAiModelName string = 'gpt-4o'

@description('OpenAI model version')
param openAiModelVersion string = '2024-08-06'

@description('Id of the user or app to assign application roles')
param principalId string = ''

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Monitor application with Azure Monitor
module monitoring './core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    tags: tags
    logAnalyticsName: !empty(logAnalyticsName) ? logAnalyticsName : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName) ? applicationInsightsName : '${abbrs.insightsComponents}${resourceToken}'
  }
}

// Store secrets in a keyvault
module keyVault './core/security/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(keyVaultName) ? keyVaultName : '${abbrs.keyVaultVaults}${resourceToken}'
    principalId: principalId
  }
}

// Create storage account for documents
module storage './core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(storageAccountName) ? storageAccountName : '${abbrs.storageStorageAccounts}${resourceToken}'
    containers: [
      { name: 'documents' }
      { name: 'exports' }
    ]
  }
}

// Create an App Service Plan to group applications under the same payment plan and region
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(appServicePlanName) ? appServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
    sku: {
      name: 'B1'
      tier: 'Basic'
    }
    kind: 'linux'
  }
}

// The API backend
module api './app/api.bicep' = {
  name: 'api'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(apiServiceName) ? apiServiceName : '${abbrs.webSitesAppService}api-${resourceToken}'
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnetcore'
    runtimeVersion: '10.0'
    appSettings: {
      APPLICATIONINSIGHTS_CONNECTION_STRING: monitoring.outputs.applicationInsightsConnectionString
      AZURE_KEY_VAULT_ENDPOINT: keyVault.outputs.endpoint
      AZURE_STORAGE_ACCOUNT_NAME: storage.outputs.name
      AZURE_OPENAI_ENDPOINT: openAi.outputs.endpoint
      AZURE_OPENAI_DEPLOYMENT_NAME: openAiModelDeploymentName
    }
    keyVaultName: keyVault.outputs.name
  }
}

// The web frontend (Static Web App)
module web './app/web.bicep' = {
  name: 'web'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(webServiceName) ? webServiceName : '${abbrs.webStaticSites}${resourceToken}'
    apiBaseUrl: api.outputs.uri
  }
}

// Azure OpenAI service
module openAi './core/ai/cognitiveservices.bicep' = {
  name: 'openai'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(openAiServiceName) ? openAiServiceName : '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    kind: 'OpenAI'
    sku: {
      name: 'S0'
    }
    deployments: [
      {
        name: openAiModelDeploymentName
        model: {
          format: 'OpenAI'
          name: openAiModelName
          version: openAiModelVersion
        }
        sku: {
          name: 'Standard'
          capacity: 30
        }
      }
    ]
  }
}

// Grant API access to Key Vault secrets
module apiKeyVaultAccess './core/security/keyvault-access.bicep' = {
  name: 'api-keyvault-access'
  scope: rg
  params: {
    keyVaultName: keyVault.outputs.name
    principalId: api.outputs.identityPrincipalId
  }
}

// Grant API access to storage
module apiStorageAccess './core/storage/storage-access.bicep' = {
  name: 'api-storage-access'
  scope: rg
  params: {
    storageAccountName: storage.outputs.name
    principalId: api.outputs.identityPrincipalId
  }
}

// Grant API access to OpenAI
module apiOpenAiAccess './core/ai/cognitiveservices-access.bicep' = {
  name: 'api-openai-access'
  scope: rg
  params: {
    cognitiveServicesAccountName: openAi.outputs.name
    principalId: api.outputs.identityPrincipalId
  }
}

// App outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name

output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.endpoint
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name

output AZURE_OPENAI_ENDPOINT string = openAi.outputs.endpoint
output AZURE_OPENAI_DEPLOYMENT_NAME string = openAiModelDeploymentName

output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name

output API_BASE_URL string = api.outputs.uri
output WEB_BASE_URL string = web.outputs.uri

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
