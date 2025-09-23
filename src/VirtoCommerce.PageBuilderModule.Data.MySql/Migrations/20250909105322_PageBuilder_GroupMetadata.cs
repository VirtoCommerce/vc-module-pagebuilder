using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.MySql.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_GroupMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CultureName",
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "Permalink",
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GroupedPageBuilderPage");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "GroupedPageBuilderPage",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "GroupedPageBuilderPage",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserGroups",
                table: "GroupedPageBuilderPage",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Visibility",
                table: "GroupedPageBuilderPage",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "UserGroups",
                table: "GroupedPageBuilderPage");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "GroupedPageBuilderPage");

            migrationBuilder.AddColumn<string>(
                name: "CultureName",
                table: "PageBuilderPage",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PageBuilderPage",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Permalink",
                table: "PageBuilderPage",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "GroupedPageBuilderPage",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
