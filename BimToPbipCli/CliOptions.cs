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

    /// <summary>Dataset name. When null, the .bim file name without extension is used.</summary>
    public string? DatasetName { get; init; }

    /// <summary>
    /// Explicit path to pbi-tools(.exe). When null the utility falls back to the
    /// PBI_TOOLS_PATH environment variable and then to the system PATH.
    /// </summary>
    public string? PbiToolsPath { get; init; }

    /// <summary>
    /// Model serialization format passed to "pbi-tools convert". Defaults to "Tmdl".
    /// </summary>
    public string ModelSerialization { get; init; } = "Tmdl";

    /// <summary>When true, the temporary working directory is kept for inspection.</summary>
    public bool KeepTemp { get; init; }

    /// <summary>When true, only help text is printed and the program exits.</summary>
    public bool ShowHelp { get; init; }
}
