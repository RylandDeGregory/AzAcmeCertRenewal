public sealed class AzAcmeConfigurationTests
{
    private const string DnsZoneResourceIdVariable = "AZURE_DNS_ZONE_RESOURCE_ID";
    private const string KeyVaultEndpointVariable = "AZURE_KEY_VAULT_ENDPOINT";
    private const string StateBlobNameVariable = "AZ_ACME_STATE_BLOB_NAME";
    private const string StorageAccountBlobEndpointVariable = "AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT";
    private const string StorageBlobContainerNameVariable = "AZURE_STORAGE_BLOB_CONTAINER_NAME";

    [Fact]
    public void LoadFromEnvironment_UsesRequiredValuesAndOptionalDefaults()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [StateBlobNameVariable] = null,
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/",
                [StorageBlobContainerNameVariable] = null
            },
            () =>
            {
                var configuration = AzAcmeConfiguration.LoadFromEnvironment();

                Assert.Equal("acme-state.json", configuration.StateBlobName);
                Assert.Equal("acme", configuration.StorageBlobContainerName);
                Assert.Equal("https://example.vault.azure.net/", configuration.KeyVaultEndpoint.ToString());
                Assert.Equal("https://storage.blob.core.windows.net/", configuration.StorageAccountBlobEndpoint.ToString());
            });
    }

    [Fact]
    public void LoadFromEnvironment_UsesOptionalOverrides()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                [DnsZoneResourceIdVariable] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/dns/providers/Microsoft.Network/dnszones/example.com",
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [StateBlobNameVariable] = "custom-state.json",
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/",
                [StorageBlobContainerNameVariable] = "custom-container"
            },
            () =>
            {
                var configuration = AzAcmeConfiguration.LoadFromEnvironment();

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
                [DnsZoneResourceIdVariable] = null,
                [KeyVaultEndpointVariable] = "https://example.vault.azure.net/",
                [StorageAccountBlobEndpointVariable] = "https://storage.blob.core.windows.net/"
            },
            () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(AzAcmeConfiguration.LoadFromEnvironment);
                Assert.Contains(DnsZoneResourceIdVariable, exception.Message, StringComparison.Ordinal);
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