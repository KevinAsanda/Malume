using Malume.Domain.Exceptions;

namespace Malume.Domain.ValueObjects;

public sealed record QrSignaturePayload
{
    public const string FormatPrefix = "MLM1";

    public Guid LearnerId { get; }

    public Guid SchoolId { get; }

    public string SignatureBase64 { get; }

    public QrSignaturePayload(Guid learnerId, Guid schoolId, string signatureBase64)
    {
        if (learnerId == Guid.Empty)
        {
            throw new DomainException("LearnerId cannot be empty.");
        }

        if (schoolId == Guid.Empty)
        {
            throw new DomainException("SchoolId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(signatureBase64))
        {
            throw new DomainException("Signature payload cannot be empty.");
        }

        LearnerId = learnerId;
        SchoolId = schoolId;
        SignatureBase64 = signatureBase64.Trim();
    }

    public string Format() => $"{FormatPrefix}:{LearnerId:D}:{SchoolId:D}:{SignatureBase64}";

    public static QrSignaturePayload Parse(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new DomainException("QR payload cannot be empty.");
        }

        string[] parts = rawPayload.Split(':', 4);
        if (parts.Length != 4 || !string.Equals(parts[0], FormatPrefix, StringComparison.Ordinal))
        {
            throw new DomainException("Invalid Malume QR badge prefix or format.");
        }

        if (!Guid.TryParse(parts[1], out Guid learnerId) || !Guid.TryParse(parts[2], out Guid schoolId))
        {
            throw new DomainException("Invalid learner or school identifier inside QR badge payload.");
        }

        return new QrSignaturePayload(learnerId, schoolId, parts[3]);
    }
}
