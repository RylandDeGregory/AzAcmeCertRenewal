using System.Text.Json;

internal static class AzAcmeStateJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}