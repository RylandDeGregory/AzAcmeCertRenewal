using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

using Acmebot.Acme.Challenges;

using Microsoft.Extensions.Logging;

internal sealed class AzureDnsChallengePublisher(
    ResourceIdentifier dnsZoneResourceId,
    TokenCredential credential,
    ILogger<AzureDnsChallengePublisher> logger,
    long ttl = 60)
{
    private readonly DnsZoneResource _dnsZone = new ArmClient(credential, dnsZoneResourceId.SubscriptionId).GetDnsZoneResource(dnsZoneResourceId);
    private readonly string _zoneName = GetZoneName(dnsZoneResourceId);

    public async Task PublishAsync(AcmeDns01ChallengeInstruction instruction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var recordSetName = GetRecordSetName(instruction.RecordName);
        var txtRecords = _dnsZone.GetDnsTxtRecords();
        var recordData = await GetRecordDataAsync(txtRecords, recordSetName, ct);
        var hasChallengeValue = recordData.DnsTxtRecords.Any(txtRecord => ContainsTxtValue(txtRecord, instruction.RecordValue));

        if (!hasChallengeValue)
        {
            recordData.DnsTxtRecords.Add(new DnsTxtRecordInfo
            {
                Values = { instruction.RecordValue }
            });
        }

        logger.LogInformation("Publish Azure DNS TXT record {RecordSetName} in zone {DnsZoneName}", recordSetName, _zoneName);

        await txtRecords.CreateOrUpdateAsync(
            WaitUntil.Completed,
            recordSetName,
            recordData,
            cancellationToken: ct);
    }

    public async Task CleanupAsync(AcmeDns01ChallengeInstruction instruction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var recordSetName = GetRecordSetName(instruction.RecordName);
        var txtRecords = _dnsZone.GetDnsTxtRecords();

        try
        {
            var txtRecord = await txtRecords.GetAsync(recordSetName, ct);
            var recordData = txtRecord.Value.Data;

            for (var i = recordData.DnsTxtRecords.Count - 1; i >= 0; i--)
            {
                var txtValue = recordData.DnsTxtRecords[i];

                if (ContainsTxtValue(txtValue, instruction.RecordValue))
                {
                    recordData.DnsTxtRecords.RemoveAt(i);
                }
            }

            logger.LogInformation("Remove Azure DNS TXT record {RecordSetName} from zone {DnsZoneName}", recordSetName, _zoneName);

            if (recordData.DnsTxtRecords.Count == 0)
            {
                await txtRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: ct);
                return;
            }

            await txtRecords.CreateOrUpdateAsync(
                WaitUntil.Completed,
                recordSetName,
                recordData,
                cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return;
        }
    }

    private async Task<DnsTxtRecordData> GetRecordDataAsync(DnsTxtRecordCollection txtRecords, string recordSetName, CancellationToken ct)
    {
        try
        {
            var txtRecord = await txtRecords.GetAsync(recordSetName, ct);
            return txtRecord.Value.Data;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new DnsTxtRecordData
            {
                TtlInSeconds = ttl
            };
        }
    }

    private static bool ContainsTxtValue(DnsTxtRecordInfo txtRecord, string recordValue)
    {
        return txtRecord.Values.Count == 1 &&
            string.Equals(txtRecord.Values[0], recordValue, StringComparison.Ordinal);
    }

    private string GetRecordSetName(string recordName)
    {
        var normalizedRecordName = recordName.TrimEnd('.');
        var normalizedZoneName = _zoneName.TrimEnd('.');

        if (!normalizedRecordName.EndsWith($".{normalizedZoneName}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"DNS challenge record [{recordName}] is not under configured zone [{_zoneName}].");
        }

        return normalizedRecordName[..^(normalizedZoneName.Length + 1)];
    }

    private static string GetZoneName(ResourceIdentifier dnsZoneResourceId)
    {
        if (string.IsNullOrWhiteSpace(dnsZoneResourceId.Name))
        {
            throw new ArgumentException("DNS zone resource id is invalid.", nameof(dnsZoneResourceId));
        }

        return dnsZoneResourceId.Name;
    }
}