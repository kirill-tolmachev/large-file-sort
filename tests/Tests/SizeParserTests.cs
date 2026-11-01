using Common;

namespace Tests;

public sealed class SizeParserTests
{
    [Theory]
    [InlineData("1024", 1024L)]
    [InlineData("10K", 10240L)]
    [InlineData("1m", 1048576L)]
    [InlineData("1.5G", 1610612736L)]
    [InlineData("2GB", 2147483648L)]
    [InlineData("3t", 3298534883328L)]
    [InlineData("0", 0L)]
    [InlineData(" 4kb ", 4096L)]
    public void ParsesSizes(string text, long expected) =>
        Assert.Equal(expected, SizeParser.Parse(text));

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("9999999T")]
    [InlineData("NaN")]
    [InlineData("1KBB")]
    public void RejectsInvalidText(string text) =>
        Assert.Throws<FormatException>(() => SizeParser.Parse(text));
}
