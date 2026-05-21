using System.Text.Json;

namespace BimToPbipCli;

/// <summary>
/// Single source of truth for the PBIP metadata files this utility emits.
///
/// IMPORTANT: the PBIP project format is owned by Microsoft and still evolving
/// (Power BI Desktop "Power BI Project" / .pbip preview). If Microsoft changes
/// the schema, only this file should need updating. The structure produced here
/// follows the publicly documented preview layout:
///
///   &lt;root&gt;/
///     &lt;dataset&gt;.pbip            -- project entry point
///     dataset/
///       .platform               -- Fabric/Git item metadata (type + logicalId GUID)
///       definition.pbism        -- semantic model item properties
///       definition/             -- TMDL model files produced by pbi-tools
///
/// See README.md and https://learn.microsoft.com/power-bi/developer/projects/
/// for the authoritative, up-to-date specification.
/// </summary>
public static class PbipTemplates
{
    /// <summary>Relative name of the dataset folder inside the project root.</summary>
    public const string DatasetFolderName = "dataset";

    /// <summary>Relative name of the model definition folder inside the dataset folder.</summary>
    public const string DefinitionFolderName = "definition";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// The .pbip project file. Points at the dataset artifact by relative path.
    /// </summary>
    public static string PbipProjectFile()
    {
        var doc = new Dictionary<string, object?>
        {
            ["version"] = "1.0",
            ["artifacts"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["dataset"] = new Dictionary<string, object?>
                    {
                        ["path"] = DatasetFolderName,
                    },
                },
            },
            ["settings"] = new Dictionary<string, object?>
            {
                ["enableAutoRecovery"] = true,
            },
        };

        return Serialize(doc);
    }

    /// <summary>
    /// The dataset's .platform file. Carries the dataset display name and a
    /// generated GUID (logicalId) that uniquely identifies the item.
    /// </summary>
    public static string DatasetPlatformFile(string datasetName, Guid logicalId)
    {
        var doc = new Dictionary<string, object?>
        {
            ["$schema"] = "https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json",
            ["metadata"] = new Dictionary<string, object?>
            {
                ["type"] = "SemanticModel",
                ["displayName"] = datasetName,
            },
            ["config"] = new Dictionary<string, object?>
            {
                ["version"] = "2.0",
                ["logicalId"] = logicalId.ToString(),
            },
        };

        return Serialize(doc);
    }

    /// <summary>
    /// The dataset's definition.pbism file (semantic model item properties).
    /// </summary>
    public static string DatasetDefinitionPropertiesFile()
    {
        var doc = new Dictionary<string, object?>
        {
            ["version"] = "4.0",
            ["settings"] = new Dictionary<string, object?>(),
        };

        return Serialize(doc);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
}
