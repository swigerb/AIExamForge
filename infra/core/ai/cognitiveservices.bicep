param location string
param tags object = {}
param name string
param kind string = 'OpenAI'
param sku object = {
  name: 'S0'
}
param deployments array = []

resource cognitiveServices 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: kind
  sku: sku
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = [for deployment in deployments: {
  parent: cognitiveServices
  name: deployment.name
  sku: deployment.?sku ?? {
    name: 'Standard'
    capacity: 20
  }
  properties: {
    model: deployment.model
    raiPolicyName: deployment.?raiPolicyName
  }
}]

output id string = cognitiveServices.id
output name string = cognitiveServices.name
output endpoint string = cognitiveServices.properties.endpoint
