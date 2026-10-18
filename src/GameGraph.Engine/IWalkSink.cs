namespace GameGraph.Engine;

public interface IWalkSink<TState>
{
    void Observe(NodeSpec<TState> node, double[] features, int outcome);
}
