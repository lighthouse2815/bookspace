using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingInsightsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reading_sessions_UserId",
                table: "reading_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_library_items_UserId_Status_FinishedAt",
                table: "library_items",
                columns: new[] { "UserId", "Status", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reading_sessions_UserId_StartedAt",
                table: "reading_sessions",
                columns: new[] { "UserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_library_items_UserId_Status_FinishedAt",
                table: "library_items");

            migrationBuilder.DropIndex(
                name: "IX_reading_sessions_UserId_StartedAt",
                table: "reading_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_reading_sessions_UserId",
                table: "reading_sessions",
                column: "UserId");
        }
    }
}
