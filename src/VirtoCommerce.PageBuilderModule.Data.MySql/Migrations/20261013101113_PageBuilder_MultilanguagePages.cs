using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.MySql.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_MultilanguagePages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MultilanguagePageId",
                table: "GroupedPageBuilderPage",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OverriddenFields",
                table: "GroupedPageBuilderPage",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceRevisionDate",
                table: "GroupedPageBuilderPage",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PageBuilderMultilanguagePage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permalink = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultCultureName = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Visibility = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UserGroups = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedBy = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderMultilanguagePage", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId",
                table: "GroupedPageBuilderPage",
                column: "MultilanguagePageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderMultilanguagePage_StoreId",
                table: "PageBuilderMultilanguagePage",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupedPageBuilderPage_PageBuilderMultilanguagePage_Multilan~",
                table: "GroupedPageBuilderPage",
                column: "MultilanguagePageId",
                principalTable: "PageBuilderMultilanguagePage",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupedPageBuilderPage_PageBuilderMultilanguagePage_Multilan~",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropTable(
                name: "PageBuilderMultilanguagePage");

            migrationBuilder.DropIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "MultilanguagePageId",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "OverriddenFields",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "SourceRevisionDate",
                table: "GroupedPageBuilderPage");
        }
    }
}
