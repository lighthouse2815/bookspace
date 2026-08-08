using BookSpace.Application.Common;

namespace BookSpace.UnitTests;

public sealed class PagingTests
{
    [Fact]
    public void Normalize_saturates_an_overflowing_skip()
    {
        var result = Paging.Normalize(int.MaxValue, 100);

        Assert.Equal(int.MaxValue, result.Page);
        Assert.Equal(100, result.Size);
        Assert.Equal(int.MaxValue, result.Skip);
    }

    [Fact]
    public void Normalize_preserves_regular_paging_behavior()
    {
        var result = Paging.Normalize(3, 20);

        Assert.Equal(3, result.Page);
        Assert.Equal(20, result.Size);
        Assert.Equal(40, result.Skip);
    }
}
