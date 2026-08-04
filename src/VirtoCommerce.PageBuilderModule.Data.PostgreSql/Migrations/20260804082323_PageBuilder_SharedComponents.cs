using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.PageBuilderModule.Data.PostgreSql.Migrations
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
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentAssetReference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SharedComponentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedAssetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    NormalizedAssetUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponentAssetReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentAssetReference_PageBuilderSharedC~",
                        column: x => x.SharedComponentId,
                        principalTable: "PageBuilderSharedComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentContent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ComponentContent = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageBuilderSharedComponentContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageBuilderSharedComponentContent_PageBuilderSharedComponen~",
                        column: x => x.Id,
                        principalTable: "PageBuilderSharedComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageBuilderSharedComponentReference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SharedComponentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
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
                        name: "FK_PageBuilderSharedComponentReference_PageBuilderSharedCompon~",
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
                name: "IX_PageBuilderSharedComponentAssetReference_NormalizedAssetUrl~",
                table: "PageBuilderSharedComponentAssetReference",
                column: "NormalizedAssetUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentAssetReference_SharedComponentId",
                table: "PageBuilderSharedComponentAssetReference",
                column: "SharedComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageBuilderSharedComponentAssetReference_SharedComponentId_~",
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
