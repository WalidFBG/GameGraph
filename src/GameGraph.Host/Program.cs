using GameGraph.Nba.Data;
using GameGraph.Nba.Possessions;

namespace GameGraph.Host;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "summarise-possessions")
        {
            var summary = new PossessionSummary();

            foreach (var game in new ValidatedGameReader(args[1]).ReadAll())
                summary.Add(PossessionReader.Read(game));

            Console.WriteLine(summary.Describe());
            return 0;
        }

        Console.Error.WriteLine("Usage: GameGraph summarise-possessions <validated data directory>");
        return 1;
    }
}
