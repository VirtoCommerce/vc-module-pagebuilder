using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.MySql.Migrations
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
                unique: true);
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
