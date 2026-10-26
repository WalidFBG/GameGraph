using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using GameGraph.Engine;
using GameGraph.Nba.Fixture;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Simulation;

namespace GameGraph.Nba.Benchmark;

public static class Program
{
    private const int Simulations = 10_000;
    private const int WarmUpRuns = 3;
    private const int Runs = 10;

    public static void Main()
    {
        if (typeof(Program).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled == true)
            throw new InvalidOperationException("The benchmark must run an optimised build: dotnet run -c Release");

        var fitted = SimulationFixture.Fitted();
        var context = SimulationFixture.Context();
        var seeds = Seeds.Draw(Simulations);

        Console.WriteLine($"{Simulations:N0} simulations of the fixture game a run, on {Environment.ProcessorCount} threads, after {WarmUpRuns} runs to warm up");

        for (var run = 0; run < WarmUpRuns; run++)
            Time(fitted, context, seeds);

        var seconds = new double[Runs];

        for (var run = 0; run < Runs; run++)
        {
            seconds[run] = Time(fitted, context, seeds);

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Run {run + 1}: {seconds[run]:F2} s"));
        }

        var sorted = seconds.Order().ToArray();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Median {sorted[Runs / 2]:F2} s, fastest {sorted[0]:F2} s, slowest {sorted[^1]:F2} s"));
    }

    private static double Time(FittedGame<NbaState> fitted, GameContext context, ulong[] seeds)
    {
        var incidents = new EmptyIncidentCollector();
        var decisions = new EmptyDecisionCollector<NbaState>();
        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, seeds.Length, i => Simulator.Run(fitted, [seeds[i]], _ => new NbaState(incidents, context), decisions));

        return stopwatch.Elapsed.TotalSeconds;
    }
}
