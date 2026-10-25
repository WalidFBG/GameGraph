namespace GameGraph.Contract.Distributions;

public abstract record DistributionDeclaration
{
    public abstract PredictedParameter[] Predicted();

    public abstract string[] Given();

    public abstract bool ChoosesAmongCandidates();

    internal void RequireParameters(IEnumerable<string> predicted, IEnumerable<string> given)
    {
        Require("predicts", Predicted().Select(p => p.Name), predicted);
        Require("is given", Given(), given);
    }

    private void Require(string relation, IEnumerable<string> expected, IEnumerable<string> names)
    {
        var sortedExpected = expected.Order(StringComparer.Ordinal).ToArray();
        var sortedNames = names.Order(StringComparer.Ordinal).ToArray();

        if (!sortedNames.SequenceEqual(sortedExpected))
            throw new ArgumentException($"{GetType().Name} {relation} [{string.Join(", ", sortedExpected)}], got [{string.Join(", ", sortedNames)}]", nameof(names));
    }
}
