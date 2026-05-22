---
title: Testing
last-updated: 2026-05-22
relates-to: [validation, architecture]
tags: [reference, implementation]
---

# Testing

How the converter is tested to ensure it produces valid PBIP projects.

## Internal Test (`tests/internal_test.py`)

The primary test suite. Runs in CI before building the Windows `.exe`.

### What It Does

The test exercises the **real C# converter** — it does not re-implement the conversion in Python.

1. **Builds the converter** with `dotnet build` (so it needs the .NET 8 SDK)

2. **Runs the real converter** via `dotnet run` against a deliberately complex test model with:
   - Multiple tables with various data types
   - Measures
   - Relationships between tables
   - Row-level security (RLS) roles
   - Perspectives
   - Multiple cultures (translations)

3. **Validates the produced TMDL project** with regression checks covering:
   - ✅ Folder structure (all required folders exist)
   - ✅ The semantic model `definition/` folder is present, non-empty, and contains `.tmdl` files including `model.tmdl`
   - ✅ No `dataset` artifact (Power BI Desktop rejects this)
   - ✅ `report` artifact declared and correct
   - ✅ Relative, forward-slash paths in `definition.pbir`
   - ✅ All metadata JSON files parse correctly
   - ✅ No UTF-8 BOM in any file
   - ✅ Token substitution worked (page ID is 20 hex chars, folders named correctly)

The test does **not** assert a byte-identical `model.bim` — there is no `model.bim` anymore. The model is normalized through TOM and written as TMDL, so the output is intentionally not byte-identical to the input.

### Running It Locally

```bash
python3 tests/internal_test.py
```

The test needs the **.NET 8 SDK** on PATH, because it builds and runs the real converter.

**Exit code**: `0` if all checks pass; non-zero if any fail.

### Running in CI

The GitHub Actions workflow (`.github/workflows/build.yml`) `test` job:
1. Sets up the .NET 8 SDK
2. Builds the converter
3. Runs `tests/internal_test.py`

If the test fails, the Windows `.exe` is not built. (The old "Check PowerShell scripts parse" step has been removed — there are no PowerShell scripts.)

---

## Test Coverage

The checks are grouped by concern:

### Structure
- Project root folder exists
- Semantic model folder (`<name>.SemanticModel`) exists
- Report folder (`<name>.Report`) exists
- `SemanticModel/definition/` folder exists, is non-empty, contains `.tmdl` files
- `model.tmdl` is present in the `definition/` folder
- Report folder contains `report.json`

### JSON Validity
- `project.pbip`, `definition.pbism`, `definition.pbir`, `report.json` are all valid JSON

### Schema Validation
- `project.pbip` has an `artifacts` array
- `artifacts[0]` is a `report` (not `dataset`)
- `definition.pbir` has `datasetReference.byPath.path`
- Path points to the semantic model folder, is relative, uses forward slashes
- `report.json` page array has at least one page
- Page ID is 20 hex characters

### Encoding
- No file in the project has a UTF-8 BOM

### Token Substitution
- `{{REPORT_FOLDER}}`, `{{SEMANTIC_MODEL_FOLDER}}`, `{{PAGE_NAME}}` are all replaced
- No unsubstituted tokens remain
- Folder names match the dataset name

---

## Why These Checks?

Each check validates something Power BI Desktop requires or a historical bug the project has hit:

- **Historical bug #1**: Tool generated a `dataset` artifact instead of `report` — Desktop rejected it
- **Historical bug #2**: Files written with a UTF-8 BOM — Desktop rejected it
- **Historical bug #3**: Verbatim `model.bim` copy — Desktop failed on edit/refresh with *"Model object-map is not consistent with the metadata-object graph"*. Fixed by normalizing through TOM and writing TMDL.

---

## Manual Testing

For manual verification in Power BI Desktop:

1. Convert a real `.bim` file
2. Open the `.pbip` → Power BI Desktop should open with the model loaded
3. Verify:
   - Model tables are visible in the Data pane
   - Relationships are intact
   - Measures are present
   - You can edit the model and refresh without the object-map error
   - You can create a new page and add a visual

---

## Next Steps

- [Validation](validation.md) — what the tool validates
- [Architecture](architecture.md) — design decisions
