using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PageBuilder_SharedComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentAssetReference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SharedComponentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedAssetUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedAssetUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponentAssetReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentAssetReference_PageBuilderSharedComponent_SharedComponentId",
                        column: x => x.SharedComponentId,
                        principalTable: "PageBuilderSharedComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentContent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ComponentContent = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponentContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentContent_PageBuilderSharedComponent_Id",
                        column: x => x.Id,
                        principalTable: "PageBuilderSharedComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentReference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SharedComponentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponentReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentReference_PageBuilderPage_PageId",
                        column: x => x.PageId,
                        principalTable: "PageBuilderPage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentReference_PageBuilderSharedComponent_SharedComponentId",
                        column: x => x.SharedComponentId,
                        principalTable: "PageBuilderSharedComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponent_StoreId",
                table: "PageBuilderSharedComponent",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentAssetReference_NormalizedAssetUrlHash",
                table: "PageBuilderSharedComponentAssetReference",
                column: "NormalizedAssetUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentAssetReference_SharedComponentId",
                table: "PageBuilderSharedComponentAssetReference",
                column: "SharedComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentAssetReference_SharedComponentId_NormalizedAssetUrlHash",
                table: "PageBuilderSharedComponentAssetReference",
                columns: new[] { "SharedComponentId", "NormalizedAssetUrlHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentReference_PageId",
                table: "PageBuilderSharedComponentReference",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentReference_PageId_SharedComponentId",
                table: "PageBuilderSharedComponentReference",
                columns: new[] { "PageId", "SharedComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentReference_SharedComponentId",
                table: "PageBuilderSharedComponentReference",
                column: "SharedComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageBuilderSharedComponentAssetReference");

            migrationBuilder.DropTable(
                name: "PageBuilderSharedComponentContent");

            migrationBuilder.DropTable(
                name: "PageBuilderSharedComponentReference");

            migrationBuilder.DropTable(
                name: "PageBuilderSharedComponent");
        }
    }
}
