---
title: Validation & Error Checking
last-updated: 2026-05-22
relates-to: [concepts, troubleshooting]
tags: [reference]
---

# Validation & Error Checking

What the tool checks at each phase to ensure a working project.

## Three-Phase Pipeline

The tool runs load + normalize, assembly, and output validation to guarantee success or fail fast with a clear error.

### Phase 1: Load + Normalize

Before assembling anything, verify the input and load it into a clean object graph.

**Checks**:
- ✅ Does `<bim-path>` exist?
- ✅ Can the Tabular Object Model (TOM) deserialize the `.bim` into a valid Tabular model (`JsonSerializer.DeserializeDatabase`)?

TOM deserialization both validates the input and rebuilds a consistent metadata object graph. If the `.bim` is missing, exit code `2`. If TOM cannot load it as a valid Tabular model, exit code `3`.

**Example**:
```
[ ERROR ] File not found: C:\Models\NonExistent.bim
[ ERROR ] Exit code: 2
```

---

### Phase 2: Assembly

Serialize the normalized model and write all project files (using UTF-8 without BOM).

**Steps**:
- ✅ Serialize the TOM model to a TMDL `definition/` folder (`TmdlSerializer.SerializeDatabaseToFolder`)
- ✅ Create the project folder structure
- ✅ Write the wrapper metadata files (load templates, substitute tokens)
- ✅ All template tokens replaced correctly?

**Exit codes on failure**: `3` if TOM cannot serialize the model to TMDL; `5` if files cannot be written (permission denied, disk full, invalid path characters).

---

### Phase 3: Output Validation

After assembly completes, verify the project is valid before reporting success.

**Checks**:
- ✅ A non-empty TMDL `definition/` folder exists, containing `.tmdl` files (including `model.tmdl`)?
- ✅ All wrapper metadata files exist?
- ✅ All metadata JSON files are valid JSON?
- ✅ `<name>.pbip` has the correct schema (a `report` artifact, no `dataset`)?
- ✅ `definition.pbir` references the semantic model by a relative, forward-slash path?
- ✅ No files have a UTF-8 BOM?

**Exit code on failure**: `4` (validation failed)

**Example**:
```
[ ERROR ] Project validation failed: report.json missing required theme configuration
[ ERROR ] Exit code: 4
```

---

## UTF-8 BOM Checking

Power BI Desktop rejects files with a byte-order mark (UTF-8 BOM = bytes `0xEF 0xBB 0xBF`).

**How we prevent it**:
1. Use `UTF8Encoding(false)` when writing files
2. After assembly, scan the entire project folder for any files with a leading BOM
3. Strip it if found

**Validation check**:
- ✅ No file in the project starts with `0xEF 0xBB 0xBF`

---

## TMDL Model Validation

The tool verifies the semantic model was written correctly:

- ✅ The `SemanticModel/definition/` folder exists and is non-empty
- ✅ It contains `.tmdl` files
- ✅ `model.tmdl` is present

The tool no longer validates a `model.bim` file — the model is now a TMDL `definition/` folder. See [Project Structure](structure.md).

---

## JSON Schema Validation

The tool verifies the wrapper metadata files match Power BI Desktop's expectations.

### project.pbip

Must have:
- ✅ `version` = `"1.0"`
- ✅ `artifacts` = array
- ✅ `artifacts[0].report` exists (not `artifacts[0].dataset`)
- ✅ `artifacts[0].report.path` is a non-empty string
- ✅ All JSON is valid

### definition.pbir

Must have:
- ✅ `version` = `"4.0"`
- ✅ `datasetReference.byPath.path` exists
- ✅ Path is relative (doesn't start with `/`, doesn't contain `:`)
- ✅ Path uses forward slashes (not backslashes)
- ✅ All JSON is valid

### report.json

Must have:
- ✅ `config` field (stringified JSON)
- ✅ Config contains `version`
- ✅ Config contains `themeCollection.baseTheme` (required by Desktop)
- ✅ `sections` array with at least one page
- ✅ Each page has `name`, `displayName`, `ordinal`, `height`, `width`
- ✅ All JSON is valid

---

## Validation in the Internal Test

The internal test (`tests/internal_test.py`) builds and runs the real C# converter via `dotnet run` and validates the produced TMDL project:

```python
# Pseudocode
build_converter()                       # dotnet build (needs .NET 8 SDK)
pbip_folder = run_converter(test_bim)   # dotnet run -- --bim <test.bim>
validate(pbip_folder)  # checks covering:
  # - structure (folders, files exist)
  # - TMDL definition/ folder is non-empty, has model.tmdl
  # - no dataset artifact, report artifact present
  # - correct relative forward-slash paths
  # - JSON validity of metadata files
  # - BOM absence
```

See [Testing](testing.md) for details.

---

## Common Validation Failures

| Error | Cause | Fix |
|-------|-------|-----|
| `BIM file not found` | Path typo or file deleted | Check the file path |
| `Not a valid Tabular model` | `.bim` corrupted, or not a Tabular model TOM can load | Verify the file is a valid TMSL `.bim` |
| `Could not serialize to TMDL` | TOM could not write the model as TMDL | Check the model for unsupported constructs; report as a bug if it persists |
| `definition/ folder missing or empty` | Tool bug (shouldn't happen) | Report as a bug |
| `Output folder not writable` | Permission denied | Check folder permissions |

---

## Exit Codes

See [CLI Reference](cli-reference.md#exit-codes) for the full list.

---

## Next Steps

- [Troubleshooting](troubleshooting.md) — what to do if validation fails
- [Concepts](concepts.md#three-phase-pipeline) — architecture overview
