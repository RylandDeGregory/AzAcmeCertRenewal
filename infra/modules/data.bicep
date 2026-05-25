@sys.description('The Azure Region to deploy the resources into.')
param location string

@sys.description('The name of the Azure Key Vault.')
@sys.minLength(3)
@sys.maxLength(24)
param keyVaultName string

@sys.description('The name of the Azure Storage Account.')
@sys.minLength(3)
@sys.maxLength(24)
param storageAccountName string

@sys.description('The name of the Azure Storage Account Blob container.')
@sys.minLength(3)
@sys.maxLength(63)
param blobContainerName string

@sys.description('If Azure Diagnostics Settings are enabled for the resources.')
param diagnosticSettingsEnabled bool

@sys.description('The name of the Azure Log Analytics Workspace for Diagnostic Settings.')
@sys.minLength(4)
@sys.maxLength(63)
param logAnalyticsWorkspaceName string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Enabled'
    softDeleteRetentionInDays: 30
    tenantId: tenant().tenantId
  }
}

resource kvDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (diagnosticSettingsEnabled) {
  name: 'All Logs and Metrics'
  scope: keyVault
  properties: {
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
    workspaceId: logAnalyticsWorkspace.id
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    allowBlobPublicAccess: false
  }

  resource blobService 'blobServices' = {
    name: 'default'
    properties: {
      containerDeleteRetentionPolicy: {
        enabled: true
        days: 30
      }
      deleteRetentionPolicy: {
        enabled: true
        days: 14
      }
    }

    resource blobContainer 'containers' = {
      name: blobContainerName
    }
  }
}

resource blobServiceDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (diagnosticSettingsEnabled) {
  name: 'All Logs and Metrics'
  scope: storageAccount::blobService
  properties: {
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]
    workspaceId: logAnalyticsWorkspace.id
  }
}

output keyVaultName string = keyVault.name
output storageAccountName string = storageAccount.name
