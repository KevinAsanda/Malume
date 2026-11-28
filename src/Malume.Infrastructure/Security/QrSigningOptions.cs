namespace Malume.Infrastructure.Security;

public sealed class QrSigningOptions
{
    public const string SectionName = "QrSigning";

    public string? PrivateKeyPem { get; set; }

    public string? PrivateKeyBase64 { get; set; }
}
