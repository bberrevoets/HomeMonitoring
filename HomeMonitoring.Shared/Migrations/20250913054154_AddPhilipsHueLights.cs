using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMonitoring.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddPhilipsHueLights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HueLights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HueId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManufacturerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BridgeIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HueLights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HueLightReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HueLightId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    On = table.Column<bool>(type: "bit", nullable: false),
                    Brightness = table.Column<byte>(type: "tinyint", nullable: false),
                    Hue = table.Column<int>(type: "int", nullable: true),
                    Saturation = table.Column<byte>(type: "tinyint", nullable: true),
                    ColorTemperature = table.Column<int>(type: "int", nullable: true),
                    Reachable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HueLightReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HueLightReadings_HueLights_HueLightId",
                        column: x => x.HueLightId,
                        principalTable: "HueLights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HueLightReadings_HueLightId_Timestamp",
                table: "HueLightReadings",
                columns: new[] { "HueLightId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_HueLights_HueId_BridgeIpAddress",
                table: "HueLights",
                columns: new[] { "HueId", "BridgeIpAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HueLightReadings");

            migrationBuilder.DropTable(
                name: "HueLights");
        }
    }
}
