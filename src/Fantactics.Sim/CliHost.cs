using Fantactics.Core.Rules;
using Fantactics.Sim.Commands;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Tournaments;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Fantactics.Sim;

/// <summary>Wires up services and runs the command line. Separate from Program so tests can run it in-process.</summary>
public static class CliHost
{
    /// <summary>Runs <c>fantactics-sim</c> with <paramref name="args"/>, writing to <paramref name="console"/>.</summary>
    /// <returns>The process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(string[] args, IConsole console)
    {
        ServiceCollection services = new();
        services.AddSingleton(RulesConfig.Default);
        services.AddSingleton(console);
        services.AddSingleton<MatchStore>();
        services.AddSingleton<TournamentRunner>();
        services.AddSingleton<OutputWriter>();
        using ServiceProvider provider = services.BuildServiceProvider();

        using CommandLineApplication<SimApp> app = new(console);
        app.Conventions
            .UseDefaultConventions()
            .UseConstructorInjection(provider);

        try
        {
            return app.Execute(args);
        }
        catch (CommandParsingException ex)
        {
            console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
