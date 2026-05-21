namespace BimToPbipCli;

/// <summary>Outcome of a BIM → PBIP conversion run.</summary>
public sealed class ConversionResult
{
    public required ExitCode ExitCode { get; init; }

    /// <summary>Absolute path to the created PBIP project root, when successful.</summary>
    public string? PbipProjectPath { get; init; }

    /// <summary>Human-readable summary of the outcome.</summary>
    public required string Message { get; init; }

    public bool Succeeded => ExitCode == ExitCode.Success;
}
