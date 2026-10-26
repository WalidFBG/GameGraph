using System.Reflection;

namespace GameGraph.Models.Predictors;

internal static class MathMethods
{
    public static MethodInfo Min { get; } = typeof(Math).GetMethod(nameof(Math.Min), [typeof(double), typeof(double)])!;

    public static MethodInfo Max { get; } = typeof(Math).GetMethod(nameof(Math.Max), [typeof(double), typeof(double)])!;
}
