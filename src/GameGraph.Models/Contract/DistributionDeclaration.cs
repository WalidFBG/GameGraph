using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "name")]
[JsonDerivedType(typeof(BernoulliDistribution), "bernoulli")]
[JsonDerivedType(typeof(CategoricalDistribution), "categorical")]
[JsonDerivedType(typeof(DiscreteJohnsonSuDistribution), "discreteJohnsonSu")]
public abstract record DistributionDeclaration
{
    public abstract string[] ParameterNames();

    internal void RequireParameters(IEnumerable<string> names)
    {
        var given = names.Order(StringComparer.Ordinal).ToArray();

        if (!given.SequenceEqual(ParameterNames().Order(StringComparer.Ordinal)))
            throw new ArgumentException($"{GetType().Name} has parameters {string.Join(", ", ParameterNames())}, got {string.Join(", ", given)}", nameof(names));
    }
}
