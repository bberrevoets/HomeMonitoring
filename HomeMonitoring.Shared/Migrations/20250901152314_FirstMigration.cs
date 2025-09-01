using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMonitoring.Shared.Migrations
{
    /// <inheritdoc />
    public partial class FirstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ProductTypeRaw = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnergyReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivePowerW = table.Column<double>(type: "float", nullable: false),
                    TotalPowerImportT1KWh = table.Column<double>(type: "float", nullable: true),
                    TotalPowerImportT2KWh = table.Column<double>(type: "float", nullable: true),
                    TotalPowerExportT1KWh = table.Column<double>(type: "float", nullable: true),
                    TotalPowerExportT2KWh = table.Column<double>(type: "float", nullable: true),
                    TotalGasM3 = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_DeviceId_Timestamp",
                table: "EnergyReadings",
                columns: new[] { "DeviceId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnergyReadings");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
