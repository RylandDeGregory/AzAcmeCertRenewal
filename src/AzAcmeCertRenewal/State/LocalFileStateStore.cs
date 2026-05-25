using System.Text.Json;

using Microsoft.Extensions.Logging;

internal sealed class LocalFileStateStore(string filePath, ILogger<LocalFileStateStore> logger) : IAzAcmeStateStore
{
    private readonly string _filePath = Path.GetFullPath(filePath);

    public async Task<AzAcmeState> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            throw new InvalidOperationException($"ACME state file [{_filePath}] was not found.");
        }

        logger.LogInformation("Load ACME state file {StateFilePath}", _filePath);

        await using var stream = File.OpenRead(_filePath);
        var state = await JsonSerializer.DeserializeAsync<AzAcmeState>(stream, AzAcmeStateJson.SerializerOptions, ct)
            ?? throw new InvalidOperationException($"ACME state file [{_filePath}] could not be parsed.");

        logger.LogInformation(
            "Loaded ACME state file {StateFilePath} with {CertificateCount} configured certificate(s)",
            _filePath,
            state.Certificates.Count);

        return state;
    }

    public async Task SaveAsync(AzAcmeState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        logger.LogInformation(
            "Save ACME state file {StateFilePath} with {CertificateCount} configured certificate(s)",
            _filePath,
            state.Certificates.Count);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, AzAcmeStateJson.SerializerOptions, ct);

        logger.LogInformation("Saved ACME state file {StateFilePath}", _filePath);
    }
}