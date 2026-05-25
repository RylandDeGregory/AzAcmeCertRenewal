using System.Text.Json;

using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

internal sealed class AzureBlobStateStore(
    Uri storageAccountBlobEndpoint,
    TokenCredential credential,
    string containerName,
    string blobName,
    ILogger<AzureBlobStateStore> logger)
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly BlobClient _blobClient = new BlobServiceClient(storageAccountBlobEndpoint, credential)
        .GetBlobContainerClient(containerName)
        .GetBlobClient(blobName);

    public async Task<AzAcmeState> LoadAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Load ACME state blob {BlobName} from container {BlobContainerName}", _blobClient.Name, _blobClient.BlobContainerName);

            var response = await _blobClient.DownloadContentAsync(ct);
            var state = response.Value.Content.ToObjectFromJson<AzAcmeState>(s_jsonSerializerOptions)
                ?? throw new InvalidOperationException($"ACME state blob [{_blobClient.Name}] could not be parsed.");

            logger.LogInformation(
                "Loaded ACME state blob {BlobName} with {CertificateCount} configured certificate(s)",
                _blobClient.Name,
                state.Certificates.Count);

            return state;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogError(ex, "ACME state blob {BlobName} was not found in container {BlobContainerName}: {Message}", _blobClient.Name, _blobClient.BlobContainerName, ex.Message);
            throw new InvalidOperationException($"ACME state blob [{_blobClient.Name}] was not found. Create it with certificate configuration before initiating renewal.", ex);
        }
    }

    public async Task SaveAsync(AzAcmeState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);

        var blobUploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            }
        };
        var content = BinaryData.FromObjectAsJson(state, s_jsonSerializerOptions);

        logger.LogInformation(
            "Save ACME state blob {BlobName} to container {BlobContainerName} with {CertificateCount} configured certificate(s)",
            _blobClient.Name,
            _blobClient.BlobContainerName,
            state.Certificates.Count);

        await _blobClient.UploadAsync(content, blobUploadOptions, ct);

        logger.LogInformation("Saved ACME state blob {BlobName}", _blobClient.Name);
    }
}