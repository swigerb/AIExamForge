param cognitiveServicesAccountName string
param principalId string

resource cognitiveServices 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: cognitiveServicesAccountName
}

// Cognitive Services OpenAI User role
resource cognitiveServicesOpenAIUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cognitiveServices.id, principalId, 'Cognitive Services OpenAI User')
  scope: cognitiveServices
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd') // Cognitive Services OpenAI User
    principalType: 'ServicePrincipal'
  }
}
