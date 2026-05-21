---
title: Testing
last-updated: 2026-05-21
relates-to: [validation, architecture]
tags: [reference, implementation]
---

# Testing

How the converter is tested to ensure it produces valid PBIP projects.

## Internal Test (`tests/internal_test.py`)

The primary test suite. Runs in CI before building the Windows `.exe`.

### What It Does

1. **Builds a complex test model** with:
   - 7 tables with various data types
   - 5 measures
   - Relationships between tables
   - Row-level security (RLS) roles
   - Perspectives
   - Multiple cultures (translations)

2. **Converts the test model** using the same logic as the shipped tools

3. **Validates the output** with 50 regression checks covering:
   - ✅ Folder structure (all required folders exist)
   - ✅ No `dataset` artifact (Power BI Desktop rejects this)
   - ✅ `report` artifact declared and correct
   - ✅ Relative paths in `definition.pbir`
   - ✅ All JSON files parse correctly
   - ✅ No UTF-8 BOM in any file
   - ✅ Theme configuration present in `report.json`
   - ✅ Token substitution worked (page ID is 20 hex chars, folders named correctly)

### Running It Locally

```bash
python3 tests/internal_test.py
```

**Output** (on success):
```
[ OK ] 50/50 checks passed
[ OK ] PBIP project is valid and ready for Power BI Desktop
```

**Exit code**: `0` if all checks pass; non-zero if any fail.

### Running in CI

The GitHub Actions workflow (`.github/workflows/build.yml`) runs the internal test on every push to the main branch. If the test fails, the Windows `.exe` is not built.

---

## Test Coverage

The 50 checks are grouped by concern:

### Structure (5 checks)
- Project root folder exists
- Semantic model folder (`<name>.SemanticModel`) exists
- Report folder (`<name>.Report`) exists
- `.bim` file copied to semantic model folder
- Report folder contains `report.json`

### JSON Validity (10 checks)
- `project.pbip` is valid JSON
- `definition.pbism` is valid JSON
- `definition.pbir` is valid JSON
- `report.json` is valid JSON
- Each JSON file can be parsed without errors

### Schema Validation (10 checks)
- `project.pbip` has `artifacts` array
- `artifacts[0]` is a `report` (not `dataset`)
- Report path is non-empty
- `definition.pbir` has `datasetReference.byPath.path`
- Path points to semantic model folder
- Path is relative (no absolute path)
- Path uses forward slashes
- Theme is configured in `report.json`
- Page array has at least one page
- Page ID is 20 hex characters

### Encoding (5 checks)
- No file in the project has a UTF-8 BOM
- All text files are encoded as UTF-8

### Token Substitution (5 checks)
- `{{REPORT_FOLDER}}` replaced with actual folder name
- `{{SEMANTIC_MODEL_FOLDER}}` replaced with actual folder name
- `{{PAGE_NAME}}` replaced with valid 20-hex ID
- No unsubstituted tokens remain
- Folder names match the dataset name

### Model Integrity (5 checks)
- `.bim` file contents match the input (verbatim copy)
- Model is still valid TMSL
- Model still contains the expected tables, measures, relationships

### Regression Checks (5 checks)
- Dataset artifact is not generated (historical failure case)
- UTF-8 BOM is not present (historical failure case)
- Theme crash is prevented (historical failure case)
- Relative paths are correct
- Project opens without schema errors

---

## Why These Checks?

Each check validates something Power BI Desktop requires or a historical bug the project has hit:

- **Historical bug #1**: Tool generated `dataset` artifact instead of `report` — Power BI Desktop rejected it
- **Historical bug #2**: Files written with UTF-8 BOM — Power BI Desktop rejected it
- **Historical bug #3**: Theme configuration missing — Power BI Desktop crashed on open
- **Modern checks**: Token substitution, relative paths, JSON validity, folder structure

---

## Adding New Checks

If you find a bug, add a regression check to prevent it happening again:

1. Add a check to `tests/internal_test.py` in the `validate()` function
2. Run the test locally: `python3 tests/internal_test.py`
3. Update `log.md` to document the new check
4. Commit with message like: "Add regression check for <issue>"

Example check:
```python
# In the validate() function
def validate(pbip_folder):
    # ... existing checks ...
    
    # New check: verify config.version in report.json
    config_version = parsed_config.get("version")
    assert config_version == "5.59", f"report.json config version should be 5.59, got {config_version}"
    checks_passed += 1
```

---

## Manual Testing

For manual verification in Power BI Desktop:

1. Convert a real `.bim` file
2. Open the `.pbip` → Power BI Desktop should open with the model loaded
3. Verify:
   - Model tables are visible in the Data pane
   - Relationships are intact
   - Measures are present
   - You can create a new page and add a visual

---

## Next Steps

- [Validation](validation.md) — what the tool validates
- [Architecture](architecture.md) — design decisions
