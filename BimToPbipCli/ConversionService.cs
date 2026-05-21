using System.Text;

namespace BimToPbipCli;

/// <summary>
/// Orchestrates the BIM → PBIP conversion:
///   1. validate environment (input .bim, pbi-tools, temp workspace);
///   2. run "pbi-tools convert" to produce a TMDL model folder;
///   3. assemble a PBIP-compatible project folder around it;
///   4. clean up the temporary workspace.
///
/// All failures are reported through <see cref="ConversionException"/> with an
/// explicit <see cref="ExitCode"/> — there are no silent failures.
/// </summary>
public sealed class ConversionService
{
    /// <summary>UTF-8 encoding that emits no byte-order mark.</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IStepLogger _log;

    public ConversionService(IStepLogger log)
    {
        _log = log;
    }

    public ConversionResult Run(CliOptions options)
    {
        string? tempDir = null;
        try
        {
            var bimPath = resolveBimPath(options);
            var datasetName = resolveDatasetName(options, bimPath);
            var outputRoot = resolveOutputRoot(options, bimPath, datasetName);
            var pbiToolsPath = resolvePbiTools(options);

            tempDir = createTempWorkspace();

            var modelFolder = runConvert(pbiToolsPath, bimPath, tempDir, options.ModelSerialization);
            assemblePbipProject(modelFolder, outputRoot, datasetName);

            cleanupTemp(tempDir, options.KeepTemp);

            printSummary(outputRoot, datasetName);

            return new ConversionResult
            {
                ExitCode = ExitCode.Success,
                PbipProjectPath = outputRoot,
                Message = $"PBIP project (dataset only) created at: {outputRoot}",
            };
        }
        catch (ConversionException ex)
        {
            _log.Error(ex.Message);
            tryCleanupOnFailure(tempDir, options.KeepTemp);
            return new ConversionResult { ExitCode = ex.ExitCode, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _log.Error($"Unexpected error: {ex.Message}");
            tryCleanupOnFailure(tempDir, options.KeepTemp);
            return new ConversionResult { ExitCode = ExitCode.UnexpectedError, Message = ex.Message };
        }
    }

    // ----- Step 1: environment validation -------------------------------------------------

    private string resolveBimPath(CliOptions options)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(options.BimPath);
        }
        catch (Exception ex)
        {
            throw new ConversionException(ExitCode.BimNotFound, $"Invalid --bim path '{options.BimPath}': {ex.Message}", ex);
        }

        if (!File.Exists(fullPath))
        {
            throw new ConversionException(ExitCode.BimNotFound, $"Input model.bim not found: {fullPath}");
        }

        _log.Info($"Input model:   {fullPath}");
        return fullPath;
    }

    private static string resolveDatasetName(CliOptions options, string bimPath)
    {
        return string.IsNullOrWhiteSpace(options.DatasetName)
            ? Path.GetFileNameWithoutExtension(bimPath)
            : options.DatasetName.Trim();
    }

    private string resolveOutputRoot(CliOptions options, string bimPath, string datasetName)
    {
        string root;
        if (!string.IsNullOrWhiteSpace(options.OutputRoot))
        {
            root = Path.GetFullPath(options.OutputRoot);
        }
        else
        {
            // Default: a sibling sub-folder next to the .bim file.
            var bimDir = Path.GetDirectoryName(bimPath) ?? Directory.GetCurrentDirectory();
            root = Path.Combine(bimDir, datasetName);
        }

        _log.Info($"Dataset name:  {datasetName}");
        _log.Info($"Output root:   {root}");
        return root;
    }

    private string resolvePbiTools(CliOptions options)
    {
        _log.Step("Checking environment for pbi-tools...");
        var resolved = PbiToolsLocator.Resolve(options.PbiToolsPath);
        if (resolved is null)
        {
            throw new ConversionException(
                ExitCode.PbiToolsNotFound,
                "pbi-tools CLI could not be located. Provide --pbiToolsPath, set the " +
                $"{PbiToolsLocator.EnvironmentVariableName} environment variable, or add pbi-tools to PATH. " +
                "Download it from https://pbi.tools/cli/.");
        }

        _log.Info($"pbi-tools:     {resolved}");
        return resolved;
    }

    private string createTempWorkspace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bim-to-pbip", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
        }
        catch (Exception ex)
        {
            throw new ConversionException(ExitCode.UnexpectedError, $"Could not create temporary workspace '{tempDir}': {ex.Message}", ex);
        }

        _log.Info($"Temp workspace: {tempDir}");
        return tempDir;
    }

    // ----- Step 2: pbi-tools convert ------------------------------------------------------

    private string runConvert(string pbiToolsPath, string bimPath, string tempDir, string modelSerialization)
    {
        var modelFolder = Path.Combine(tempDir, "model");
        _log.Step($"Running pbi-tools convert ({modelSerialization})...");

        var args = new List<string>
        {
            "convert",
            bimPath,
            modelFolder,
            modelSerialization,
        };

        ProcessResult result;
        try
        {
            result = ProcessRunner.Run(pbiToolsPath, args);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversionException(ExitCode.ConvertFailed, ex.Message, ex);
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                _log.Info($"  pbi-tools> {line.TrimEnd()}");
            }
        }

        if (!result.Succeeded)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? "(no stderr output)"
                : result.StandardError.Trim();
            throw new ConversionException(
                ExitCode.ConvertFailed,
                $"pbi-tools convert failed with exit code {result.ExitCode}.{Environment.NewLine}{detail}");
        }

        if (!Directory.Exists(modelFolder) || !Directory.EnumerateFileSystemEntries(modelFolder).Any())
        {
            throw new ConversionException(
                ExitCode.ConvertFailed,
                $"pbi-tools convert reported success but produced no output in '{modelFolder}'.");
        }

        _log.Success("Model converted to TMDL.");
        return modelFolder;
    }

    // ----- Step 3: assemble PBIP project --------------------------------------------------

    private void assemblePbipProject(string modelFolder, string outputRoot, string datasetName)
    {
        _log.Step("Assembling PBIP project structure...");

        var datasetDir = Path.Combine(outputRoot, PbipTemplates.DatasetFolderName);
        var definitionDir = Path.Combine(datasetDir, PbipTemplates.DefinitionFolderName);

        try
        {
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(datasetDir);

            // The model definition folder is replaced wholesale to keep re-runs idempotent.
            if (Directory.Exists(definitionDir))
            {
                Directory.Delete(definitionDir, recursive: true);
            }

            copyDirectory(modelFolder, definitionDir);

            var logicalId = Guid.NewGuid();
            // Power BI Desktop rejects PBIP files that begin with a UTF-8 BOM.
            // Utf8NoBom guarantees these files are written without one.
            File.WriteAllText(
                Path.Combine(outputRoot, datasetName + ".pbip"),
                PbipTemplates.PbipProjectFile(),
                Utf8NoBom);
            File.WriteAllText(
                Path.Combine(datasetDir, ".platform"),
                PbipTemplates.DatasetPlatformFile(datasetName, logicalId),
                Utf8NoBom);
            File.WriteAllText(
                Path.Combine(datasetDir, "definition.pbism"),
                PbipTemplates.DatasetDefinitionPropertiesFile(),
                Utf8NoBom);

            _log.Info($"Dataset GUID:  {logicalId}");

            // Final safety sweep: strip a UTF-8 BOM from every file in the
            // finished project, including TMDL files written by pbi-tools.
            stripBomFromTree(outputRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConversionException(
                ExitCode.OutputWriteFailed,
                $"Could not write the PBIP project under '{outputRoot}': {ex.Message}",
                ex);
        }

        _log.Success("PBIP structure assembled.");
    }

    private static void copyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            copyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    /// <summary>
    /// Removes a leading UTF-8 BOM (EF BB BF) from every file under <paramref name="root"/>.
    /// PBIP files must be UTF-8 without a BOM; this guards against BOMs that may
    /// have come from pbi-tools' TMDL output or a model.bim that already had one.
    /// </summary>
    private void stripBomFromTree(string root)
    {
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        var stripped = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 3 && bytes.AsSpan(0, 3).SequenceEqual(bom))
            {
                File.WriteAllBytes(file, bytes[3..]);
                stripped++;
            }
        }

        if (stripped > 0)
        {
            _log.Info($"Stripped a UTF-8 BOM from {stripped} file(s).");
        }
    }

    // ----- Step 4: cleanup ----------------------------------------------------------------

    private void cleanupTemp(string tempDir, bool keepTemp)
    {
        if (keepTemp)
        {
            _log.Info($"--keepTemp set; temporary workspace kept at: {tempDir}");
            return;
        }

        _log.Step("Cleaning up temporary workspace...");
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch (Exception ex)
        {
            // Non-fatal: the project is already built. Warn and continue.
            _log.Warn($"Could not delete temporary workspace '{tempDir}': {ex.Message}");
        }
    }

    private void tryCleanupOnFailure(string? tempDir, bool keepTemp)
    {
        if (tempDir is null || keepTemp || !Directory.Exists(tempDir))
        {
            return;
        }

        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on the failure path; the original error is what matters.
        }
    }

    // ----- Step 5: result summary ---------------------------------------------------------

    private void printSummary(string outputRoot, string datasetName)
    {
        var datasetDir = Path.Combine(outputRoot, PbipTemplates.DatasetFolderName);
        var definitionDir = Path.Combine(datasetDir, PbipTemplates.DefinitionFolderName);

        _log.Detail(string.Empty);
        _log.Success("Conversion complete.");
        _log.Detail($"  PBIP project root : {outputRoot}");
        _log.Detail($"  Project file      : {Path.Combine(outputRoot, datasetName + ".pbip")}");
        _log.Detail($"  Dataset folder    : {datasetDir}");
        _log.Detail($"  Model definition  : {definitionDir} (TMDL)");
        _log.Detail(string.Empty);
        _log.Detail("  Created: a PBIP dataset (semantic model) project. No report (.Report) part");
        _log.Detail("  was generated — open the .pbip in Power BI Desktop to add a report.");
    }
}
