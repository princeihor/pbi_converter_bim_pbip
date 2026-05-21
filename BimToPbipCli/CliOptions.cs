namespace BimToPbipCli;

/// <summary>
/// Parsed, validated command line options. Pure data — no business logic here.
/// </summary>
public sealed class CliOptions
{
    /// <summary>Absolute or relative path to the input model.bim file. Required.</summary>
    public required string BimPath { get; init; }

    /// <summary>
    /// Root folder of the PBIP project to create.
    /// When null, a sub-folder named after the dataset is created next to the .bim file.
    /// </summary>
    public string? OutputRoot { get; init; }

    /// <summary>Dataset / project name. When null, the .bim file name without extension is used.</summary>
    public string? DatasetName { get; init; }

    /// <summary>When true, only help text is printed and the program exits.</summary>
    public bool ShowHelp { get; init; }
}
