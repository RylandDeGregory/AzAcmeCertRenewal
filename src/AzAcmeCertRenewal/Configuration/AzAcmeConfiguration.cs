internal sealed record AzAcmeConfiguration
{
    public bool AllowInsecureAcmeServerCertificate { get; init; }

    public required string DnsZoneResourceId { get; init; }

    public required Uri KeyVaultEndpoint { get; init; }

    public string? LocalStateFilePath { get; init; }

    public string StateBlobName { get; init; } = "acme-state.json";

    public Uri? StorageAccountBlobEndpoint { get; init; }

    public required string StorageBlobContainerName { get; init; } = "acme";

    public static AzAcmeConfiguration LoadFromEnvironment()
    {
        var localStateFilePath = Environment.GetEnvironmentVariable("AZ_ACME_STATE_FILE_PATH");

        return new AzAcmeConfiguration
        {
            AllowInsecureAcmeServerCertificate = GetOptionalBooleanEnvironmentVariable("AZ_ACME_ALLOW_INSECURE_ACME_SERVER_CERTIFICATE"),
            DnsZoneResourceId = GetRequiredEnvironmentVariable("AZURE_DNS_ZONE_RESOURCE_ID"),
            KeyVaultEndpoint = new Uri(GetRequiredEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT")),
            LocalStateFilePath = string.IsNullOrWhiteSpace(localStateFilePath) ? null : localStateFilePath,
            StateBlobName = Environment.GetEnvironmentVariable("AZ_ACME_STATE_BLOB_NAME") ?? "acme-state.json",
            StorageAccountBlobEndpoint = string.IsNullOrWhiteSpace(localStateFilePath)
                ? new Uri(GetRequiredEnvironmentVariable("AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT"))
                : GetOptionalUriEnvironmentVariable("AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT"),
            StorageBlobContainerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_BLOB_CONTAINER_NAME") ?? "acme"
        };
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} environment variable is not set.");
    }

    private static Uri? GetOptionalUriEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value);
    }

    private static bool GetOptionalBooleanEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out var parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidOperationException($"{name} environment variable must be true or false.");
    }
}