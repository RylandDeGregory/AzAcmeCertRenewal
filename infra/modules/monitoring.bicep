@sys.description('The Azure Region to deploy the resources into.')
param location string

@sys.description('The name of the Azure Log Analytics Workspace that Container Apps logs and Diagnostic Settings will be connected to.')
@sys.minLength(4)
@sys.maxLength(63)
param logAnalyticsWorkspaceName string

@sys.description('The name of the Azure Application Insights resource used by OpenTelemetry.')
@sys.minLength(1)
@sys.maxLength(260)
param applicationInsightsName string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    DisableLocalAuth: true
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output applicationInsightsName string = applicationInsights.name
