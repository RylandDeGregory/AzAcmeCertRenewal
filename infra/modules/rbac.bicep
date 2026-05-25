@sys.description('The name of the Azure Container Apps job.')
@sys.minLength(1)
@sys.maxLength(32)
param containerAppJobName string

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

@sys.description('Allows Container Apps job managed identity to read and write ACME state blobs.')
resource containerJobBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, containerAppJob.id, storageBlobContributorRole.id)
  scope: resourceGroup()
  properties: {
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobContributorRole.id
  }
}

@sys.description('Allows Container Apps job managed identity to manage Key Vault Certificates.')
resource containerJobVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, containerAppJob.id, keyVaultCertificatesOfficerRole.id)
  scope: resourceGroup()
  properties: {
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultCertificatesOfficerRole.id
  }
}

@sys.description('Allows Container Apps job managed identity to publish OpenTelemetry to Application Insights with Microsoft Entra authentication.')
resource containerJobApplicationInsightsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, containerAppJob.id, monitoringMetricsPublisherRole.id)
  scope: resourceGroup()
  properties: {
    principalId: containerAppJob.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: monitoringMetricsPublisherRole.id
  }
}

module containerJobDnsRole 'dns.bicep' = {
  name: 'Rbac-DnsZone'
  scope: resourceGroup(dnsZoneSubscription, dnsZoneResourceGroup)
  params: {
    dnsZoneName: dnsZoneName
    principalId: containerAppJob.identity.principalId
    uniqueSuffix: uniqueSuffix
  }
}
