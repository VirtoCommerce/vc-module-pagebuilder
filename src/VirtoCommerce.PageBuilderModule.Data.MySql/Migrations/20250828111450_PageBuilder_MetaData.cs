using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.MySql.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_MetaData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "PageBuilderPage",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "PageBuilderPage",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserGroups",
                table: "PageBuilderPage",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Visibility",
                table: "PageBuilderPage",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

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
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "UserGroups",
                table: "PageBuilderPage");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "PageBuilderPage");

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
        }
    }
}
