using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class BookListDomainTests
{
    [Fact]
    public void Book_list_normalizes_content_and_tracks_updates()
    {
        var ownerId = Guid.NewGuid();
        var list = new BookList(
            ownerId,
            "  Sách cho mùa mưa  ",
            "  Đọc thật chậm.  ",
            BookListVisibility.PUBLIC);

        Assert.Equal(ownerId, list.OwnerId);
        Assert.Equal("Sách cho mùa mưa", list.Name);
        Assert.Equal("SÁCH CHO MÙA MƯA", list.NormalizedName);
        Assert.Equal("Đọc thật chậm.", list.Description);
        Assert.Null(list.UpdatedAt);

        list.Update("Kệ riêng", null, BookListVisibility.PRIVATE);
        Assert.Equal(BookListVisibility.PRIVATE, list.Visibility);
        Assert.NotNull(list.UpdatedAt);
    }

    [Fact]
    public void Book_list_rejects_invalid_name_owner_and_visibility()
    {
        Assert.Equal(
            "VALIDATION_ERROR",
            Assert.Throws<DomainException>(() =>
                new BookList(Guid.Empty, "Hợp lệ", null, BookListVisibility.PUBLIC)).Code);
        Assert.Equal(
            "VALIDATION_ERROR",
            Assert.Throws<DomainException>(() =>
                new BookList(Guid.NewGuid(), "   ", null, BookListVisibility.PUBLIC)).Code);
        Assert.Equal(
            "INVALID_BOOK_LIST_VISIBILITY",
            Assert.Throws<DomainException>(() =>
                new BookList(Guid.NewGuid(), "Hợp lệ", null, (BookListVisibility)99)).Code);
    }

    [Fact]
    public void Book_list_item_can_move_soft_delete_and_restore()
    {
        var item = new BookListItem(Guid.NewGuid(), Guid.NewGuid(), 2);
        item.MoveTo(0);
        Assert.Equal(0, item.Position);

        item.SoftDelete();
        Assert.True(item.IsDeleted);
        item.Restore(4);
        Assert.False(item.IsDeleted);
        Assert.Equal(4, item.Position);

        Assert.Equal(
            "BOOK_ALREADY_IN_LIST",
            Assert.Throws<DomainException>(() => item.Restore(1)).Code);
        Assert.Equal(
            "INVALID_BOOK_LIST_POSITION",
            Assert.Throws<DomainException>(() => item.MoveTo(-1)).Code);
    }
}
