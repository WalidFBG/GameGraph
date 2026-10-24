namespace GameGraph.Models;

internal static class LogOnePlus
{
    public static double Of(double x)
    {
        var u = 1 + x;

        return u == 1 ? x : Math.Log(u) * x / (u - 1);
    }
}
