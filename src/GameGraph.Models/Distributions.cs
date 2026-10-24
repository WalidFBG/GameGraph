using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal static class Distributions
{
    public static IDistribution For(DistributionDeclaration declaration) => declaration switch
    {
        BernoulliDistribution => new Bernoulli(),
        CategoricalDistribution categorical => new Categorical(categorical.Categories.Length),
        DiscreteJohnsonSuDistribution => new DiscreteJohnsonSu(),
        _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration.GetType().Name, "Not a distribution"),
    };
}
