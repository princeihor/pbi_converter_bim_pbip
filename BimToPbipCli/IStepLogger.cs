namespace BimToPbipCli;

/// <summary>
/// Sink for the conversion pipeline's progress messages. Decoupling the pipeline
/// from the console lets the same <see cref="ConversionService"/> drive both the
/// CLI (writes to stdout/stderr) and the web UI (captures messages for JSON).
/// </summary>
public interface IStepLogger
{
    void Step(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Success(string message);

    /// <summary>An unprefixed detail/summary line.</summary>
    void Detail(string message);
}

/// <summary>A single captured log line.</summary>
public sealed record LogEntry(string Level, string Message);
