namespace BimToPbipCli;

/// <summary>
/// Resolves the pbi-tools executable. The path is never hard-coded; resolution
/// order is: explicit --pbiToolsPath, then the PBI_TOOLS_PATH environment
/// variable, then a search of the system PATH.
/// </summary>
public static class PbiToolsLocator
{
    public const string EnvironmentVariableName = "PBI_TOOLS_PATH";

    private static readonly string[] CandidateNames =
    [
        "pbi-tools.exe",
        "pbi-tools.core.exe",
        "pbi-tools",
    ];

    /// <summary>
    /// Returns the full path to the pbi-tools executable, or null if it cannot be found.
    /// </summary>
    public static string? Resolve(string? explicitPath)
    {
        // 1. Explicit --pbiToolsPath argument.
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return ResolveFromHint(explicitPath);
        }

        // 2. PBI_TOOLS_PATH environment variable.
        var envValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            var resolved = ResolveFromHint(envValue);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        // 3. System PATH.
        return SearchPath();
    }

    /// <summary>
    /// A hint may point directly at the executable or at the folder containing it.
    /// </summary>
    private static string? ResolveFromHint(string hint)
    {
        if (File.Exists(hint))
        {
            return Path.GetFullPath(hint);
        }

        if (Directory.Exists(hint))
        {
            foreach (var name in CandidateNames)
            {
                var candidate = Path.Combine(hint, name);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    private static string? SearchPath()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var dir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var name in CandidateNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, name);
                }
                catch (ArgumentException)
                {
                    // PATH entry contains invalid characters — skip it.
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }
}
