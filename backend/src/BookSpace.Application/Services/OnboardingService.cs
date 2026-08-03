using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class OnboardingService(
    IBookSpaceDbContext db,
    IOnboardingMutationBoundary mutationBoundary) : IOnboardingService
{
    private const int MinimumPreferenceCount = 3;
    private const int MaximumPreferenceCount = 5;

    public OnboardingStateDto Get(Guid userId)
    {
        var user = FindUser(userId);
        return Map(user);
    }

    public Task<OnboardingStateDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateOnboardingPreferencesRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var user = FindUser(userId);
                var categoryIds = (request.PreferredCategoryIds ?? [])
                    .Distinct()
                    .ToArray();
                var bookIds = (request.ReferenceBookIds ?? [])
                    .Distinct()
                    .ToArray();

                EnsureWithinLimit(
                    categoryIds.Length,
                    "ONBOARDING_PREFERRED_CATEGORY_LIMIT_EXCEEDED",
                    "Bạn chỉ có thể chọn tối đa 5 thể loại yêu thích.");
                EnsureWithinLimit(
                    bookIds.Length,
                    "ONBOARDING_REFERENCE_BOOK_LIMIT_EXCEEDED",
                    "Bạn chỉ có thể chọn tối đa 5 cuốn sách tham chiếu.");
                EnsureActiveCategories(categoryIds);
                EnsureActiveBooks(bookIds);
                if (user.OnboardingStatus == OnboardingStatus.COMPLETED)
                {
                    EnsureCompletePreferenceCounts(categoryIds.Length, bookIds.Length);
                }

                db.RemoveRange(db.UserPreferredCategoriesIncludingDeleted
                    .Where(link => link.UserId == userId)
                    .ToList());
                db.RemoveRange(db.UserReferenceBooksIncludingDeleted
                    .Where(link => link.UserId == userId)
                    .ToList());
                db.AddRange(categoryIds.Select(categoryId =>
                    new UserPreferredCategory(userId, categoryId)));
                db.AddRange(bookIds.Select(bookId =>
                    new UserReferenceBook(userId, bookId)));

                await db.SaveChangesAsync(operationCancellationToken);
                return new OnboardingStateDto(
                    user.OnboardingStatus,
                    user.OnboardingFinishedAt,
                    categoryIds.Order().ToArray(),
                    bookIds.Order().ToArray());
            },
            cancellationToken);

    public Task<OnboardingStateDto> CompleteAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var user = FindUser(userId);
                if (user.OnboardingStatus == OnboardingStatus.COMPLETED)
                {
                    return Map(user);
                }

                var categoryCount = ActivePreferredCategoryIds(userId).Count();
                var bookCount = ActiveReferenceBookIds(userId).Count();
                EnsureCompletePreferenceCounts(categoryCount, bookCount);

                user.CompleteOnboarding();
                await db.SaveChangesAsync(operationCancellationToken);
                return Map(user);
            },
            cancellationToken);

    public Task<OnboardingStateDto> SkipAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var user = FindUser(userId);
                var previousStatus = user.OnboardingStatus;
                user.SkipOnboarding();
                if (user.OnboardingStatus != previousStatus)
                {
                    await db.SaveChangesAsync(operationCancellationToken);
                }

                return Map(user);
            },
            cancellationToken);

    private OnboardingStateDto Map(User user) =>
        new(
            user.OnboardingStatus,
            user.OnboardingFinishedAt,
            ActivePreferredCategoryIds(user.Id).Order().ToArray(),
            ActiveReferenceBookIds(user.Id).Order().ToArray());

    private IQueryable<Guid> ActivePreferredCategoryIds(Guid userId) =>
        db.UserPreferredCategories
            .Where(link =>
                link.UserId == userId &&
                link.DeletedAt == null &&
                db.Categories.Any(category =>
                    category.Id == link.CategoryId &&
                    category.DeletedAt == null))
            .Select(link => link.CategoryId)
            .Distinct();

    private IQueryable<Guid> ActiveReferenceBookIds(Guid userId) =>
        db.UserReferenceBooks
            .Where(link =>
                link.UserId == userId &&
                link.DeletedAt == null &&
                db.Books.Any(book =>
                    book.Id == link.BookId &&
                    book.DeletedAt == null))
            .Select(link => link.BookId)
            .Distinct();

    private void EnsureActiveCategories(IReadOnlyCollection<Guid> categoryIds)
    {
        var activeCount = db.Categories.Count(category =>
            categoryIds.Contains(category.Id) &&
            category.DeletedAt == null);
        if (activeCount != categoryIds.Count)
        {
            throw ServiceErrors.NotFound(
                "ONBOARDING_PREFERRED_CATEGORY_NOT_FOUND",
                "Có thể loại yêu thích không tồn tại hoặc không còn hoạt động.");
        }
    }

    private void EnsureActiveBooks(IReadOnlyCollection<Guid> bookIds)
    {
        var activeCount = db.Books.Count(book =>
            bookIds.Contains(book.Id) &&
            book.DeletedAt == null);
        if (activeCount != bookIds.Count)
        {
            throw ServiceErrors.NotFound(
                "ONBOARDING_REFERENCE_BOOK_NOT_FOUND",
                "Có sách tham chiếu không tồn tại hoặc không còn hoạt động.");
        }
    }

    private static void EnsureWithinLimit(int count, string code, string message)
    {
        if (count > MaximumPreferenceCount)
        {
            throw ServiceErrors.BadRequest(code, message);
        }
    }

    private static void EnsureCompletePreferenceCounts(int categoryCount, int bookCount)
    {
        if (categoryCount is < MinimumPreferenceCount or > MaximumPreferenceCount ||
            bookCount is < MinimumPreferenceCount or > MaximumPreferenceCount)
        {
            throw ServiceErrors.BadRequest(
                "ONBOARDING_INCOMPLETE",
                "Bạn cần giữ từ 3 đến 5 thể loại yêu thích và từ 3 đến 5 cuốn sách tham chiếu đang hoạt động.");
        }
    }

    private User FindUser(Guid userId) =>
        db.Users.FirstOrDefault(user => user.Id == userId)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
}
