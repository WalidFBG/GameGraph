namespace GameGraph.Models.Contract;

public sealed record GivenParameter(string Feature) : Parameter
{
    public override IEnumerable<string> Features() => [Feature];
}
