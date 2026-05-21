---
title: Architecture & Design
last-updated: 2026-05-21
relates-to: [concepts, csharp-impl, powershell-impl]
tags: [reference, implementation]
---

# Architecture & Design Decisions

Why the tool is built the way it is.

## Core Principle: Single Source of Truth

The PBIP folder structure is **not** invented in code. It lives in `pbip-templates/` — four files taken verbatim from a real Power BI Desktop export, documented field-by-field in `pbip-templates/REFERENCE.md`.

**Why this matters**:
- No guessing at the structure
- Structure comes directly from Microsoft
- Easy to update (change it in one place)
- Both PowerShell and C# implementations use the same templates
- Internal test validates against the same templates

**Trade-off**: Templates are embedded in the C# `.exe` (build-time), and read from disk in PowerShell (runtime). This makes C# distribution easier but requires rebuilding for structure changes.

---

## Three-Phase Pipeline

The tool validates in three distinct phases:

```
Input Validation → Assembly → Output Validation
```

**Why three phases**:
1. Fail fast if the input is bad (before wasting time on assembly)
2. Separate concerns (validate → build → verify)
3. Clearer error messages (you know exactly which phase failed)
4. Easier to debug and test each phase independently

**No optimistic generation**: We never report success if any phase failed. The project either fully validates or we report an error and exit code.

---

## UTF-8 BOM: A Lesson

Power BI Desktop rejects PBIP files with a UTF-8 byte-order mark (BOM). This was discovered during user testing.

**Root cause**: Windows PowerShell 5.1's `Set-Content -Encoding UTF8` writes UTF-8 *with* BOM.

**Solution**:
- Use `UTF8Encoding(false)` in C#
- Use `[System.Text.UTF8Encoding]($false)` in PowerShell
- Scan the completed project and strip any stray BOMs (e.g., from a model.bim that already had one)

**Learning**: The tool now validates BOM absence as a mandatory check.

---

## Two Implementations: Why?

| Aspect | PowerShell | C# |
|--------|-----------|-----|
| **Installation** | None (ships with Windows) | Need .NET 8 SDK or download `.exe` |
| **Distribution** | Folder of scripts | Single `.exe` (self-contained) |
| **UI** | GUI window (Windows Forms) | Browser-based UI |
| **Automation** | Command-line or script | Command-line or script |
| **Dependency** | PowerShell 5.1+ | .NET 8 runtime (embedded in `.exe`) |

**Why both exist**:
- PowerShell: No install, no admin rights, runs immediately
- C#: Standalone `.exe`, browser UI, CI integration friendly

**Parity**: Both produce identical project structures (same templates).

---

## No External Dependencies

The tool deliberately avoids `pbi-tools` and other external tools.

**Why**:
- `pbi-tools` is for TMDL-based models (folder of files). We work with TMSL (single `.bim` file).
- No model conversion needed — `.bim` is stored as-is in the PBIP.
- Fewer dependencies = faster, simpler, no version conflicts.
- Works offline (no need to download or compile anything).

**Trade-off**: If you need to convert between TMSL and TMDL, use `pbi-tools` separately (not in this tool).

---

## Schema & Validation

The tool enforces Power BI Desktop's PBIP schema:

**Mandatory fields**:
- `project.pbip`: `version`, `artifacts` (with `report` entry)
- `definition.pbism`: `version`, `settings`
- `definition.pbir`: `version`, `datasetReference.byPath.path`
- `report.json`: theme configuration, at least one page

**Relative paths required**: Report references the model by relative path (e.g., `../MyModel.SemanticModel`), not absolute. This allows moving the project folder without breaking the reference.

**UTF-8 without BOM**: All files must be UTF-8 without byte-order mark.

---

## Testability

The internal test (`tests/internal_test.py`) uses a **deliberate complex model** with 7 tables, 5 measures, relationships, RLS, perspectives, and cultures. This ensures:
- The tool handles real-world complexity
- Regression checks catch historical bugs
- Output validates against the same rules the shipped tools use

---

## Future Extensibility

**TMDL support**: Currently the tool works with TMSL (single `.bim` file). If we want to support TMDL (folder of files), we'd:
1. Update `pbip-templates/` to include `definition/` subfolder
2. Add TMDL file generation (or copy from pbi-tools output)
3. Add TMDL validation to Phase 3
4. Test with TMDL-based models

The single-source-of-truth principle makes this straightforward.

**Theming**: The tool currently includes a blank report with a built-in theme (CY24SU10). Future: allow custom theme specification.

---

## Error Handling Strategy

The tool uses **exit codes** rather than exceptions for user-facing errors:

- `0`: Success
- `1`: Invalid arguments
- `2`: File not found
- `3`: Invalid TMSL JSON
- `4`: Output validation failed
- `5`: File system error
- `99`: Unexpected error (bug)

**Why**: Exit codes are portable across PowerShell, C#, and CI systems. They're also scriptable (check the exit code in a batch file or CI pipeline).

---

## Assumptions & Constraints

1. **Input `.bim` is valid TMSL**: We validate that it's JSON and has a top-level object, but we don't parse the Tabular model itself.

2. **Output is a blank report**: The tool generates a minimal blank report. It doesn't import existing visualizations or report configuration (out of scope).

3. **Single report per PBIP**: The tool creates one report artifact. If you need multiple reports, create them in Power BI Desktop after opening the project.

4. **PBIP with TMSL layout only**: We don't (currently) support TMDL layout. Power BI Desktop supports both; we support the simpler TMSL variant (`.bim` stored directly).

5. **Windows only**: The tool is designed for Windows. PowerShell GUI only works on Windows; C# can run on other OS but assumes Windows-friendly paths.

---

## Performance Considerations

- **File I/O**: The tool reads one `.bim` file and writes 5-6 project files. For typical models (< 1 MB), this is negligible.

- **JSON parsing**: Phase 1 parses the `.bim` as JSON (validation). Phase 3 parses all output files. For large models, this could take a few hundred milliseconds.

- **BOM scanning**: Phase 3 scans every file for BOM. For typical projects, negligible.

**Overall**: The tool is I/O-bound, not CPU-bound. No optimization needed for most use cases.

---

## Next Steps

- [Concepts](concepts.md) — three-phase pipeline overview
- [Testing](testing.md) — how validation works
- [Validation](validation.md) — the exact checks performed
