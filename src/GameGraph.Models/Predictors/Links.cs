using System.Reflection;
using GameGraph.Contract.Distributions;

namespace GameGraph.Models.Predictors;

internal static class Links
{
    public static MethodInfo InverseMethod { get; } = typeof(Links).GetMethod(nameof(Inverse))!;

    public static double Inverse(Link link, double eta) => link switch
    {
        Link.Identity => eta,
        Link.Log => Math.Exp(eta),
        Link.Logit => 1 / (1 + Math.Exp(-eta)),
        _ => throw new ArgumentOutOfRangeException(nameof(link), link, "Not a link"),
    };
}
