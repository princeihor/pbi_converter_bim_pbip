---
title: Validation & Error Checking
last-updated: 2026-05-21
relates-to: [concepts, troubleshooting]
tags: [reference]
---

# Validation & Error Checking

What the tool checks at each phase to ensure a working project.

## Three-Phase Pipeline

The tool validates the input, assembly, and output to guarantee success or fail fast with a clear error.

### Phase 1: Input Validation

Before doing any work, verify the input is correct.

**Checks**:
- ✅ Does `<bim-path>` exist?
- ✅ Is it a file (not a directory)?
- ✅ Can we read it?
- ✅ Is it valid JSON?
- ✅ Does it have a top-level object (TMSL requirement)?

**Exit code on failure**: `2` (file not found) or `3` (invalid JSON/TMSL)

**Example**:
```
[ ERROR ] File not found: C:\Models\NonExistent.bim
[ ERROR ] Exit code: 2
```

---

### Phase 2: Assembly

Create the folder structure and write files (using UTF-8 without BOM).

**Checks during assembly**:
- ✅ Can we create the output folder?
- ✅ Can we copy the `.bim` file?
- ✅ Can we write template files?
- ✅ All template tokens replaced correctly?

**Exit code on failure**: `5` (file system error)

**Typical failures**: Permission denied, disk full, invalid path characters.

---

### Phase 3: Output Validation

After assembly completes, verify the project is valid before reporting success.

**Checks**:
- ✅ All required files exist?
- ✅ All files are valid JSON?
- ✅ `<name>.pbip` has the correct schema (report artifact, no dataset)?
- ✅ `definition.pbir` references the semantic model by relative path?
- ✅ `report.json` has the required theme configuration?
- ✅ No files have a UTF-8 BOM?
- ✅ Relative paths use forward slashes (not backslashes)?

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
1. Use `UTF8Encoding(false)` / `[System.Text.UTF8Encoding]($false)` when writing files
2. After assembly, scan the entire project folder for any files with a leading BOM
3. Strip it if found (a model.bim might already have one)

**Validation check**:
- ✅ No file in the project starts with `0xEF 0xBB 0xBF`

---

## JSON Schema Validation

The tool verifies the structure matches Power BI Desktop's expectations.

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

The internal test (`tests/internal_test.py`) generates a complex model and validates it against the rules:

```python
# Pseudocode
model = create_test_model_with_7_tables_5_measures_relationships_etc()
pbip_folder = convert(model)
validate(pbip_folder)  # 50 checks covering:
  # - structure (folders, files exist)
  # - no dataset artifact
  # - report artifact present
  # - correct relative paths
  # - JSON validity
  # - BOM absence
  # - theme presence
```

If all 50 checks pass, the tool is working correctly.

---

## Common Validation Failures

| Error | Cause | Fix |
|-------|-------|-----|
| `BIM file not found` | Path typo or file deleted | Check the file path |
| `BIM is not valid JSON` | Corrupted `.bim` file | Verify the file is valid TMSL |
| `report.json missing theme` | Tool bug (shouldn't happen) | Report as a bug |
| `Output folder not writable` | Permission denied | Check folder permissions |
| `Relative path uses backslashes` | Tool bug (shouldn't happen) | Report as a bug |

---

## Exit Codes

See [CLI Reference](cli-reference.md#exit-codes) for the full list.

---

## Next Steps

- [Troubleshooting](../troubleshooting.md) — what to do if validation fails
- [Concepts](concepts.md#three-phase-pipeline) — architecture overview
