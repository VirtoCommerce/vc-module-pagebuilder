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
                    GroupId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CultureName = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedAssetUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedAssetUrlHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderAssetReference", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_GroupId",
                table: "PageBuilderAssetReference",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_PageId",
                table: "PageBuilderAssetReference",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_PageId_NormalizedAssetUrlHash",
                table: "PageBuilderAssetReference",
                columns: new[] { "PageId", "NormalizedAssetUrlHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderAssetReference_StoreId_NormalizedAssetUrlHash_Sta~",
                table: "PageBuilderAssetReference",
                columns: new[] { "StoreId", "NormalizedAssetUrlHash", "Status", "CultureName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageBuilderAssetReference");
        }
    }
}
