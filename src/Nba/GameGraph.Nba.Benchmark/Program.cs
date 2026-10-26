using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using GameGraph.Engine.Walk;
using GameGraph.Nba.Fixture;
using GameGraph.Nba.Graph;
using GameGraph.Nba.Incidents;
using GameGraph.Simulation.Compiled;
using GameGraph.Simulation.Randomness;
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
        var compiling = Stopwatch.StartNew();
        var compiled = new CompiledGraph<NbaState>(fitted);
        var context = SimulationFixture.Context();
        var seeds = Seeds.Draw(Simulations);
        var incidents = new EmptyIncidentCollector();
        var decisions = new NoDecisionCollector<NbaState>();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Compiled the game in {compiling.Elapsed.TotalSeconds:F2} s"));
        Console.WriteLine($"{Simulations:N0} simulations of the fixture game a run, on {Environment.ProcessorCount} threads, after {WarmUpRuns} runs to warm up");

        Report("Plain", () => Parallel.For(0, seeds.Length, i => Simulator.Run(fitted, [seeds[i]], _ => new NbaState(incidents, context), decisions)));
        Report("Compiled", () => Parallel.For(0, seeds.Length, i => Simulator.Run(compiled, [seeds[i]], _ => new NbaState(incidents, context))));
    }

    private static void Report(string walk, Action run)
    {
        for (var warm = 0; warm < WarmUpRuns; warm++)
            run();

        var seconds = new double[Runs];

        for (var r = 0; r < Runs; r++)
        {
            var stopwatch = Stopwatch.StartNew();
            run();
            seconds[r] = stopwatch.Elapsed.TotalSeconds;
        }

        var sorted = seconds.Order().ToArray();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{walk}: {string.Join(" ", seconds.Select(s => s.ToString("F2", CultureInfo.InvariantCulture)))} s"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{walk}: median {sorted[Runs / 2]:F2} s, fastest {sorted[0]:F2} s, slowest {sorted[^1]:F2} s"));
    }
}
