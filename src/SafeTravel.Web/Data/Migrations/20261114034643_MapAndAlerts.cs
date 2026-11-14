using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeTravel.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MapAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "Alerts",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Alerts",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Alerts",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RiskZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskZones_IsActive",
                table: "RiskZones",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskZones");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Alerts");
        }
    }
}
