using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer.Migrations
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
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "PageBuilderPage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserGroups",
                table: "PageBuilderPage",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Visibility",
                table: "PageBuilderPage",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "GroupedPageBuilderPage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "GroupedPageBuilderPage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserGroups",
                table: "GroupedPageBuilderPage",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Visibility",
                table: "GroupedPageBuilderPage",
                type: "bit",
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
