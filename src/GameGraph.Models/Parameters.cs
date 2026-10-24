using GameGraph.Models.Contract;

namespace GameGraph.Models;

internal static class Parameters
{
    public static IParameterValue FromFitted(FittedParameter parameter, IReadOnlyList<string> features) => parameter switch
    {
        FittedPredictedParameter predicted => Predictor.FromFitted(predicted, features),
        FittedGivenParameter given => new GivenColumn(TermColumns.ColumnOf(given.Feature, features)),
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter.GetType().Name, "Not a parameter"),
    };
}
