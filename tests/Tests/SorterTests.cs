using System.Globalization;
using Generator;
using Sorter;

namespace Tests;

public sealed class SorterTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "LargeFileSortTests", Guid.NewGuid().ToString("N"));
    readonly string _input;
    readonly string _output;
    readonly string _temp;

    public SorterTests()
    {
        _input = Path.Combine(_directory, "input.txt");
        _output = Path.Combine(_directory, "output.txt");
        _temp = Directory.CreateDirectory(Path.Combine(_directory, "temp")).FullName;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void SortsGeneratedFile()
    {
        SortGenerated();
        Assert.Equal(ReferenceSort(File.ReadAllLines(_input)), File.ReadAllLines(_output));
    }

    [Fact]
    public void PreservesGeneratedFileLength()
    {
        SortGenerated();
        Assert.Equal(new FileInfo(_input).Length, new FileInfo(_output).Length);
    }

    [Fact]
    public void LeavesNoTemporaryFiles()
    {
        SortGenerated();
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp));
    }

    [Fact]
    public void SortsExample() =>
        Assert.Equal(
            "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n",
            SortText("415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n"));

    [Fact]
    public void TerminatesLastLineWithoutNewline() =>
        Assert.Equal("1. Apple\n2. Banana\n3. Cherry\n", SortText("2. Banana\n3. Cherry\n1. Apple"));

    [Fact]
    public void KeepsCrLfLineEndings() =>
        Assert.Equal("1. Apple\r\n3. Apple\r\n2. Banana\r\n", SortText("2. Banana\r\n1. Apple\r\n3. Apple\r\n"));

    [Fact]
    public void OrdersEqualTextsByNumber()
    {
        var rng = new Random(5);
        var lines = Enumerable.Range(0, 20_000).Select(_ => $"{rng.Next(-1000, 1000)}. Same").ToArray();
        Assert.Equal(Join(ReferenceSort(lines)), SortText(Join(lines)));
    }

    [Fact]
    public void SortsEmptyFile() =>
        Assert.Empty(SortText(""));

    [Fact]
    public void RejectsMalformedLine()
    {
        File.WriteAllText(_input, "2. Banana\nhello world\n1. Apple\n");
        Assert.Contains("byte 10", Assert.Throws<InvalidDataException>(Sort).Message);
    }

    [Fact]
    public void LeavesNoTemporaryFilesAfterFailure()
    {
        new LineGenerator(Words.Default()).Write(_input, 3 << 20, 42);
        File.AppendAllText(_input, "hello world\n");
        Assert.Throws<InvalidDataException>(Sort);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp));
        Assert.Equal(_input, Assert.Single(Directory.GetFiles(_directory)));
    }

    [Fact]
    public void KeepsPreviousOutputOnFailure()
    {
        File.WriteAllText(_input, "hello world\n");
        File.WriteAllText(_output, "1. Old\n");
        Assert.Throws<InvalidDataException>(Sort);
        Assert.Equal("1. Old\n", File.ReadAllText(_output));
    }

    [Fact]
    public void SortsInPlace()
    {
        File.WriteAllText(_input, "2. Banana\n1. Apple\n");
        ExternalSorter.Sort(Options with { Output = _input });
        Assert.Equal("1. Apple\n2. Banana\n", File.ReadAllText(_input));
    }

    [Fact]
    public void RejectsTooSmallMemoryBudget()
    {
        File.WriteAllText(_input, "1. Apple\n");
        Assert.Throws<ArgumentException>(() => ExternalSorter.Sort(Options with { MemoryBudget = (1 << 20) - 1 }));
        Assert.False(File.Exists(_output));
    }

    [Fact]
    public void FailsOnMissingInput()
    {
        Assert.Throws<FileNotFoundException>(Sort);
        Assert.False(File.Exists(_output));
    }

    [Fact]
    public void FailsOnBadOutputPathBeforeSorting()
    {
        File.WriteAllText(_input, "1. Apple\n");
        Assert.Throws<DirectoryNotFoundException>(() => ExternalSorter.Sort(Options with { Output = Path.Combine(_directory, "missing", "output.txt") }));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp));
    }

    SortOptions Options => new(_input, _output) { MemoryBudget = 1 << 20, Threads = 2, TempDirectory = _temp };

    void Sort() => ExternalSorter.Sort(Options);

    void SortGenerated()
    {
        new LineGenerator(Words.Default()).Write(_input, 3 << 20, 42);
        Sort();
    }

    string SortText(string text)
    {
        File.WriteAllText(_input, text);
        Sort();
        return File.ReadAllText(_output);
    }

    static string Join(IEnumerable<string> lines) => string.Concat(lines.Select(line => line + "\n"));

    static string[] ReferenceSort(IEnumerable<string> lines) =>
        [.. lines.OrderBy(Text, StringComparer.Ordinal).ThenBy(Number)];

    static string Text(string line) => line[(Separator(line) + 2)..];

    static long Number(string line) => long.Parse(line.AsSpan(0, Separator(line)), CultureInfo.InvariantCulture);

    static int Separator(string line) => line.IndexOf(". ", StringComparison.Ordinal);
}
