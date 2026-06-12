using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_AssetReferenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageBuilderAssetReference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedAssetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    NormalizedAssetUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderAssetReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderAssetReference_PageBuilderPage_PageId",
                        column: x => x.PageId,
                        principalTable: "PageBuilderPage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_NormalizedAssetUrlHash",
                table: "PageBuilderAssetReference",
                column: "NormalizedAssetUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_PageId",
                table: "PageBuilderAssetReference",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_PageId_NormalizedAssetUrlHash",
                table: "PageBuilderAssetReference",
                columns: new[] { "PageId", "NormalizedAssetUrlHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageBuilderAssetReference");
        }
    }
}
