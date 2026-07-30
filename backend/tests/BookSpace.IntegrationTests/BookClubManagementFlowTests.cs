using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class BookClubManagementFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Owner_can_create_update_private_club_while_outsiders_cannot_discover_or_join_it()
    {
        using var owner = await RegisterAsync("club-private-owner");
        using var outsider = await RegisterAsync("club-private-outsider");
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var originalName = $"CLB Riêng tư {suffix[..8]}";

        var createResponse = await owner.Client.PostAsJsonAsync("/api/clubs", new
        {
            name = originalName,
            description = "Không gian đọc riêng dành cho thành viên được mời.",
            coverImageUrl = "https://images.example.com/private-club.jpg",
            isPrivate = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync(createResponse);
        var clubId = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("isPrivate").GetBoolean());
        Assert.True(created.GetProperty("isJoined").GetBoolean());
        Assert.Equal("OWNER", created.GetProperty("viewerRole").GetString());
        Assert.True(created.GetProperty("permissions").GetProperty("canEdit").GetBoolean());
        Assert.True(created.GetProperty("permissions").GetProperty("canInvite").GetBoolean());
        Assert.True(created.GetProperty("permissions").GetProperty("canManageMembers").GetBoolean());
        Assert.True(created.GetProperty("permissions").GetProperty("canManageCurrentBook").GetBoolean());
        Assert.False(created.GetProperty("permissions").GetProperty("canLeave").GetBoolean());

        await AssertClubHiddenAsync(anonymous, clubId, originalName);
        await AssertClubHiddenAsync(outsider.Client, clubId, originalName);

        var privateJoin = await outsider.Client.PostAsync($"/api/clubs/{clubId}/join", null);
        await AssertFailureAsync(
            privateJoin,
            HttpStatusCode.Forbidden,
            "PRIVATE_CLUB");

        var outsiderUpdate = await outsider.Client.PatchAsJsonAsync($"/api/clubs/{clubId}", new
        {
            name = "Không được phép",
            description = "Người ngoài không thể sửa câu lạc bộ.",
            coverImageUrl = (string?)null,
            isPrivate = false
        });
        await AssertFailureAsync(
            outsiderUpdate,
            HttpStatusCode.Forbidden,
            "CLUB_OWNER_REQUIRED");

        var updatedName = $"CLB Riêng tư đã cập nhật {suffix[..8]}";
        var updateResponse = await owner.Client.PatchAsJsonAsync($"/api/clubs/{clubId}", new
        {
            name = updatedName,
            description = "Mô tả và ảnh bìa đã được chủ câu lạc bộ cập nhật.",
            coverImageUrl = "https://images.example.com/private-club-updated.jpg",
            isPrivate = true
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadDataAsync(updateResponse);
        Assert.Equal(updatedName, updated.GetProperty("name").GetString());
        Assert.Equal(
            "Mô tả và ảnh bìa đã được chủ câu lạc bộ cập nhật.",
            updated.GetProperty("description").GetString());
        Assert.Equal(
            "https://images.example.com/private-club-updated.jpg",
            updated.GetProperty("coverImageUrl").GetString());
        Assert.True(updated.GetProperty("isPrivate").GetBoolean());

        var bookId = await GetFirstBookIdAsync(owner.Client);
        var setResponse = await owner.Client.PutAsJsonAsync(
            $"/api/clubs/{clubId}/current-book",
            new { bookId });
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        var setClub = await ReadDataAsync(setResponse);
        Assert.Equal(bookId, setClub.GetProperty("currentBook").GetProperty("id").GetGuid());

        var repeatedSetResponse = await owner.Client.PutAsJsonAsync(
            $"/api/clubs/{clubId}/current-book",
            new { bookId });
        Assert.Equal(HttpStatusCode.OK, repeatedSetResponse.StatusCode);
        Assert.Equal(
            bookId,
            (await ReadDataAsync(repeatedSetResponse))
            .GetProperty("currentBook")
            .GetProperty("id")
            .GetGuid());

        await AssertClubHiddenAsync(anonymous, clubId, updatedName);
        await AssertClubHiddenAsync(outsider.Client, clubId, updatedName);

        var clearResponse = await owner.Client.DeleteAsync($"/api/clubs/{clubId}/current-book");
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            (await ReadDataAsync(clearResponse)).GetProperty("currentBook").ValueKind);

        var repeatedClearResponse = await owner.Client.DeleteAsync($"/api/clubs/{clubId}/current-book");
        Assert.Equal(HttpStatusCode.OK, repeatedClearResponse.StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            (await ReadDataAsync(repeatedClearResponse)).GetProperty("currentBook").ValueKind);
    }

    [Fact]
    public async Task Invitations_are_idempotent_and_only_the_recipient_or_manager_can_process_them()
    {
        using var owner = await RegisterAsync("club-invite-owner");
        using var acceptingUser = await RegisterAsync("club-invite-accept");
        using var decliningUser = await RegisterAsync("club-invite-decline");
        using var revokedUser = await RegisterAsync("club-invite-revoke");
        using var unrelatedUser = await RegisterAsync("club-invite-unrelated");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: true);

        var firstInvite = await InviteAsync(
            owner.Client,
            clubId,
            $"  {acceptingUser.Email.ToUpperInvariant()}  ");
        var repeatedInvite = await InviteAsync(owner.Client, clubId, acceptingUser.Email);
        var acceptedInvitationId = firstInvite.GetProperty("id").GetGuid();
        Assert.Equal(acceptedInvitationId, repeatedInvite.GetProperty("id").GetGuid());
        Assert.Equal("PENDING", firstInvite.GetProperty("status").GetString());

        var managerPending = await GetDataAsync(
            owner.Client,
            $"/api/clubs/{clubId}/invitations?status=PENDING");
        Assert.Single(
            managerPending.GetProperty("items").EnumerateArray(),
            invitation => invitation.GetProperty("id").GetGuid() == acceptedInvitationId);

        var recipientInbox = await GetDataAsync(
            acceptingUser.Client,
            "/api/clubs/invitations?status=PENDING");
        Assert.Single(
            recipientInbox.GetProperty("items").EnumerateArray(),
            invitation => invitation.GetProperty("id").GetGuid() == acceptedInvitationId);

        var unrelatedInbox = await GetDataAsync(
            unrelatedUser.Client,
            "/api/clubs/invitations?status=PENDING");
        Assert.DoesNotContain(
            unrelatedInbox.GetProperty("items").EnumerateArray(),
            invitation => invitation.GetProperty("id").GetGuid() == acceptedInvitationId);

        var unauthorizedAccept = await unrelatedUser.Client.PostAsync(
            $"/api/clubs/invitations/{acceptedInvitationId}/accept",
            null);
        await AssertFailureAsync(
            unauthorizedAccept,
            HttpStatusCode.Forbidden,
            "CLUB_INVITATION_FORBIDDEN");

        var acceptResponse = await acceptingUser.Client.PostAsync(
            $"/api/clubs/invitations/{acceptedInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var acceptedMember = await ReadDataAsync(acceptResponse);
        var membershipId = acceptedMember.GetProperty("id").GetGuid();
        Assert.Equal(acceptingUser.Id, acceptedMember.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("MEMBER", acceptedMember.GetProperty("role").GetString());

        var repeatedAcceptResponse = await acceptingUser.Client.PostAsync(
            $"/api/clubs/invitations/{acceptedInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.OK, repeatedAcceptResponse.StatusCode);
        Assert.Equal(membershipId, (await ReadDataAsync(repeatedAcceptResponse)).GetProperty("id").GetGuid());

        var inviteExistingMember = await owner.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invitations",
            new { email = acceptingUser.Email });
        await AssertFailureAsync(
            inviteExistingMember,
            HttpStatusCode.Conflict,
            "ALREADY_CLUB_MEMBER");

        var memberCanViewPrivateClub = await acceptingUser.Client.GetAsync($"/api/clubs/{clubId}");
        Assert.Equal(HttpStatusCode.OK, memberCanViewPrivateClub.StatusCode);

        var declinedInvitation = await InviteAsync(owner.Client, clubId, decliningUser.Email);
        var declinedInvitationId = declinedInvitation.GetProperty("id").GetGuid();
        var unauthorizedDecline = await unrelatedUser.Client.PostAsync(
            $"/api/clubs/invitations/{declinedInvitationId}/decline",
            null);
        await AssertFailureAsync(
            unauthorizedDecline,
            HttpStatusCode.Forbidden,
            "CLUB_INVITATION_FORBIDDEN");

        var declineResponse = await decliningUser.Client.PostAsync(
            $"/api/clubs/invitations/{declinedInvitationId}/decline",
            null);
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        Assert.Equal("DECLINED", (await ReadDataAsync(declineResponse)).GetProperty("status").GetString());

        var repeatedDeclineResponse = await decliningUser.Client.PostAsync(
            $"/api/clubs/invitations/{declinedInvitationId}/decline",
            null);
        Assert.Equal(HttpStatusCode.OK, repeatedDeclineResponse.StatusCode);
        Assert.Equal(
            "DECLINED",
            (await ReadDataAsync(repeatedDeclineResponse)).GetProperty("status").GetString());

        var acceptDeclined = await decliningUser.Client.PostAsync(
            $"/api/clubs/invitations/{declinedInvitationId}/accept",
            null);
        await AssertFailureAsync(
            acceptDeclined,
            HttpStatusCode.Conflict,
            "CLUB_INVITATION_NOT_PENDING");

        var invitationToRevoke = await InviteAsync(owner.Client, clubId, revokedUser.Email);
        var revokedInvitationId = invitationToRevoke.GetProperty("id").GetGuid();
        var unauthorizedRevoke = await unrelatedUser.Client.DeleteAsync(
            $"/api/clubs/{clubId}/invitations/{revokedInvitationId}");
        await AssertFailureAsync(
            unauthorizedRevoke,
            HttpStatusCode.Forbidden,
            "CLUB_MANAGEMENT_FORBIDDEN");

        var revokeResponse = await owner.Client.DeleteAsync(
            $"/api/clubs/{clubId}/invitations/{revokedInvitationId}");
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal("REVOKED", (await ReadDataAsync(revokeResponse)).GetProperty("status").GetString());

        var repeatedRevokeResponse = await owner.Client.DeleteAsync(
            $"/api/clubs/{clubId}/invitations/{revokedInvitationId}");
        Assert.Equal(HttpStatusCode.OK, repeatedRevokeResponse.StatusCode);
        Assert.Equal(
            "REVOKED",
            (await ReadDataAsync(repeatedRevokeResponse)).GetProperty("status").GetString());

        var revokedInbox = await GetDataAsync(
            revokedUser.Client,
            "/api/clubs/invitations?status=REVOKED");
        Assert.Contains(
            revokedInbox.GetProperty("items").EnumerateArray(),
            invitation => invitation.GetProperty("id").GetGuid() == revokedInvitationId);

        var acceptRevoked = await revokedUser.Client.PostAsync(
            $"/api/clubs/invitations/{revokedInvitationId}/accept",
            null);
        await AssertFailureAsync(
            acceptRevoked,
            HttpStatusCode.Conflict,
            "CLUB_INVITATION_NOT_PENDING");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await decliningUser.Client.GetAsync($"/api/clubs/{clubId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await revokedUser.Client.GetAsync($"/api/clubs/{clubId}")).StatusCode);
    }

    [Fact]
    public async Task Owner_moderator_and_member_permissions_protect_roles_removal_and_current_book()
    {
        using var owner = await RegisterAsync("club-role-owner");
        using var moderator = await RegisterAsync("club-role-moderator");
        using var secondModerator = await RegisterAsync("club-role-second-moderator");
        using var member = await RegisterAsync("club-role-member");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);

        await JoinClubAsync(moderator.Client, clubId);
        await JoinClubAsync(secondModerator.Client, clubId);
        await JoinClubAsync(member.Client, clubId);

        var promoteModerator = await owner.Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{moderator.Id}/role",
            new { role = "MODERATOR" });
        Assert.Equal(HttpStatusCode.OK, promoteModerator.StatusCode);
        var moderatorMembershipId = (await ReadDataAsync(promoteModerator)).GetProperty("id").GetGuid();

        var repeatedPromotion = await owner.Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{moderator.Id}/role",
            new { role = "MODERATOR" });
        Assert.Equal(HttpStatusCode.OK, repeatedPromotion.StatusCode);
        Assert.Equal(
            moderatorMembershipId,
            (await ReadDataAsync(repeatedPromotion)).GetProperty("id").GetGuid());

        var promoteSecondModerator = await owner.Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{secondModerator.Id}/role",
            new { role = "MODERATOR" });
        Assert.Equal(HttpStatusCode.OK, promoteSecondModerator.StatusCode);

        var moderatorChangesRole = await moderator.Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{member.Id}/role",
            new { role = "MODERATOR" });
        await AssertFailureAsync(
            moderatorChangesRole,
            HttpStatusCode.Forbidden,
            "CLUB_OWNER_REQUIRED");

        var changeOwnerRole = await owner.Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{owner.Id}/role",
            new { role = "MEMBER" });
        await AssertFailureAsync(
            changeOwnerRole,
            HttpStatusCode.Conflict,
            "OWNER_ROLE_IMMUTABLE");

        var ownerLeaves = await owner.Client.DeleteAsync($"/api/clubs/{clubId}/join");
        await AssertFailureAsync(
            ownerLeaves,
            HttpStatusCode.Conflict,
            "OWNER_CANNOT_LEAVE");

        var ownerRemoved = await owner.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{owner.Id}");
        await AssertFailureAsync(
            ownerRemoved,
            HttpStatusCode.Conflict,
            "OWNER_CANNOT_BE_REMOVED");

        var ordinaryMemberRemovesAnother = await member.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{secondModerator.Id}");
        await AssertFailureAsync(
            ordinaryMemberRemovesAnother,
            HttpStatusCode.Forbidden,
            "CLUB_MANAGEMENT_FORBIDDEN");

        var moderatorRemovesStaff = await moderator.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{secondModerator.Id}");
        await AssertFailureAsync(
            moderatorRemovesStaff,
            HttpStatusCode.Forbidden,
            "MODERATOR_CANNOT_REMOVE_STAFF");

        var moderatorRemovesOwner = await moderator.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{owner.Id}");
        await AssertFailureAsync(
            moderatorRemovesOwner,
            HttpStatusCode.Conflict,
            "OWNER_CANNOT_BE_REMOVED");

        var bookId = await GetFirstBookIdAsync(owner.Client);
        var memberSetsBook = await member.Client.PutAsJsonAsync(
            $"/api/clubs/{clubId}/current-book",
            new { bookId });
        await AssertFailureAsync(
            memberSetsBook,
            HttpStatusCode.Forbidden,
            "CLUB_MANAGEMENT_FORBIDDEN");

        var notificationsBefore = await CountClubNotificationsAsync(owner.Client, clubId);
        var moderatorSetsBook = await moderator.Client.PutAsJsonAsync(
            $"/api/clubs/{clubId}/current-book",
            new { bookId });
        Assert.Equal(HttpStatusCode.OK, moderatorSetsBook.StatusCode);
        Assert.Equal(
            bookId,
            (await ReadDataAsync(moderatorSetsBook))
            .GetProperty("currentBook")
            .GetProperty("id")
            .GetGuid());
        var notificationsAfterSet = await CountClubNotificationsAsync(owner.Client, clubId);
        Assert.Equal(notificationsBefore + 1, notificationsAfterSet);

        var repeatedModeratorSet = await moderator.Client.PutAsJsonAsync(
            $"/api/clubs/{clubId}/current-book",
            new { bookId });
        Assert.Equal(HttpStatusCode.OK, repeatedModeratorSet.StatusCode);
        Assert.Equal(
            notificationsAfterSet,
            await CountClubNotificationsAsync(owner.Client, clubId));

        var memberClearsBook = await member.Client.DeleteAsync($"/api/clubs/{clubId}/current-book");
        await AssertFailureAsync(
            memberClearsBook,
            HttpStatusCode.Forbidden,
            "CLUB_MANAGEMENT_FORBIDDEN");

        var moderatorClearsBook = await moderator.Client.DeleteAsync($"/api/clubs/{clubId}/current-book");
        Assert.Equal(HttpStatusCode.OK, moderatorClearsBook.StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            (await ReadDataAsync(moderatorClearsBook)).GetProperty("currentBook").ValueKind);
        var notificationsAfterClear = await CountClubNotificationsAsync(owner.Client, clubId);
        Assert.Equal(notificationsAfterSet + 1, notificationsAfterClear);

        var repeatedModeratorClear = await moderator.Client.DeleteAsync($"/api/clubs/{clubId}/current-book");
        Assert.Equal(HttpStatusCode.OK, repeatedModeratorClear.StatusCode);
        Assert.Equal(
            notificationsAfterClear,
            await CountClubNotificationsAsync(owner.Client, clubId));

        var moderatorRemovesMember = await moderator.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{member.Id}");
        Assert.Equal(HttpStatusCode.OK, moderatorRemovesMember.StatusCode);
        var membersAfterRemoval = await GetDataAsync(owner.Client, $"/api/clubs/{clubId}/members");
        Assert.DoesNotContain(
            membersAfterRemoval.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("user").GetProperty("id").GetGuid() == member.Id);

        var ownerRemovesModerator = await owner.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{secondModerator.Id}");
        Assert.Equal(HttpStatusCode.OK, ownerRemovesModerator.StatusCode);
        var finalMembers = await GetDataAsync(owner.Client, $"/api/clubs/{clubId}/members");
        Assert.DoesNotContain(
            finalMembers.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("user").GetProperty("id").GetGuid() == secondModerator.Id);
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"{prefix}-{suffix}@bookspace.local";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Reader123!",
            displayName = $"{prefix} {suffix[..8]}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid(),
            email);
    }

    private static async Task<Guid> CreateClubAsync(HttpClient owner, bool isPrivate)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await owner.PostAsJsonAsync("/api/clubs", new
        {
            name = $"CLB Flow {suffix[..10]}",
            description = "Câu lạc bộ dùng để kiểm thử đầy đủ luồng quản trị.",
            coverImageUrl = (string?)null,
            isPrivate
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> InviteAsync(
        HttpClient manager,
        Guid clubId,
        string email)
    {
        var response = await manager.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invitations",
            new { email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task JoinClubAsync(HttpClient client, Guid clubId)
    {
        var response = await client.PostAsync($"/api/clubs/{clubId}/join", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> GetFirstBookIdAsync(HttpClient client)
    {
        var books = await GetDataAsync(client, "/api/books");
        return books.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<int> CountClubNotificationsAsync(HttpClient client, Guid clubId)
    {
        var notifications = await GetDataAsync(client, "/api/notifications?pageSize=100");
        var link = $"/clubs/{clubId}";
        return notifications
            .GetProperty("items")
            .EnumerateArray()
            .Count(item =>
                item.GetProperty("type").GetString() == "CLUB" &&
                item.GetProperty("link").GetString() == link);
    }

    private static async Task AssertClubHiddenAsync(
        HttpClient client,
        Guid clubId,
        string clubName)
    {
        var detailResponse = await client.GetAsync($"/api/clubs/{clubId}");
        await AssertFailureAsync(
            detailResponse,
            HttpStatusCode.NotFound,
            "CLUB_NOT_FOUND");

        var membersResponse = await client.GetAsync($"/api/clubs/{clubId}/members");
        await AssertFailureAsync(
            membersResponse,
            HttpStatusCode.NotFound,
            "CLUB_NOT_FOUND");

        var search = Uri.EscapeDataString(clubName);
        var clubs = await GetDataAsync(client, $"/api/clubs?search={search}");
        Assert.DoesNotContain(
            clubs.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == clubId);
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(expectedCode, envelope.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record RegisteredUser(HttpClient Client, Guid Id, string Email) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
