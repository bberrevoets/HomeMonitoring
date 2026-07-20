using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMonitoring.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceStatusColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiVersion",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceInfoUpdatedAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmwareVersion",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WifiSsid",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WifiStrength",
                table: "Devices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceInfoUpdatedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FirmwareVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WifiSsid",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WifiStrength",
                table: "Devices");
        }
    }
}
