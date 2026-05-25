internal sealed record AzAcmeConfiguration
{

    public required string DnsZoneResourceId { get; init; }

    public required Uri KeyVaultEndpoint { get; init; }

    public string StateBlobName { get; init; } = "acme-state.json";

    public required Uri StorageAccountBlobEndpoint { get; init; }

    public required string StorageBlobContainerName { get; init; } = "acme";

    public static AzAcmeConfiguration LoadFromEnvironment()
    {
        return new AzAcmeConfiguration
        {
            DnsZoneResourceId = GetRequiredEnvironmentVariable("AZURE_DNS_ZONE_RESOURCE_ID"),
            KeyVaultEndpoint = new Uri(GetRequiredEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT")),
            StateBlobName = Environment.GetEnvironmentVariable("AZ_ACME_STATE_BLOB_NAME") ?? "acme-state.json",
            StorageAccountBlobEndpoint = new Uri(GetRequiredEnvironmentVariable("AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT")),
            StorageBlobContainerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_BLOB_CONTAINER_NAME") ?? "acme"
        };
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} environment variable is not set.");
    }
}