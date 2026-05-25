public sealed class AzAcmeConfigurationTests
{
    private const string AllowInsecureAcmeServerCertificateVariable = "AZ_ACME_ALLOW_INSECURE_ACME_SERVER_CERTIFICATE";
    private const string DnsZoneResourceIdVariable = "AZURE_DNS_ZONE_RESOURCE_ID";
    private const string KeyVaultEndpointVariable = "AZURE_KEY_VAULT_ENDPOINT";
    private const string LocalStateFilePathVariable = "AZ_ACME_STATE_FILE_PATH";
    private const string StateBlobNameVariable = "AZ_ACME_STATE_BLOB_NAME";
    private const string StorageAccountBlobEndpointVariable = "AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT";
    private const string StorageBlobContainerNameVariable = "AZURE_STORAGE_BLOB_CONTAINER_NAME";

    [Fact]
    public void LoadFromEnvironment_UsesRequiredValuesAndOptionalDefaults()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [AllowInsecureAcmeServerCertificateVariable] = null,
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [LocalStateFilePathVariable] = null,
                [StateBlobNameVariable] = null,
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/",
                [StorageBlobContainerNameVariable] = null
            },
            () =>
            {
                var configuration = AzAcmeConfiguration.LoadFromEnvironment();

                Assert.False(configuration.AllowInsecureAcmeServerCertificate);
                Assert.Null(configuration.LocalStateFilePath);
                Assert.Equal("acme-state.json", configuration.StateBlobName);
                Assert.Equal("acme", configuration.StorageBlobContainerName);
                Assert.Equal("https://example.vault.azure.net/", configuration.KeyVaultEndpoint.ToString());
                Assert.Equal("https://storage.blob.core.windows.net/", configuration.StorageAccountBlobEndpoint?.ToString());
            });
    }

    [Fact]
    public void LoadFromEnvironment_UsesOptionalOverrides()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [AllowInsecureAcmeServerCertificateVariable] = "true",
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [LocalStateFilePathVariable] = null,
                [StateBlobNameVariable] = "custom-state.json",
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/",
                [StorageBlobContainerNameVariable] = "custom-container"
            },
            () =>
            {
                var configuration = AzAcmeConfiguration.LoadFromEnvironment();

                Assert.True(configuration.AllowInsecureAcmeServerCertificate);
                Assert.Equal("custom-state.json", configuration.StateBlobName);
                Assert.Equal("custom-container", configuration.StorageBlobContainerName);
            });
    }

    [Fact]
    public void LoadFromEnvironment_Throws_WhenRequiredValueIsMissing()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [AllowInsecureAcmeServerCertificateVariable] = null,
                [DnsZoneResourceIdVariable] = null,
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [LocalStateFilePathVariable] = null,
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/"
            },
            () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(AzAcmeConfiguration.LoadFromEnvironment);
                Assert.Contains(DnsZoneResourceIdVariable, exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void LoadFromEnvironment_UsesLocalStateFileWithoutStorageEndpoint()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [AllowInsecureAcmeServerCertificateVariable] = null,
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [LocalStateFilePathVariable] = "../acme-state.pebble.sample.json",
                [StateBlobNameVariable] = null,
                [StorageAccountBlobEndpointVariable] = null,
                [StorageBlobContainerNameVariable] = null
            },
            () =>
            {
                var configuration = AzAcmeConfiguration.LoadFromEnvironment();

                Assert.Equal("../acme-state.pebble.sample.json", configuration.LocalStateFilePath);
                Assert.Null(configuration.StorageAccountBlobEndpoint);
            });
    }

    [Fact]
    public void LoadFromEnvironment_Throws_WhenBooleanValueIsInvalid()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [AllowInsecureAcmeServerCertificateVariable] = "not-a-bool",
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [LocalStateFilePathVariable] = null,
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/"
            },
            () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(AzAcmeConfiguration.LoadFromEnvironment);
                Assert.Contains(AllowInsecureAcmeServerCertificateVariable, exception.Message, StringComparison.Ordinal);
            });
    }

    private static void WithEnvironment(IReadOnlyDictionary<string, string?> values, Action test)
    {
        var originalValues = values.ToDictionary(pair => pair.Key, pair => Environment.GetEnvironmentVariable(pair.Key));

        try
        {
            foreach (var (name, value) in values)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            test();
        }
        finally
        {
            foreach (var (name, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}