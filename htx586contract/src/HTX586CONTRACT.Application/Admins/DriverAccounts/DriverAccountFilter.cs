namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

public sealed class DriverAccountFilter
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public string? AdminId { get; set; }
    public Guid? CompanyProfileId { get; set; } // tương thích dữ liệu cũ
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
