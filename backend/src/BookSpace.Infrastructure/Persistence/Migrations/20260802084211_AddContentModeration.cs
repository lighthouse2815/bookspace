using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReporterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetOwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TargetPreview = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetLink = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ModeratorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_reports_users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_reports_users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_reports_users_TargetOwnerId",
                        column: x => x.TargetOwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_ModeratorId",
                table: "content_reports",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_ReporterId_TargetType_TargetId",
                table: "content_reports",
                columns: new[] { "ReporterId", "TargetType", "TargetId" },
                unique: true,
                filter: "\"Status\" = 'PENDING' AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_Status_CreatedAt_Id",
                table: "content_reports",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_TargetOwnerId",
                table: "content_reports",
                column: "TargetOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_TargetType_TargetId",
                table: "content_reports",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_reports");
        }
    }
}
