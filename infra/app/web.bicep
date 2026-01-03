param location string
param tags object = {}
param name string
param apiBaseUrl string = ''

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

resource staticWebAppSettings 'Microsoft.Web/staticSites/config@2024-04-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    VITE_API_BASE_URL: apiBaseUrl
  }
}

output id string = staticWebApp.id
output name string = staticWebApp.name
output uri string = 'https://${staticWebApp.properties.defaultHostname}'
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
