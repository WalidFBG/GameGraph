using GameGraph.Nba.Observed;

namespace GameGraph.Nba.Replay;

internal sealed class ObservedActionCursor
{
    private readonly ObservedFreeThrow[] _freeThrows;
    private readonly ObservedRebound? _rebound;
    private int _freeThrow;
    private bool _reboundRead;

    public ObservedActionCursor(int possession, int index, ObservedAction action)
    {
        Possession = possession;
        Index = index;
        Action = action;
        (_freeThrows, _rebound) = action switch
        {
            ObservedShot shot => (shot.FreeThrows, shot.Rebound),
            ObservedFoul foul => (foul.FreeThrows, foul.Rebound),
            _ => ([], null),
        };
    }

    public int Possession { get; }

    public int Index { get; }

    public ObservedAction Action { get; }

    public bool IsRead => _freeThrow == _freeThrows.Length && (_reboundRead || _rebound is null);

    public bool HasRebound => _rebound is not null;

    public ObservedRebound Rebound => _rebound ?? throw new RulesDisagreeWithDataException("The rules ask for a rebound the action does not have");

    public T As<T>()
        where T : ObservedAction =>
        Action as T ?? throw new RulesDisagreeWithDataException($"The rules ask about a {typeof(T).Name}, the data has a {Action.GetType().Name}");

    public ObservedMoment BeforeNextFreeThrow() => NextFreeThrow().Before;

    public ObservedFreeThrow TakeFreeThrow()
    {
        var freeThrow = NextFreeThrow();

        _freeThrow++;
        return freeThrow;
    }

    public ObservedRebound ReadRebound()
    {
        var rebound = Rebound;

        _reboundRead = true;
        return rebound;
    }

    private ObservedFreeThrow NextFreeThrow() =>
        _freeThrow < _freeThrows.Length
            ? _freeThrows[_freeThrow]
            : throw new RulesDisagreeWithDataException("The rules ask for a free throw the action does not have");
}
