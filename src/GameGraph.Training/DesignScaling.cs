namespace GameGraph.Training;

public sealed class DesignScaling
{
    private readonly double[] _means;
    private readonly double[] _deviations;

    public DesignScaling(double[][] designs, string node)
    {
        var columns = designs[0].Length;

        _means = new double[columns];
        _deviations = new double[columns];

        for (var c = 0; c < columns; c++)
        {
            var values = designs.Select(d => d[c]).ToArray();
            var mean = values.Average();
            var deviation = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));

            if (deviation == 0)
                throw new InvalidDataException($"Column {c} of the design of {node} is constant and cannot be fitted");

            _means[c] = mean;
            _deviations[c] = deviation;
        }
    }

    public double[] Scale(double[] design)
    {
        var scaled = new double[design.Length];

        for (var c = 0; c < design.Length; c++)
            scaled[c] = (design[c] - _means[c]) / _deviations[c];

        return scaled;
    }

    public double[] Unscale(double[] parameters, int predictors)
    {
        var block = _means.Length + 1;

        if (parameters.Length != predictors * block)
            throw new ArgumentException($"{predictors} predictors of {block} parameters need {predictors * block}, got {parameters.Length}", nameof(parameters));

        var unscaled = parameters.ToArray();

        for (var p = 0; p < predictors; p++)
        {
            var start = p * block;

            for (var c = 0; c < _means.Length; c++)
            {
                unscaled[start + c + 1] = parameters[start + c + 1] / _deviations[c];
                unscaled[start] -= unscaled[start + c + 1] * _means[c];
            }
        }

        return unscaled;
    }
}
