using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookSpaceDbContext))]
[Migration("20260803120000_AddExternalCatalogImports")]
public partial class AddExternalCatalogImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "external_book_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_external_book_links", x => x.Id);
                table.ForeignKey(
                    name: "FK_external_book_links_books_BookId",
                    column: x => x.BookId,
                    principalTable: "books",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_external_book_links_BookId",
            table: "external_book_links",
            column: "BookId");

        migrationBuilder.CreateIndex(
            name: "IX_external_book_links_Provider_ExternalId",
            table: "external_book_links",
            columns: new[] { "Provider", "ExternalId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "external_book_links");
    }
}
