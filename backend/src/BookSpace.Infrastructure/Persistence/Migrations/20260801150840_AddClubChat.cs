using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSpace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "club_chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClubId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SenderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_chat_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_chat_messages_book_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "book_clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_chat_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_chat_read_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastReadAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_chat_read_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_chat_read_states_book_club_members_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "book_club_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_club_chat_messages_ClubId_CreatedAt_Id",
                table: "club_chat_messages",
                columns: new[] { "ClubId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_club_chat_messages_SenderId",
                table: "club_chat_messages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_club_chat_read_states_MembershipId",
                table: "club_chat_read_states",
                column: "MembershipId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_chat_messages");

            migrationBuilder.DropTable(
                name: "club_chat_read_states");
        }
    }
}
