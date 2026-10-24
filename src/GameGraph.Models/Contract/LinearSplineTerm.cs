namespace GameGraph.Models.Contract;

public sealed record LinearSplineTerm : Term
{
    public LinearSplineTerm(string feature, int knots)
        : base(feature)
    {
        if (knots < 2)
            throw new ArgumentOutOfRangeException(nameof(knots), knots, $"A linear spline of {feature} needs at least 2 knots");

        Knots = knots;
    }

    public int Knots { get; }
}
