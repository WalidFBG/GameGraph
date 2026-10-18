namespace GameGraph.Training;

public sealed class FeatureScaling
{
    private readonly double[] _means;
    private readonly double[] _deviations;

    public FeatureScaling(TrainingData data)
    {
        _means = new double[data.Features.Length];
        _deviations = new double[data.Features.Length];

        for (var i = 0; i < data.Features.Length; i++)
        {
            var values = data.Examples.Select(e => e[i]).ToArray();
            var mean = values.Average();
            var deviation = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));

            if (deviation == 0)
                throw new InvalidDataException($"Feature {data.Features[i]} of {data.Node} is constant and cannot be fitted");

            _means[i] = mean;
            _deviations[i] = deviation;
        }
    }

    public double[] Scale(double[] features)
    {
        var scaled = new double[features.Length];

        for (var i = 0; i < features.Length; i++)
            scaled[i] = (features[i] - _means[i]) / _deviations[i];

        return scaled;
    }

    public double[] Unscale(double[] parameters)
    {
        var unscaled = new double[parameters.Length];

        unscaled[0] = parameters[0];

        for (var i = 0; i < _means.Length; i++)
        {
            unscaled[i + 1] = parameters[i + 1] / _deviations[i];
            unscaled[0] -= unscaled[i + 1] * _means[i];
        }

        return unscaled;
    }
}
