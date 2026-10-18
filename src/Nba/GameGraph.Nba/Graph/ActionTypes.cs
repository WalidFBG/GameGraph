namespace GameGraph.Nba.Graph;

public static class ActionTypes
{
    public const int Count = 4;

    public static int Category(ActionType type)
    {
        if (type == ActionType.NotSet)
            throw new ArgumentOutOfRangeException(nameof(type), type, "No action type");

        return (int)type - 1;
    }

    public static ActionType FromCategory(int category)
    {
        if (category is < 0 or >= Count)
            throw new ArgumentOutOfRangeException(nameof(category), category, $"An action category is from 0 to {Count - 1}");

        return (ActionType)(category + 1);
    }
}
