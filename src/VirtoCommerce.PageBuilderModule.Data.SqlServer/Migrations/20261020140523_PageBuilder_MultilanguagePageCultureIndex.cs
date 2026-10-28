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
            // A database that ran PageBuilder_MultilanguagePages before this index may have two versions of one language
            // in a multilanguage page. The oldest stays linked; the others become separate Landing Pages, so the index can be created.
            migrationBuilder.Sql(@"
                UPDATE g SET MultilanguagePageId = NULL, OverriddenFields = NULL, SourceRevisionDate = NULL
                FROM GroupedPageBuilderPage g
                WHERE g.MultilanguagePageId IS NOT NULL AND g.CultureName IS NOT NULL AND EXISTS (
                    SELECT 1 FROM GroupedPageBuilderPage k
                    WHERE k.MultilanguagePageId = g.MultilanguagePageId AND k.CultureName = g.CultureName
                      AND (k.CreatedDate < g.CreatedDate OR (k.CreatedDate = g.CreatedDate AND k.Id < g.Id)))");

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
