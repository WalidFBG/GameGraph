using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Sportradar;

internal sealed class PossessionBuilder
{
    private readonly List<Possession> _possessions = [];
    private int? _side;
    private int _period;
    private bool _scored;

    public void Act(int actingSide, bool madeFieldGoal)
    {
        if (actingSide != _side)
        {
            End();
            _side = actingSide;
        }

        _scored |= madeFieldGoal;
    }

    public void EndPeriod()
    {
        End();
        _period++;
    }

    public Possession[] Build()
    {
        End();
        return _possessions.ToArray();
    }

    private void End()
    {
        if (_side is not { } side)
            return;

        _possessions.Add(new Possession(side, _period, _scored));
        _side = null;
        _scored = false;
    }
}
