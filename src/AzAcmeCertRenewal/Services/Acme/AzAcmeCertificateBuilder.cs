using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Acmebot.Acme;

internal static class AzAcmeCertificateBuilder
{
    public static CertificateBuildResult CreateCertificateRequest(AzAcmeCertificateState certificateState)
    {
        var privateKey = !certificateState.AlwaysNewKey && !string.IsNullOrWhiteSpace(certificateState.PrivateKeyPem)
            ? LoadPrivateKey(certificateState.PrivateKeyPem)
            : CreatePrivateKey(certificateState.KeyType);

        if (certificateState.AlwaysNewKey || string.IsNullOrWhiteSpace(certificateState.PrivateKeyPem))
        {
            certificateState.PrivateKeyPem = ExportPrivateKeyPem(privateKey);
        }

        var csrDer = CreateCertificateSigningRequest(certificateState, privateKey);

        return new CertificateBuildResult(privateKey, csrDer);
    }

    public static byte[] CreatePfx(
        AzAcmeCertificateState certificateState,
        AcmeCertificateChain certificateChain,
        AsymmetricAlgorithm privateKey)
    {
        var collection = new X509Certificate2Collection();
        using var leafCertificate = CopyWithPrivateKey(certificateChain.Certificates[0], privateKey);
        collection.Add(leafCertificate);

        foreach (var issuerCertificate in certificateChain.Certificates.Skip(1))
        {
            collection.Add(issuerCertificate);
        }

        return collection.Export(X509ContentType.Pkcs12, certificateState.PfxPassword)
            ?? throw new InvalidOperationException($"Unable to export renewed certificate [{certificateState.Name}] as a PFX.");
    }

    private static AsymmetricAlgorithm LoadPrivateKey(string keyPem)
    {
        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(keyPem);
            return rsa;
        }
        catch (CryptographicException)
        {
        }

        try
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(keyPem);
            return ecdsa;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Certificate private key could not be loaded as RSA or ECDSA PEM.", ex);
        }
    }

    private static AsymmetricAlgorithm CreatePrivateKey(string? keyType)
    {
        return keyType?.Trim().ToLowerInvariant() switch
        {
            "ec-256" or "ecdsa-256" or "p-256" => ECDsa.Create(ECCurve.NamedCurves.nistP256),
            "ec-384" or "ecdsa-384" or "p-384" => ECDsa.Create(ECCurve.NamedCurves.nistP384),
            "ec-521" or "ecdsa-521" or "p-521" => ECDsa.Create(ECCurve.NamedCurves.nistP521),
            "3072" or "rsa-3072" => RSA.Create(3072),
            "4096" or "rsa-4096" => RSA.Create(4096),
            _ => RSA.Create(2048)
        };
    }

    private static byte[] CreateCertificateSigningRequest(AzAcmeCertificateState certificateState, AsymmetricAlgorithm privateKey)
    {
        var subject = new X500DistinguishedName($"CN={EscapeDistinguishedNameValue(certificateState.MainDomain)}");
        var request = privateKey switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256),
            _ => throw new NotSupportedException($"Unsupported certificate private key type [{privateKey.GetType().Name}].")
        };

        var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        foreach (var domain in certificateState.Domains)
        {
            subjectAlternativeNameBuilder.AddDnsName(domain);
        }

        request.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        if (certificateState.OcspMustStaple)
        {
            request.CertificateExtensions.Add(CreateOcspMustStapleExtension());
        }

        return request.CreateSigningRequest();
    }

    private static X509Extension CreateOcspMustStapleExtension()
    {
        return new X509Extension(
            new Oid("1.3.6.1.5.5.7.1.24"),
            [0x30, 0x03, 0x02, 0x01, 0x05],
            critical: false);
    }

    private static X509Certificate2 CopyWithPrivateKey(X509Certificate2 certificate, AsymmetricAlgorithm privateKey)
    {
        return privateKey switch
        {
            RSA rsa => certificate.CopyWithPrivateKey(rsa),
            ECDsa ecdsa => certificate.CopyWithPrivateKey(ecdsa),
            _ => throw new NotSupportedException($"Unsupported certificate private key type [{privateKey.GetType().Name}].")
        };
    }

    private static string ExportPrivateKeyPem(AsymmetricAlgorithm privateKey)
    {
        return privateKey switch
        {
            RSA rsa => rsa.ExportPkcs8PrivateKeyPem(),
            ECDsa ecdsa => ecdsa.ExportPkcs8PrivateKeyPem(),
            _ => throw new NotSupportedException($"Unsupported certificate private key type [{privateKey.GetType().Name}].")
        };
    }

    private static string EscapeDistinguishedNameValue(string value)
    {
        var escaped = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is ',' or '+' or '"' or '\\' or '<' or '>' or ';')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }
}

internal sealed record CertificateBuildResult(
    AsymmetricAlgorithm PrivateKey,
    byte[] CertificateSigningRequest) : IDisposable
{
    public void Dispose()
    {
        PrivateKey.Dispose();
    }
}