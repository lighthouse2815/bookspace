using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BookSpace.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    BookSpaceDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (!environment.IsDevelopment() ||
            !configuration.GetValue("SeedData:Enabled", true))
        {
            return;
        }

        if (await db.UserSet.AnyAsync(cancellationToken))
        {
            await EnsureReadingSprintDemoAsync(cancellationToken);
            return;
        }

        var admin = new User(
            "admin@bookspace.local",
            passwordHasher.Hash("Admin123!"),
            "Quản trị BookSpace",
            UserRole.ADMIN);
        var reader = new User(
            "reader@bookspace.local",
            passwordHasher.Hash("Reader123!"),
            "Minh Anh");
        reader.UpdateProfile(
            "Minh Anh",
            "Mỗi cuốn sách là một cuộc đối thoại mới.",
            "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=400");

        var nguyenNhatAnh = new Author(
            "Nguyễn Nhật Ánh",
            "Nhà văn Việt Nam được yêu mến với những tác phẩm trong trẻo về tuổi thơ.");
        var toHoai = new Author(
            "Tô Hoài",
            "Nhà văn lớn của văn học Việt Nam hiện đại.");
        var namCao = new Author(
            "Nam Cao",
            "Nhà văn hiện thực xuất sắc của văn học Việt Nam.");
        var georgeOrwell = new Author(
            "George Orwell",
            "Nhà văn Anh nổi tiếng với các tác phẩm phản địa đàng và phê bình xã hội.");
        var harukiMurakami = new Author(
            "Haruki Murakami",
            "Nhà văn Nhật Bản với phong cách hiện thực huyền ảo giàu chất nhạc.");
        var antoine = new Author(
            "Antoine de Saint-Exupéry",
            "Nhà văn và phi công Pháp, tác giả của Hoàng tử bé.");
        var pauloCoelho = new Author(
            "Paulo Coelho",
            "Nhà văn Brazil với những câu chuyện về hành trình tìm kiếm ý nghĩa sống.");
        var jkRowling = new Author(
            "J. K. Rowling",
            "Nhà văn Anh, tác giả bộ truyện giả tưởng Harry Potter.");
        var vuTrongPhung = new Author(
            "Vũ Trọng Phụng",
            "Nhà văn hiện thực xuất sắc với giọng văn trào phúng sắc sảo.");
        var daleCarnegie = new Author(
            "Dale Carnegie",
            "Tác giả và diễn giả người Mỹ về giao tiếp và phát triển bản thân.");
        var marioPuzo = new Author(
            "Mario Puzo",
            "Nhà văn Mỹ nổi tiếng với những tiểu thuyết về gia đình và quyền lực.");
        var literature = new Category("Văn học Việt Nam", "Tác phẩm văn học của các tác giả Việt Nam.");
        var childhood = new Category("Tuổi thơ", "Những câu chuyện nuôi dưỡng trí tưởng tượng và ký ức.");
        var classic = new Category("Kinh điển", "Các tác phẩm có giá trị bền vững theo thời gian.");
        var international = new Category("Văn học nước ngoài", "Tác phẩm nổi bật từ nhiều nền văn hóa.");
        var fantasy = new Category("Giả tưởng", "Những thế giới giàu trí tưởng tượng và phép màu.");
        var lifeJourney = new Category("Hành trình sống", "Những câu chuyện truyền cảm hứng khám phá bản thân.");

        var yellowFlowers = new Book(
            "Tôi thấy hoa vàng trên cỏ xanh",
            "Câu chuyện tuổi thơ trong trẻo, dịu dàng và đôi khi nhói buốt tại một làng quê Việt Nam.",
            "9786041173124",
            "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=800",
            378,
            2018,
            "vi");
        var deMen = new Book(
            "Dế Mèn phiêu lưu ký",
            "Hành trình trưởng thành giàu tưởng tượng của chú Dế Mèn.",
            "9786042135602",
            "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=800",
            192,
            2020,
            "vi");
        var chiPheo = new Book(
            "Chí Phèo",
            "Tác phẩm hiện thực sâu sắc về con người và xã hội làng Vũ Đại.",
            "9786043076232",
            "https://images.unsplash.com/photo-1524995997946-a1c2e315a42f?w=800",
            224,
            2021,
            "vi");
        var nineteenEightyFour = new Book(
            "1984",
            "Tiểu thuyết phản địa đàng kinh điển về tự do, sự thật và quyền lực.",
            "9786043458168",
            "https://images.unsplash.com/photo-1495640388908-05fa85288e61?w=800",
            328,
            2022,
            "vi");
        var norwegianWood = new Book(
            "Rừng Na Uy",
            "Câu chuyện trưởng thành sâu lắng về tình yêu, mất mát và ký ức tuổi trẻ.",
            "9786049521088",
            "https://images.unsplash.com/photo-1507842217343-583bb7270b66?w=800",
            512,
            2021,
            "vi");
        var littlePrince = new Book(
            "Hoàng tử bé",
            "Một câu chuyện nhỏ dành cho mọi lứa tuổi về tình bạn, trách nhiệm và yêu thương.",
            "9786043298719",
            "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=800",
            112,
            2023,
            "vi");
        var alchemist = new Book(
            "Nhà giả kim",
            "Hành trình theo đuổi kho báu và lắng nghe tiếng gọi sâu thẳm của chính mình.",
            "9786041104996",
            "https://images.unsplash.com/photo-1511108690759-009324a90311?w=800",
            228,
            2020,
            "vi");
        var harryPotter = new Book(
            "Harry Potter và Hòn đá Phù thủy",
            "Khởi đầu chuyến phiêu lưu kỳ diệu của Harry tại trường phù thủy Hogwarts.",
            "9786041159784",
            "https://images.unsplash.com/photo-1618666012174-83b441c0bc76?w=800",
            432,
            2022,
            "vi");
        var soDo = new Book(
            "Số đỏ",
            "Tiểu thuyết trào phúng kinh điển về xã hội thị dân Việt Nam đầu thế kỷ XX.",
            "9786043496382",
            "https://images.unsplash.com/photo-1532012197267-da84d127e765?w=800",
            244,
            2022,
            "vi");
        var howToWinFriends = new Book(
            "Đắc nhân tâm",
            "Những nguyên tắc thực tế để giao tiếp chân thành và xây dựng quan hệ bền vững.",
            "9786043354392",
            "https://images.unsplash.com/photo-1456513080510-7bf3a84b82f8?w=800",
            320,
            2021,
            "vi");
        var kafkaOnTheShore = new Book(
            "Kafka bên bờ biển",
            "Một hành trình hiện thực huyền ảo đan xen ký ức, số phận và khát vọng tự do.",
            "9786049521217",
            "https://images.unsplash.com/photo-1516979187457-637abb4f9353?w=800",
            560,
            2020,
            "vi");
        var godfather = new Book(
            "Bố già",
            "Thiên sử thi về gia đình Corleone, lòng trung thành và cái giá của quyền lực.",
            "9786043491424",
            "https://images.unsplash.com/photo-1529590003495-b2646e2718bf?w=800",
            664,
            2021,
            "vi");

        db.AddRange([admin, reader]);
        db.AddRange([
            nguyenNhatAnh,
            toHoai,
            namCao,
            georgeOrwell,
            harukiMurakami,
            antoine,
            pauloCoelho,
            jkRowling,
            vuTrongPhung,
            daleCarnegie,
            marioPuzo
        ]);
        db.AddRange([literature, childhood, classic, international, fantasy, lifeJourney]);
        db.AddRange([
            yellowFlowers,
            deMen,
            chiPheo,
            nineteenEightyFour,
            norwegianWood,
            littlePrince,
            alchemist,
            harryPotter,
            soDo,
            howToWinFriends,
            kafkaOnTheShore,
            godfather
        ]);
        db.AddRange([
            new BookAuthor(yellowFlowers.Id, nguyenNhatAnh.Id),
            new BookAuthor(deMen.Id, toHoai.Id),
            new BookAuthor(chiPheo.Id, namCao.Id),
            new BookAuthor(nineteenEightyFour.Id, georgeOrwell.Id),
            new BookAuthor(norwegianWood.Id, harukiMurakami.Id),
            new BookAuthor(littlePrince.Id, antoine.Id),
            new BookAuthor(alchemist.Id, pauloCoelho.Id),
            new BookAuthor(harryPotter.Id, jkRowling.Id),
            new BookAuthor(soDo.Id, vuTrongPhung.Id),
            new BookAuthor(howToWinFriends.Id, daleCarnegie.Id),
            new BookAuthor(kafkaOnTheShore.Id, harukiMurakami.Id),
            new BookAuthor(godfather.Id, marioPuzo.Id)
        ]);
        db.AddRange([
            new BookCategory(yellowFlowers.Id, literature.Id),
            new BookCategory(yellowFlowers.Id, childhood.Id),
            new BookCategory(deMen.Id, literature.Id),
            new BookCategory(deMen.Id, childhood.Id),
            new BookCategory(deMen.Id, classic.Id),
            new BookCategory(chiPheo.Id, literature.Id),
            new BookCategory(chiPheo.Id, classic.Id),
            new BookCategory(nineteenEightyFour.Id, international.Id),
            new BookCategory(nineteenEightyFour.Id, classic.Id),
            new BookCategory(norwegianWood.Id, international.Id),
            new BookCategory(littlePrince.Id, international.Id),
            new BookCategory(littlePrince.Id, classic.Id),
            new BookCategory(littlePrince.Id, childhood.Id),
            new BookCategory(alchemist.Id, international.Id),
            new BookCategory(alchemist.Id, lifeJourney.Id),
            new BookCategory(harryPotter.Id, international.Id),
            new BookCategory(harryPotter.Id, fantasy.Id),
            new BookCategory(soDo.Id, literature.Id),
            new BookCategory(soDo.Id, classic.Id),
            new BookCategory(howToWinFriends.Id, lifeJourney.Id),
            new BookCategory(kafkaOnTheShore.Id, international.Id),
            new BookCategory(kafkaOnTheShore.Id, fantasy.Id),
            new BookCategory(godfather.Id, international.Id),
            new BookCategory(godfather.Id, classic.Id)
        ]);

        var libraryItem = new LibraryItem(reader.Id, yellowFlowers.Id, LibraryStatus.READING);
        libraryItem.UpdateProgress(126, yellowFlowers.PageCount);
        var completedItem = new LibraryItem(reader.Id, deMen.Id, LibraryStatus.READ);
        completedItem.UpdateProgress(deMen.PageCount, deMen.PageCount);
        var wantToReadItem = new LibraryItem(reader.Id, nineteenEightyFour.Id, LibraryStatus.WANT_TO_READ);
        db.AddRange([libraryItem, completedItem, wantToReadItem]);
        db.Add(new ReadingSession(
            reader.Id,
            yellowFlowers.Id,
            DateTimeOffset.UtcNow.AddDays(-1).AddHours(-1),
            null,
            36,
            55,
            "Một buổi đọc rất thư thái."));
        db.Add(new ReadingSession(
            reader.Id,
            yellowFlowers.Id,
            DateTimeOffset.UtcNow.AddHours(-2),
            null,
            24,
            35,
            "Đọc tiếp trước giờ làm việc."));

        var currentMonthStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddSeconds(-1);
        db.Add(new ReadingGoal(
            reader.Id,
            ReadingGoalMetric.PAGES,
            ReadingGoalPeriod.MONTH,
            500,
            currentMonthStart,
            currentMonthEnd));
        db.Add(new ReadingNote(
            reader.Id,
            yellowFlowers.Id,
            126,
            "Tuổi thơ là nơi những điều bình dị nhất cũng có thể trở thành ký ức dài lâu.",
            "Đoạn này làm mình nhớ lại cách tác giả giữ cảm xúc trong trẻo nhưng không né tránh những tổn thương của tuổi nhỏ.",
            ["tuổi thơ", "ký ức", "đọc lại"]));

        var review = new Review(
            reader.Id,
            deMen.Id,
            5,
            "Một hành trình trưởng thành vừa vui nhộn vừa sâu sắc. Đọc lại khi trưởng thành vẫn thấy nhiều điều mới.",
            false);
        db.Add(review);
        db.Add(new Review(
            reader.Id,
            yellowFlowers.Id,
            4,
            "Không khí làng quê và tình anh em được kể rất tự nhiên, nhiều đoạn khiến mình nhớ tuổi thơ.",
            false));
        db.Add(new Follow(reader.Id, admin.Id));

        var club = new BookClub(
            reader.Id,
            "Những trang sách Việt",
            "Không gian cùng đọc và trò chuyện về văn học Việt Nam.",
            "https://images.unsplash.com/photo-1526243741027-444d633d7365?w=1200",
            ClubVisibility.PUBLIC);
        db.Add(club);
        db.Add(new BookClubMember(club.Id, reader.Id, ClubMemberRole.OWNER));
        db.Add(new ClubPost(
            club.Id,
            reader.Id,
            "Cuốn sách tháng này",
            "Tháng này chúng ta cùng đọc Dế Mèn phiêu lưu ký nhé. Bạn ấn tượng nhất với chặng đường nào?"));
        var worldClub = new BookClub(
            admin.Id,
            "Đọc sách bốn phương",
            "Cùng khám phá văn học thế giới và những góc nhìn mới.",
            "https://images.unsplash.com/photo-1519682337058-a94d519337bc?w=1200",
            ClubVisibility.PUBLIC);
        db.Add(worldClub);
        db.Add(new BookClubMember(worldClub.Id, admin.Id, ClubMemberRole.OWNER));
        db.Add(new BookClubMember(worldClub.Id, reader.Id, ClubMemberRole.MEMBER));
        db.Add(new ClubPost(
            worldClub.Id,
            admin.Id,
            "Chủ đề phản địa đàng",
            "Tuần này hãy cùng trao đổi về 1984: điều gì trong cuốn sách vẫn còn gần với đời sống hôm nay?"));

        var challenge = new ReadingChallenge(
            admin.Id,
            "12 cuốn sách Việt trong năm",
            "Khám phá ít nhất 12 tác phẩm Việt Nam trong một năm.",
            12,
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 12, 31, 23, 59, 59, TimeSpan.Zero),
            "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=1200",
            true);
        var participation = new ChallengeParticipation(challenge.Id, reader.Id);
        participation.UpdateProgress(2, challenge.TargetBooks);
        db.Add(challenge);
        db.Add(participation);
        var internationalChallenge = new ReadingChallenge(
            admin.Id,
            "8 chân trời văn học",
            "Đọc 8 tác phẩm đến từ nhiều quốc gia và chia sẻ điều bạn khám phá được.",
            8,
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 12, 31, 23, 59, 59, TimeSpan.Zero),
            "https://images.unsplash.com/photo-1521587760476-6c12a4b040da?w=1200",
            true);
        db.Add(internationalChallenge);
        var welcomeNotification = new Notification(
            reader.Id,
            NotificationType.SYSTEM,
            "Chào mừng đến BookSpace",
            "Thư viện, cộng đồng và hành trình đọc của bạn đã sẵn sàng.",
            "/dashboard");
        welcomeNotification.MarkRead();
        db.Add(welcomeNotification);
        db.Add(new Notification(
            reader.Id,
            NotificationType.CHALLENGE,
            "Thử thách đang chờ bạn",
            "Hãy tiếp tục hành trình 12 cuốn sách Việt trong năm.",
            $"/challenges/{challenge.Id}"));
        db.Add(new ReadingChallenge(
            admin.Id,
            "Mùa thu đọc chậm",
            "Thử thách dự kiến dành cho những phiên đọc sâu và đều đặn.",
            5,
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(DateTimeOffset.UtcNow.Year, 11, 30, 23, 59, 59, TimeSpan.Zero),
            "https://images.unsplash.com/photo-1506880018603-83d5b814b5a6?w=1200",
            false));

        await db.SaveChangesAsync(cancellationToken);
        await EnsureReadingSprintDemoAsync(cancellationToken);
    }

    private async Task EnsureReadingSprintDemoAsync(CancellationToken cancellationToken)
    {
        const string demoTitle = "1984: đọc sâu về tự do và sự thật";
        var admin = await db.UserSet.FirstOrDefaultAsync(
            x => x.Email == "admin@bookspace.local",
            cancellationToken);
        var reader = await db.UserSet.FirstOrDefaultAsync(
            x => x.Email == "reader@bookspace.local",
            cancellationToken);
        if (admin is null || reader is null)
        {
            return;
        }

        var worldClub = await db.BookClubSet.FirstOrDefaultAsync(
            x => x.OwnerId == admin.Id && x.Name == "Đọc sách bốn phương",
            cancellationToken);
        var book = await db.BookSet.FirstOrDefaultAsync(
            x => x.Isbn == "9786043458168",
            cancellationToken);
        if (worldClub is null || book is null)
        {
            return;
        }

        var readerMembershipExists = await db.BookClubMemberSet.AnyAsync(
            x =>
                x.ClubId == worldClub.Id &&
                x.UserId == reader.Id &&
                x.DeletedAt == null,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var demoSprints = await db.ClubReadingSprintSet
            .Where(x => x.ClubId == worldClub.Id && x.Title == demoTitle)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var sprint = demoSprints.FirstOrDefault(
            x => x.GetStatus(now) == ReadingSprintStatus.ACTIVE);
        var aggregateChanged = false;
        if (sprint is null)
        {
            sprint = new ClubReadingSprint(
                worldClub.Id,
                book.Id,
                admin.Id,
                demoTitle,
                "Cùng đọc 1984 theo từng chặng, theo dõi tiến độ và trao đổi ở các cột mốc quan trọng.",
                now.AddDays(-3),
                now.AddDays(11),
                ReadingSprintTargetUnit.PAGES,
                Math.Min(240, book.PageCount),
                now.AddDays(-3));
            db.Add(sprint);
            aggregateChanged = true;
        }

        var firstMilestone = await db.ClubReadingSprintMilestoneSet.FirstOrDefaultAsync(
            x => x.SprintId == sprint.Id && x.Title == "Thế giới của Big Brother",
            cancellationToken);
        if (firstMilestone is null)
        {
            db.Add(new ClubReadingSprintMilestone(
                sprint.Id,
                admin.Id,
                "Thế giới của Big Brother",
                "Chia sẻ ấn tượng về bối cảnh, ngôn ngữ và cách quyền lực kiểm soát đời sống.",
                Math.Min(80, sprint.TargetValue),
                sprint.TargetValue,
                now.AddDays(-2)));
            aggregateChanged = true;
        }

        var secondMilestone = await db.ClubReadingSprintMilestoneSet.FirstOrDefaultAsync(
            x => x.SprintId == sprint.Id && x.Title == "Sự thật và ký ức",
            cancellationToken);
        if (secondMilestone is null)
        {
            db.Add(new ClubReadingSprintMilestone(
                sprint.Id,
                admin.Id,
                "Sự thật và ký ức",
                "Cùng thảo luận về cách sự thật bị thay đổi và vai trò của ký ức cá nhân.",
                Math.Min(160, sprint.TargetValue),
                sprint.TargetValue,
                now.AddDays(-1)));
            aggregateChanged = true;
        }

        var readerParticipant = readerMembershipExists
            ? await db.ClubReadingSprintParticipantSet.FirstOrDefaultAsync(
                x => x.SprintId == sprint.Id && x.UserId == reader.Id,
                cancellationToken)
            : null;
        if (readerMembershipExists && readerParticipant is null)
        {
            readerParticipant = new ClubReadingSprintParticipant(
                sprint.Id,
                reader.Id,
                now.AddDays(-3));
            readerParticipant.UpdateProgress(
                Math.Min(64, sprint.TargetValue),
                sprint.TargetValue,
                now.AddDays(-2));
            db.Add(readerParticipant);
            db.Add(new ClubReadingSprintCheckIn(
                readerParticipant.Id,
                sprint.Id,
                reader.Id,
                readerParticipant.ProgressValue,
                "Mở đầu cuốn hút và không khí ngột ngạt hiện lên rất rõ.",
                now.AddDays(-2)));
            if (sprint.TargetValue > readerParticipant.ProgressValue)
            {
                readerParticipant.UpdateProgress(
                    Math.Min(126, sprint.TargetValue),
                    sprint.TargetValue,
                    now.AddHours(-8));
                db.Add(new ClubReadingSprintCheckIn(
                    readerParticipant.Id,
                    sprint.Id,
                    reader.Id,
                    readerParticipant.ProgressValue,
                    "Đã tới phần những mâu thuẫn trong ký ức của Winston.",
                    now.AddHours(-8)));
            }

            aggregateChanged = true;
        }

        var adminParticipant = await db.ClubReadingSprintParticipantSet.FirstOrDefaultAsync(
            x => x.SprintId == sprint.Id && x.UserId == admin.Id,
            cancellationToken);
        if (adminParticipant is null)
        {
            adminParticipant = new ClubReadingSprintParticipant(
                sprint.Id,
                admin.Id,
                now.AddDays(-3));
            adminParticipant.UpdateProgress(
                Math.Min(92, sprint.TargetValue),
                sprint.TargetValue,
                now.AddHours(-18));
            db.Add(adminParticipant);
            db.Add(new ClubReadingSprintCheckIn(
                adminParticipant.Id,
                sprint.Id,
                admin.Id,
                adminParticipant.ProgressValue,
                "Đang ghi lại các chi tiết về cách ngôn ngữ định hình suy nghĩ.",
                now.AddHours(-18)));
            aggregateChanged = true;
        }

        if (aggregateChanged)
        {
            sprint.RecordActivity(now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
