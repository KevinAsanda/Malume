using Malume.Domain.Common;

namespace Malume.Domain.Entities;

public sealed class Fleet : AuditableEntity
{
    private Fleet()
    {
    }

    public Fleet(
        string name,
        Guid ownerUserId,
        string? companyRegistrationNumber = null,
        string? paystackSubaccountCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        OwnerUserId = ownerUserId;
        CompanyRegistrationNumber = companyRegistrationNumber?.Trim();
        PaystackSubaccountCode = paystackSubaccountCode?.Trim();
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string? CompanyRegistrationNumber { get; private set; }

    public string? PaystackSubaccountCode { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public bool IsActive { get; private set; }

    public void SetPaystackSubaccount(string subaccountCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountCode);
        PaystackSubaccountCode = subaccountCode.Trim();
        MarkUpdated();
    }
}
