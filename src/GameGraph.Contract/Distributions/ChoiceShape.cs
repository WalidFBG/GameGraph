namespace GameGraph.Contract.Distributions;

public readonly record struct ChoiceShape
{
    public const int MostCandidates = 31;

    private ChoiceShape(int candidates, int size)
    {
        Candidates = candidates;
        Size = size;
    }

    public int Candidates { get; }

    public int Size { get; }

    public bool HasOneOutcome => Size == 0 || Size == Candidates;

    public static ChoiceShape Of(string choice, int candidates, double size)
    {
        if (candidates > MostCandidates)
            throw new ArgumentOutOfRangeException(nameof(candidates), candidates, $"{choice} has {candidates} candidates, more than a choice's {MostCandidates}");

        if (size < 0 || size > candidates || size != Math.Round(size))
            throw new ArgumentOutOfRangeException(nameof(size), size, $"{choice} chooses {size} of {candidates} candidates");

        return new(candidates, (int)size);
    }

    public static ChoiceShape OfOutcome(string choice, int candidates, int outcome)
    {
        var shape = Of(choice, candidates, int.PopCount(outcome));

        shape.RequireOutcome(choice, outcome);

        return shape;
    }

    public void RequireOutcome(string choice, int outcome)
    {
        if (outcome < 0 || outcome >> Candidates != 0 || int.PopCount(outcome) != Size)
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, $"{choice} chooses {Size} of {Candidates} candidates, so its outcome is a set of {Size} of their rows");
    }

    public static bool IsChosen(int outcome, int row) => (outcome >> row & 1) == 1;

    public static int Choose(int outcome, int row) => outcome | 1 << row;

    public static bool TryOutcome(ReadOnlySpan<int> candidates, ReadOnlySpan<int> chosen, out int outcome)
    {
        outcome = 0;

        foreach (var candidate in chosen)
        {
            var row = candidates.IndexOf(candidate);

            if (row < 0 || IsChosen(outcome, row))
                return false;

            outcome = Choose(outcome, row);
        }

        return true;
    }

    public void WriteChosen(ReadOnlySpan<int> candidates, int outcome, Span<int> chosen)
    {
        if (candidates.Length != Candidates || chosen.Length != Size)
            throw new ArgumentException($"A choice of {Size} of {Candidates} candidates needs {Candidates} candidates and room for {Size}, given {candidates.Length} and {chosen.Length}", nameof(chosen));

        var taken = 0;

        for (var row = 0; row < Candidates; row++)
        {
            if (IsChosen(outcome, row))
                chosen[taken++] = candidates[row];
        }
    }
}
