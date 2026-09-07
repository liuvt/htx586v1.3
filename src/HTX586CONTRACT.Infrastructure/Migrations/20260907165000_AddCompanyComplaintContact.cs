using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907165000_AddCompanyComplaintContact")]
public partial class AddCompanyComplaintContact : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ComplaintContact",
            table: "CompanyProfiles",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE [CompanyProfiles]
            SET [ComplaintContact] = N'Sở GTVT Cần Thơ: 0939.984.333 - 0907.877.758'
            WHERE [ComplaintContact] IS NULL OR LTRIM(RTRIM([ComplaintContact])) = N'';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ComplaintContact",
            table: "CompanyProfiles");
    }
}
