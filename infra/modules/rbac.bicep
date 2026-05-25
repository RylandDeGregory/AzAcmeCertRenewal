@sys.description('The name of the Azure Container Apps job.')
@sys.minLength(1)
@sys.maxLength(32)
param containerAppJobName string

@sys.description('The name of the Azure Storage Account.')
@sys.minLength(3)
@sys.maxLength(24)
param storageAccountName string

@sys.description('The name of the Azure Key Vault.')
@sys.minLength(3)
@sys.maxLength(24)
param keyVaultName string

@sys.description('The name of the Azure Application Insights resource used by OpenTelemetry.')
@sys.minLength(1)
@sys.maxLength(260)
param applicationInsightsName string

@sys.description('The Azure Resource ID of the existing Azure DNS Zone.')
param dnsZoneResourceId string

@sys.description('A unique string to add as a suffix to all resources.')
@sys.maxLength(5)
param uniqueSuffix string

var dnsZoneSubscription = split(dnsZoneResourceId, '/')[2]
var dnsZoneResourceGroup = split(dnsZoneResourceId, '/')[4]
var dnsZoneName = split(dnsZoneResourceId, '/')[8]

@sys.description('Built-in Storage Blob Data Contributor role. See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-blob-data-contributor')
resource storageBlobContributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  scope: subscription()
}

@sys.description('Built-in Key Vault Certificates Officer role. See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#key-vault-certificates-officer')
resource keyVaultCertificatesOfficerRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'a4417e6f-fecd-4de8-b567-7b0420556985'
  scope: subscription()
}

@sys.description('Built-in Monitoring Metrics Publisher role. See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#monitoring-metrics-publisher')
resource monitoringMetricsPublisherRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '3913510d-42f4-4e42-8a64-420c390055eb'
  scope: subscription()
}

resource containerAppJob 'Microsoft.App/jobs@2026-01-01' existing = {
  name: containerAppJobName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' existing = {
  name: keyVaultName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

@sys.description('Allows Container Apps job managed identity to read and write ACME state blobs.')
resource containerJobBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerAppJob.id, storageAccount.id, storageBlobContributorRole.id)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobContributorRole.id
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

@sys.description('Allows Container Apps job managed identity to manage Key Vault Certificates.')
resource containerJobVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerAppJob.id, keyVault.id, keyVaultCertificatesOfficerRole.id)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultCertificatesOfficerRole.id
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

@sys.description('Allows Container Apps job managed identity to publish OpenTelemetry to Application Insights with Microsoft Entra authentication.')
resource containerJobApplicationInsightsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerAppJob.id, applicationInsights.id, monitoringMetricsPublisherRole.id)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisherRole.id
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

module containerJobDnsRole 'dns.bicep' = {
  name: 'DNSZoneRoleAssignment'
  scope: resourceGroup(dnsZoneSubscription, dnsZoneResourceGroup)
  params: {
    dnsZoneName: dnsZoneName
    principalId: containerAppJob.identity.principalId
    uniqueSuffix: uniqueSuffix
  }
}
