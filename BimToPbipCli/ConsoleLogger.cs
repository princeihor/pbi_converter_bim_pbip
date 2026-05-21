namespace BimToPbipCli;

/// <summary>
/// Step logger for CLI mode. Informational output goes to stdout, errors to
/// stderr, so the utility plays well inside scripts and CI pipelines.
/// </summary>
public sealed class ConsoleLogger : IStepLogger
{
    public void Step(string message) => Console.Out.WriteLine($"[ STEP ] {message}");

    public void Info(string message) => Console.Out.WriteLine($"[ INFO ] {message}");

    public void Warn(string message) => Console.Out.WriteLine($"[ WARN ] {message}");

    public void Error(string message) => Console.Error.WriteLine($"[ ERROR ] {message}");

    public void Success(string message) => Console.Out.WriteLine($"[  OK  ] {message}");

    public void Detail(string message) => Console.Out.WriteLine(message);
}
