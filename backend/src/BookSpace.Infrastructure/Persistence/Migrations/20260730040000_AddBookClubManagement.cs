using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookClubManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_book_club_members_ClubId_UserId",
                table: "book_club_members");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentBookId",
                table: "book_clubs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "club_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClubId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InviterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RespondedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_invitations_book_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "book_clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_invitations_users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_invitations_users_InviterId",
                        column: x => x.InviterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_clubs_CurrentBookId",
                table: "book_clubs",
                column: "CurrentBookId");

            migrationBuilder.CreateIndex(
                name: "IX_book_club_members_ClubId_UserId",
                table: "book_club_members",
                columns: new[] { "ClubId", "UserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_club_invitations_ClubId_InvitedUserId",
                table: "club_invitations",
                columns: new[] { "ClubId", "InvitedUserId" },
                unique: true,
                filter: "\"Status\" = 'PENDING' AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_club_invitations_ClubId_Status_CreatedAt",
                table: "club_invitations",
                columns: new[] { "ClubId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_invitations_InvitedUserId_Status_ExpiresAt",
                table: "club_invitations",
                columns: new[] { "InvitedUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_club_invitations_InviterId",
                table: "club_invitations",
                column: "InviterId");

            migrationBuilder.AddForeignKey(
                name: "FK_book_clubs_books_CurrentBookId",
                table: "book_clubs",
                column: "CurrentBookId",
                principalTable: "books",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_book_clubs_books_CurrentBookId",
                table: "book_clubs");

            migrationBuilder.DropTable(
                name: "club_invitations");

            migrationBuilder.DropIndex(
                name: "IX_book_clubs_CurrentBookId",
                table: "book_clubs");

            migrationBuilder.DropIndex(
                name: "IX_book_club_members_ClubId_UserId",
                table: "book_club_members");

            migrationBuilder.DropColumn(
                name: "CurrentBookId",
                table: "book_clubs");

            migrationBuilder.CreateIndex(
                name: "IX_book_club_members_ClubId_UserId",
                table: "book_club_members",
                columns: new[] { "ClubId", "UserId" },
                unique: true);
        }
    }
}
