internal interface IAzAcmeStateStore
{
    Task<AzAcmeState> LoadAsync(CancellationToken ct);

    Task SaveAsync(AzAcmeState state, CancellationToken ct);
}