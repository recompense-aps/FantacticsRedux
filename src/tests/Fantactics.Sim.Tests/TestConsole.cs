using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Tests;

/// <summary>An in-memory console that captures output.</summary>
internal sealed class TestConsole : IConsole
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public event ConsoleCancelEventHandler? CancelKeyPress
    {
        add { }
        remove { }
    }

    public TextWriter Out => _out;

    public TextWriter Error => _error;

    public TextReader In => TextReader.Null;

    public bool IsInputRedirected => true;

    public bool IsOutputRedirected => true;

    public bool IsErrorRedirected => true;

    public ConsoleColor ForegroundColor { get; set; }

    public ConsoleColor BackgroundColor { get; set; }

    /// <summary>Everything written to standard output.</summary>
    public string Output => _out.ToString();

    public void ResetColor()
    {
    }
}
