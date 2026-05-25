public sealed class AzAcmeCertificateStateTests
{
    [Fact]
    public void MainDomain_ReturnsFirstConfiguredDomain()
    {
        var certificate = new AzAcmeCertificateState
        {
            Name = "example-com",
            Domains = ["example.com", "www.example.com"]
        };

        Assert.Equal("example.com", certificate.MainDomain);
    }

    [Fact]
    public void MainDomain_Throws_WhenNoDomainsAreConfigured()
    {
        var certificate = new AzAcmeCertificateState
        {
            Name = "example-com"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => certificate.MainDomain);
        Assert.Contains("example-com", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "example-com")]
    [InlineData("", "example-com")]
    [InlineData("custom-cert-name", "custom-cert-name")]
    public void ResolvedKeyVaultCertificateName_UsesOverrideWhenConfigured(string? keyVaultCertificateName, string expectedName)
    {
        var certificate = new AzAcmeCertificateState
        {
            Name = "example-com",
            KeyVaultCertificateName = keyVaultCertificateName
        };

        Assert.Equal(expectedName, certificate.ResolvedKeyVaultCertificateName);
    }
}