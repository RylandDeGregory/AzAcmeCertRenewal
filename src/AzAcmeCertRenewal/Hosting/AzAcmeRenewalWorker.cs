using System.Diagnostics;

using Acmebot.Acme;

using Microsoft.Extensions.Logging;

internal sealed class AzAcmeRenewalWorker(
    AzAcmeConfiguration configuration,
    AzureBlobStateStore blobStateStore,
    AzureKeyVaultCertificateStore keyVaultCertificateStore,
    AzureDnsChallengePublisher dnsChallengePublisher,
    AzAcmeAccount acmeAccountService,
    AzAcmeRenewalProcessor renewalProcessor,
    ActivitySource activitySource,
    ILogger<AzAcmeRenewalWorker> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var activity = activitySource.StartActivity("RenewCertificates");
        activity?.SetTag("az_acme.state_blob_name", configuration.StateBlobName);

        var state = await blobStateStore.LoadAsync(ct);

        activity?.SetTag("az_acme.certificate_count", state.Certificates.Count);

        if (state.Certificates.Count == 0)
        {
            logger.LogError("ACME state blob {StateBlobName} does not define any certificates", configuration.StateBlobName);
            return;
        }

        logger.LogInformation("Initialize ACME client for account: {AcmeAccountUrl}", state.AccountUrl);
        using var acmeClient = new AcmeClient(new Uri(state.AcmeDirectoryUrl));
        var acmeAccount = await acmeAccountService.LoadOrCreateAsync(acmeClient, state, ct);

        logger.LogInformation("Processing {CertificateCount} certificate(s) for renewal", state.Certificates.Count);
        foreach (var certificate in state.Certificates.OrderBy(certificate => certificate.Name))
        {
            var domainList = string.Join(", ", certificate.Domains);

            using var certificateScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["CertificateName"] = certificate.Name,
                ["Domains"] = domainList
            });

            logger.LogInformation("Process certificate {CertificateName} for domains {Domains}", certificate.Name, domainList);

            var keyVaultCertificate = await keyVaultCertificateStore.GetCertificateAsync(certificate, ct);
            if (!AzureKeyVaultCertificateStore.ShouldRenew(certificate, keyVaultCertificate))
            {
                logger.LogInformation(
                    "Certificate {CertificateName} is valid until {ExpiresOn}. No action required",
                    certificate.Name,
                    keyVaultCertificate!.Properties.ExpiresOn);
                continue;
            }

            logger.LogInformation("Certificate {CertificateName} is ready for renewal", certificate.Name);

            var renewalOrder = await renewalProcessor.CreateRenewalOrderAsync(acmeClient, acmeAccount, certificate, ct);
            await renewalProcessor.ProcessAuthorizationsAsync(acmeClient, acmeAccount, dnsChallengePublisher, renewalOrder.Resource, certificate, ct);

            var pfxBytes = await renewalProcessor.FinalizeOrderAndCreatePfxAsync(acmeClient, acmeAccount, renewalOrder, certificate, ct);
            await keyVaultCertificateStore.ImportCertificateAsync(certificate, pfxBytes, ct);
        }

        await blobStateStore.SaveAsync(state, ct);

        logger.LogInformation("Certificate renewal run completed");
    }
}