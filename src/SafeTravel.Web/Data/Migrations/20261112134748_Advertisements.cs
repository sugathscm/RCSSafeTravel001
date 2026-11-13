using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeTravel.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Advertisements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Advertisements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    LinkLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advertisements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Advertisements_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Slot",
                table: "Advertisements",
                column: "Slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_SponsorId",
                table: "Advertisements",
                column: "SponsorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Advertisements");
        }
    }
}
