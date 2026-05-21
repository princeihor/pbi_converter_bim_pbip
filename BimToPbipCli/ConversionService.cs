using System.Text;
using System.Text.Json;

namespace BimToPbipCli;

/// <summary>
/// Converts a Tabular model.bim into a Power BI Desktop project (PBIP).
///
/// A .bim file IS TMSL JSON, so it is stored directly as the semantic model's
/// model.bim — the "PBIP with TMSL" layout that Power BI Desktop itself uses.
/// No model conversion / pbi-tools, no network access is needed. The PBIP file
/// structure comes entirely from the embedded templates (see
/// <see cref="PbipTemplates"/> and pbip-templates/REFERENCE.md).
///
/// Pipeline: validate input -> assemble project -> validate output.
/// Every failure surfaces as a <see cref="ConversionException"/> with an
/// explicit <see cref="ExitCode"/> — there are no silent failures.
/// </summary>
public sealed class ConversionService
{
    /// <summary>UTF-8 encoding that emits no byte-order mark (Power BI rejects a BOM).</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IStepLogger _log;

    public ConversionService(IStepLogger log) => _log = log;

    public ConversionResult Run(CliOptions options)
    {
        try
        {
            var bimPath = resolveBimPath(options);
            validateBim(bimPath);

            var datasetName = resolveDatasetName(options, bimPath);
            var outputRoot = resolveOutputRoot(options, bimPath, datasetName);

            assemblePbipProject(bimPath, outputRoot, datasetName);
            validatePbipProject(outputRoot, datasetName);

            printSummary(outputRoot, datasetName);

            return new ConversionResult
            {
                ExitCode = ExitCode.Success,
                PbipProjectPath = outputRoot,
                Message = $"PBIP project created at: {outputRoot}",
            };
        }
        catch (ConversionException ex)
        {
            _log.Error(ex.Message);
            return new ConversionResult { ExitCode = ex.ExitCode, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _log.Error($"Unexpected error: {ex.Message}");
            return new ConversionResult { ExitCode = ExitCode.UnexpectedError, Message = ex.Message };
        }
    }

    // ----- Step 1: input validation -------------------------------------------------------

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

        return fullPath;
    }

    /// <summary>
    /// Confirms the input .bim parses as a JSON object (TMSL). A missing
    /// top-level 'model' member is only a warning so an unusual but valid
    /// complex model is never blocked.
    /// </summary>
    private void validateBim(string bimPath)
    {
        _log.Step("Validating input model...");

        string text;
        try
        {
            text = File.ReadAllText(bimPath);
        }
        catch (Exception ex)
        {
            throw new ConversionException(ExitCode.BimInvalid, $"Could not read '{bimPath}': {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ConversionException(ExitCode.BimInvalid, $"Input .bim file is empty: {bimPath}");
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ConversionException(ExitCode.BimInvalid,
                    $"Input .bim must be a JSON object (TMSL model): {bimPath}");
            }

            if (!doc.RootElement.TryGetProperty("model", out _))
            {
                _log.Warn("Input .bim has no top-level 'model' member — copying it anyway.");
            }
        }
        catch (JsonException ex)
        {
            throw new ConversionException(ExitCode.BimInvalid,
                $"Input .bim is not valid JSON ({bimPath}): {ex.Message}", ex);
        }

        _log.Info($"Input model:   {bimPath}");
    }

    private string resolveDatasetName(CliOptions options, string bimPath)
    {
        var raw = string.IsNullOrWhiteSpace(options.DatasetName)
            ? Path.GetFileNameWithoutExtension(bimPath)
            : options.DatasetName.Trim();

        // The dataset name becomes a folder name, so drop illegal characters.
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(raw.Where(c => Array.IndexOf(invalid, c) < 0).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe))
        {
            throw new ConversionException(ExitCode.InvalidArguments,
                $"Dataset name '{raw}' contains no valid file-name characters.");
        }

        _log.Info($"Dataset name:  {safe}");
        return safe;
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
            var bimDir = Path.GetDirectoryName(bimPath) ?? Directory.GetCurrentDirectory();
            root = Path.Combine(bimDir, datasetName);
        }

        _log.Info($"Output root:   {root}");
        return root;
    }

    // ----- Step 2: assemble PBIP project --------------------------------------------------

    private void assemblePbipProject(string bimPath, string outputRoot, string datasetName)
    {
        _log.Step("Assembling PBIP project (TMSL layout)...");

        var smFolder = PbipTemplates.SemanticModelFolderName(datasetName);
        var reportFolder = PbipTemplates.ReportFolderName(datasetName);
        var smDir = Path.Combine(outputRoot, smFolder);
        var reportDir = Path.Combine(outputRoot, reportFolder);

        try
        {
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(smDir);
            Directory.CreateDirectory(reportDir);

            // The .bim IS the TMSL model -> store it verbatim as model.bim,
            // re-encoded as UTF-8 without a BOM.
            File.WriteAllText(Path.Combine(smDir, "model.bim"), File.ReadAllText(bimPath), Utf8NoBom);

            File.WriteAllText(
                Path.Combine(outputRoot, datasetName + ".pbip"),
                PbipTemplates.ProjectFile(reportFolder), Utf8NoBom);
            File.WriteAllText(
                Path.Combine(smDir, "definition.pbism"),
                PbipTemplates.SemanticModelDefinition(), Utf8NoBom);
            File.WriteAllText(
                Path.Combine(reportDir, "definition.pbir"),
                PbipTemplates.ReportDefinition(smFolder), Utf8NoBom);
            File.WriteAllText(
                Path.Combine(reportDir, "report.json"),
                PbipTemplates.ReportJson(PbipTemplates.NewPageName()), Utf8NoBom);

            // Belt-and-braces: strip a UTF-8 BOM from every file in the project.
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

    /// <summary>Removes a leading UTF-8 BOM (EF BB BF) from every file under <paramref name="root"/>.</summary>
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

    // ----- Step 3: validate the assembled project -----------------------------------------

    /// <summary>
    /// Validates the finished project against the rules Power BI Desktop
    /// enforces, so a broken project is never reported as success — this is the
    /// guard that turns silent "Cannot read .pbip" failures into a clear error.
    /// </summary>
    private void validatePbipProject(string outputRoot, string datasetName)
    {
        _log.Step("Validating PBIP project...");

        var smFolder = PbipTemplates.SemanticModelFolderName(datasetName);
        var reportFolder = PbipTemplates.ReportFolderName(datasetName);
        var smDir = Path.Combine(outputRoot, smFolder);
        var reportDir = Path.Combine(outputRoot, reportFolder);

        var pbipPath = Path.Combine(outputRoot, datasetName + ".pbip");
        var pbirPath = Path.Combine(reportDir, "definition.pbir");
        var reportJsonPath = Path.Combine(reportDir, "report.json");
        var pbismPath = Path.Combine(smDir, "definition.pbism");
        var modelPath = Path.Combine(smDir, "model.bim");

        foreach (var p in new[] { pbipPath, pbirPath, reportJsonPath, pbismPath, modelPath })
        {
            if (!File.Exists(p))
            {
                throw new ConversionException(ExitCode.ValidationFailed, $"PBIP validation failed: missing '{p}'.");
            }
        }

        // .pbip : exactly a 'report' artifact pointing at the report folder,
        // and never a 'dataset' artifact (Power BI's schema rejects that).
        using (var pbip = parseJson(pbipPath))
        {
            if (!pbip.RootElement.TryGetProperty("artifacts", out var artifacts)
                || artifacts.ValueKind != JsonValueKind.Array
                || artifacts.GetArrayLength() < 1)
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{pbipPath}' has no artifacts.");
            }

            var artifact = artifacts[0];
            if (artifact.TryGetProperty("dataset", out _))
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{pbipPath}' declares a 'dataset' artifact (Power BI rejects this).");
            }

            if (!artifact.TryGetProperty("report", out var report)
                || !report.TryGetProperty("path", out var reportPath)
                || reportPath.GetString() != reportFolder)
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{pbipPath}' has no 'report' artifact pointing at '{reportFolder}'.");
            }
        }

        // definition.pbir : relative reference to the semantic model folder.
        using (var pbir = parseJson(pbirPath))
        {
            var expectedRef = "../" + smFolder;
            var actualRef = pbir.RootElement
                .GetProperty("datasetReference")
                .GetProperty("byPath")
                .GetProperty("path")
                .GetString();
            if (actualRef != expectedRef)
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{pbirPath}' byPath is '{actualRef}', expected '{expectedRef}'.");
            }
        }

        // report.json : parses and has at least one page.
        using (var reportDoc = parseJson(reportJsonPath))
        {
            if (!reportDoc.RootElement.TryGetProperty("sections", out var sections)
                || sections.ValueKind != JsonValueKind.Array
                || sections.GetArrayLength() < 1)
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{reportJsonPath}' has no report pages.");
            }
        }

        // definition.pbism and model.bim : must parse as JSON.
        parseJson(pbismPath).Dispose();
        parseJson(modelPath).Dispose();

        // No file in the project may carry a UTF-8 BOM.
        foreach (var file in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                throw new ConversionException(ExitCode.ValidationFailed,
                    $"PBIP validation failed: '{file}' starts with a UTF-8 BOM.");
            }
        }

        _log.Success("PBIP structure validated (all checks passed).");
    }

    private static JsonDocument parseJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new ConversionException(ExitCode.ValidationFailed,
                $"PBIP validation failed: '{path}' is not valid JSON: {ex.Message}", ex);
        }
    }

    // ----- Step 4: result summary ---------------------------------------------------------

    private void printSummary(string outputRoot, string datasetName)
    {
        var smDir = Path.Combine(outputRoot, PbipTemplates.SemanticModelFolderName(datasetName));
        var reportDir = Path.Combine(outputRoot, PbipTemplates.ReportFolderName(datasetName));

        _log.Detail(string.Empty);
        _log.Success("Conversion complete.");
        _log.Detail($"  PBIP project root : {outputRoot}");
        _log.Detail($"  Open in Power BI  : {Path.Combine(outputRoot, datasetName + ".pbip")}");
        _log.Detail($"  Semantic model    : {smDir}");
        _log.Detail($"  Report            : {reportDir}");
        _log.Detail(string.Empty);
        _log.Detail("  Double-click the .pbip file to open it in Power BI Desktop.");
    }
}
