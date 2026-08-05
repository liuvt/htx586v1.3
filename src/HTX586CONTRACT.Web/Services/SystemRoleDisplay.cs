namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Tên hiển thị tiếng Việt cho các role hệ thống.
/// Giá trị role Identity vẫn giữ nguyên để không ảnh hưởng phân quyền.
/// </summary>
public static class SystemRoleDisplay
{
    public const string AdminRole = "Admin";
    public const string VehicleOwnerRole = "VehicleOwner";
    public const string OwnerRole = "Owner";

    public static string Name(string? role) => role switch
    {
        AdminRole => "Quản lý",
        VehicleOwnerRole => "Chủ xe",
        OwnerRole => "Chủ hệ thống",
        _ => string.IsNullOrWhiteSpace(role) ? "—" : role
    };
}
