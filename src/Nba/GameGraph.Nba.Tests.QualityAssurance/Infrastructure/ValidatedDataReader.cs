using GameGraph.Nba.Observed;
using GameGraph.Nba.Sportradar;

namespace GameGraph.Nba.Tests.QualityAssurance.Infrastructure;

internal static class ValidatedDataReader
{
    public static IEnumerable<ObservedGame> ReadAll() => new ValidatedGameReader(DataDirectories.ValidatedData).ReadAll();
}
