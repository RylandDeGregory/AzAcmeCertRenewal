using System.Security.Cryptography;

public sealed class AzAcmeCertificateBuilderTests
{
    [Fact]
    public void CreateCertificateRequest_GeneratesRsaPrivateKey_WhenNoPrivateKeyExists()
    {
        var certificate = CreateCertificateState(keyType: "rsa-2048");

        using var result = AzAcmeCertificateBuilder.CreateCertificateRequest(certificate);

        var rsa = Assert.IsType<RSA>(result.PrivateKey, exactMatch: false);
        Assert.Equal(2048, rsa.KeySize);
        Assert.NotEmpty(result.CertificateSigningRequest);
        Assert.Contains("PRIVATE KEY", certificate.PrivateKeyPem, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCertificateRequest_ReusesExistingPrivateKey_WhenAlwaysNewKeyIsFalse()
    {
        var certificate = CreateCertificateState(keyType: "rsa-2048");
        using (AzAcmeCertificateBuilder.CreateCertificateRequest(certificate))
        {
        }

        var originalPrivateKeyPem = certificate.PrivateKeyPem;

        using var result = AzAcmeCertificateBuilder.CreateCertificateRequest(certificate);

        Assert.IsType<RSA>(result.PrivateKey, exactMatch: false);
        Assert.Equal(originalPrivateKeyPem, certificate.PrivateKeyPem);
    }

    [Fact]
    public void CreateCertificateRequest_ReplacesExistingPrivateKey_WhenAlwaysNewKeyIsTrue()
    {
        var certificate = CreateCertificateState(keyType: "rsa-2048");
        using (AzAcmeCertificateBuilder.CreateCertificateRequest(certificate))
        {
        }

        var originalPrivateKeyPem = certificate.PrivateKeyPem;
        certificate.AlwaysNewKey = true;

        using var result = AzAcmeCertificateBuilder.CreateCertificateRequest(certificate);

        Assert.IsType<RSA>(result.PrivateKey, exactMatch: false);
        Assert.NotEqual(originalPrivateKeyPem, certificate.PrivateKeyPem);
    }

    [Fact]
    public void CreateCertificateRequest_GeneratesEcdsaPrivateKey_WhenConfigured()
    {
        var certificate = CreateCertificateState(keyType: "ec-256");

        using var result = AzAcmeCertificateBuilder.CreateCertificateRequest(certificate);

        var ecdsa = Assert.IsType<ECDsa>(result.PrivateKey, exactMatch: false);
        Assert.Equal(256, ecdsa.KeySize);
        Assert.NotEmpty(result.CertificateSigningRequest);
    }

    private static AzAcmeCertificateState CreateCertificateState(string keyType)
    {
        return new AzAcmeCertificateState
        {
            Name = "example-com",
            Domains = ["example.com", "www.example.com"],
            KeyType = keyType
        };
    }
}