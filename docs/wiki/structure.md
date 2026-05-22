---
title: Project Structure
last-updated: 2026-05-22
relates-to: [templates]
tags: [reference]
---

# PBIP Project Structure

The exact folder layout and files the tool produces.

## Output Layout

```
<OutputRoot>/
  <DatasetName>.pbip                        ← Project entry point (open this)
  <DatasetName>.SemanticModel/
    definition.pbism                        ← Model item metadata
    definition/                             ← TMDL model (folder of .tmdl files)
      database.tmdl
      model.tmdl
      tables/*.tmdl
      relationships.tmdl
      ...
  <DatasetName>.Report/
    definition.pbir                         ← Report item metadata & model binding
    report.json                             ← Blank report with one empty page
```

**Example** (DatasetName = "Sales"):
```
C:\PBIP\Sales\
  Sales.pbip
  Sales.SemanticModel/
    definition.pbism
    definition/
      database.tmdl
      model.tmdl
      tables/Sales.tmdl
      relationships.tmdl
  Sales.Report/
    definition.pbir
    report.json
```

Double-click `Sales.pbip` → Power BI Desktop opens with the model and a blank report.

---

## File Formats

### `<name>.pbip`

Project manifest. Declares the report artifact and project version.

```json
{
  "version": "1.0",
  "artifacts": [
    {
      "report": {
        "path": "Sales.Report"
      }
    }
  ],
  "settings": {
    "enableAutoRecovery": true
  }
}
```

**Fields**:
- `version` — PBIP manifest schema version (always `"1.0"`)
- `artifacts` — array of project items. Must contain exactly one `report`; `dataset` is forbidden.
- `artifacts[0].report.path` — relative folder name of the report (e.g., `Sales.Report`)
- `settings.enableAutoRecovery` — allow Desktop to auto-recover unsaved changes

---

### `SemanticModel/definition.pbism`

Item properties for the semantic model folder.

```json
{
  "version": "4.1",
  "settings": {}
}
```

**Fields**:
- `version` — semantic model item schema version
- `settings` — item-specific overrides (empty = no overrides)

---

### `SemanticModel/definition/`

The Tabular model, written as TMDL (Tabular Model Definition Language) — a folder of `.tmdl` text files. There is **no** `model.bim` in the output.

Typical contents:
```
definition/
  database.tmdl          ← database-level properties
  model.tmdl             ← model-level properties, annotations, culture
  relationships.tmdl     ← all relationships
  tables/
    Sales.tmdl           ← one file per table (columns, measures, partitions)
    Date.tmdl
    ...
```

**How it is produced**: The tool loads your input `.bim` through the Tabular Object Model (TOM) library — which rebuilds a consistent metadata object graph — and serializes that graph to this folder with `TmdlSerializer.SerializeDatabaseToFolder`. The model is **normalized**, not copied verbatim. This is what makes the project safe to edit and refresh in Power BI Desktop.

See [TOM normalization](concepts.md#tom-normalization) for why this matters.

---

### `Report/definition.pbir`

Item properties for the report. Binds the report to the semantic model.

```json
{
  "version": "4.0",
  "datasetReference": {
    "byPath": {
      "path": "../Sales.SemanticModel"
    }
  }
}
```

**Fields**:
- `version` — report item schema version
- `datasetReference.byPath.path` — **relative** path to the semantic model folder
  - Uses `/` (forward slashes), not `\`
  - Must be relative (not absolute)
  - `..` means "go up one folder"

---

### `Report/report.json`

The report canvas and page definitions (legacy single-file format, still fully supported).

```json
{
  "config": "{\"version\":\"5.59\",\"themeCollection\":{\"baseTheme\":{\"name\":\"CY24SU10\",\"version\":\"5.61\",\"type\":2}},\"activeSectionIndex\":0,\"defaultDrillFilterOtherVisuals\":true}",
  "layoutOptimization": 0,
  "resourcePackages": [ ... ],
  "sections": [
    {
      "name": "a1b2c3d4e5f6g7h8i9j0",
      "displayName": "Page 1",
      "displayOption": 1,
      "ordinal": 0,
      "height": 720,
      "width": 1280,
      "config": "{}",
      "filters": "[]",
      "visualContainers": []
    }
  ]
}
```

**Key fields**:
- `config` — stringified JSON with report-wide settings
  - `version` — report layout schema version
  - `themeCollection.baseTheme` — active theme (required; `CY24SU10` is built-in)
  - `activeSectionIndex` — which page is active (0 = first page)
- `layoutOptimization` — 0 = standard (non-mobile) layout
- `resourcePackages` — external resources (e.g., the built-in base theme)
- `sections` — report pages. The tool includes one blank page:
  - `name` — unique page ID (20 hex chars, randomly generated)
  - `displayName` — page tab caption (`"Page 1"`)
  - `displayOption` — 1 = fit-to-page
  - `height`/`width` — canvas size in pixels (1280×720 = standard 16:9)
  - `visualContainers` — the visuals on the page (empty = blank page)

---

## Encoding & BOM

**All files are UTF-8 without BOM.**

Power BI Desktop rejects files that start with the UTF-8 byte-order mark (`0xEF 0xBB 0xBF`). The tool:
1. Writes all files as UTF-8 without BOM
2. Scans the completed project and strips any stray BOMs

See [Validation](validation.md) for how this is verified.

---

## Token Substitution

The PBIP *wrapper* files come from template files in `pbip-templates/` that use placeholder tokens, replaced at conversion time:

| Token | Replaced with |
|-------|---|
| `{{REPORT_FOLDER}}` | `<DatasetName>.Report` |
| `{{SEMANTIC_MODEL_FOLDER}}` | `<DatasetName>.SemanticModel` |
| `{{PAGE_NAME}}` | 20-character random hex ID |

The `SemanticModel/definition/` folder is not a template — it is generated per-model by TOM.

See [Templates](templates.md) for details.

---

## Single Source of Truth

The wrapper structure is not invented in code. The four wrapper files live in `pbip-templates/`, taken verbatim from a real Power BI Desktop export. Every field is documented in `pbip-templates/REFERENCE.md` with rationale and source.

The tool reads these templates and fills in the tokens.

---

## Next Steps

- [Templates](templates.md) — how token substitution works
- [Validation](validation.md) — what the tool checks
