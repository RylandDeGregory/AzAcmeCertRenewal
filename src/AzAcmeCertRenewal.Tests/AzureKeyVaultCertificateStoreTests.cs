using Azure.Security.KeyVault.Certificates;

public sealed class AzureKeyVaultCertificateStoreTests
{
    [Fact]
    public void ShouldRenew_ReturnsTrue_WhenCertificateDoesNotExist()
    {
        var certificateState = CreateCertificateState(renewBeforeDays: 30);

        Assert.True(AzureKeyVaultCertificateStore.ShouldRenew(certificateState, keyVaultCertificate: null));
    }

    [Theory]
    [InlineData(15, 30, true)]
    [InlineData(45, 30, false)]
    public void ShouldRenew_UsesConfiguredRenewalWindow(int expiresInDays, int renewBeforeDays, bool expected)
    {
        var certificateState = CreateCertificateState(renewBeforeDays);
        var keyVaultCertificate = CreateKeyVaultCertificate(DateTimeOffset.UtcNow.AddDays(expiresInDays));

        Assert.Equal(expected, AzureKeyVaultCertificateStore.ShouldRenew(certificateState, keyVaultCertificate));
    }

    private static AzAcmeCertificateState CreateCertificateState(int renewBeforeDays)
    {
        return new AzAcmeCertificateState
        {
            Name = "example-com",
            RenewBeforeDays = renewBeforeDays
        };
    }

    private static KeyVaultCertificateWithPolicy CreateKeyVaultCertificate(DateTimeOffset expiresOn)
    {
        var vaultUri = new Uri("https://example.vault.azure.net/");
        var properties = CertificateModelFactory.CertificateProperties(
            id: new Uri(vaultUri, "certificates/example-com/version"),
            name: "example-com",
            vaultUri: vaultUri,
            version: "version",
            x509thumbprint: null,
            notBefore: null,
            expiresOn: expiresOn,
            createdOn: null,
            updatedOn: null,
            recoveryLevel: null);

        return CertificateModelFactory.KeyVaultCertificateWithPolicy(
            properties: properties,
            keyId: null,
            secretId: null,
            cer: [],
            policy: null);
    }
}