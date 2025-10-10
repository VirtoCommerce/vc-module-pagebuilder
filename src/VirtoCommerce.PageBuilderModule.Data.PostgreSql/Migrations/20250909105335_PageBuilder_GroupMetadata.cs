using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.PostgreSql.Migrations
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
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "GroupedPageBuilderPage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserGroups",
                table: "GroupedPageBuilderPage",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Visibility",
                table: "GroupedPageBuilderPage",
                type: "boolean",
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
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PageBuilderPage",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Permalink",
                table: "PageBuilderPage",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "GroupedPageBuilderPage",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
