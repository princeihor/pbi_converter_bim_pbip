namespace BimToPbipCli;

/// <summary>
/// Step logger for web UI mode. Collects every message so it can be returned to
/// the browser as JSON instead of being written to a console.
/// </summary>
public sealed class CapturingLogger : IStepLogger
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public void Step(string message) => _entries.Add(new LogEntry("step", message));

    public void Info(string message) => _entries.Add(new LogEntry("info", message));

    public void Warn(string message) => _entries.Add(new LogEntry("warn", message));

    public void Error(string message) => _entries.Add(new LogEntry("error", message));

    public void Success(string message) => _entries.Add(new LogEntry("success", message));

    public void Detail(string message) => _entries.Add(new LogEntry("detail", message));
}
