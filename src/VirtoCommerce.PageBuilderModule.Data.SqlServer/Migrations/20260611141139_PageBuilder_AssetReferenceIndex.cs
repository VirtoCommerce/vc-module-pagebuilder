using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer.Migrations
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
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CultureName = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedAssetUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedAssetUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderAssetReference", x => x.Id);
                });

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
                name: "IX_PageBuilderAssetReference_StoreId_NormalizedAssetUrlHash_Status_CultureName",
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
