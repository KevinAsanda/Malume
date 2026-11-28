namespace Malume.Infrastructure.Payments;

public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";

    public string? SecretKey { get; set; }

    public string? PublicKey { get; set; }

    public string? WebhookSecret { get; set; }

    public string BaseUrl { get; set; } = "https://api.paystack.co";
}
