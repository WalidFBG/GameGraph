using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GameGraph.Engine.Nodes;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;

namespace GameGraph.Nba.Tests.Unit;

internal sealed class SimulationDigest : IDecisionCollector<NbaState>, INbaIncidentCollector
{
    private static readonly byte[][] Names = [.. NbaGraph.Instance.Nodes.Select(n => Encoding.UTF8.GetBytes(n.Name + "\0"))];

    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly int[] _decisions = new int[NbaGraph.Instance.Nodes.Length];

    public void Collect(Node<NbaState> node, NbaState state, ReadOnlySpan<double> features, int outcome)
    {
        Name(node);
        Add(outcome, features.Length);
        _hash.AppendData(MemoryMarshal.AsBytes(features));
    }

    public void Outcome(Node<NbaState> node, int outcome)
    {
        Name(node);
        Add(outcome);
    }

    public void Possession(in PossessionIncident incident) => Add(-1, incident.TeamInPossession, incident.Period, incident.Points);

    public void AwardedFreeThrow(in AwardedFreeThrowIncident incident) => Add(-2, incident.Team, incident.Period, incident.Made ? 1 : 0);

    public void Credited(in CreditIncident incident) => Add(-3, incident.Player, (int)incident.Credit);

    public void Played(in PlayedIncident incident) => Add(-4, incident.Player, incident.Team, incident.Started ? 1 : 0, incident.Seconds);

    public int Decisions(Node<NbaState> node) => _decisions[NbaGraph.Instance.IndexOf(node)];

    public string Hex() => Convert.ToHexStringLower(_hash.GetCurrentHash());

    private void Name(Node<NbaState> node)
    {
        var index = NbaGraph.Instance.IndexOf(node);

        _decisions[index]++;
        _hash.AppendData(Names[index]);
    }

    private void Add(params ReadOnlySpan<int> values) => _hash.AppendData(MemoryMarshal.AsBytes(values));
}
