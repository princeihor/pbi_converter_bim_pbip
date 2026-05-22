---
title: Templates and Token Substitution
last-updated: 2026-05-22
relates-to: [structure, concepts]
tags: [reference]
---

# Templates and Token Substitution

How the tool uses template files to generate the PBIP *wrapper* files.

## The Templates Folder

The `pbip-templates/` directory contains the canonical PBIP wrapper structure — four files taken verbatim from a real Power BI Desktop export, documented in `pbip-templates/REFERENCE.md`.

```
pbip-templates/
  project.pbip                          ← Project manifest template
  SemanticModel/
    definition.pbism                    ← Model item properties template
  Report/
    definition.pbir                     ← Report item properties template
    report.json                         ← Report template (blank page)
  REFERENCE.md                          ← Field-by-field documentation
```

**Key principle**: The wrapper structure is NOT defined in code. It lives here, and the tool reads these templates at conversion time.

**What templates do NOT cover**: The semantic model `SemanticModel/definition/` folder. That TMDL folder is generated per-model by the Tabular Object Model (TOM) library (`TmdlSerializer.SerializeDatabaseToFolder`), not from a template. See [TOM normalization](concepts.md#tom-normalization).

---

## Token Substitution

Three tokens appear in the templates and get replaced by the tool:

### `{{REPORT_FOLDER}}`

Replaced with the report folder name: `<DatasetName>.Report`

**Where used**:
- `pbip-templates/project.pbip` → artifact path

**Example** (DatasetName = "Sales"):
```json
{
  "artifacts": [
    { "report": { "path": "Sales.Report" } }
  ]
}
```

### `{{SEMANTIC_MODEL_FOLDER}}`

Replaced with the semantic model folder name: `<DatasetName>.SemanticModel`

**Where used**:
- `pbip-templates/Report/definition.pbir` → model reference path

**Example** (DatasetName = "Sales"):
```json
{
  "datasetReference": {
    "byPath": {
      "path": "../Sales.SemanticModel"
    }
  }
}
```

### `{{PAGE_NAME}}`

Replaced with a randomly-generated 20-character hex ID.

**Where used**:
- `pbip-templates/Report/report.json` → page name in sections array

**Example**:
```json
{
  "sections": [
    {
      "name": "a1b2c3d4e5f6g7h8i9j0",
      "displayName": "Page 1",
      ...
    }
  ]
}
```

The page ID is generated fresh for each conversion (ensures uniqueness).

---

## How the Tool Uses Templates

1. Templates are embedded as resources in the compiled `.exe` (at build time)
2. Code loads them from the assembly
3. Performs token replacement using `string.Replace()`
4. Writes the result using `UTF8Encoding(false)`

```csharp
// Pseudocode
string template = PbipTemplates.LoadTemplate("pbip-templates.project.pbip");
string result = template.Replace("{{REPORT_FOLDER}}", reportFolderName);
File.WriteAllText(outputPath, result, new UTF8Encoding(false));
```

---

## Why Embedding Matters

The C# `.exe` is **self-contained** — it embeds the templates and doesn't need the `pbip-templates/` folder at runtime.

```xml
<!-- In BimToPbipCli.csproj -->
<ItemGroup>
  <EmbeddedResource Include="../../pbip-templates/project.pbip" Link="pbip-templates/project.pbip" />
  <EmbeddedResource Include="../../pbip-templates/SemanticModel/definition.pbism" Link="pbip-templates/SemanticModel/definition.pbism" />
  <!-- etc. -->
</ItemGroup>
```

At build time, these files are compiled into the `.exe`. At runtime, the tool loads them from the executable.

**Benefit**: You can distribute a single `.exe` file and it works everywhere (Windows, any folder). The templates don't need to be present.

---

## Token Uniqueness & Safety

Tokens use `{{` and `}}` delimiters to avoid accidental replacement if the PBIP files happen to contain similar text.

The tool only replaces known tokens. Unknown text is left alone.

---

## If You Need to Modify the Wrapper Structure

1. Edit the file in `pbip-templates/`
2. Update the documentation in `pbip-templates/REFERENCE.md`
3. Rebuild the project — embedded resources are baked in at build time
4. Update the internal test (`tests/internal_test.py`) if the structure changed

This applies only to the wrapper files. The semantic model `definition/` folder is produced by TOM and is not editable via templates.

See [Single Source of Truth](concepts.md#single-source-of-truth).

---

## Next Steps

- [Project Structure](structure.md) — field-by-field breakdown
- [Validation](validation.md) — how we verify templates were substituted correctly
- [Concepts](concepts.md#template-tokens) — why we use tokens
