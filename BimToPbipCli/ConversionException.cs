namespace BimToPbipCli;

/// <summary>
/// Raised by <see cref="ConversionService"/> when a step fails. Carries the
/// <see cref="ExitCode"/> that should be returned to the OS, so failures are
/// always explicit — never silent.
/// </summary>
public sealed class ConversionException : Exception
{
    public ConversionException(ExitCode exitCode, string message) : base(message)
    {
        ExitCode = exitCode;
    }

    public ConversionException(ExitCode exitCode, string message, Exception inner)
        : base(message, inner)
    {
        ExitCode = exitCode;
    }

    public ExitCode ExitCode { get; }
}
