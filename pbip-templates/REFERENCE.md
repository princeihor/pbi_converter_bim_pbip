# PBIP templates — single source of truth

These four files are the **only** place the PBIP project structure is defined.
The PowerShell tool (`powershell/bim-to-pbip.ps1`), the C# tool
(`BimToPbipCli`) and the internal test (`tests/internal_test.py`) all consume
**these exact files**. Nothing about the PBIP structure is invented anywhere
else — if a field has to change, it changes here.

JSON files cannot carry comments, so every field is documented below, with the
reason it exists and the authoritative source it was taken from.

## Source of truth

The structure is taken verbatim from a real Power BI Desktop "Save as PBIP"
export — the `02_pbip_tmsl` ("PBIP with TMSL") layout — cross-checked against
Microsoft Learn, *Power BI Desktop projects (PBIP)*:
<https://learn.microsoft.com/power-bi/developer/projects/projects-overview>

The TMSL layout stores the model directly as `model.bim` (a `.bim` *is* TMSL
JSON), so no model conversion / `pbi-tools` is required.

## Produced layout

```
<root>/
  <name>.pbip                       <- project.pbip
  <name>.SemanticModel/
    definition.pbism                <- SemanticModel/definition.pbism
    model.bim                       <- the input .bim, copied verbatim
  <name>.Report/
    definition.pbir                 <- Report/definition.pbir
    report.json                     <- Report/report.json
```

## Tokens

Replaced by the tools at conversion time (plain text substitution):

| Token                        | Replaced with                                  |
|------------------------------|------------------------------------------------|
| `{{REPORT_FOLDER}}`          | `<name>.Report`                                |
| `{{SEMANTIC_MODEL_FOLDER}}`  | `<name>.SemanticModel`                         |
| `{{PAGE_NAME}}`              | a fresh 20-hex-character random page id        |

## `project.pbip`

The project entry point that Power BI Desktop opens.

- `version` `"1.0"` — PBIP manifest schema version (current).
- `artifacts` — array of items in the project. **Each entry must be a `report`.**
  Power BI Desktop's `.pbip` schema *requires* `report` and *rejects* a
  `dataset` entry ("Property 'dataset' has not been defined ... Required
  properties are missing from object: report"). This is why a dataset-only
  PBIP fails to open and the project always includes a `.Report` part.
- `artifacts[0].report.path` — relative folder name of the report item.
- `settings.enableAutoRecovery` `true` — present in the reference export; lets
  Desktop auto-recover unsaved changes.

## `SemanticModel/definition.pbism`

Item-properties file for the semantic model. Marks the folder as a PBIP
semantic model item; Power BI Desktop auto-detects whether the model is stored
as `model.bim` (TMSL) or `definition/` (TMDL) by what is present in the folder.

- `version` `"4.1"` — semantic-model item schema version from the reference
  TMSL export.
- `settings` `{}` — no overrides; required by the schema, empty is valid.

## `Report/definition.pbir`

Item-properties file for the report; binds the report to its semantic model.

- `version` `"4.0"` — report item schema version from the reference export.
- `datasetReference.byPath.path` — **relative** path to the semantic model
  folder. Must use `/` separators and stay relative (absolute paths are not
  supported). `byPath` makes Desktop open the model in full edit mode.

## `Report/report.json`

The report canvas (legacy single-file report format — still fully supported;
Desktop upgrades it to PBIR on save if needed).

- `config` — stringified JSON of report-wide settings:
  - `version` `"5.59"` — report layout schema version (from the reference).
  - `themeCollection.baseTheme` — the active theme. **This is required.**
    Power BI Desktop's ribbon dereferences `config.themeCollection` while
    rendering; if it is missing the report fails to render with
    *"Cannot read properties of undefined (reading 'customTheme')"*.
    `CY24SU10` is a built-in base theme (Power BI resolves built-in themes
    itself — no theme file has to exist in the project). `version`/`type`
    are taken verbatim from the reference report.
  - `activeSectionIndex` `0` — the first page is active.
  - `defaultDrillFilterOtherVisuals` `true` — standard default.
- `layoutOptimization` `0` — standard (non-mobile-optimized) layout.
- `resourcePackages` — records the built-in base theme. The `SharedResources`
  package (type `2`) with item `BaseThemes/CY24SU10.json` (type `202`) is how
  the legacy format references a built-in base theme; the path is resolved by
  Power BI internally, no file is shipped. This pairs with
  `config.themeCollection.baseTheme` so the theme resolves cleanly.
- `sections` — the report pages. Exactly one blank page so the report is
  valid and openable:
  - `name` — unique page id (`{{PAGE_NAME}}` token).
  - `displayName` `"Page 1"` — page tab caption.
  - `displayOption` `1` — fit-to-page.
  - `ordinal` `0` — first page.
  - `height` `720`, `width` `1280` — standard 16:9 canvas.
  - `config` `"{}"` — stringified per-page settings; none needed.
  - `filters` `"[]"` — stringified page filters; none.
  - `visualContainers` `[]` — no visuals (blank page); a valid empty report.
