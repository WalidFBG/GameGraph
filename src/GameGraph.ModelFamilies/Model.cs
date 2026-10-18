namespace GameGraph.ModelFamilies;

public sealed class Model
{
    private readonly int[] _featureOfTerm;

    public Model(IModelFamily family, ITerm[] terms, string[] features)
    {
        if (terms.Length == 0)
            throw new ArgumentException("A model needs at least one term", nameof(terms));

        if (terms.Select(t => (t.Feature, t.Basis)).Distinct().Count() != terms.Length)
            throw new ArgumentException("A model cannot have the same term twice", nameof(terms));

        _featureOfTerm = terms.Select(t => Array.IndexOf(features, t.Feature)).ToArray();

        var missing = terms.Where((_, t) => _featureOfTerm[t] < 0).Select(t => t.Feature).ToArray();

        if (missing.Length > 0)
            throw new ArgumentException($"No feature {string.Join(", ", missing)} among {string.Join(", ", features)}", nameof(terms));

        Family = family;
        Terms = terms;
        Columns = terms.Sum(t => t.Columns);
    }

    public IModelFamily Family { get; }

    public ITerm[] Terms { get; }

    public int Columns { get; }

    public int ParameterCount => Family.Predictors * (Columns + 1);

    public ModelDescription Describe() => new(Family.Describe(), Terms.Select(t => t.Describe()).ToArray());

    public double[] Design(double[] features)
    {
        var design = new double[Columns];
        var column = 0;

        for (var t = 0; t < Terms.Length; t++)
        {
            Terms[t].Expand(features[_featureOfTerm[t]], design.AsSpan(column, Terms[t].Columns));
            column += Terms[t].Columns;
        }

        return design;
    }

    public double NegativeLogLikelihood(double[] parameters, double[] design, int outcome) =>
        Family.NegativeLogLikelihood(Eta(parameters, design), outcome);

    public double[] Gradient(double[] parameters, double[] design, int outcome)
    {
        var etaGradient = Family.Gradient(Eta(parameters, design), outcome);
        var gradient = new double[ParameterCount];

        for (var p = 0; p < Family.Predictors; p++)
        {
            var start = p * (Columns + 1);

            gradient[start] = etaGradient[p];

            for (var c = 0; c < Columns; c++)
                gradient[start + 1 + c] = etaGradient[p] * design[c];
        }

        return gradient;
    }

    public int Sample(double[] parameters, double[] design, double uniform) =>
        Family.Sample(Eta(parameters, design), uniform);

    public PredictorCoefficients[] Describe(double[] parameters)
    {
        CheckParameters(parameters);

        var predictors = new PredictorCoefficients[Family.Predictors];

        for (var p = 0; p < predictors.Length; p++)
        {
            var start = p * (Columns + 1);
            var column = start + 1;
            var terms = new TermCoefficients[Terms.Length];

            for (var t = 0; t < Terms.Length; t++)
            {
                terms[t] = new TermCoefficients(Terms[t].Feature, Terms[t].Basis, parameters[column..(column + Terms[t].Columns)]);
                column += Terms[t].Columns;
            }

            predictors[p] = new PredictorCoefficients(parameters[start], terms);
        }

        return predictors;
    }

    public double[] Parameters(PredictorCoefficients[] predictors)
    {
        if (predictors.Length != Family.Predictors)
            throw new ArgumentException($"{Family.Name} needs {Family.Predictors} predictors, got {predictors.Length}", nameof(predictors));

        var parameters = new double[ParameterCount];

        for (var p = 0; p < predictors.Length; p++)
        {
            var byTerm = predictors[p].Terms.ToDictionary(t => (t.Feature, t.Basis));

            if (byTerm.Count != Terms.Length)
                throw new ArgumentException($"Predictor {p} has {byTerm.Count} terms, the model has {Terms.Length}", nameof(predictors));

            var start = p * (Columns + 1);
            var column = start + 1;

            parameters[start] = predictors[p].Intercept;

            foreach (var term in Terms)
            {
                if (!byTerm.TryGetValue((term.Feature, term.Basis), out var coefficients))
                    throw new ArgumentException($"Predictor {p} has no {term.Basis} term for {term.Feature}", nameof(predictors));

                if (coefficients.Coefficients.Length != term.Columns)
                    throw new ArgumentException($"The {term.Basis} term for {term.Feature} needs {term.Columns} coefficients, got {coefficients.Coefficients.Length}", nameof(predictors));

                coefficients.Coefficients.CopyTo(parameters, column);
                column += term.Columns;
            }
        }

        return parameters;
    }

    private double[] Eta(double[] parameters, double[] design)
    {
        CheckParameters(parameters);

        if (design.Length != Columns)
            throw new ArgumentException($"The design has {design.Length} columns, the model {Columns}", nameof(design));

        var eta = new double[Family.Predictors];

        for (var p = 0; p < eta.Length; p++)
        {
            var start = p * (Columns + 1);

            eta[p] = parameters[start];

            for (var c = 0; c < Columns; c++)
                eta[p] += parameters[start + 1 + c] * design[c];
        }

        return eta;
    }

    private void CheckParameters(double[] parameters)
    {
        if (parameters.Length != ParameterCount)
            throw new ArgumentException($"The model needs {ParameterCount} parameters, got {parameters.Length}", nameof(parameters));
    }
}
