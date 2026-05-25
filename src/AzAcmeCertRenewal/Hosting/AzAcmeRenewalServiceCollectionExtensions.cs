using System.Diagnostics;

using Azure.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

internal static class AzAcmeRenewalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the certificate renewal configuration, Azure boundary services, ACME workflow services, and renewal worker.
    /// </summary>
    /// <param name="services">The service collection to add renewal services to.</param>
    /// <param name="credential">The shared Azure credential used by Azure SDK clients.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddAzAcmeRenewal(this IServiceCollection services, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        services.TryAddSingleton(credential);
        services.AddAzAcmeConfiguration();
        services.AddAzureServices();
        services.AddAcmeServices();

        return services;
    }

    /// <summary>
    /// Registers configuration and activity source services used by the renewal workflow.
    /// </summary>
    /// <param name="services">The service collection to add configuration services to.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    private static IServiceCollection AddAzAcmeConfiguration(this IServiceCollection services)
    {
        services.AddSingleton(_ => AzAcmeConfiguration.LoadFromEnvironment());
        services.AddSingleton(_ => new ActivitySource(AzAcmeTelemetry.ActivitySourceName));

        return services;
    }

    /// <summary>
    /// Registers Azure SDK-backed services used for state storage, certificate import, and DNS challenge updates.
    /// </summary>
    /// <param name="services">The service collection to add Azure services to.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    private static IServiceCollection AddAzureServices(this IServiceCollection services)
    {
        services.AddSingleton<IAzAcmeStateStore>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<AzAcmeConfiguration>();
            if (!string.IsNullOrWhiteSpace(configuration.LocalStateFilePath))
            {
                return new LocalFileStateStore(
                    configuration.LocalStateFilePath,
                    serviceProvider.GetRequiredService<ILogger<LocalFileStateStore>>());
            }

            var storageAccountBlobEndpoint = configuration.StorageAccountBlobEndpoint
                ?? throw new InvalidOperationException("AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT environment variable is not set.");

            return new AzureBlobStateStore(
                storageAccountBlobEndpoint,
                serviceProvider.GetRequiredService<TokenCredential>(),
                configuration.StorageBlobContainerName,
                configuration.StateBlobName,
                serviceProvider.GetRequiredService<ILogger<AzureBlobStateStore>>());
        });

        services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<AzAcmeConfiguration>();
            var credential = serviceProvider.GetRequiredService<TokenCredential>();
            var logger = serviceProvider.GetRequiredService<ILogger<AzureKeyVaultCertificateStore>>();

            return new AzureKeyVaultCertificateStore(configuration.KeyVaultEndpoint, credential, logger);
        });

        services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<AzAcmeConfiguration>();
            var credential = serviceProvider.GetRequiredService<TokenCredential>();
            var logger = serviceProvider.GetRequiredService<ILogger<AzureDnsChallengePublisher>>();

            return new AzureDnsChallengePublisher(new ResourceIdentifier(configuration.DnsZoneResourceId), credential, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers ACME account, renewal processing, and worker orchestration services.
    /// </summary>
    /// <param name="services">The service collection to add ACME services to.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    private static IServiceCollection AddAcmeServices(this IServiceCollection services)
    {
        services.AddSingleton<AzAcmeAccount>();
        services.AddSingleton<AzAcmeRenewalProcessor>();
        services.AddSingleton<AzAcmeRenewalWorker>();

        return services;
    }
}