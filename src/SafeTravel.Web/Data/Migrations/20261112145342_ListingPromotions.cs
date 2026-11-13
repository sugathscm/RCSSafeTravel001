using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeTravel.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ListingPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromoText",
                table: "Listings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromoTitle",
                table: "Listings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PromoUntil",
                table: "Listings",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromoText",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PromoTitle",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PromoUntil",
                table: "Listings");
        }
    }
}
