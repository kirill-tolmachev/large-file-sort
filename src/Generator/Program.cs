using System.Globalization;
using Common;
using Generator;

if (args.Length < 2 || args.Length % 2 != 0)
    return Usage();

try
{
    long size = SizeParser.Parse(args[0]);
    int seed = Random.Shared.Next();
    string? wordsPath = null;
    for (int i = 2; i < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--seed":
                seed = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                break;
            case "--words":
                wordsPath = args[i + 1];
                break;
            default:
                throw new FormatException($"Unknown option {args[i]}");
        }
    }
    var words = wordsPath is null ? Words.Default() : Words.Load(wordsPath);
    new LineGenerator(words).Write(args[1], size, seed);
    return 0;
}
catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
{
    Console.Error.WriteLine(e.Message);
    return Usage();
}
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

static int Usage()
{
    Console.Error.WriteLine("Usage: Generator <size> <output> [--seed <n>] [--words <file>]");
    Console.Error.WriteLine("  size     bytes, optionally with a K, M, G or T suffix, e.g. 10G");
    Console.Error.WriteLine("  --seed   makes the output reproducible; random when omitted");
    Console.Error.WriteLine("  --words  text file with one ASCII word per line; default: built-in list of 300 words");
    return 1;
}
