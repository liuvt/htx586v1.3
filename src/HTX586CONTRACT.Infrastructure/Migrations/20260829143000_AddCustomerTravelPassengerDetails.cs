using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829143000_AddCustomerTravelPassengerDetails")]
public partial class AddCustomerTravelPassengerDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CustomerTravelBirthYear",
            table: "Contracts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerTravelNote",
            table: "Contracts",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CustomerTravelBirthYear",
            table: "Contracts");

        migrationBuilder.DropColumn(
            name: "CustomerTravelNote",
            table: "Contracts");
    }
}
