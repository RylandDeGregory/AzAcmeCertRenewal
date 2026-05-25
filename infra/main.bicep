@sys.description('The name of the Azure Application Insights resource used by OpenTelemetry. Default: appi-lecertrenew-$<uniqueSuffix>')
@sys.minLength(1)
@sys.maxLength(260)
param applicationInsightsName string = 'appi-lecertrenew-${uniqueSuffix}'

@sys.description('The name of the Azure Container Apps managed environment. Default: cae-lecertrenew-$<uniqueSuffix>')
@sys.minLength(1)
@sys.maxLength(60)
param containerAppEnvironmentName string = 'cae-lecertrenew-${uniqueSuffix}'

@sys.description('The name of the Azure Container Apps job. Default: caj-lecertrenew-$<uniqueSuffix>')
@sys.minLength(1)
@sys.maxLength(32)
param containerAppJobName string = 'caj-lecertrenew-${uniqueSuffix}'

@sys.description('The CPU cores allocated to each job replica. Default: 0.25')
param containerCpuCores string = '0.25'

@sys.description('The container image to run for certificate renewal. Use a fully qualified image reference including tag. Default: ghcr.io/rylanddegregory/azacmecertrenewal:latest')
param containerImage string = 'ghcr.io/rylanddegregory/azacmecertrenewal:latest'

@sys.description('The cron schedule for the Container Apps job in UTC. Default: 00:00 every Sunday')
param containerJobCronExpression string = '0 0 * * 0'

@sys.description('The retry limit for failed job replicas. Default: 1')
param containerJobReplicaRetryLimit int = 1

@sys.description('The maximum runtime for a job replica in seconds. Default: 600')
param containerJobReplicaTimeoutSeconds int = 600

@sys.description('The memory allocated to each job replica. Default: 0.5Gi')
param containerMemory string = '0.5Gi'

@sys.description('If Azure Diagnostics Settings are enabled for the resources. Default: false')
param diagnosticSettingsEnabled bool = false

@sys.description('The Azure Resource ID of the existing Azure DNS Zone.')
param dnsZoneResourceId string

@sys.description('The name of the Azure Key Vault. Default: kv-lecertrenew-$<uniqueSuffix>')
@sys.minLength(3)
@sys.maxLength(24)
param keyVaultName string = 'kv-lecertrenew-${uniqueSuffix}'

@sys.description('The Azure Region to deploy the resources into. Default: resourceGroup().location')
param location string = resourceGroup().location

@sys.description('The name of the Azure Log Analytics Workspace that Container Apps logs and Diagnostic Settings will be connected to. Default: log-lecertrenew-$<uniqueSuffix>')
@sys.minLength(4)
@sys.maxLength(63)
param logAnalyticsWorkspaceName string = 'log-lecertrenew-${uniqueSuffix}'

@sys.description('The name of the Azure Storage Account Blob container. Default: acme')
@sys.minLength(3)
@sys.maxLength(63)
param storageAccountBlobContainerName string = 'acme'

@sys.description('The name of the Azure Storage Account. Default: stlecertrenew$<uniqueSuffix>')
@sys.minLength(3)
@sys.maxLength(24)
param storageAccountName string = 'stlecertrenew${replace(uniqueSuffix, '-', '')}'

@sys.description('A unique string to add as a suffix to all resources. Default: substring(uniqueString(resourceGroup().id), 0, 5)')
@sys.maxLength(5)
param uniqueSuffix string = substring(uniqueString(resourceGroup().id), 0, 5)

module monitoring 'modules/monitoring.bicep' = {
  name: 'Monitoring'
  params: {
    applicationInsightsName: applicationInsightsName
    location: location
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
  }
}

module data 'modules/data.bicep' = {
  name: 'Data'
  params: {
    diagnosticSettingsEnabled: diagnosticSettingsEnabled
    keyVaultName: keyVaultName
    location: location
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    storageAccountBlobContainerName: storageAccountBlobContainerName
    storageAccountName: storageAccountName
  }
}

module app 'modules/app.bicep' = {
  name: 'App'
  params: {
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    blobContainerName: storageAccountBlobContainerName
    containerAppEnvironmentName: containerAppEnvironmentName
    containerAppJobName: containerAppJobName
    containerCpuCores: containerCpuCores
    containerImage: containerImage
    containerJobCronExpression: containerJobCronExpression
    containerJobReplicaRetryLimit: containerJobReplicaRetryLimit
    containerJobReplicaTimeoutSeconds: containerJobReplicaTimeoutSeconds
    containerMemory: containerMemory
    diagnosticSettingsEnabled: diagnosticSettingsEnabled
    dnsZoneResourceId: dnsZoneResourceId
    keyVaultName: data.outputs.keyVaultName
    location: location
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    storageAccountName: data.outputs.storageAccountName
  }
}

module rbac 'modules/rbac.bicep' = {
  name: 'Rbac'
  params: {
    containerAppJobName: app.outputs.containerAppJobName
    dnsZoneResourceId: dnsZoneResourceId
    uniqueSuffix: uniqueSuffix
  }
}

resource rgLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'DoNotDelete'
  scope: resourceGroup()
  properties: {
    level: 'CanNotDelete'
    notes: 'This lock prevents the accidental deletion of resources'
  }
}
