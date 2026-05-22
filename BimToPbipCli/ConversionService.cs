using System.Text;
using System.Text.Json;
using Tom = Microsoft.AnalysisServices.Tabular;

namespace BimToPbipCli;

/// <summary>
/// Converts a Tabular model.bim into a Power BI Desktop project (PBIP).
///
/// The input .bim is never copied verbatim. It is deserialized through the
/// Tabular Object Model (TOM) and re-serialized as TMDL into the semantic
/// model's <c>definition/</c> folder — the modern PBIP layout that Power BI
/// Desktop itself uses. The TOM round-trip rebuilds a consistent metadata
/// object graph, which is what prevents Desktop's "Model object-map is not
/// consistent with the metadata-object graph" failure on edit/refresh.
///
/// The PBIP wrapper files (.pbip, definition.pbism, definition.pbir,
/// report.json) come from the embedded templates (see <see cref="PbipTemplates"/>
/// and pbip-templates/REFERENCE.md). TOM is bundled into the self-contained
/// .exe, so no separate install and no network access is needed.
///
/// Pipeline: load + normalize model -> assemble project -> validate output.
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
            var database = loadModel(bimPath);

            var datasetName = resolveDatasetName(options, bimPath);
            var outputRoot = resolveOutputRoot(options, bimPath, datasetName);

            assemblePbipProject(database, outputRoot, datasetName);
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

    // ----- Step 1: load and normalize the input model -------------------------------------

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
    /// Reads the input .bim and deserializes it through TOM. This is the
    /// authoritative validation: TOM rejects a malformed model with a precise
    /// message, and the resulting <see cref="Tom.Database"/> is a consistent
    /// object graph ready to be re-serialized as TMDL.
    /// </summary>
    private Tom.Database loadModel(string bimPath)
    {
        _log.Step("Loading and normalizing the input model (TOM)...");

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

        Tom.Database database;
        try
        {
            database = Tom.JsonSerializer.DeserializeDatabase(text);
        }
        catch (Exception ex)
        {
            throw new ConversionException(ExitCode.BimInvalid,
                $"Input .bim is not a valid Tabular model ({bimPath}): {ex.Message}", ex);
        }

        if (database.Model is null)
        {
            throw new ConversionException(ExitCode.BimInvalid,
                $"Input .bim has no model definition: {bimPath}");
        }

        _log.Info($"Input model:   {bimPath}");
        _log.Info($"Model:         {database.Name} (compatibility level {database.CompatibilityLevel}, "
            + $"{database.Model.Tables.Count} table(s))");
        return database;
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

    private void assemblePbipProject(Tom.Database database, string outputRoot, string datasetName)
    {
        _log.Step("Assembling PBIP project (TMDL layout)...");

        var smFolder = PbipTemplates.SemanticModelFolderName(datasetName);
        var reportFolder = PbipTemplates.ReportFolderName(datasetName);
        var smDir = Path.Combine(outputRoot, smFolder);
        var reportDir = Path.Combine(outputRoot, reportFolder);
        var definitionDir = Path.Combine(smDir, "definition");

        try
        {
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(smDir);
            Directory.CreateDirectory(reportDir);

            // Re-serialize the normalized model as TMDL into the definition/
            // folder. TOM writes database.tmdl, model.tmdl, tables/*.tmdl, etc.
            serializeModelToTmdl(database, definitionDir);

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
        catch (ConversionException)
        {
            throw;
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

    /// <summary>
    /// Serializes the TOM model to a TMDL <c>definition/</c> folder. A failure
    /// here means TOM could not turn the model into valid TMDL — the model
    /// itself is the problem, so it surfaces as an invalid-model error.
    /// </summary>
    private void serializeModelToTmdl(Tom.Database database, string definitionDir)
    {
        if (Directory.Exists(definitionDir))
        {
            Directory.Delete(definitionDir, recursive: true);
        }

        try
        {
            Tom.TmdlSerializer.SerializeDatabaseToFolder(database, definitionDir);
        }
        catch (Exception ex)
        {
            throw new ConversionException(ExitCode.BimInvalid,
                $"The model could not be serialized to TMDL: {ex.Message}", ex);
        }

        _log.Info($"Semantic model written as TMDL: {definitionDir}");
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
        var definitionDir = Path.Combine(smDir, "definition");

        var pbipPath = Path.Combine(outputRoot, datasetName + ".pbip");
        var pbirPath = Path.Combine(reportDir, "definition.pbir");
        var reportJsonPath = Path.Combine(reportDir, "report.json");
        var pbismPath = Path.Combine(smDir, "definition.pbism");

        foreach (var p in new[] { pbipPath, pbirPath, reportJsonPath, pbismPath })
        {
            if (!File.Exists(p))
            {
                throw new ConversionException(ExitCode.ValidationFailed, $"PBIP validation failed: missing '{p}'.");
            }
        }

        // The semantic model must be a non-empty TMDL definition/ folder.
        if (!Directory.Exists(definitionDir))
        {
            throw new ConversionException(ExitCode.ValidationFailed,
                $"PBIP validation failed: missing TMDL folder '{definitionDir}'.");
        }

        var tmdlFiles = Directory.GetFiles(definitionDir, "*.tmdl", SearchOption.AllDirectories);
        if (tmdlFiles.Length == 0)
        {
            throw new ConversionException(ExitCode.ValidationFailed,
                $"PBIP validation failed: TMDL folder '{definitionDir}' contains no .tmdl files.");
        }

        var modelTmdl = Path.Combine(definitionDir, "model.tmdl");
        if (!File.Exists(modelTmdl))
        {
            throw new ConversionException(ExitCode.ValidationFailed,
                $"PBIP validation failed: TMDL folder '{definitionDir}' has no model.tmdl.");
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

        // definition.pbism : must parse as JSON.
        parseJson(pbismPath).Dispose();

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
        _log.Detail($"  Semantic model    : {Path.Combine(smDir, "definition")} (TMDL)");
        _log.Detail($"  Report            : {reportDir}");
        _log.Detail(string.Empty);
        _log.Detail("  Double-click the .pbip file to open it in Power BI Desktop.");
    }
}
