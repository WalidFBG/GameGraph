namespace GameGraph.Models.Contract;

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

    public IEnumerable<string> Features() => Terms.Values.SelectMany(t => t).Select(t => t.Feature).Concat(Given.Values).Distinct();
}
