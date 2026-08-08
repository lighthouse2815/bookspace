using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class ChallengeService(
    IBookSpaceDbContext db,
    IAsyncQueryExecutor queryExecutor,
    IChallengeParticipationReader participationReader,
    IChallengeMutationBoundary mutationBoundary,
    IChallengeProgressSynchronizer progressSynchronizer) : IChallengeService
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

    public async Task<PageResult<ChallengeLeaderboardItem>> GetLeaderboardAsync(
        Guid challengeId,
        Guid viewerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var challenge = await queryExecutor.FirstOrDefaultAsync(
            db.ReadingChallenges.Where(x => x.Id == challengeId && x.IsPublished),
            cancellationToken)
            ?? throw ServiceErrors.NotFound(
                "CHALLENGE_NOT_FOUND",
                "Không tìm thấy thử thách.");

        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, viewerId);
        var visibleParticipants =
            from participation in db.ChallengeParticipations
            join user in db.Users on participation.UserId equals user.Id
            where participation.ChallengeId == challengeId &&
                  !user.IsLocked &&
                  (user.Id == viewerId ||
                   user.IsReadingActivityPublic && !hiddenUserIds.Contains(user.Id))
            select new
            {
                participation.CompletedBooks,
                participation.CompletedAt,
                JoinedAt = participation.CreatedAt,
                UserId = user.Id,
                user.DisplayName,
                user.AvatarUrl,
                user.Role
            };

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = await queryExecutor.CountAsync(visibleParticipants, cancellationToken);
        var rows = await queryExecutor.ToListAsync(
            visibleParticipants
                .OrderByDescending(x => x.CompletedBooks)
                .ThenByDescending(x =>
                    x.CompletedBooks >= challenge.TargetBooks)
                .ThenBy(x => x.CompletedAt == null)
                .ThenBy(x => x.CompletedAt)
                .ThenBy(x => x.JoinedAt)
                .ThenBy(x => x.UserId)
                .Skip(skip)
                .Take(size),
            cancellationToken);

        var items = rows
            .Select((row, index) => new ChallengeLeaderboardItem(
                skip + index + 1,
                new UserSummary(
                    row.UserId,
                    null,
                    row.DisplayName,
                    row.AvatarUrl,
                    row.Role),
                row.CompletedBooks,
                challenge.TargetBooks,
                Math.Clamp(
                    (int)Math.Round(
                        row.CompletedBooks * 100d / challenge.TargetBooks),
                    0,
                    100),
                row.CompletedAt,
                row.UserId == viewerId))
            .ToList();

        return PageResult<ChallengeLeaderboardItem>.Create(
            items,
            normalizedPage,
            size,
            total);
    }

    public async Task<ChallengeDto> JoinAsync(
        Guid userId,
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        ReadingChallenge? challenge = null;
        try
        {
            return await progressSynchronizer.ExecuteMutationAndSyncAsync(
                userId,
                async transactionCancellationToken =>
                {
                    challenge = await FindChallengeAsync(
                        challengeId,
                        transactionCancellationToken);
                    EnsureAcceptingParticipants(challenge);

                    if (await queryExecutor.AnyAsync(
                        db.ChallengeParticipations.Where(x =>
                            x.ChallengeId == challengeId &&
                            x.UserId == userId),
                        transactionCancellationToken))
                    {
                        throw ServiceErrors.Conflict(
                            "CHALLENGE_ALREADY_JOINED",
                            "Bạn đã tham gia thử thách.");
                    }

                    db.Add(new ChallengeParticipation(challengeId, userId));
                },
                () => _mapper.Challenge(challenge!, userId),
                cancellationToken);
        }
        catch (DuplicateChallengeParticipationException)
        {
            throw ServiceErrors.Conflict(
                "CHALLENGE_ALREADY_JOINED",
                "Bạn đã tham gia thử thách.");
        }
    }

    public async Task<ChallengeDto> LeaveAsync(
        Guid userId,
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        ReadingChallenge? challenge = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                challenge = await FindChallengeAsync(
                    challengeId,
                    transactionCancellationToken);
                var participation = await queryExecutor.FirstOrDefaultAsync(
                    db.ChallengeParticipations.Where(x =>
                        x.ChallengeId == challengeId &&
                        x.UserId == userId),
                    transactionCancellationToken)
                    ?? throw ServiceErrors.NotFound(
                        "CHALLENGE_PARTICIPATION_NOT_FOUND",
                        "Bạn chưa tham gia thử thách.");
                db.Remove(participation);
            },
            () => _mapper.Challenge(challenge!, userId),
            cancellationToken);
    }

    public async Task SyncProgressAsync(Guid userId, CancellationToken cancellationToken)
        => await progressSynchronizer.SyncAsync(userId, cancellationToken);

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
        if (request.IsPublished)
        {
            var challenge = FindChallenge(challengeId);
            challenge.Publish();
            await db.SaveChangesAsync(cancellationToken);
            return _mapper.Challenge(challenge, null);
        }

        return await mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var challenge = await FindChallengeAsync(
                    challengeId,
                    transactionCancellationToken);
                if (await participationReader.AnyPhysicalForChallengeAsync(
                    challenge.Id,
                    transactionCancellationToken))
                {
                    throw ServiceErrors.Conflict(
                        "CHALLENGE_HAS_PARTICIPANTS",
                        "Không thể chuyển về bản nháp khi thử thách đã có người tham gia.");
                }

                challenge.Unpublish();
                await db.SaveChangesAsync(transactionCancellationToken);
                return _mapper.Challenge(challenge, null);
            },
            cancellationToken);
    }

    public Task DeleteAsync(Guid challengeId, CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var challenge = await FindChallengeAsync(
                    challengeId,
                    transactionCancellationToken);
                if (challenge.IsPublished)
                {
                    throw ServiceErrors.Conflict(
                        "CHALLENGE_DELETE_REQUIRES_DRAFT",
                        "Chỉ có thể xóa bản nháp thử thách.");
                }

                if (await participationReader.AnyPhysicalForChallengeAsync(
                    challenge.Id,
                    transactionCancellationToken))
                {
                    throw ServiceErrors.Conflict(
                        "CHALLENGE_HAS_PARTICIPANTS",
                        "Không thể xóa thử thách đã có người tham gia.");
                }

                challenge.SoftDelete();
                await db.SaveChangesAsync(transactionCancellationToken);
                return true;
            },
            cancellationToken);

    private async Task<ReadingChallenge> FindChallengeAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await queryExecutor.FirstOrDefaultAsync(
            db.ReadingChallenges.Where(x => x.Id == id),
            cancellationToken)
        ?? throw ServiceErrors.NotFound(
            "CHALLENGE_NOT_FOUND",
            "Không tìm thấy thử thách.");

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
            throw ServiceErrors.Conflict(
                "CHALLENGE_NOT_PUBLISHED",
                "Thử thách chưa được xuất bản.");
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
