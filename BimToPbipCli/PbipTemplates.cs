using System.Reflection;

namespace BimToPbipCli;

/// <summary>
/// Provides the PBIP metadata files. The structure itself is NOT defined here:
/// it lives in the <c>pbip-templates</c> folder (the single source of truth,
/// embedded into this assembly — see <c>pbip-templates/REFERENCE.md</c> for the
/// field-by-field rationale). This class only loads those templates and fills
/// in the per-conversion tokens.
/// </summary>
public static class PbipTemplates
{
    /// <summary>Semantic-model item folder name for a given dataset name.</summary>
    public static string SemanticModelFolderName(string datasetName) => datasetName + ".SemanticModel";

    /// <summary>Report item folder name for a given dataset name.</summary>
    public static string ReportFolderName(string datasetName) => datasetName + ".Report";

    /// <summary>A fresh 20-hex-character report page id, as Power BI Desktop names pages.</summary>
    public static string NewPageName() => Guid.NewGuid().ToString("N")[..20];

    /// <summary>The .pbip project file, pointing at the report folder.</summary>
    public static string ProjectFile(string reportFolderName) =>
        LoadTemplate("pbip-templates.project.pbip")
            .Replace("{{REPORT_FOLDER}}", reportFolderName);

    /// <summary>The semantic model's definition.pbism item-properties file.</summary>
    public static string SemanticModelDefinition() =>
        LoadTemplate("pbip-templates.SemanticModel.definition.pbism");

    /// <summary>The report's definition.pbir file, referencing the semantic model by path.</summary>
    public static string ReportDefinition(string semanticModelFolderName) =>
        LoadTemplate("pbip-templates.Report.definition.pbir")
            .Replace("{{SEMANTIC_MODEL_FOLDER}}", semanticModelFolderName);

    /// <summary>The report's report.json (a blank, openable single page).</summary>
    public static string ReportJson(string pageName) =>
        LoadTemplate("pbip-templates.Report.report.json")
            .Replace("{{PAGE_NAME}}", pageName);

    private static string LoadTemplate(string resourceName)
    {
        var assembly = typeof(PbipTemplates).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded PBIP template '{resourceName}' was not found in the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
