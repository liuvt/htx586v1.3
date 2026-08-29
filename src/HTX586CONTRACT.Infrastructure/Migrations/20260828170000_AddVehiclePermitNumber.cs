using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260828170000_AddVehiclePermitNumber")]
public partial class AddVehiclePermitNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PermitNumber",
            table: "Vehicles",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PermitNumber",
            table: "Vehicles");
    }
}
