using System.Text.Json.Serialization;

namespace GameGraph.Models.Contract;

[JsonConverter(typeof(FittedModelJson))]
public sealed record FittedModel
{
    public FittedModel(string node, ModelDeclaration model, IReadOnlyDictionary<string, PredictorFit> fit)
    {
        if (!Sorted(fit.Keys).SequenceEqual(Sorted(model.Terms.Keys)))
            throw new ArgumentException($"{node} predicts [{string.Join(", ", Sorted(model.Terms.Keys))}], but its fit has [{string.Join(", ", Sorted(fit.Keys))}]", nameof(fit));

        foreach (var parameter in model.Distribution.Predicted())
        {
            if (!parameter.HasIntercept && fit[parameter.Name].Intercept != 0)
                throw new ArgumentException($"The {parameter.Name} of {node} has no intercept, but its fit has {fit[parameter.Name].Intercept}", nameof(fit));
        }

        foreach (var (name, terms) in model.Terms)
        {
            var fits = fit[name].Terms;

            if (!Sorted(fits.Keys).SequenceEqual(Sorted(terms.Select(t => t.Feature))))
                throw new ArgumentException($"The {name} of {node} has terms [{string.Join(", ", terms.Select(t => t.Feature))}], fitted [{string.Join(", ", Sorted(fits.Keys))}]", nameof(fit));

            foreach (var term in terms)
                RequireFitOf(term, fits[term.Feature]);
        }

        Node = node;
        Model = model;
        Fit = fit;
    }

    public string Node { get; }

    public ModelDeclaration Model { get; }

    public IReadOnlyDictionary<string, PredictorFit> Fit { get; }

    private static void RequireFitOf(Term term, TermFit fit)
    {
        switch (term, fit)
        {
            case (LinearTerm, LinearFit):
                return;
            case (LinearSplineTerm spline, LinearSplineFit splineFit):
                if (splineFit.KnotPositions.Length > spline.Knots)
                    throw new ArgumentException($"A linear spline of {spline.Feature} with {spline.Knots} knots has {splineFit.KnotPositions.Length} positions", nameof(fit));
                return;
            default:
                throw new ArgumentException($"The fit of {term.Feature} does not match its basis", nameof(fit));
        }
    }

    private static string[] Sorted(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToArray();
}
