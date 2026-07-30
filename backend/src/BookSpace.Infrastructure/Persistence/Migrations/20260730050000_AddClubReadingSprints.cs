using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations
{
    /// <summary>Adds the reading sprint aggregate and its supporting tables.</summary>
    public partial class AddClubReadingSprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "club_reading_sprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClubId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartsAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndsAt = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TargetValue = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelledAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LastReminderAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_reading_sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_reading_sprints_book_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "book_clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprints_books_BookId",
                        column: x => x.BookId,
                        principalTable: "books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprints_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_reading_sprint_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SprintId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TargetValue = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_reading_sprint_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_milestones_club_reading_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "club_reading_sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_milestones_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_reading_sprint_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SprintId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JoinedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LeftAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ProgressValue = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LastCheckInAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_reading_sprint_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_participants_club_reading_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "club_reading_sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_participants_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_reading_sprint_milestone_responses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_reading_sprint_milestone_responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_milestone_responses_club_reading_sprint_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "club_reading_sprint_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_milestone_responses_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_reading_sprint_check_ins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SprintId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgressValue = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_reading_sprint_check_ins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_check_ins_club_reading_sprint_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "club_reading_sprint_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_check_ins_club_reading_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "club_reading_sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_reading_sprint_check_ins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_check_ins_ParticipantId",
                table: "club_reading_sprint_check_ins",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_check_ins_SprintId_CreatedAt",
                table: "club_reading_sprint_check_ins",
                columns: new[] { "SprintId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_check_ins_UserId",
                table: "club_reading_sprint_check_ins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_milestone_responses_AuthorId",
                table: "club_reading_sprint_milestone_responses",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_milestone_responses_MilestoneId_CreatedAt",
                table: "club_reading_sprint_milestone_responses",
                columns: new[] { "MilestoneId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_milestones_CreatedById",
                table: "club_reading_sprint_milestones",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_milestones_SprintId_TargetValue",
                table: "club_reading_sprint_milestones",
                columns: new[] { "SprintId", "TargetValue" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_participants_SprintId_LeftAt_ProgressValue",
                table: "club_reading_sprint_participants",
                columns: new[] { "SprintId", "LeftAt", "ProgressValue" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_participants_SprintId_UserId",
                table: "club_reading_sprint_participants",
                columns: new[] { "SprintId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprint_participants_UserId",
                table: "club_reading_sprint_participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprints_BookId",
                table: "club_reading_sprints",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprints_ClubId_CreatedAt",
                table: "club_reading_sprints",
                columns: new[] { "ClubId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprints_ClubId_StartsAt_EndsAt_CompletedAt_CancelledAt",
                table: "club_reading_sprints",
                columns: new[] { "ClubId", "StartsAt", "EndsAt", "CompletedAt", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_reading_sprints_CreatedById",
                table: "club_reading_sprints",
                column: "CreatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_reading_sprint_check_ins");

            migrationBuilder.DropTable(
                name: "club_reading_sprint_milestone_responses");

            migrationBuilder.DropTable(
                name: "club_reading_sprint_participants");

            migrationBuilder.DropTable(
                name: "club_reading_sprint_milestones");

            migrationBuilder.DropTable(
                name: "club_reading_sprints");
        }
    }
}
