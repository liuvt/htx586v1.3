using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908130000_AddCompanyBusinessLicenseIssuedPlace")]
public partial class AddCompanyBusinessLicenseIssuedPlace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessLicenseIssuedPlace",
            table: "CompanyProfiles",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE [CompanyProfiles]
            SET [BusinessLicenseIssuedPlace] = N'Sở Giao Thông Vận Tải Cần Thơ'
            WHERE [BusinessLicenseIssuedPlace] IS NULL OR LTRIM(RTRIM([BusinessLicenseIssuedPlace])) = N'';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BusinessLicenseIssuedPlace",
            table: "CompanyProfiles");
    }
}
