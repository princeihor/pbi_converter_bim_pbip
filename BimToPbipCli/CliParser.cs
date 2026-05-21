namespace BimToPbipCli;

/// <summary>
/// Translates raw process arguments into <see cref="CliOptions"/>.
/// Kept separate from the conversion logic so argument handling can be tested
/// and changed independently.
/// </summary>
public static class CliParser
{
    public const string HelpText = """
        BimToPbipCli — converts a Tabular model.bim into a PBIP-compatible project folder
                       using the pbi-tools CLI.

        USAGE:
          BimToPbipCli                 Launch the browser-based UI (also happens when
                                       the .exe is started with no arguments / double-clicked).
          BimToPbipCli --ui            Force the browser-based UI explicitly.
          BimToPbipCli --bim <path> [--out <path>] [--dataset <name>]
                       [--pbiToolsPath <path>] [--modelSerialization <format>]
                       [--keepTemp] [--help]

        OPTIONS:
          --bim <path>                 (required) Path to the input model.bim file.
          --out <path>                 PBIP project root folder. Defaults to a
                                       sub-folder named after the dataset, created
                                       next to the .bim file.
          --dataset <name>             Dataset name. Defaults to the .bim file name
                                       without extension.
          --pbiToolsPath <path>        Path to pbi-tools(.exe). If omitted, the
                                       PBI_TOOLS_PATH environment variable is used,
                                       then the system PATH.
          --modelSerialization <fmt>   Serialization passed to "pbi-tools convert".
                                       Defaults to "Tmdl".
          --keepTemp                   Keep the temporary working directory.
          --help, -h                   Show this help and exit.

        EXAMPLES:
          BimToPbipCli --bim "C:\Models\MyModel.bim"
          BimToPbipCli --bim "C:\Models\MyModel.bim" --out "C:\PBIP\MyModel" --dataset "MyModelDataset"
          BimToPbipCli --bim "C:\Models\MyModel.bim" --pbiToolsPath "C:\tools\pbi-tools\pbi-tools.exe"
        """;

    /// <summary>
    /// Parses the supplied arguments.
    /// </summary>
    /// <exception cref="CliParseException">Thrown for unknown or malformed arguments.</exception>
    public static CliOptions Parse(string[] args)
    {
        string? bimPath = null;
        string? outputRoot = null;
        string? datasetName = null;
        string? pbiToolsPath = null;
        string? modelSerialization = null;
        var keepTemp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var (name, inlineValue) = SplitArgument(args[i]);

            switch (name.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    return new CliOptions { BimPath = string.Empty, ShowHelp = true };

                case "--keeptemp":
                    keepTemp = true;
                    break;

                case "--bim":
                    bimPath = TakeValue(args, ref i, name, inlineValue);
                    break;

                case "--out":
                case "--output":
                case "--outputroot":
                    outputRoot = TakeValue(args, ref i, name, inlineValue);
                    break;

                case "--dataset":
                case "--datasetname":
                    datasetName = TakeValue(args, ref i, name, inlineValue);
                    break;

                case "--pbitoolspath":
                    pbiToolsPath = TakeValue(args, ref i, name, inlineValue);
                    break;

                case "--modelserialization":
                    modelSerialization = TakeValue(args, ref i, name, inlineValue);
                    break;

                default:
                    throw new CliParseException($"Unknown argument: '{args[i]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(bimPath))
        {
            throw new CliParseException("Missing required argument: --bim <path>.");
        }

        return new CliOptions
        {
            BimPath = bimPath,
            OutputRoot = string.IsNullOrWhiteSpace(outputRoot) ? null : outputRoot,
            DatasetName = string.IsNullOrWhiteSpace(datasetName) ? null : datasetName,
            PbiToolsPath = string.IsNullOrWhiteSpace(pbiToolsPath) ? null : pbiToolsPath,
            ModelSerialization = string.IsNullOrWhiteSpace(modelSerialization) ? "Tmdl" : modelSerialization,
            KeepTemp = keepTemp,
            ShowHelp = false,
        };
    }

    /// <summary>Splits "--name=value" into ("--name", "value"); plain flags yield a null value.</summary>
    private static (string Name, string? Value) SplitArgument(string raw)
    {
        var eq = raw.IndexOf('=');
        return eq < 0
            ? (raw, null)
            : (raw[..eq], raw[(eq + 1)..]);
    }

    /// <summary>
    /// Returns an inline "--name=value" value, or consumes the next argument as the value.
    /// </summary>
    private static string TakeValue(string[] args, ref int i, string name, string? inlineValue)
    {
        if (inlineValue is not null)
        {
            if (inlineValue.Length == 0)
            {
                throw new CliParseException($"Argument '{name}' has an empty value.");
            }

            return inlineValue;
        }

        if (i + 1 >= args.Length)
        {
            throw new CliParseException($"Argument '{name}' expects a value.");
        }

        return args[++i];
    }
}
