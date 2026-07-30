using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ChallengeService(IBookSpaceDbContext db) : IChallengeService
{
    private readonly ServiceMapper _mapper = new(db);

    public async Task<PageResult<ChallengeDto>> GetChallengesAsync(
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (userId.HasValue)
        {
            await SyncProgressAsync(userId.Value, cancellationToken);
        }

        var query = db.ReadingChallenges.Where(x => x.IsPublished);
        return Page(query, userId, page, pageSize);
    }

    public PageResult<ChallengeDto> GetAdminChallenges(int page, int pageSize) =>
        Page(db.ReadingChallenges, null, page, pageSize);

    public async Task<PageResult<ChallengeDto>> GetMineAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await SyncProgressAsync(userId, cancellationToken);
        var ids = db.ChallengeParticipations.Where(x => x.UserId == userId).Select(x => x.ChallengeId);
        return Page(db.ReadingChallenges.Where(x => ids.Contains(x.Id)), userId, page, pageSize);
    }

    public async Task<ChallengeDto> GetPublicAsync(
        Guid challengeId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var challenge = FindChallenge(challengeId);
        if (!challenge.IsPublished)
        {
            throw ServiceErrors.NotFound("CHALLENGE_NOT_FOUND", "Không tìm thấy thử thách.");
        }

        if (userId.HasValue)
        {
            await SyncProgressAsync(userId.Value, cancellationToken);
        }

        return _mapper.Challenge(challenge, userId);
    }

    public async Task JoinAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        var challenge = FindChallenge(challengeId);
        EnsureAcceptingParticipants(challenge);

        if (db.ChallengeParticipations.Any(x => x.ChallengeId == challengeId && x.UserId == userId))
        {
            throw ServiceErrors.Conflict("CHALLENGE_ALREADY_JOINED", "Bạn đã tham gia thử thách.");
        }

        db.Add(new ChallengeParticipation(challengeId, userId));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        FindChallenge(challengeId);
        var participation = db.ChallengeParticipations.FirstOrDefault(x =>
            x.ChallengeId == challengeId && x.UserId == userId)
            ?? throw ServiceErrors.NotFound("CHALLENGE_PARTICIPATION_NOT_FOUND", "Bạn chưa tham gia thử thách.");
        db.Remove(participation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncProgressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var participations = db.ChallengeParticipations
            .Where(x => x.UserId == userId)
            .ToList();
        if (participations.Count == 0)
        {
            return;
        }

        var challenges = db.ReadingChallenges
            .Where(x => participations.Select(p => p.ChallengeId).Contains(x.Id))
            .ToDictionary(x => x.Id);
        var completedBooks = db.LibraryItems
            .Where(x =>
                x.UserId == userId &&
                x.Status == LibraryStatus.READ &&
                x.FinishedAt != null)
            .Select(x => x.FinishedAt!.Value)
            .ToList();
        var changed = false;

        foreach (var participation in participations)
        {
            if (!challenges.TryGetValue(participation.ChallengeId, out var challenge))
            {
                continue;
            }

            var next = ChallengeProgress.Derive(
                completedBooks,
                challenge.StartsAt,
                challenge.EndsAt,
                challenge.TargetBooks,
                participation.CompletedBooks);
            if (next <= participation.CompletedBooks)
            {
                continue;
            }

            var wasCompleted = participation.CompletedAt.HasValue;
            participation.UpdateProgress(next, challenge.TargetBooks);
            changed = true;
            var notificationLink = $"/challenges/{challenge.Id}";
            if (!wasCompleted &&
                participation.CompletedAt.HasValue &&
                !db.Notifications.Any(x =>
                    x.UserId == userId &&
                    x.Type == NotificationType.CHALLENGE &&
                    x.Link == notificationLink))
            {
                db.Add(new Notification(
                    userId,
                    NotificationType.CHALLENGE,
                    "Hoàn thành thử thách",
                    $"Chúc mừng! Bạn đã hoàn thành “{challenge.Title}”.",
                    notificationLink));
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ChallengeDto> CreateAsync(
        Guid adminId,
        SaveChallengeRequest request,
        CancellationToken cancellationToken)
    {
        var challenge = new ReadingChallenge(
            adminId,
            request.Title,
            request.Description,
            request.GoalBooks,
            request.StartDate,
            request.EndDate,
            request.CoverImageUrl,
            false);
        db.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Challenge(challenge, adminId);
    }

    public async Task<ChallengeDto> UpdateAsync(
        Guid challengeId,
        SaveChallengeRequest request,
        CancellationToken cancellationToken)
    {
        var challenge = FindChallenge(challengeId);
        if (challenge.IsPublished &&
            (request.GoalBooks != challenge.TargetBooks ||
             request.StartDate != challenge.StartsAt ||
             request.EndDate != challenge.EndsAt))
        {
            throw ServiceErrors.Conflict(
                "CHALLENGE_RULES_LOCKED",
                "Không thể thay đổi mục tiêu hoặc thời gian của thử thách đã xuất bản.");
        }

        challenge.Update(
            request.Title,
            request.Description,
            request.GoalBooks,
            request.StartDate,
            request.EndDate,
            request.CoverImageUrl);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Challenge(challenge, null);
    }

    public async Task<ChallengeDto> PublishAsync(
        Guid challengeId,
        PublishChallengeRequest request,
        CancellationToken cancellationToken)
    {
        var challenge = FindChallenge(challengeId);
        if (request.IsPublished)
        {
            challenge.Publish();
        }
        else
        {
            if (db.ChallengeParticipations.Any(x => x.ChallengeId == challenge.Id))
            {
                throw ServiceErrors.Conflict(
                    "CHALLENGE_HAS_PARTICIPANTS",
                    "Không thể chuyển về bản nháp khi thử thách đã có người tham gia.");
            }

            challenge.Unpublish();
        }

        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Challenge(challenge, null);
    }

    public async Task DeleteAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var challenge = FindChallenge(challengeId);
        if (challenge.IsPublished)
        {
            throw ServiceErrors.Conflict(
                "CHALLENGE_DELETE_REQUIRES_DRAFT",
                "Chỉ có thể xóa bản nháp thử thách.");
        }

        if (db.ChallengeParticipations.Any(x => x.ChallengeId == challenge.Id))
        {
            throw ServiceErrors.Conflict(
                "CHALLENGE_HAS_PARTICIPANTS",
                "Không thể xóa thử thách đã có người tham gia.");
        }

        challenge.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    private PageResult<ChallengeDto> Page(
        IQueryable<ReadingChallenge> query,
        Guid? userId,
        int page,
        int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.StartsAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => _mapper.Challenge(x, userId))
            .ToList();
        return PageResult<ChallengeDto>.Create(items, normalizedPage, size, total);
    }

    private ReadingChallenge FindChallenge(Guid id) =>
        db.ReadingChallenges.FirstOrDefault(x => x.Id == id)
        ?? throw ServiceErrors.NotFound("CHALLENGE_NOT_FOUND", "Không tìm thấy thử thách.");

    private static void EnsureAcceptingParticipants(ReadingChallenge challenge)
    {
        if (!challenge.IsPublished)
        {
            throw ServiceErrors.Conflict("CHALLENGE_NOT_PUBLISHED", "Thử thách chưa được xuất bản.");
        }

        var now = DateTimeOffset.UtcNow;
        if (challenge.StartsAt > now || challenge.EndsAt < now)
        {
            throw ServiceErrors.Conflict(
                "CHALLENGE_NOT_ACTIVE",
                "Thử thách chưa bắt đầu hoặc đã kết thúc.");
        }
    }
}
