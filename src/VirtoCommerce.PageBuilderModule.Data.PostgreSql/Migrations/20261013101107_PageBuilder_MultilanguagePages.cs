using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.PostgreSql.Migrations
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
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverriddenFields",
                table: "GroupedPageBuilderPage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceRevisionDate",
                table: "GroupedPageBuilderPage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PageBuilderMultilanguagePage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Permalink = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DefaultCultureName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    OrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Visibility = table.Column<bool>(type: "boolean", nullable: false),
                    UserGroups = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderMultilanguagePage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId",
                table: "GroupedPageBuilderPage",
                column: "MultilanguagePageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderMultilanguagePage_StoreId",
                table: "PageBuilderMultilanguagePage",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupedPageBuilderPage_PageBuilderMultilanguagePage_Multila~",
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
                name: "FK_GroupedPageBuilderPage_PageBuilderMultilanguagePage_Multila~",
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
