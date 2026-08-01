using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookSpaceDbContext))]
[Migration("20260801113000_AddNotificationPreferences")]
public partial class AddNotificationPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsChallengeNotificationEnabled",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsClubNotificationEnabled",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsFollowNotificationEnabled",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsReviewNotificationEnabled",
            table: "users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsChallengeNotificationEnabled",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsClubNotificationEnabled",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsFollowNotificationEnabled",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsReviewNotificationEnabled",
            table: "users");
    }
}
