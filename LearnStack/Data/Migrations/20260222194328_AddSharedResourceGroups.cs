using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnStack.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedResourceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedResourceGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ShareToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedResourceGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedResourceGroups_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedResourceGroupItems",
                columns: table => new
                {
                    SharedResourceGroupId = table.Column<int>(type: "int", nullable: false),
                    LearningResourceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedResourceGroupItems", x => new { x.SharedResourceGroupId, x.LearningResourceId });
                    table.ForeignKey(
                        name: "FK_SharedResourceGroupItems_LearningResources_LearningResourceId",
                        column: x => x.LearningResourceId,
                        principalTable: "LearningResources",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SharedResourceGroupItems_SharedResourceGroups_SharedResourceGroupId",
                        column: x => x.SharedResourceGroupId,
                        principalTable: "SharedResourceGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedResourceGroupItems_LearningResourceId",
                table: "SharedResourceGroupItems",
                column: "LearningResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedResourceGroups_ShareToken",
                table: "SharedResourceGroups",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedResourceGroups_UserId",
                table: "SharedResourceGroups",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedResourceGroupItems");

            migrationBuilder.DropTable(
                name: "SharedResourceGroups");
        }
    }
}
