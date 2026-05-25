using System.Net;
using System.Security.Cryptography;

using Acmebot.Acme;
using Acmebot.Acme.Models;

using Microsoft.Extensions.Logging;

internal sealed class AzAcmeAccount(ILogger<AzAcmeAccount> logger)
{
    public async Task<AcmeAccountHandle> LoadOrCreateAsync(AcmeClient acmeClient, AzAcmeState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(acmeClient);
        ArgumentNullException.ThrowIfNull(state);

        var signer = CreateSigner(state);

        if (!string.IsNullOrWhiteSpace(state.AccountUrl))
        {
            try
            {
                var account = await acmeClient.FindAccountAsync(signer, ct);
                state.AccountUrl = account.AccountUrl.ToString();
                return account;
            }
            catch (AcmeProtocolException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("No existing ACME account was found for the configured account key. Creating a new account");
            }
        }
        else
        {
            logger.LogInformation("No ACME account URL is configured. Creating a new account");
        }

        var createdAccount = await acmeClient.CreateAccountAsync(
            signer,
            new AcmeNewAccountRequest
            {
                Contact = state.Contact,
                TermsOfServiceAgreed = state.TermsOfServiceAgreed
            },
            cancellationToken: ct);

        state.AccountUrl = createdAccount.AccountUrl.ToString();
        return createdAccount;
    }

    private AcmeSigner CreateSigner(AzAcmeState state)
    {
        // Generate new account key
        if (string.IsNullOrWhiteSpace(state.AccountKeyPem))
        {
            var accountKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            state.AccountKeyPem = accountKey.ExportPkcs8PrivateKeyPem();
            return AcmeSigner.Create(accountKey, ownsKey: true);
        }

        // Import existing account key
        try
        {
            var accountKey = ECDsa.Create();
            accountKey.ImportFromPem(state.AccountKeyPem);
            return AcmeSigner.Create(accountKey, ownsKey: true);
        }
        catch (CryptographicException)
        {
            logger.LogInformation("The configured ACME account key could not be loaded as ECDSA PEM. Attempting to load as RSA PEM");
        }

        try
        {
            var accountKey = RSA.Create();
            accountKey.ImportFromPem(state.AccountKeyPem);
            return AcmeSigner.Create(accountKey, ownsKey: true);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("The configured ACME account key could not be loaded as ECDSA or RSA PEM.", ex);
        }
    }
}