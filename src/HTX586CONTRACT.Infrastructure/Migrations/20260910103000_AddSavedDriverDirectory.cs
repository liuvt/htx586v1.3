using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910103000_AddSavedDriverDirectory")]
public partial class AddSavedDriverDirectory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavedDrivers",
            columns: table => new
            {
                DriverLicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DriverLicenseClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedDrivers", x => x.DriverLicenseNumber);
                table.ForeignKey(
                    name: "FK_SavedDrivers_AspNetUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SavedDrivers_CreatedBy_CreatedAt",
            table: "SavedDrivers",
            columns: new[] { "CreatedByUserId", "CreatedAt" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_SavedDrivers_CreatedBy_IsDeleted",
            table: "SavedDrivers",
            columns: new[] { "CreatedByUserId", "IsDeleted" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SavedDrivers");
    }
}
