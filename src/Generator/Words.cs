using System.Text;

namespace Generator;

static class Words
{
    const int MaxLength = 64;

    public static byte[][] Default()
    {
        using var stream = typeof(Words).Assembly.GetManifestResourceStream("words.txt")!;
        return Read(stream);
    }

    public static byte[][] Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    static byte[][] Read(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var words = new List<byte[]>();
        for (int lineNumber = 1; reader.ReadLine() is { } line; lineNumber++)
        {
            var word = line.Trim();
            if (word.Length == 0)
                continue;
            if (word.Length > MaxLength || !word.All(char.IsAsciiLetter))
                throw new InvalidDataException($"Line {lineNumber} of the word list is not a word of up to {MaxLength} ASCII letters");
            words.Add(Encoding.ASCII.GetBytes(word));
        }
        return words.Count > 0 ? [.. words] : throw new InvalidDataException("The word list is empty");
    }
}
