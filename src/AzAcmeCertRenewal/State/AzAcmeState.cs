using System.Text.Json.Serialization;

internal sealed class AzAcmeState
{
    [JsonPropertyName("acmeDirectoryUrl")]
    public string AcmeDirectoryUrl { get; set; } = "https://acme-v02.api.letsencrypt.org/directory";

    [JsonPropertyName("contact")]
    public List<string> Contact { get; set; } = [];

    [JsonPropertyName("termsOfServiceAgreed")]
    public bool TermsOfServiceAgreed { get; set; } = true;

    [JsonPropertyName("accountUrl")]
    public string? AccountUrl { get; set; }

    [JsonPropertyName("accountKeyPem")]
    public string? AccountKeyPem { get; set; }

    [JsonPropertyName("certificates")]
    public List<AzAcmeCertificateState> Certificates { get; set; } = [];
}

internal sealed class AzAcmeCertificateState
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("domains")]
    public List<string> Domains { get; set; } = [];

    [JsonPropertyName("keyVaultCertificateName")]
    public string? KeyVaultCertificateName { get; set; }

    [JsonPropertyName("privateKeyPem")]
    public string? PrivateKeyPem { get; set; }

    [JsonPropertyName("keyType")]
    public string? KeyType { get; set; } = "rsa-2048";

    [JsonPropertyName("alwaysNewKey")]
    public bool AlwaysNewKey { get; set; }

    [JsonPropertyName("renewBeforeDays")]
    public int RenewBeforeDays { get; set; } = 30;

    [JsonPropertyName("dnsSleepSeconds")]
    public int DnsSleepSeconds { get; set; } = 60;

    [JsonPropertyName("validationTimeoutSeconds")]
    public int ValidationTimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("ocspMustStaple")]
    public bool OcspMustStaple { get; set; }

    [JsonPropertyName("pfxPassword")]
    public string? PfxPassword { get; set; }

    [JsonIgnore]
    public string MainDomain => Domains.FirstOrDefault()
        ?? throw new InvalidOperationException($"Certificate [{Name}] must define at least one domain.");

    [JsonIgnore]
    public string ResolvedKeyVaultCertificateName => string.IsNullOrWhiteSpace(KeyVaultCertificateName)
        ? Name
        : KeyVaultCertificateName;
}