namespace Malume.Domain.Enums;

public enum DriverComplianceStatus
{
    PendingReview = 1,
    ActiveCompliant = 2,
    ActiveExpiringSoon = 3,
    SuspendedExpiredDoc = 4,
    SuspendedSafetyIncident = 5
}

public enum DriverOperatingMode
{
    FixedRoute = 1,
    OffPeakAdHoc = 2
}

public enum ComplianceDocumentType
{
    Prdp = 1,
    PoliceClearance = 2,
    LicenseDisc = 3,
    CertificateOfFitness = 4
}
