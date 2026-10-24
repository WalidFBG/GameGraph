using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal static class Links
{
    public static double Inverse(Link link, double eta) => link switch
    {
        Link.Identity => eta,
        Link.Log => Math.Exp(eta),
        Link.Logit => 1 / (1 + Math.Exp(-eta)),
        _ => throw new ArgumentOutOfRangeException(nameof(link), link, "Not a link"),
    };
}
