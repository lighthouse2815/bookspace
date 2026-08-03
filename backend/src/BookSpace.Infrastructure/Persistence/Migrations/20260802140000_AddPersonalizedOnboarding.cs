using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookSpaceDbContext))]
[Migration("20260802140000_AddPersonalizedOnboarding")]
public partial class AddPersonalizedOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "OnboardingFinishedAt",
            table: "users",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnboardingStatus",
            table: "users",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "PENDING");

        migrationBuilder.CreateTable(
            name: "user_preferred_categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_preferred_categories", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_preferred_categories_categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_preferred_categories_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_reference_books",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_reference_books", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_reference_books_books_BookId",
                    column: x => x.BookId,
                    principalTable: "books",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_reference_books_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_preferred_categories_CategoryId",
            table: "user_preferred_categories",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_user_preferred_categories_UserId_CategoryId",
            table: "user_preferred_categories",
            columns: new[] { "UserId", "CategoryId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_reference_books_BookId",
            table: "user_reference_books",
            column: "BookId");

        migrationBuilder.CreateIndex(
            name: "IX_user_reference_books_UserId_BookId",
            table: "user_reference_books",
            columns: new[] { "UserId", "BookId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_preferred_categories");
        migrationBuilder.DropTable(name: "user_reference_books");

        migrationBuilder.DropColumn(name: "OnboardingFinishedAt", table: "users");
        migrationBuilder.DropColumn(name: "OnboardingStatus", table: "users");
    }
}
