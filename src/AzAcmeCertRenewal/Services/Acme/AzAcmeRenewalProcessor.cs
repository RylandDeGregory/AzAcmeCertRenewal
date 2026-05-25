using Acmebot.Acme;
using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;

using Microsoft.Extensions.Logging;

internal sealed class AzAcmeRenewalProcessor(ILogger<AzAcmeRenewalProcessor> logger)
{
    private static readonly TimeSpan s_defaultValidationTimeout = TimeSpan.FromSeconds(120);

    private static DateTimeOffset CreateValidationDeadline(int validationTimeoutSeconds)
    {
        var validationTimeout = validationTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(validationTimeoutSeconds)
            : s_defaultValidationTimeout;

        return DateTimeOffset.UtcNow.Add(validationTimeout);
    }
    public async Task<AcmeResult<AcmeOrderResource>> CreateRenewalOrderAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        AzAcmeCertificateState certificateState,
        CancellationToken ct)
    {
        var identifiers = certificateState.Domains
            .Select(domain => new AcmeIdentifier
            {
                Type = AcmeIdentifierTypes.Dns,
                Value = domain
            })
            .ToArray();

        logger.LogInformation("Create ACME order for domains {Domains}", string.Join(", ", certificateState.Domains));

        return await acmeClient.CreateOrderAsync(
            account,
            identifiers,
            cancellationToken: ct);
    }

    public async Task ProcessAuthorizationsAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        AzureDnsChallengePublisher dnsChallengePublisher,
        AcmeOrderResource order,
        AzAcmeCertificateState certificateState,
        CancellationToken ct)
    {
        var pendingChallengeUrls = new List<Uri>();
        var publishedInstructions = new List<AcmeDns01ChallengeInstruction>();

        foreach (var authorizationUrl in order.Authorizations)
        {
            var authorizationResult = await acmeClient.GetAuthorizationAsync(account, authorizationUrl, ct);
            var authorization = authorizationResult.Resource;

            if (authorization.Status == AcmeAuthorizationStatuses.Valid)
            {
                logger.LogInformation("Authorization for {IdentifierValue} is already valid", authorization.Identifier.Value);
                continue;
            }

            if (authorization.Status != AcmeAuthorizationStatuses.Pending)
            {
                throw new InvalidOperationException($"Authorization for [{authorization.Identifier.Value}] is [{authorization.Status}] and cannot be completed.");
            }

            var dnsChallenge = authorization.Challenges.FirstOrDefault(challenge => challenge.Type == AcmeChallengeTypes.Dns01)
                ?? throw new InvalidOperationException($"Authorization for [{authorization.Identifier.Value}] does not include a dns-01 challenge.");

            var instruction = AcmeChallengeInstructions.CreateDns01(account, authorization, dnsChallenge);
            await dnsChallengePublisher.PublishAsync(instruction, ct);

            publishedInstructions.Add(instruction);
            pendingChallengeUrls.Add(dnsChallenge.Url);
        }

        if (pendingChallengeUrls.Count == 0)
        {
            return;
        }

        try
        {
            if (certificateState.DnsSleepSeconds > 0)
            {
                logger.LogInformation("Wait {DnsSleepSeconds} seconds for DNS TXT records to propagate", certificateState.DnsSleepSeconds);
                await Task.Delay(TimeSpan.FromSeconds(certificateState.DnsSleepSeconds), ct);
            }

            foreach (var challengeUrl in pendingChallengeUrls)
            {
                logger.LogInformation("Answer ACME dns-01 challenge {ChallengeUrl}", challengeUrl);
                await acmeClient.AnswerChallengeAsync(account, challengeUrl, ct);
            }

            await WaitForChallengeValidationAsync(acmeClient, account, pendingChallengeUrls, certificateState.ValidationTimeoutSeconds, ct);
        }
        finally
        {
            foreach (var instruction in publishedInstructions)
            {
                await dnsChallengePublisher.CleanupAsync(instruction, ct);
            }
        }
    }

    private async Task WaitForChallengeValidationAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        IReadOnlyCollection<Uri> challengeUrls,
        int validationTimeoutSeconds,
        CancellationToken ct)
    {
        var deadline = CreateValidationDeadline(validationTimeoutSeconds);
        var pendingChallengeUrls = challengeUrls.ToHashSet();

        while (pendingChallengeUrls.Count > 0)
        {
            foreach (var challengeUrl in pendingChallengeUrls.ToArray())
            {
                var challengeResult = await acmeClient.GetChallengeAsync(account, challengeUrl, ct);
                var challenge = challengeResult.Resource;

                if (challenge.Status == AcmeChallengeStatuses.Valid)
                {
                    logger.LogInformation("ACME dns-01 challenge {ChallengeUrl} is valid", challengeUrl);
                    pendingChallengeUrls.Remove(challengeUrl);
                    continue;
                }

                if (challenge.Status == AcmeChallengeStatuses.Invalid)
                {
                    throw new InvalidOperationException($"ACME dns-01 challenge [{challengeUrl}] failed validation: {challenge.Error?.Detail ?? challenge.Error?.Type?.ToString() ?? "No error detail returned."}");
                }
            }

            if (pendingChallengeUrls.Count == 0)
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for [{pendingChallengeUrls.Count}] ACME dns-01 challenge(s) to validate.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    public async Task<byte[]> FinalizeOrderAndCreatePfxAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        AcmeResult<AcmeOrderResource> orderResult,
        AzAcmeCertificateState certificateState,
        CancellationToken ct)
    {
        var orderUrl = orderResult.Location ?? throw new InvalidOperationException("ACME server did not return a renewal order URL.");
        var readyOrder = await WaitForOrderReadyAsync(acmeClient, account, orderUrl, certificateState.ValidationTimeoutSeconds, ct);
        var finalizeUrl = readyOrder.Finalize ?? throw new InvalidOperationException("ACME renewal order does not include a finalize URL.");

        using var certificateRequest = AzAcmeCertificateBuilder.CreateCertificateRequest(certificateState);
        logger.LogInformation("Finalize ACME order {OrderUrl}", orderUrl);

        var finalizedOrder = await acmeClient.FinalizeOrderAsync(account, finalizeUrl, certificateRequest.CertificateSigningRequest, ct);
        var validOrder = await WaitForOrderValidAsync(acmeClient, account, orderUrl, finalizedOrder.Resource, certificateState.ValidationTimeoutSeconds, ct);
        var certificateUrl = validOrder.Certificate ?? throw new InvalidOperationException("ACME renewal order completed without a certificate URL.");

        logger.LogInformation("Download renewed certificate from {CertificateUrl}", certificateUrl);
        var certificateChain = await acmeClient.DownloadCertificateAsync(account, certificateUrl, ct);

        return AzAcmeCertificateBuilder.CreatePfx(certificateState, certificateChain, certificateRequest.PrivateKey);
    }

    private static async Task<AcmeOrderResource> WaitForOrderReadyAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        Uri orderUrl,
        int validationTimeoutSeconds,
        CancellationToken ct)
    {
        var deadline = CreateValidationDeadline(validationTimeoutSeconds);

        while (true)
        {
            var orderResult = await acmeClient.GetOrderAsync(account, orderUrl, ct);
            var order = orderResult.Resource;

            if (order.Status == AcmeOrderStatuses.Ready || order.Status == AcmeOrderStatuses.Valid)
            {
                return order;
            }

            if (order.Status == AcmeOrderStatuses.Invalid)
            {
                throw new InvalidOperationException($"ACME order [{orderUrl}] is invalid: {order.Error?.Detail ?? order.Error?.Type?.ToString() ?? "No error detail returned."}");
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for ACME order [{orderUrl}] to become ready.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private static async Task<AcmeOrderResource> WaitForOrderValidAsync(
        AcmeClient acmeClient,
        AcmeAccountHandle account,
        Uri orderUrl,
        AcmeOrderResource currentOrder,
        int validationTimeoutSeconds,
        CancellationToken ct)
    {
        var deadline = CreateValidationDeadline(validationTimeoutSeconds);
        var order = currentOrder;

        while (true)
        {
            if (order.Status == AcmeOrderStatuses.Valid)
            {
                return order;
            }

            if (order.Status == AcmeOrderStatuses.Invalid)
            {
                throw new InvalidOperationException($"ACME order [{orderUrl}] is invalid: {order.Error?.Detail ?? order.Error?.Type?.ToString() ?? "No error detail returned."}");
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for ACME order [{orderUrl}] to become valid.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            var orderResult = await acmeClient.GetOrderAsync(account, orderUrl, ct);
            order = orderResult.Resource;
        }
    }
}