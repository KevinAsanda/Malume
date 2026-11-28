using Malume.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Malume.Api.IntegrationTests.Security;

public sealed class EcdsaQrBadgeSignerTests
{
    [Fact]
    public void SignAndVerify_WithValidPayload_ShouldReturnTrue()
    {
        using var signer = new EcdsaQrBadgeSigner(Options.Create(new QrSigningOptions()));
        Guid learnerId = Guid.NewGuid();
        Guid schoolId = Guid.NewGuid();

        string payload = signer.SignLearnerBadge(learnerId, schoolId);

        payload.ShouldStartWith($"MLM1:{learnerId:D}:{schoolId:D}:");
        signer.VerifyLearnerBadge(payload, learnerId, schoolId).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WithTamperedSignatureOrMismatchedIds_ShouldReturnFalse()
    {
        using var signer = new EcdsaQrBadgeSigner(Options.Create(new QrSigningOptions()));
        Guid learnerId = Guid.NewGuid();
        Guid schoolId = Guid.NewGuid();

        string payload = signer.SignLearnerBadge(learnerId, schoolId);
        string tampered = $"{payload[..^4]}AAAA";

        signer.VerifyLearnerBadge(payload, Guid.NewGuid(), schoolId).ShouldBeFalse();
        signer.VerifyLearnerBadge(payload, learnerId, Guid.NewGuid()).ShouldBeFalse();
        signer.VerifyLearnerBadge(tampered, learnerId, schoolId).ShouldBeFalse();
        signer.VerifyLearnerBadge("INVALID:1:2:3", learnerId, schoolId).ShouldBeFalse();
    }
}
