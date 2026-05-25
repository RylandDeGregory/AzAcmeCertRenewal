@sys.description('The name of the Azure Application Insights resource used by OpenTelemetry.')
@sys.minLength(1)
@sys.maxLength(260)
param applicationInsightsName string

@sys.description('The name of the Azure Storage Account Blob container.')
@sys.minLength(3)
@sys.maxLength(63)
param blobContainerName string

@sys.description('The name of the Azure Container Apps managed environment.')
@sys.minLength(1)
@sys.maxLength(60)
param containerAppEnvironmentName string

@sys.description('The name of the Azure Container Apps job.')
@sys.minLength(1)
@sys.maxLength(32)
param containerAppJobName string

@sys.description('The CPU cores allocated to each job replica.')
param containerCpuCores string

@sys.description('The container image to run for certificate renewal. Use a fully qualified image reference including tag.')
param containerImage string

@sys.description('The cron schedule for the Container Apps job in UTC.')
param containerJobCronExpression string

@sys.description('The retry limit for failed job replicas.')
param containerJobReplicaRetryLimit int

@sys.description('The maximum runtime for a job replica in seconds.')
param containerJobReplicaTimeoutSeconds int

@sys.description('The memory allocated to each job replica.')
param containerMemory string

@sys.description('If Azure Diagnostics Settings are enabled for the resources.')
param diagnosticSettingsEnabled bool

@sys.description('The Azure Resource ID of the existing Azure DNS Zone.')
param dnsZoneResourceId string

@sys.description('The name of the Azure Key Vault.')
@sys.minLength(3)
@sys.maxLength(24)
param keyVaultName string

@sys.description('The Azure Region to deploy the resources into.')
param location string

@sys.description('The name of the Azure Log Analytics Workspace that Container Apps logs and Diagnostic Settings will be connected to.')
@sys.minLength(4)
@sys.maxLength(63)
param logAnalyticsWorkspaceName string

@sys.description('The name of the Azure Storage Account.')
@sys.minLength(3)
@sys.maxLength(24)
param storageAccountName string

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' existing = {
  name: storageAccountName
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2026-01-01' = {
  name: containerAppEnvironmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource containerAppEnvironmentDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (diagnosticSettingsEnabled) {
  name: 'All Logs and Metrics'
  scope: containerAppEnvironment
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

resource containerAppJob 'Microsoft.App/jobs@2026-01-01' = {
  name: containerAppJobName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    configuration: {
      replicaRetryLimit: containerJobReplicaRetryLimit
      replicaTimeout: containerJobReplicaTimeoutSeconds
      scheduleTriggerConfig: {
        cronExpression: containerJobCronExpression
        parallelism: 1
        replicaCompletionCount: 1
      }
      triggerType: 'Schedule'
    }
    environmentId: containerAppEnvironment.id
    template: {
      containers: [
        {
          env: [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsights.properties.ConnectionString
            }
            {
              name: 'AZURE_DNS_ZONE_RESOURCE_ID'
              value: dnsZoneResourceId
            }
            {
              name: 'AZURE_KEY_VAULT_ENDPOINT'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT'
              value: storageAccount.properties.primaryEndpoints.blob
            }
            {
              name: 'AZURE_STORAGE_BLOB_CONTAINER_NAME'
              value: blobContainerName
            }
          ]
          image: containerImage
          name: 'azacmecertrenewal'
          resources: {
            cpu: json(containerCpuCores)
            memory: containerMemory
          }
        }
      ]
    }
    workloadProfileName: 'Consumption'
  }
}

resource containerAppJobDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (diagnosticSettingsEnabled) {
  name: 'All Logs and Metrics'
  scope: containerAppJob
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

output containerAppJobName string = containerAppJob.name
