# Serverless Let's Encrypt certificate renewal on Azure

This repository renews Let's Encrypt certificates with a .NET Container App Job that talks directly to ACME and Azure SDKs.

- Let's Encrypt ACME certificate renewal is implemented in .NET 10 under [src/AzAcmeCertRenewal](src/AzAcmeCertRenewal).
- Domain verification is performed with DNS-01 challenges written directly to Azure DNS.
- Renewed certificates are imported to [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/certificates/certificate-scenarios) as new versions of configured Key Vault certificates.
- ACME account and certificate renewal state is maintained in a single application-owned JSON blob.
- The Azure host is an [Azure Container Apps job](https://learn.microsoft.com/en-us/azure/container-apps/jobs).

## Setup

The following instructions assume that you are using [Azure DNS](https://learn.microsoft.com/en-us/azure/dns/dns-overview) with your domain. If you are not, follow the Microsoft documentation to set up an Azure DNS Zone for your domain. [Tutorial: Host your domain in Azure DNS](https://learn.microsoft.com/en-us/azure/dns/dns-delegate-domain-azure-dns).

### Installation

1. Clone this git repository to your workstation.
1. Build and push a container image using the steps in [Container image](#container-image).
1. Deploy required Azure resources using the steps in the [Infrastructure](#infrastructure) section of this document.
1. Create an ACME state blob using [acme-state.sample.json](acme-state.sample.json) as a starting point.
1. Configure the app settings described in [Configuration](#configuration).

## Usage

### Infrastructure

The Bicep template [main.bicep](infra/main.bicep) deploys the Azure resources used by the renewal job. Notes:

- Container Apps managed environment using the `Consumption` workload profile.
- No virtual network integration is configured. This keeps the deployment on the Container Apps free tier and avoids the extra cost of dedicated networking resources.

**NOTE:** Your Azure DNS Zone must be in a Resource Group in the same Azure Subscription as the Resource Group you are deploying to.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FRylandDeGregory%2FAzFuncCertRenewal%2Fmain%2Finfra%2Fmain.json)

The template can also be deployed programmatically using [Azure PowerShell](https://learn.microsoft.com/en-us/powershell/module/az.resources/new-azresourcegroupdeployment) or the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/group/deployment?view=azure-cli-latest#az-group-deployment-create).

```PowerShell
# Azure PowerShell
$Params = @{
    ResourceGroupName = 'testing'
    TemplateFile      = './infra/main.bicep'
    dnsZoneResourceId = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/testing/providers/Microsoft.Network/dnszones/example.com'
    containerImage    = 'myregistry.azurecr.io/azacmecertrenewal:latest'
    Verbose           = $true
}
New-AzResourceGroupDeployment @Params

# Azure CLI
az group deployment create --resource-group 'testing' --template-file ./infra/main.bicep --parameters dnsZoneResourceId='/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/MyResourceGroup/providers/Microsoft.Network/dnszones/example.com' containerImage='myregistry.azurecr.io/azacmecertrenewal:latest' --verbose
```

### Container image

The [Dockerfile](Dockerfile) builds the .NET app as a self-contained Linux executable and copies it into a pinned .NET `runtime-deps` [Ubuntu chiseled image](https://github.com/dotnet/dotnet-docker/blob/main/documentation/ubuntu-chiseled.md).

Build the image from the repository root:

```PowerShell
docker build -t myregistry.azurecr.io/azacmecertrenewal:latest .
```

Push the image to the registry referenced by the `containerImage` deployment parameter:

```PowerShell
docker push myregistry.azurecr.io/azacmecertrenewal:latest
```

### CI/CD

GitHub Actions publishes the container image to the GitHub Container Registry.

The workflow in [.github/workflows/publish.yml](.github/workflows/publish.yml) publishes mutable convenience tags plus immutable build tags:

| Tag | Trigger |
| --- | --- |
| `preview` | Mutable alias for the latest pull request image from this repository. |
| `latest` | Mutable alias for the latest `main` image, including merged pull requests. |
| `sha-<commit-sha>` | Immutable source commit tag published for pull request and `main` builds. |
| `1.<run-number>.0` | Immutable run-version tag published for pull request and `main` builds. |

The workflow also asks BuildKit to publish provenance and SBOM attestations for each image. For deployments that need immutable image identity, prefer a `sha-<commit-sha>` tag, a `1.<run-number>.0` tag, or the image digest over `preview` or `latest`.

Pull request image publishing is restricted to branches from this repository; fork pull requests do not publish images.

### Telemetry

`APPLICATIONINSIGHTS_CONNECTION_STRING` is set by default. This allows the app to export OpenTelemetry logs, HTTP traces, and runtime metrics to Application Insights with the same `DefaultAzureCredential` used by Azure SDK clients.

### Add ACME state to Storage Account

Using [Azure Storage Explorer](https://learn.microsoft.com/en-us/azure/vs-azure-tools-storage-manage-with-storage-explorer), upload an ACME state JSON file to the configured blob container. By default, the app reads `acme-state.json`; override the blob name with `AZ_ACME_STATE_BLOB_NAME`.

The state blob defines the ACME account and the Key Vault certificates to maintain. The sample file includes separate certificate entries for an apex domain and `www` domain.

On first successful run, the app writes generated ACME account and certificate private keys back to the same state blob when those fields are missing.
If you are migrating from Posh-ACME, the existing account JWK can be converted to PKCS#8 PEM and stored in `accountKeyPem` with the existing ACME account URL in `accountUrl`.

Treat the state blob as sensitive secret material. Local files named `acme-state.json` are ignored by git for this reason.

### Configuration

The .NET app reads configuration from environment variables and authenticates to Azure with `DefaultAzureCredential`.

Required settings:

| Name | Description |
| --- | --- |
| `AZURE_STORAGE_ACCOUNT_BLOB_ENDPOINT` | Blob service endpoint for the storage account, for example `https://mystorage.blob.core.windows.net/`. |
| `AZURE_KEY_VAULT_ENDPOINT` | Key Vault endpoint, for example `https://myvault.vault.azure.net/`. |
| `AZURE_DNS_ZONE_RESOURCE_ID` | Full Azure resource ID of the DNS zone used for DNS-01 challenges. |

Optional settings:

| Name | Default | Description |
| --- | --- | --- |
| `AZURE_STORAGE_BLOB_CONTAINER_NAME` | `acme` | Blob container that stores the ACME state file. |
| `AZ_ACME_STATE_BLOB_NAME` | `acme-state.json` | Blob name of the ACME state JSON document. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | set by bicep deployment | Enables OpenTelemetry export to Application Insights when set. |

The Azure identity running the app needs access to:

- Read and write the configured state blob.
- Read and import certificates in the configured Key Vault.
- Read, create, update, and delete TXT record sets in the configured Azure DNS zone.
- Publish OpenTelemetry to the configured Application Insights resource.

### Run locally

From the repository root:

```PowerShell
dotnet build src/AzAcmeCertRenewal/AzAcmeCertRenewal.csproj
dotnet run --project src/AzAcmeCertRenewal/AzAcmeCertRenewal.csproj
```

The app loads the state blob, checks the matching Key Vault certificate expiration date for each configured certificate, renews certificates that are inside their renewal window, publishes and cleans up Azure DNS TXT records for DNS-01 validation, imports renewed PFX bytes into Key Vault, and saves updated state back to Blob Storage.

### Container Apps job

The Container Apps job runs on a UTC cron schedule. By default, the template runs it at `00:00` every Sunday:

```text
0 0 * * 0
```

Override the `containerJobCronExpression` bicep parameter during deployment to change the schedule. On each run, the job will:

1. Load ACME state from Blob Storage.
2. Check Key Vault certificate expiration dates.
3. Renew certificates that are inside their renewal window.
4. Publish and clean up Azure DNS TXT records for ACME DNS-01 validation.
5. Import renewed certificate(s) to Key Vault.
6. Save updated ACME state back to Blob Storage.

## Optional

### Configure Azure CDN custom domain to use Key Vault certificate

1. Navigate to your CDN profile, then to the endpoint using the Azure Portal.

<img width="593" alt="Screenshot 2023-02-01 at 12 22 40 PM" src="https://user-images.githubusercontent.com/18073815/216117168-6b508aa8-47de-400a-b48c-041e1b19f337.png">

1. Open the CDN endpoint's custom domain that you want to assign the certificate to.
1. In the custom domain, select the Key Vault certificate you just imported (make sure the Azure CDN identity can access the Key Vault).

<img width="712" alt="Screenshot 2023-02-01 at 12 20 08 PM" src="https://user-images.githubusercontent.com/18073815/216116513-b8ec396f-7fec-4bcb-86aa-ddf277aadd3d.png">

## License

This project is licensed under the [Apache License 2.0](LICENSE).

This repository includes code derived from Acmebot. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for attribution and third-party license details.
