namespace GameGraph.Models.Contract;

public sealed record FittedGivenParameter(string Feature) : FittedParameter
{
    public override Parameter Declaration() => new GivenParameter(Feature);
}
