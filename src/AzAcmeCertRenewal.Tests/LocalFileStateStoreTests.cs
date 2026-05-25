using Microsoft.Extensions.Logging.Abstractions;

public sealed class LocalFileStateStoreTests
{
    [Fact]
    public async Task LoadAndSaveAsync_RoundTripsStateFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"az-acme-state-{Guid.NewGuid():N}.json");

        try
        {
            var store = new LocalFileStateStore(filePath, NullLogger<LocalFileStateStore>.Instance);
            var state = new AzAcmeState
            {
                AcmeDirectoryUrl = "https://localhost:14000/dir",
                Contact = ["mailto:admin@example.test"],
                TermsOfServiceAgreed = true,
                AccountUrl = "https://localhost:14000/my-account",
                Certificates =
                [
                    new AzAcmeCertificateState
                    {
                        Name = "example-test",
                        Domains = ["example.test"]
                    }
                ]
            };

            await store.SaveAsync(state, CancellationToken.None);
            var loadedState = await store.LoadAsync(CancellationToken.None);

            Assert.Equal("https://localhost:14000/dir", loadedState.AcmeDirectoryUrl);
            Assert.Equal("https://localhost:14000/my-account", loadedState.AccountUrl);
            Assert.Equal("mailto:admin@example.test", Assert.Single(loadedState.Contact));
            Assert.Equal("example-test", Assert.Single(loadedState.Certificates).Name);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LoadAsync_ThrowsWhenFileDoesNotExist()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"missing-az-acme-state-{Guid.NewGuid():N}.json");
        var store = new LocalFileStateStore(filePath, NullLogger<LocalFileStateStore>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));

        Assert.Contains(filePath, exception.Message, StringComparison.Ordinal);
    }
}