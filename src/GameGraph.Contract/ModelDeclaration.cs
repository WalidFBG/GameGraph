using GameGraph.Contract.Distributions;
using GameGraph.Contract.Terms;

namespace GameGraph.Contract;

public sealed record ModelDeclaration
{
    public ModelDeclaration(DistributionDeclaration distribution, IReadOnlyDictionary<string, Term[]> terms, IReadOnlyDictionary<string, string> given)
    {
        distribution.RequireParameters(terms.Keys, given.Keys);

        foreach (var parameterTerms in terms.Values)
            DistinctNames.Require(parameterTerms.Select(t => t.Feature));

        Distribution = distribution;
        Terms = terms;
        Given = given;
    }

    public DistributionDeclaration Distribution { get; }

    public IReadOnlyDictionary<string, Term[]> Terms { get; }

    public IReadOnlyDictionary<string, string> Given { get; }

    public bool Equals(ModelDeclaration? other) =>
        other is not null
        && Distribution.Equals(other.Distribution)
        && Terms.Count == other.Terms.Count
        && Terms.All(t => other.Terms.TryGetValue(t.Key, out var terms) && t.Value.SequenceEqual(terms))
        && Given.Count == other.Given.Count
        && Given.All(g => other.Given.TryGetValue(g.Key, out var feature) && g.Value == feature);

    public override int GetHashCode() => Distribution.GetHashCode();

    public void RequireFeatures(string node, IReadOnlyCollection<string> features)
    {
        var missing = Features().Except(features).Order(StringComparer.Ordinal).ToArray();

        if (missing.Length > 0)
            throw new ArgumentException($"The model of {node} uses {string.Join(", ", missing)}, which are not among its features", nameof(features));

        var unused = features.Except(Features()).Order(StringComparer.Ordinal).ToArray();

        if (unused.Length > 0)
            throw new ArgumentException($"{node} has features {string.Join(", ", unused)} that its model does not use", nameof(features));
    }

    public IEnumerable<string> Features() => Terms.Values.SelectMany(t => t).Select(t => t.Feature).Concat(Given.Values).Distinct();
}
