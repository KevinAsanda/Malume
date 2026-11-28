namespace Malume.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public bool Enabled { get; set; } = true;

    public string? ServiceAccountJson { get; set; }

    public string? ServiceAccountFilePath { get; set; }
}
