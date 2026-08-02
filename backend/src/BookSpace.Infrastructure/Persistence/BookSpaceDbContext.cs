using BookSpace.Application.Abstractions;
using BookSpace.Domain.Common;
using BookSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BookSpace.Infrastructure.Persistence;

public sealed class BookSpaceDbContext(DbContextOptions<BookSpaceDbContext> options)
    : DbContext(options), IBookSpaceDbContext
{
    public DbSet<User> UserSet => Set<User>();
    public DbSet<RefreshToken> RefreshTokenSet => Set<RefreshToken>();
    public DbSet<Follow> FollowSet => Set<Follow>();
    public DbSet<Author> AuthorSet => Set<Author>();
    public DbSet<Category> CategorySet => Set<Category>();
    public DbSet<Book> BookSet => Set<Book>();
    public DbSet<BookAuthor> BookAuthorSet => Set<BookAuthor>();
    public DbSet<BookCategory> BookCategorySet => Set<BookCategory>();
    public DbSet<LibraryItem> LibraryItemSet => Set<LibraryItem>();
    public DbSet<ReadingSession> ReadingSessionSet => Set<ReadingSession>();
    public DbSet<ActiveReadingSession> ActiveReadingSessionSet => Set<ActiveReadingSession>();
    public DbSet<Review> ReviewSet => Set<Review>();
    public DbSet<ReviewComment> ReviewCommentSet => Set<ReviewComment>();
    public DbSet<ReviewLike> ReviewLikeSet => Set<ReviewLike>();
    public DbSet<BookClub> BookClubSet => Set<BookClub>();
    public DbSet<BookClubMember> BookClubMemberSet => Set<BookClubMember>();
    public DbSet<ClubInvitation> ClubInvitationSet => Set<ClubInvitation>();
    public DbSet<ClubPost> ClubPostSet => Set<ClubPost>();
    public DbSet<ClubPostComment> ClubPostCommentSet => Set<ClubPostComment>();
    public DbSet<ClubChatMessage> ClubChatMessageSet => Set<ClubChatMessage>();
    public DbSet<ClubChatReadState> ClubChatReadStateSet => Set<ClubChatReadState>();
    public DbSet<ClubReadingSprint> ClubReadingSprintSet => Set<ClubReadingSprint>();
    public DbSet<ClubReadingSprintParticipant> ClubReadingSprintParticipantSet =>
        Set<ClubReadingSprintParticipant>();
    public DbSet<ClubReadingSprintCheckIn> ClubReadingSprintCheckInSet =>
        Set<ClubReadingSprintCheckIn>();
    public DbSet<ClubReadingSprintMilestone> ClubReadingSprintMilestoneSet =>
        Set<ClubReadingSprintMilestone>();
    public DbSet<ClubReadingSprintMilestoneResponse> ClubReadingSprintMilestoneResponseSet =>
        Set<ClubReadingSprintMilestoneResponse>();
    public DbSet<ReadingChallenge> ReadingChallengeSet => Set<ReadingChallenge>();
    public DbSet<ChallengeParticipation> ChallengeParticipationSet => Set<ChallengeParticipation>();
    public DbSet<Notification> NotificationSet => Set<Notification>();

    IQueryable<User> IBookSpaceDbContext.Users => UserSet;
    IQueryable<RefreshToken> IBookSpaceDbContext.RefreshTokens => RefreshTokenSet;
    IQueryable<Follow> IBookSpaceDbContext.Follows => FollowSet;
    IQueryable<Author> IBookSpaceDbContext.Authors => AuthorSet;
    IQueryable<Category> IBookSpaceDbContext.Categories => CategorySet;
    IQueryable<Book> IBookSpaceDbContext.Books => BookSet;
    IQueryable<BookAuthor> IBookSpaceDbContext.BookAuthors => BookAuthorSet;
    IQueryable<BookCategory> IBookSpaceDbContext.BookCategories => BookCategorySet;
    IQueryable<LibraryItem> IBookSpaceDbContext.LibraryItems => LibraryItemSet;
    IQueryable<LibraryItem> IBookSpaceDbContext.LibraryItemsIncludingDeleted =>
        LibraryItemSet.IgnoreQueryFilters();
    IQueryable<ReadingSession> IBookSpaceDbContext.ReadingSessions => ReadingSessionSet;
    IQueryable<ActiveReadingSession> IBookSpaceDbContext.ActiveReadingSessions =>
        ActiveReadingSessionSet;
    IQueryable<Review> IBookSpaceDbContext.Reviews => ReviewSet;
    IQueryable<ReviewComment> IBookSpaceDbContext.ReviewComments => ReviewCommentSet;
    IQueryable<ReviewLike> IBookSpaceDbContext.ReviewLikes => ReviewLikeSet;
    IQueryable<BookClub> IBookSpaceDbContext.BookClubs => BookClubSet;
    IQueryable<BookClubMember> IBookSpaceDbContext.BookClubMembers => BookClubMemberSet;
    IQueryable<ClubInvitation> IBookSpaceDbContext.ClubInvitations => ClubInvitationSet;
    IQueryable<ClubPost> IBookSpaceDbContext.ClubPosts => ClubPostSet;
    IQueryable<ClubPostComment> IBookSpaceDbContext.ClubPostComments => ClubPostCommentSet;
    IQueryable<ClubChatMessage> IBookSpaceDbContext.ClubChatMessages => ClubChatMessageSet;
    IQueryable<ClubChatReadState> IBookSpaceDbContext.ClubChatReadStates => ClubChatReadStateSet;
    IQueryable<ClubReadingSprint> IBookSpaceDbContext.ClubReadingSprints =>
        ClubReadingSprintSet;
    IQueryable<ClubReadingSprintParticipant> IBookSpaceDbContext.ClubReadingSprintParticipants =>
        ClubReadingSprintParticipantSet;
    IQueryable<ClubReadingSprintCheckIn> IBookSpaceDbContext.ClubReadingSprintCheckIns =>
        ClubReadingSprintCheckInSet;
    IQueryable<ClubReadingSprintMilestone> IBookSpaceDbContext.ClubReadingSprintMilestones =>
        ClubReadingSprintMilestoneSet;
    IQueryable<ClubReadingSprintMilestone>
        IBookSpaceDbContext.ClubReadingSprintMilestonesIncludingDeleted =>
        ClubReadingSprintMilestoneSet.IgnoreQueryFilters();
    IQueryable<ClubReadingSprintMilestoneResponse>
        IBookSpaceDbContext.ClubReadingSprintMilestoneResponses =>
        ClubReadingSprintMilestoneResponseSet;
    IQueryable<ReadingChallenge> IBookSpaceDbContext.ReadingChallenges => ReadingChallengeSet;
    IQueryable<ChallengeParticipation> IBookSpaceDbContext.ChallengeParticipations => ChallengeParticipationSet;
    IQueryable<Notification> IBookSpaceDbContext.Notifications => NotificationSet;

    void IBookSpaceDbContext.Add<T>(T entity) => Set<T>().Add(entity);
    void IBookSpaceDbContext.AddRange<T>(IEnumerable<T> entities) => Set<T>().AddRange(entities);
    void IBookSpaceDbContext.Remove<T>(T entity) => Set<T>().Remove(entity);
    void IBookSpaceDbContext.RemoveRange<T>(IEnumerable<T> entities) => Set<T>().RemoveRange(entities);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureReading(modelBuilder);
        modelBuilder.ConfigureReadingGoals();
        modelBuilder.ConfigureReadingNotes();
        ConfigureCommunity(modelBuilder);
        ConfigureClubs(modelBuilder);
        ConfigureReadingSprints(modelBuilder);
        ConfigureChallenges(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ApplySoftDeleteFilters(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.AvatarUrl).HasMaxLength(1000);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.IsReadingShelfPublic).HasDefaultValue(false);
            entity.Property(x => x.IsReadingActivityPublic).HasDefaultValue(false);
            entity.Property(x => x.IsFollowNotificationEnabled).HasDefaultValue(true);
            entity.Property(x => x.IsReviewNotificationEnabled).HasDefaultValue(true);
            entity.Property(x => x.IsClubNotificationEnabled).HasDefaultValue(true);
            entity.Property(x => x.IsChallengeNotificationEnabled).HasDefaultValue(true);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(x => x.IsActive);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<Follow>(entity =>
        {
            entity.ToTable("follows");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FollowerId, x.FollowingId }).IsUnique();
            entity.HasOne(x => x.Follower)
                .WithMany(x => x.Following)
                .HasForeignKey(x => x.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Following)
                .WithMany(x => x.Followers)
                .HasForeignKey(x => x.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.ToTable("authors");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Biography).HasMaxLength(2000);
            entity.Property(x => x.AvatarUrl).HasMaxLength(1000);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("books");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Isbn).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(5000);
            entity.Property(x => x.Isbn).HasMaxLength(20);
            entity.Property(x => x.CoverUrl).HasMaxLength(1000);
            entity.Property(x => x.Language).HasMaxLength(20).IsRequired();
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<BookAuthor>(entity =>
        {
            entity.ToTable("book_authors");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BookId, x.AuthorId }).IsUnique();
            entity.HasOne(x => x.Book).WithMany(x => x.Authors).HasForeignKey(x => x.BookId);
            entity.HasOne(x => x.Author).WithMany(x => x.Books).HasForeignKey(x => x.AuthorId);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<BookCategory>(entity =>
        {
            entity.ToTable("book_categories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BookId, x.CategoryId }).IsUnique();
            entity.HasOne(x => x.Book).WithMany(x => x.Categories).HasForeignKey(x => x.BookId);
            entity.HasOne(x => x.Category).WithMany(x => x.Books).HasForeignKey(x => x.CategoryId);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureReading(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LibraryItem>(entity =>
        {
            entity.ToTable("library_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status, x.FinishedAt });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ReadingSession>(entity =>
        {
            entity.ToTable("reading_sessions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.StartedAt });
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ActiveReadingSession>(entity =>
        {
            entity.ToTable("active_reading_sessions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book)
                .WithMany()
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureCommunity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
            entity.Property(x => x.Content).HasMaxLength(5000).IsRequired();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ReviewComment>(entity =>
        {
            entity.ToTable("review_comments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Review).WithMany(x => x.Comments).HasForeignKey(x => x.ReviewId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ReviewLike>(entity =>
        {
            entity.ToTable("review_likes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ReviewId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Review).WithMany(x => x.Likes).HasForeignKey(x => x.ReviewId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureClubs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookClub>(entity =>
        {
            entity.ToTable("book_clubs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CoverUrl).HasMaxLength(1000);
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentBook)
                .WithMany()
                .HasForeignKey(x => x.CurrentBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<BookClubMember>(entity =>
        {
            entity.ToTable("book_club_members");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ClubId, x.UserId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();
            entity.HasOne(x => x.Club).WithMany(x => x.Members).HasForeignKey(x => x.ClubId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ClubInvitation>(entity =>
        {
            entity.ToTable("club_invitations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ClubId, x.InvitedUserId })
                .IsUnique()
                .HasFilter("\"Status\" = 'PENDING' AND \"DeletedAt\" IS NULL");
            entity.HasIndex(x => new { x.InvitedUserId, x.Status, x.ExpiresAt });
            entity.HasIndex(x => new { x.ClubId, x.Status, x.CreatedAt });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Club)
                .WithMany(x => x.Invitations)
                .HasForeignKey(x => x.ClubId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Inviter)
                .WithMany()
                .HasForeignKey(x => x.InviterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InvitedUser)
                .WithMany()
                .HasForeignKey(x => x.InvitedUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ClubPost>(entity =>
        {
            entity.ToTable("club_posts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(10000).IsRequired();
            entity.HasOne(x => x.Club).WithMany(x => x.Posts).HasForeignKey(x => x.ClubId);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ClubPostComment>(entity =>
        {
            entity.ToTable("club_post_comments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ClubChatMessage>(entity =>
        {
            entity.ToTable("club_chat_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ClubId, x.CreatedAt, x.Id });
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Club)
                .WithMany()
                .HasForeignKey(x => x.ClubId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ClubChatReadState>(entity =>
        {
            entity.ToTable("club_chat_read_states");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.MembershipId).IsUnique();
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();
            entity.HasOne(x => x.Membership)
                .WithMany()
                .HasForeignKey(x => x.MembershipId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureChallenges(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadingChallenge>(entity =>
        {
            entity.ToTable("reading_challenges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CoverImageUrl).HasMaxLength(1000);
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
        modelBuilder.Entity<ChallengeParticipation>(entity =>
        {
            entity.ToTable("challenge_participations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ChallengeId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Challenge).WithMany(x => x.Participants).HasForeignKey(x => x.ChallengeId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureReadingSprints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClubReadingSprint>(entity =>
        {
            entity.ToTable("club_reading_sprints");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ClubId, x.CreatedAt });
            entity.HasIndex(x => new
            {
                x.ClubId,
                x.StartsAt,
                x.EndsAt,
                x.CompletedAt,
                x.CancelledAt
            });
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.TargetUnit).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();
            entity.HasOne(x => x.Club)
                .WithMany()
                .HasForeignKey(x => x.ClubId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book)
                .WithMany()
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<ClubReadingSprintParticipant>(entity =>
        {
            entity.ToTable("club_reading_sprint_participants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SprintId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.SprintId, x.LeftAt, x.ProgressValue });
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();
            entity.HasOne(x => x.Sprint)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.SprintId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsActive);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<ClubReadingSprintCheckIn>(entity =>
        {
            entity.ToTable("club_reading_sprint_check_ins");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SprintId, x.CreatedAt });
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Sprint)
                .WithMany(x => x.CheckIns)
                .HasForeignKey(x => x.SprintId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<ClubReadingSprintMilestone>(entity =>
        {
            entity.ToTable("club_reading_sprint_milestones");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SprintId, x.TargetValue });
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasOne(x => x.Sprint)
                .WithMany(x => x.Milestones)
                .HasForeignKey(x => x.SprintId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });

        modelBuilder.Entity<ClubReadingSprintMilestoneResponse>(entity =>
        {
            entity.ToTable("club_reading_sprint_milestone_responses");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.MilestoneId, x.CreatedAt });
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Milestone)
                .WithMany(x => x.Responses)
                .HasForeignKey(x => x.MilestoneId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Link).HasMaxLength(1000);
            entity.Property(x => x.DeduplicationKey).HasMaxLength(200);
            entity.HasIndex(x => x.DeduplicationKey)
                .IsUnique()
                .HasFilter("\"DeduplicationKey\" IS NOT NULL");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(x => x.IsRead);
            entity.Ignore(x => x.IsDeleted);
        });
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(x => x.DeletedAt == null && x.User.DeletedAt == null);
        modelBuilder.Entity<Follow>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Follower.DeletedAt == null &&
            x.Following.DeletedAt == null);
        modelBuilder.Entity<Author>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<Category>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<Book>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<BookAuthor>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Book.DeletedAt == null &&
            x.Author.DeletedAt == null);
        modelBuilder.Entity<BookCategory>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Book.DeletedAt == null &&
            x.Category.DeletedAt == null);
        modelBuilder.Entity<LibraryItem>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ReadingSession>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ActiveReadingSession>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<Review>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ReviewComment>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ReviewLike>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Review.DeletedAt == null &&
            x.User.DeletedAt == null);
        modelBuilder.Entity<BookClub>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<BookClubMember>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Club.DeletedAt == null &&
            x.User.DeletedAt == null);
        modelBuilder.Entity<ClubInvitation>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Club.DeletedAt == null &&
            x.Inviter.DeletedAt == null &&
            x.InvitedUser.DeletedAt == null);
        modelBuilder.Entity<ClubPost>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ClubPostComment>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ClubChatMessage>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Club.DeletedAt == null &&
            x.Sender.DeletedAt == null);
        modelBuilder.Entity<ClubChatReadState>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Membership.DeletedAt == null &&
            x.Membership.Club.DeletedAt == null &&
            x.Membership.User.DeletedAt == null);
        modelBuilder.Entity<ClubReadingSprint>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Club.DeletedAt == null &&
            x.Book.DeletedAt == null &&
            x.CreatedBy.DeletedAt == null);
        modelBuilder.Entity<ClubReadingSprintParticipant>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Sprint.DeletedAt == null &&
            x.User.DeletedAt == null);
        modelBuilder.Entity<ClubReadingSprintCheckIn>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Sprint.DeletedAt == null &&
            x.Participant.DeletedAt == null &&
            x.User.DeletedAt == null);
        modelBuilder.Entity<ClubReadingSprintMilestone>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Sprint.DeletedAt == null &&
            x.CreatedBy.DeletedAt == null);
        modelBuilder.Entity<ClubReadingSprintMilestoneResponse>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Milestone.DeletedAt == null &&
            x.Author.DeletedAt == null);
        modelBuilder.Entity<ReadingChallenge>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<ChallengeParticipation>().HasQueryFilter(x =>
            x.DeletedAt == null &&
            x.Challenge.DeletedAt == null &&
            x.User.DeletedAt == null);
        modelBuilder.Entity<Notification>().HasQueryFilter(x => x.DeletedAt == null);
    }
}
