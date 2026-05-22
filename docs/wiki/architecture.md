---
title: Architecture & Design
last-updated: 2026-05-22
relates-to: [concepts, testing, validation]
tags: [reference, implementation]
---

# Architecture & Design Decisions

Why the tool is built the way it is.

## One Implementation

The tool ships as a single self-contained C# (.NET 8) `.exe`. It runs in CLI mode, or opens a browser-based UI when started with no `--bim` argument. There is no PowerShell version — the earlier PowerShell scripts have been retired and removed.

The `.exe` bundles everything it needs:
- The .NET 8 runtime
- The PBIP wrapper templates (embedded resources)
- The Tabular Object Model (TOM) library — NuGet package `Microsoft.AnalysisServices.NetCore.retail.amd64`, Windows-x64

No external installs, no internet access, no admin rights.

---

## The Model Is Normalized Through TOM

The single most important design decision: the tool does **not** copy the input `.bim` byte-for-byte.

**The problem with verbatim copy**: The tool used to write the input `.bim` straight into the project as `model.bim`. Power BI Desktop would then fail on edit/refresh with the internal error:

> *Model object-map is not consistent with the metadata-object graph*

A raw `.bim` can carry an object map that does not line up with what Desktop's metadata engine expects.

**The fix — round-trip through TOM**:
1. Deserialize the `.bim` with `Microsoft.AnalysisServices.Tabular.JsonSerializer.DeserializeDatabase`. TOM rebuilds the model as a **consistent metadata object graph**.
2. Serialize that object graph to a TMDL `definition/` folder with `TmdlSerializer.SerializeDatabaseToFolder`.

The re-serialized model has a clean, internally consistent object graph, so Desktop no longer hits the object-map error. The round-trip happens **in-process** — no `pbi-tools` or other external tool is involved.

This replaced an earlier design that deliberately avoided round-tripping the model. That avoidance was the root cause of the Desktop failure; round-tripping in-process via TOM is now the core of the tool.

---

## The Output Is TMDL

The semantic model is no longer a single `model.bim` file. It is a `<name>.SemanticModel/definition/` folder of TMDL text files (`database.tmdl`, `model.tmdl`, `tables/*.tmdl`, `relationships.tmdl`, etc.).

This is the modern PBIP layout. Per Microsoft Learn, in current PBIP projects *"the existing TMSL file (`model.bim`) is replaced with a `\definition` folder."* TMDL is written by TOM, so the layout always matches what Power BI Desktop produces itself.

---

## Single Source of Truth (Wrapper Files)

The PBIP *wrapper* structure is **not** invented in code. It lives in `pbip-templates/` — four files (`project.pbip`, `SemanticModel/definition.pbism`, `Report/definition.pbir`, `Report/report.json`) taken verbatim from a real Power BI Desktop export, documented field-by-field in `pbip-templates/REFERENCE.md`.

**Why this matters**:
- No guessing at the wrapper structure
- Structure comes directly from Microsoft
- Easy to update (change it in one place)
- The internal test validates against the same templates

The semantic model `definition/` folder is not a template — it is generated per-model by TOM.

**Trade-off**: Templates are embedded in the `.exe` at build time, so changing the wrapper structure requires a rebuild.

---

## Three-Phase Pipeline

```
Load + Normalize → Assembly → Output Validation
```

**Why three phases**:
1. Fail fast if the input is bad (before wasting time on assembly)
2. Separate concerns (load/normalize → build → verify)
3. Clearer error messages (you know exactly which phase failed)
4. Easier to debug and test each phase independently

**No optimistic generation**: The tool never reports success if any phase failed. The project either fully validates or the tool reports an error and exit code.

---

## UTF-8 BOM: A Lesson

Power BI Desktop rejects PBIP files with a UTF-8 byte-order mark (BOM).

**Solution**:
- Write every file with `UTF8Encoding(false)` (UTF-8, no BOM)
- After assembly, sweep the completed project and strip any stray BOM

**Learning**: The tool validates BOM absence as a mandatory check. (An earlier standalone BOM-repair script existed; it is no longer needed — the in-tree sweep is automatic.)

---

## Schema & Validation

The tool enforces Power BI Desktop's PBIP schema:

**Mandatory pieces**:
- `project.pbip`: `version`, `artifacts` (with a `report` entry, no `dataset`)
- `definition.pbism`: `version`, `settings`
- `definition.pbir`: `version`, `datasetReference.byPath.path`
- `SemanticModel/definition/`: a non-empty TMDL folder including `model.tmdl`
- `report.json`: theme configuration, at least one page

**Relative paths required**: The report references the model by relative, forward-slash path (e.g., `../MyModel.SemanticModel`), so the project folder can be moved without breaking the reference.

**UTF-8 without BOM**: All files must be UTF-8 without a byte-order mark.

---

## Testability

The internal test (`tests/internal_test.py`) builds and runs the real C# converter (`dotnet run`, so it needs the .NET 8 SDK) against a deliberately complex model with multiple tables, measures, relationships, RLS, perspectives, and cultures. It validates the produced TMDL project against the same rules the shipped tool uses. See [Testing](testing.md).

---

## Error Handling Strategy

The tool uses **exit codes** for user-facing errors:

- `0`: Success
- `1`: Invalid arguments
- `2`: `.bim` file not found
- `3`: `.bim` is not a valid Tabular model (TOM could not load it, OR could not serialize it to TMDL)
- `4`: Assembled project failed structural validation
- `5`: Could not write files
- `99`: Unexpected error (bug)

**Why**: Exit codes are portable and scriptable (check the code in CI or a batch file).

---

## Assumptions & Constraints

1. **Input `.bim` is a valid Tabular model**: The tool relies on TOM to load it. If TOM cannot deserialize the `.bim`, the tool exits with code `3`.

2. **Output is a blank report**: The tool generates a minimal blank report. It doesn't import existing visualizations or report configuration (out of scope). Power BI's `.pbip` schema requires a `report` artifact — a `dataset`-only project is rejected — so the project always contains both a semantic model and a blank report.

3. **Single report per PBIP**: The tool creates one report artifact. If you need multiple reports, add them in Power BI Desktop after opening the project.

4. **Windows-x64**: The bundled TOM package is the Windows-x64 retail build, and the `.exe` is published as a self-contained Windows-x64 executable.

---

## Performance Considerations

- **TOM round-trip**: Phase 1 deserializes the model and Phase 2 serializes it to TMDL. For typical models this is fast (sub-second); very large models may take a little longer.
- **File I/O**: The tool writes the TMDL folder plus a handful of wrapper files. For typical models this is negligible.
- **BOM scanning**: Phase 3 scans every file for a BOM — negligible.

**Overall**: The tool is mostly I/O-bound. No optimization needed for typical use.

---

## Next Steps

- [Concepts](concepts.md) — TOM normalization, three-phase pipeline
- [Testing](testing.md) — how validation works
- [Validation](validation.md) — the exact checks performed
