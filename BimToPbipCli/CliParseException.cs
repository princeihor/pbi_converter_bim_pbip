namespace BimToPbipCli;

/// <summary>
/// Thrown when the command line cannot be parsed into valid <see cref="CliOptions"/>.
/// </summary>
public sealed class CliParseException : Exception
{
    public CliParseException(string message) : base(message)
    {
    }
}
