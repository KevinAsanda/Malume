using System.Security.Cryptography;
using System.Text;
using Malume.Application.Abstractions.Security;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Malume.Infrastructure.Security;

public sealed class EcdsaQrBadgeSigner : IQrBadgeSigner, IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly object _syncLock = new();

    public EcdsaQrBadgeSigner(IOptions<QrSigningOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        QrSigningOptions settings = options.Value;

        if (!string.IsNullOrWhiteSpace(settings.PrivateKeyPem))
        {
            _ecdsa = ECDsa.Create();
            _ecdsa.ImportFromPem(settings.PrivateKeyPem);
        }
        else if (!string.IsNullOrWhiteSpace(settings.PrivateKeyBase64))
        {
            _ecdsa = ECDsa.Create();
            byte[] pkcs8 = Convert.FromBase64String(settings.PrivateKeyBase64.Trim());
            _ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        }
        else
        {
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        }

        PublicKeyBase64 = Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());
    }

    public string PublicKeyBase64 { get; }

    public string SignLearnerBadge(Guid learnerId, Guid schoolId)
    {
        if (learnerId == Guid.Empty)
        {
            throw new DomainException("LearnerId cannot be empty.");
        }

        if (schoolId == Guid.Empty)
        {
            throw new DomainException("SchoolId cannot be empty.");
        }

        byte[] data = Encoding.UTF8.GetBytes(BuildCanonicalInput(learnerId, schoolId));
        byte[] signature;
        lock (_syncLock)
        {
            signature = _ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }

        return new QrSignaturePayload(learnerId, schoolId, Convert.ToBase64String(signature)).Format();
    }

    public bool VerifyLearnerBadge(string rawPayload, Guid expectedLearnerId, Guid expectedSchoolId)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return false;
        }

        QrSignaturePayload parsed;
        try
        {
            parsed = QrSignaturePayload.Parse(rawPayload);
        }
        catch (DomainException)
        {
            return false;
        }

        if (parsed.LearnerId != expectedLearnerId || parsed.SchoolId != expectedSchoolId)
        {
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(parsed.SignatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] data = Encoding.UTF8.GetBytes(BuildCanonicalInput(expectedLearnerId, expectedSchoolId));
        lock (_syncLock)
        {
            return _ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            _ecdsa.Dispose();
        }
    }

    private static string BuildCanonicalInput(Guid learnerId, Guid schoolId) =>
        $"{QrSignaturePayload.FormatPrefix}:{learnerId:D}:{schoolId:D}";
}
