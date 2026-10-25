using GameGraph.Contract;
using GameGraph.Models;

namespace GameGraph.Nba.Tests.QualityAssurance.Domain;

internal static class CalibrationModel
{
    public static Model For(string node, TrainingDataManifest manifest, FittedModel fitted)
    {
        if (manifest.Node != node || fitted.Node != node)
            throw new InvalidDataException($"The training data of {node} is for {manifest.Node}, its fitted model for {fitted.Node}");

        if (!fitted.Model.Equals(manifest.Model))
            throw new InvalidDataException($"The fitted model of {node} was declared differently from its training data's model");

        return Model.FromFitted(fitted, manifest.Features);
    }
}
