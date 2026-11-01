using System.Text;
using Generator;

namespace Tests;

public sealed class WordsTests : IDisposable
{
    readonly string _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LargeFileSortTests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void DefaultHas300DistinctLowercaseWords()
    {
        var words = Text(Words.Default());
        Assert.Equal(300, words.Length);
        Assert.All(words, word => Assert.Matches("^[a-z]+$", word));
        Assert.Distinct(words, StringComparer.Ordinal);
    }

    [Fact]
    public void LoadTrimsAndSkipsBlankLines()
    {
        string[] expected = ["alpha", "beta", "gamma"];
        Assert.Equal(expected, Text(Load("alpha\n beta \n\ngamma\n")));
    }

    [Fact]
    public void LoadAcceptsWordOfMaxLength() =>
        Assert.Equal(64, Assert.Single(Load(new string('a', 64))).Length);

    [Theory]
    [InlineData("be-ta")]
    [InlineData("b3ta")]
    [InlineData("be ta")]
    [InlineData("béta")]
    public void LoadRejectsNonLetters(string word) =>
        Assert.Contains("Line 3", LoadError($"alpha\n\n{word}\n"));

    [Fact]
    public void LoadRejectsTooLongWord() =>
        Assert.Contains("Line 3", LoadError($"alpha\n\n{new string('a', 65)}\n"));

    [Theory]
    [InlineData("")]
    [InlineData("\n \n\t\n")]
    public void LoadRejectsEmptyList(string text) =>
        Assert.Throws<InvalidDataException>(() => Load(text));

    string LoadError(string text) => Assert.Throws<InvalidDataException>(() => Load(text)).Message;

    byte[][] Load(string text)
    {
        var path = Path.Combine(_directory, Path.GetRandomFileName());
        File.WriteAllText(path, text);
        return Words.Load(path);
    }

    static string[] Text(byte[][] words) => [.. words.Select(word => Encoding.ASCII.GetString(word))];
}
