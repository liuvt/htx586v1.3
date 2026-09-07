using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907182000_AddCargoContractDetails")]
public partial class AddCargoContractDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CargoSpecification",
            table: "Contracts",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CargoQuantity",
            table: "Contracts",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CargoSpecification", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoQuantity", table: "Contracts");
    }
}
