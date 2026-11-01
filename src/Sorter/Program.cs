using Common;
using Sorter;

if (args.Length < 2 || args.Length % 2 != 0)
    return Usage();

try
{
    var options = new SortOptions(args[0], args[1]);
    for (int i = 2; i < args.Length; i += 2)
        options = args[i] switch
        {
            "--memory" => options with { MemoryBudget = SizeParser.Parse(args[i + 1]) },
            "--temp" => options with { TempDirectory = args[i + 1] },
            _ => throw new FormatException($"Unknown option {args[i]}"),
        };
    ExternalSorter.Sort(options);
    return 0;
}
catch (Exception e) when (e is FormatException or ArgumentException)
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
    Console.Error.WriteLine("Usage: Sorter <input> <output> [--memory <size>] [--temp <dir>]");
    Console.Error.WriteLine("  --memory  approximate memory budget, default 2G");
    Console.Error.WriteLine("  --temp    directory for temporary run files, default next to the output");
    return 1;
}
