using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class TextFormatTests
{
    [Fact]
    public void Truncate_leaves_short_text_untouched()
    {
        Assert.Equal("abc", TextFormat.Truncate("abc", 10));
    }

    [Fact]
    public void Truncate_leaves_exact_width_untouched()
    {
        Assert.Equal("abcde", TextFormat.Truncate("abcde", 5));
    }

    [Fact]
    public void Truncate_shortens_with_ellipsis_to_exact_width()
    {
        var result = TextFormat.Truncate("abcdefghij", 5);
        Assert.Equal("abcd…", result);
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void Truncate_handles_width_of_one()
    {
        Assert.Equal("…", TextFormat.Truncate("abcdef", 1));
    }

    [Fact]
    public void Truncate_returns_empty_for_nonpositive_width()
    {
        Assert.Equal("", TextFormat.Truncate("abcdef", 0));
        Assert.Equal("", TextFormat.Truncate("abcdef", -3));
    }
}
