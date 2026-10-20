using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_MultilanguagePageCultureIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId_CultureName",
                table: "GroupedPageBuilderPage",
                columns: new[] { "MultilanguagePageId", "CultureName" },
                unique: true,
                filter: "[MultilanguagePageId] IS NOT NULL AND [CultureName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId_CultureName",
                table: "GroupedPageBuilderPage");
        }
    }
}
