namespace GameGraph.Models.Contract;

public sealed record FittedModel
{
    public FittedModel(string node, DistributionDeclaration distribution, IReadOnlyDictionary<string, FittedPredictor> predictors, IReadOnlyDictionary<string, string> given)
    {
        distribution.RequireParameters(predictors.Keys, given.Keys);

        Node = node;
        Distribution = distribution;
        Predictors = predictors;
        Given = given;
    }

    public string Node { get; }

    public DistributionDeclaration Distribution { get; }

    public IReadOnlyDictionary<string, FittedPredictor> Predictors { get; }

    public IReadOnlyDictionary<string, string> Given { get; }

    public ModelDeclaration Declaration() => new(Distribution, Predictors.ToDictionary(p => p.Key, p => p.Value.Declaration()), Given);
}
