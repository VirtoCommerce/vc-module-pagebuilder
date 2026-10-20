using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_MultilanguagePageCultureIndex : Migration
    {
        private static readonly string[] _indexColumns = ["MultilanguagePageId", "CultureName"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GroupedPageBuilderPage_MultilanguagePageId_CultureName",
                table: "GroupedPageBuilderPage",
                columns: _indexColumns,
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
