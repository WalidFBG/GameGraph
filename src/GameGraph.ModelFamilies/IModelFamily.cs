namespace GameGraph.ModelFamilies;

public interface IModelFamily
{
    string Name { get; }

    int Predictors { get; }

    FamilyDescription Describe();

    double NegativeLogLikelihood(double[] eta, int outcome);

    double[] Gradient(double[] eta, int outcome);

    int Sample(double[] eta, double uniform);
}
