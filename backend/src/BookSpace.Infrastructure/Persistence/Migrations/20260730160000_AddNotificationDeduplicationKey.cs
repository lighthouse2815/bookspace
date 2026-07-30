using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations;

public partial class AddNotificationDeduplicationKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeduplicationKey",
            table: "notifications",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.Sql(
            """
            DELETE FROM notifications
            WHERE Type = 'CHALLENGE'
              AND Title = 'Hoàn thành thử thách'
              AND Link LIKE '/challenges/%'
              AND Id NOT IN (
                  SELECT keep.Id
                  FROM notifications AS keep
                  WHERE keep.UserId = notifications.UserId
                    AND keep.Type = notifications.Type
                    AND keep.Title = notifications.Title
                    AND keep.Link = notifications.Link
                  ORDER BY keep.CreatedAt, keep.Id
                  LIMIT 1
              );
            """);

        migrationBuilder.Sql(
            """
            UPDATE notifications
            SET DeduplicationKey =
                'challenge-completed:' ||
                lower(replace(substr(Link, length('/challenges/') + 1), '-', '')) ||
                ':' ||
                lower(replace(UserId, '-', ''))
            WHERE Type = 'CHALLENGE'
              AND Title = 'Hoàn thành thử thách'
              AND Link LIKE '/challenges/%';
            """);

        migrationBuilder.CreateIndex(
            name: "IX_notifications_DeduplicationKey",
            table: "notifications",
            column: "DeduplicationKey",
            unique: true,
            filter: "\"DeduplicationKey\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_notifications_DeduplicationKey",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "DeduplicationKey",
            table: "notifications");
    }
}
