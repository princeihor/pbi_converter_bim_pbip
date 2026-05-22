---
title: Core Concepts
last-updated: 2026-05-22
tags: [reference, concepts]
---

# Core Concepts

Key ideas you need to understand the tool.

## PBIP (Power BI Project)

A Power BI project (`.pbip` file) is actually a **folder structure** that Power BI Desktop can open and edit.

**Not a single file** — PBIP is a project layout:
```
MyProject.pbip              ← entry point (a text file)
MyProject.SemanticModel/
  definition.pbism          ← model item metadata
  definition/               ← the Tabular model, as TMDL text files
MyProject.Report/
  definition.pbir           ← report item metadata
  report.json               ← report layout
```

**Why PBIP matters**:
- Desktop can open and edit the project directly (no compile step)
- The model is stored as a folder of human-readable TMDL files
- You can version-control the project (git-friendly)

See [Project Structure](structure.md) for the exact file format.

---

## TMSL (Tabular Model Scripting Language)

The JSON format of the **input** `.bim` file.

**Key fact**: A `.bim` file **is** TMSL. It's a JSON document describing the entire model — tables, measures, relationships, roles, perspectives, cultures, and more.

```json
{
  "name": "MyModel",
  "defaultLanguage": "en-US",
  "tables": [ { "name": "Sales", "columns": [...], ...} ]
}
```

**For this tool**: TMSL is only the *input* format. The tool reads the `.bim`, but does not store it in the project as-is — it converts it to TMDL (see below).

---

## TMDL (Tabular Model Definition Language)

The **output** format of the semantic model. Instead of a single `.bim` JSON file, the model is stored as a `definition/` folder of `.tmdl` text files — one or more per object area (`database.tmdl`, `model.tmdl`, `tables/*.tmdl`, `relationships.tmdl`, etc.).

This is the modern PBIP layout. Per Microsoft Learn, in current PBIP projects *"the existing TMSL file (`model.bim`) is replaced with a `\definition` folder."*

**For this tool**: TMDL is a first-class concept — it is what the tool produces. The converter writes the TMDL folder using TOM's `TmdlSerializer.SerializeDatabaseToFolder`. There is no `model.bim` in the output.

**See**: [TOM normalization](#tom-normalization), [Project Structure](structure.md)

---

## TOM Normalization

The tool does **not** copy the input `.bim` verbatim. It round-trips the model through the **Tabular Object Model (TOM)** library:

1. **Deserialize** the input `.bim` (TMSL JSON) with
   `Microsoft.AnalysisServices.Tabular.JsonSerializer.DeserializeDatabase`.
   TOM parses the model into an in-memory object graph and **rebuilds it as a consistent metadata object graph**.
2. **Serialize** that object graph to a TMDL `definition/` folder with
   `TmdlSerializer.SerializeDatabaseToFolder`.

**Why this matters**: Previously the tool copied the `.bim` byte-for-byte into the project as `model.bim`. That caused Power BI Desktop to fail on edit/refresh with the internal error:

> *Model object-map is not consistent with the metadata-object graph*

A raw `.bim` can carry an object map that doesn't line up with what Desktop expects. Loading the model through TOM **rebuilds** a clean, internally consistent object graph, so the re-serialized model no longer triggers that error. Normalization is in-process — no `pbi-tools` or other external tool is involved.

If TOM cannot load the `.bim` as a valid Tabular model, or cannot serialize it to TMDL, the tool exits with code `3`.

**See**: [Troubleshooting](troubleshooting.md), [Architecture](architecture.md)

---

## UTF-8 BOM (Byte-Order Mark)

A text-encoding quirk that Power BI Desktop is sensitive to.

**The problem**: Power BI Desktop rejects files that start with the UTF-8 byte-order mark (invisible leading bytes `EF BB BF`).

**The solution**:
1. Every file the tool writes is UTF-8 **without** a BOM (`UTF8Encoding(false)`).
2. After writing all files, the tool sweeps the entire finished project and strips any stray BOM.

See [Validation](validation.md) for how this is verified.

---

## Template Tokens

The PBIP *wrapper* files use placeholders that the tool fills in at conversion time.

**Tokens used**:

| Token | Replaced with | Example |
|-------|---|---|
| `{{REPORT_FOLDER}}` | Report item folder name | `MyModel.Report` |
| `{{SEMANTIC_MODEL_FOLDER}}` | Semantic model folder name | `MyModel.SemanticModel` |
| `{{PAGE_NAME}}` | Report page ID (20 hex chars) | `a1b2c3d4e5f6g7h8i9j0` |

The tokens live in `pbip-templates/` — the tool loads those templates, replaces the tokens, and writes the final wrapper files. The semantic model `definition/` folder itself is written by TOM, not by a template.

---

## Three-Phase Pipeline

The tool runs in three phases to ensure a working project.

**Phase 1: Load + Normalize**
- Does the `.bim` file exist?
- Can TOM deserialize it into a consistent object graph (`JsonSerializer.DeserializeDatabase`)?
- Resolve the dataset name and output folder.

**Phase 2: Assemble Project**
- Serialize the TOM model to a TMDL `definition/` folder (`TmdlSerializer.SerializeDatabaseToFolder`).
- Create the project folder structure.
- Write the wrapper metadata files (load templates, substitute tokens).
- Write everything UTF-8 without BOM.

**Phase 3: Validate Output**
- Does the structure match expectations?
- Is there a non-empty TMDL `definition/` folder (with `model.tmdl`)?
- Is the report artifact declared (not dataset)?
- Are all metadata JSON files valid?
- Are relative paths correct?
- Do any files have a BOM?

If any phase fails, the tool stops and reports the error and exit code. If all three pass, the project is ready to open in Desktop.

See [Validation](validation.md) for the exact checks.

---

## Single Source of Truth

The PBIP *wrapper* structure isn't invented in code. It lives in `pbip-templates/` — four files (`project.pbip`, `SemanticModel/definition.pbism`, `Report/definition.pbir`, `Report/report.json`) taken verbatim from a real Power BI Desktop export.

**Why**:
- The wrapper structure comes directly from Microsoft (no guessing)
- Every field is documented in `pbip-templates/REFERENCE.md`
- The internal test uses the same templates
- If the wrapper needs to change, you change it in one place

The semantic model `definition/` folder is not a template — it is generated per-model by TOM.

See [Templates](templates.md) for details.

---

## Next Steps

- [Glossary](glossary.md) — unfamiliar terms
- [Project Structure](structure.md) — the exact folder layout
- [Templates](templates.md) — how token substitution works
