using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;

namespace GameGraph.Nba.Audit.Infrastructure;

internal static class ValidatedDataReader
{
    public static IEnumerable<ObservedGame> ReadAll() => new ValidatedGameReader(AuditDirectories.ValidatedData).ReadAll();
}
