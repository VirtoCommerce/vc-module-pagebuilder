using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.MySql.Migrations
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
                    Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PageId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedAssetUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedAssetUrlHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
