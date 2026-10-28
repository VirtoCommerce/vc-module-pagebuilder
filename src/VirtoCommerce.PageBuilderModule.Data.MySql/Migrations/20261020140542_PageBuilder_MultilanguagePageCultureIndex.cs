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
            // A database that ran PageBuilder_MultilanguagePages before this index may have two versions of one language
            // in a multilanguage page. The oldest stays linked; the others become separate Landing Pages, so the index can be created.
            migrationBuilder.Sql(@"
                UPDATE GroupedPageBuilderPage g
                JOIN (
                    SELECT o.Id FROM GroupedPageBuilderPage o
                    JOIN GroupedPageBuilderPage k ON k.MultilanguagePageId = o.MultilanguagePageId AND k.CultureName = o.CultureName
                      AND (k.CreatedDate < o.CreatedDate OR (k.CreatedDate = o.CreatedDate AND k.Id < o.Id))
                    WHERE o.MultilanguagePageId IS NOT NULL AND o.CultureName IS NOT NULL
                ) d ON d.Id = g.Id
                SET g.MultilanguagePageId = NULL, g.OverriddenFields = NULL, g.SourceRevisionDate = NULL");

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
