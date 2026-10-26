using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Observed;
using GameGraph.Nba.Replay;
using Xunit;
using static GameGraph.Nba.Tests.Unit.ObservedGames;

namespace GameGraph.Nba.Tests.Unit;

public sealed class ObservedReplayTests
{
    [Fact]
    public void AHandMadeGameFollowsTheRules() =>
        Assert.True(ObservedOutcomeDecider.Follows(Ending(team => [Last(team, 20), Last(Sides.Opponent(team), 0)])));

    [Fact]
    public void AnActionsStoppageIsReadBeforeIt()
    {
        var game = Ending(team =>
        [
            new Possession(team, FinalPeriod, [Made(team, 20) with { Before = new ObservedMoment([team], [new ObservedSubstitution(team, [Starters(team)[4]], [Bench(team)[0]])]) }]),
            Last(Sides.Opponent(team), 0),
        ]);

        var courts = OnCourtAt(game, NbaNodes.ShotMade, Sides.Home);

        Assert.True(ObservedOutcomeDecider.Follows(game));
        Assert.Contains(Starters(Sides.Home)[4], courts[^3]);
        Assert.DoesNotContain(Starters(Sides.Home)[4], courts[^2]);
    }

    [Fact]
    public void APossessionsStoppageAtThePeriodsEndIsReadAfterIt()
    {
        var game = Ending(team =>
        [
            Last(team, 20),
            new Possession(Sides.Opponent(team), FinalPeriod, [Made(Sides.Opponent(team), 0)]) { After = Substitution(team, Starters(team)[4], Bench(team)[0]) },
        ]);

        Assert.True(ObservedOutcomeDecider.Follows(game));
    }

    [Fact]
    public void AFreeThrowsStoppageIsReadBeforeIt()
    {
        var game = Ending(team =>
        {
            var defence = Sides.Opponent(team);
            var fouled = new ObservedShot(20, Starters(team)[0], 2, Starters(defence)[0], true, null, null, [new ObservedFreeThrow(true, Substitution(defence, Starters(defence)[4], Bench(defence)[0]))], null);

            return [new Possession(team, FinalPeriod, [fouled]), Last(defence, 0)];
        });

        Assert.True(ObservedOutcomeDecider.Follows(game));
        Assert.Contains(Starters(Sides.Away)[4], Assert.Single(OnCourtAt(game, NbaNodes.Fouler, Sides.Away)));
        Assert.DoesNotContain(Starters(Sides.Away)[4], Assert.Single(OnCourtAt(game, NbaNodes.FreeThrowMade, Sides.Away)));
    }

    [Fact]
    public void ASubstitutionAtThePeriodsEndIsReadAfterTheReboundItFollows()
    {
        var game = Ending(team =>
        {
            var other = Sides.Opponent(team);
            var rebounded = Missed(other, 0, new ObservedRebound(true, null));
            var heave = Missed(other, 0, null) with { Before = Substitution(other, Starters(other)[4], Bench(other)[0]) };

            return [Last(team, 20), new Possession(other, FinalPeriod, [rebounded, heave])];
        });

        Assert.True(ObservedOutcomeDecider.Follows(game));
        Assert.Contains(Starters(Sides.Away)[4], Assert.Single(OnCourtAt(game, NbaNodes.OffensiveRebound, Sides.Away)));
        Assert.DoesNotContain(Starters(Sides.Away)[4], OnCourtAt(game, NbaNodes.ShotMade, Sides.Away).Last());
    }

    [Fact]
    public void AnActionOfTheTeamWithoutTheBallFails() =>
        Assert.False(ObservedOutcomeDecider.Follows(Ending(team => [Last(team, 20), Last(team, 0)])));

    [Fact]
    public void ASubstitutionOfAPlayerOnTheBenchFails()
    {
        var game = Ending(team =>
        [
            new Possession(team, FinalPeriod, [Made(team, 20) with { Before = Substitution(team, Bench(team)[0], Bench(team)[1]) }]),
            Last(Sides.Opponent(team), 0),
        ]);

        Assert.False(ObservedOutcomeDecider.Follows(game));
    }

    private static Possession Last(int team, int clock) => new(team, FinalPeriod, [Made(team, clock)]);

    private static List<int[]> OnCourtAt(AcceptedGame game, Node<NbaState> node, int team)
    {
        var courts = new Courts(node, team);

        ObservedWalk.Walk(game, State(game), courts);

        return courts.Seen;
    }

    private sealed class Courts(Node<NbaState> node, int team) : IDecisionCollector<NbaState>
    {
        public List<int[]> Seen { get; } = [];

        public void Collect(Node<NbaState> asked, NbaState state, ReadOnlySpan<double> features, int outcome)
        {
            if (asked == node)
                Seen.Add([.. state.OnCourt[team]]);
        }
    }
}
