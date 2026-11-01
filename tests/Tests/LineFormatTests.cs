using System.Text;
using Common;

namespace Tests;

public sealed class LineFormatTests
{
    [Theory]
    [InlineData("415. Apple", 415L, "Apple")]
    [InlineData("30432. Something something something", 30432L, "Something something something")]
    [InlineData("1. Apple", 1L, "Apple")]
    [InlineData("32. Cherry is the best", 32L, "Cherry is the best")]
    [InlineData("2. Banana is yellow", 2L, "Banana is yellow")]
    [InlineData("-5. Apple", -5L, "Apple")]
    public void TryParseReadsNumberAndText(string line, long number, string text) =>
        Assert.Equal((true, number, text), Parse(line));

    [Fact]
    public void TryParseStripsTrailingCarriageReturn() =>
        Assert.Equal((true, 3L, "Apple"), Parse("3. Apple\r"));

    [Fact]
    public void TryParseAllowsEmptyText() =>
        Assert.Equal((true, 7L, ""), Parse("7. "));

    [Theory]
    [InlineData("")]
    [InlineData("Apple")]
    [InlineData("5.Apple")]
    [InlineData("5 Apple")]
    [InlineData("x5. Apple")]
    [InlineData(" 5. Apple")]
    [InlineData("99999999999999999999. Apple")]
    public void TryParseRejectsMalformedLines(string line) =>
        Assert.False(Parse(line).Parsed);

    [Theory]
    [InlineData("Ap", "Apple")]
    [InlineData("Apple", "Apples")]
    [InlineData("Apples", "B")]
    [InlineData("", "A")]
    public void PrefixOrderMatchesOrdinalOrder(string smaller, string larger) =>
        Assert.True(LineFormat.Prefix(Bytes(smaller)) < LineFormat.Prefix(Bytes(larger)));

    [Fact]
    public void PrefixCoversOnlyFirstEightBytes() =>
        Assert.Equal(LineFormat.Prefix("Somethin"u8), LineFormat.Prefix("Something"u8));

    [Fact]
    public void CompareOrdersExampleLines()
    {
        string[] lines = ["415. Apple", "30432. Something something something", "1. Apple", "32. Cherry is the best", "2. Banana is yellow"];
        string[] expected = ["1. Apple", "415. Apple", "2. Banana is yellow", "32. Cherry is the best", "30432. Something something something"];
        Assert.Equal(expected, lines.Order(Comparer<string>.Create(Compare)).ToArray());
    }

    [Theory]
    [InlineData("-10. Same", "-1. Same")]
    [InlineData("-1. Same", "0. Same")]
    [InlineData("0. Same", "5. Same")]
    public void CompareOrdersEqualTextsByNumber(string smaller, string larger) =>
        AssertOrdered(smaller, larger);

    [Theory]
    [InlineData("1. Something A", "1. Something B")]
    [InlineData("9. Something A", "1. Something B")]
    [InlineData("1. Somethin", "1. Something")]
    public void CompareDistinguishesTextsSharingPrefix(string smaller, string larger) =>
        AssertOrdered(smaller, larger);

    static void AssertOrdered(string smaller, string larger) =>
        Assert.Equal((-1, 1), (Math.Sign(Compare(smaller, larger)), Math.Sign(Compare(larger, smaller))));

    static int Compare(string a, string b)
    {
        var (numberA, textA) = Split(a);
        var (numberB, textB) = Split(b);
        return LineFormat.Compare(LineFormat.Prefix(textA), textA, numberA, LineFormat.Prefix(textB), textB, numberB);
    }

    static (long Number, byte[] Text) Split(string line)
    {
        var bytes = Bytes(line);
        Assert.True(LineFormat.TryParse(bytes, out long number, out var text));
        return (number, bytes[text]);
    }

    static (bool Parsed, long Number, string Text) Parse(string line)
    {
        var bytes = Bytes(line);
        bool parsed = LineFormat.TryParse(bytes, out long number, out var text);
        return (parsed, number, Encoding.ASCII.GetString(bytes[text]));
    }

    static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);
}
