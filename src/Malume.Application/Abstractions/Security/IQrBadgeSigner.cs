namespace Malume.Application.Abstractions.Security;

public interface IQrBadgeSigner
{
    string PublicKeyBase64 { get; }

    string SignLearnerBadge(Guid learnerId, Guid schoolId);

    bool VerifyLearnerBadge(string rawPayload, Guid expectedLearnerId, Guid expectedSchoolId);
}
