using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookSpaceDbContext))]
[Migration("20260801090000_AddPublicReaderProfilePrivacy")]
public partial class AddPublicReaderProfilePrivacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsReadingActivityPublic",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsReadingShelfPublic",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsReadingActivityPublic",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsReadingShelfPublic",
            table: "users");
    }
}
