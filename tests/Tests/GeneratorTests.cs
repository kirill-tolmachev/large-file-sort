using System.Text.RegularExpressions;
using Generator;

namespace Tests;

public sealed class GeneratorTests : IDisposable
{
    const long Size = 1 << 20;

    readonly string _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LargeFileSortTests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void FillsRequestedSize() =>
        Assert.InRange(new FileInfo(Generate(Size, 1)).Length, Size - 200, Size);

    [Fact]
    public void EndsWithNewline() =>
        Assert.Equal((byte)'\n', File.ReadAllBytes(Generate(Size, 1))[^1]);

    [Fact]
    public void WritesWellFormedLines()
    {
        var format = new Regex(@"^-?[0-9]{1,6}\. [A-Z][a-z]*( [a-z]+){0,5}$");
        Assert.DoesNotContain(File.ReadAllLines(Generate(Size, 1)), line => !format.IsMatch(line));
    }

    [Fact]
    public void UsesCustomWords()
    {
        var format = new Regex(@"^-?[0-9]{1,6}\. (Alpha|Beta|Gamma)( (alpha|beta|gamma)){0,5}$");
        Assert.DoesNotContain(File.ReadAllLines(Generate(Size, 1, [[.. "alpha"u8], [.. "beta"u8], [.. "gamma"u8]])), line => !format.IsMatch(line));
    }

    [Fact]
    public void WritesNegativeNumbers() =>
        Assert.Contains(File.ReadAllLines(Generate(Size, 1)), line => line.StartsWith('-'));

    [Fact]
    public void RepeatsSomeTexts()
    {
        var texts = File.ReadAllLines(Generate(Size, 1)).Select(line => line[(line.IndexOf(". ", StringComparison.Ordinal) + 2)..]).ToArray();
        Assert.InRange(texts.Distinct(StringComparer.Ordinal).Count(), 1, texts.Length * 9 / 10);
    }

    [Fact]
    public void SameSeedProducesSameFile() =>
        Assert.Equal(File.ReadAllBytes(Generate(Size, 7)), File.ReadAllBytes(Generate(Size, 7)));

    [Fact]
    public void DifferentSeedProducesDifferentFile() =>
        Assert.NotEqual(File.ReadAllBytes(Generate(Size, 7)), File.ReadAllBytes(Generate(Size, 8)));

    [Fact]
    public void SizeBelowOneLineProducesEmptyFile() =>
        Assert.Equal(0L, new FileInfo(Generate(3, 1)).Length);

    [Fact]
    public void RejectsNegativeSize() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate(-1, 1));

    string Generate(long size, int seed, byte[][]? words = null)
    {
        var path = Path.Combine(_directory, Path.GetRandomFileName());
        new LineGenerator(words ?? Words.Default()).Write(path, size, seed);
        return path;
    }
}
