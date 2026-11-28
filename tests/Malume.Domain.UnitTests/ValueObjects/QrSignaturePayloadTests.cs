using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.ValueObjects;

public sealed class QrSignaturePayloadTests
{
    [Fact]
    public void FormatAndParse_ShouldRoundTripPayload()
    {
        Guid learnerId = Guid.NewGuid();
        Guid schoolId = Guid.NewGuid();
        var payload = new QrSignaturePayload(learnerId, schoolId, "SIG_ABC_123==");

        string formatted = payload.Format();
        QrSignaturePayload parsed = QrSignaturePayload.Parse(formatted);

        parsed.LearnerId.ShouldBe(learnerId);
        parsed.SchoolId.ShouldBe(schoolId);
        parsed.SignatureBase64.ShouldBe("SIG_ABC_123==");
    }

    [Fact]
    public void Parse_WithInvalidPrefix_ShouldThrowDomainException()
    {
        Should.Throw<DomainException>(() =>
            QrSignaturePayload.Parse($"BAD:{Guid.NewGuid()}:{Guid.NewGuid()}:SIG"));
    }
}
