namespace GameGraph.Models;

internal static class Uniforms
{
    public static void Require(double uniform)
    {
        if (uniform is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(uniform), uniform, "A uniform is in [0, 1)");
    }
}
