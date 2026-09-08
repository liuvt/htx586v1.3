using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTX586CONTRACT.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908153000_AddCargoTransportPageTwoAndSecondDriver")]
public partial class AddCargoTransportPageTwoAndSecondDriver : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "SecondDriverPhoneNumber", table: "Contracts", type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SecondDriverLicenseNumber", table: "Contracts", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CargoTransportGoodsName", table: "Contracts", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CargoTransportRoute", table: "Contracts", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CargoLoadingPoint", table: "Contracts", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CargoDeliveryPoint", table: "Contracts", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CargoOtherInformation", table: "Contracts", type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.CreateTable(
            name: "ContractCargoHandlingEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                Location = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CargoWeight = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                EventTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                Confirmation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContractCargoHandlingEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContractCargoHandlingEvents_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_ContractCargoHandlingEvents_Contract_Type_SortOrder",
            table: "ContractCargoHandlingEvents",
            columns: new[] { "ContractId", "Type", "SortOrder" },
            unique: true,
            filter: "[IsDeleted] = 0");

        // Hợp đồng hàng hóa dùng cùng luồng ký với hợp đồng hành khách:
        // Tài xế 1 ký trước, sau đó khách hàng/Đại diện Bên A ký.
        migrationBuilder.Sql("UPDATE [ContractTypes] SET [RequireDriverSignature] = 1 WHERE [Code] = N'CARGO';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE [ContractTypes] SET [RequireDriverSignature] = 0 WHERE [Code] = N'CARGO';");
        migrationBuilder.DropTable(name: "ContractCargoHandlingEvents");
        migrationBuilder.DropColumn(name: "SecondDriverPhoneNumber", table: "Contracts");
        migrationBuilder.DropColumn(name: "SecondDriverLicenseNumber", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoTransportGoodsName", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoTransportRoute", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoLoadingPoint", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoDeliveryPoint", table: "Contracts");
        migrationBuilder.DropColumn(name: "CargoOtherInformation", table: "Contracts");
    }
}
