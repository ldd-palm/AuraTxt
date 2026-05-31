namespace AuraTxt.Cli.Commands;

public static class ArgParser
{
    /// Converts ["--id","foo","--force"] into {"id":"foo","force":"true"}
    public static Dictionary<string, string> Parse(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                d[key] = args[++i];
            else
                d[key] = "true";
        }
        return d;
    }
}
