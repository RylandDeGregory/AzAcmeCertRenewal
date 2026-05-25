using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Certificates;

using Microsoft.Extensions.Logging;

using System.Security.Cryptography.X509Certificates;

internal sealed class AzureKeyVaultCertificateStore(
    Uri keyVaultEndpoint,
    TokenCredential credential,
    ILogger<AzureKeyVaultCertificateStore> logger)
{
    private readonly CertificateClient _certificateClient = new(keyVaultEndpoint, credential);

    public async Task<KeyVaultCertificateWithPolicy?> GetCertificateAsync(AzAcmeCertificateState certificateState, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(certificateState);

        try
        {
            logger.LogInformation("Get certificate {KeyVaultCertificateName} from Azure Key Vault", certificateState.ResolvedKeyVaultCertificateName);
            return await _certificateClient.GetCertificateAsync(certificateState.ResolvedKeyVaultCertificateName, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogInformation("Certificate {KeyVaultCertificateName} does not exist in Azure Key Vault", certificateState.ResolvedKeyVaultCertificateName);
            return null;
        }
    }

    public static bool ShouldRenew(AzAcmeCertificateState certificateState, KeyVaultCertificateWithPolicy? keyVaultCertificate)
    {
        ArgumentNullException.ThrowIfNull(certificateState);

        if (keyVaultCertificate?.Properties.ExpiresOn is null)
        {
            return true;
        }

        var renewAfter = keyVaultCertificate.Properties.ExpiresOn.Value.AddDays(-certificateState.RenewBeforeDays);
        return DateTimeOffset.UtcNow >= renewAfter;
    }

    public async Task ImportCertificateAsync(AzAcmeCertificateState certificateState, byte[] pfxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(certificateState);
        ArgumentNullException.ThrowIfNull(pfxBytes);

        using var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, certificateState.PfxPassword);
        var certificateThumbprint = Convert.ToHexString(certificate.GetCertHash()).ToUpperInvariant();

        logger.LogInformation(
            "Import certificate {KeyVaultCertificateName} with thumbprint {CertificateThumbprint} to Azure Key Vault",
            certificateState.ResolvedKeyVaultCertificateName,
            certificateThumbprint);

        var importOptions = new ImportCertificateOptions(certificateState.ResolvedKeyVaultCertificateName, pfxBytes)
        {
            Password = certificateState.PfxPassword
        };

        try
        {
            await _certificateClient.ImportCertificateAsync(importOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to import certificate {KeyVaultCertificateName} with thumbprint {CertificateThumbprint} to Azure Key Vault",
                certificateState.ResolvedKeyVaultCertificateName,
                certificateThumbprint);

            throw;
        }
    }
}