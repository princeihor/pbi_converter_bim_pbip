---
title: Core Concepts
last-updated: 2026-05-21
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
  definition.pbism          ← metadata
  model.bim                 ← the Tabular model (TMSL JSON)
MyProject.Report/
  definition.pbir           ← metadata
  report.json               ← report layout
```

**Why PBIP matters**: 
- Desktop can open and edit the project directly (no compile step)
- The model is stored as `model.bim` (human-readable TMSL)
- You can version-control the project (git-friendly)

See [Project Structure](structure.md) for the exact file format.

---

## TMSL (Tabular Model Scripting Language)

The language/format of Tabular models.

**Key fact**: A `.bim` file **is** TMSL. It's a JSON document describing the entire model — tables, measures, relationships, roles, perspectives, cultures, and more.

```json
{
  "name": "MyModel",
  "defaultLanguage": "en-US",
  "tables": [ { "name": "Sales", "columns": [...], ...} ]
}
```

**For this tool**: The `.bim` is immutable. We take it as-is and wrap it in PBIP project files. No conversion, no transformation.

---

## TMDL (Tabular Model Definition Language)

An **alternative** to TMSL. Instead of a single `.bim` JSON file, a model is stored as a folder of `.tmdl` text files (one per object — tables, measures, etc.).

**For this tool**: Not relevant (yet). We work with TMSL only. TMDL is for future extensibility if needed.

---

## UTF-8 BOM (Byte-Order Mark)

A quirk of Windows text encoding.

**The problem**: Windows PowerShell 5.1's `Set-Content -Encoding UTF8` writes UTF-8 *with* a BOM (invisible leading bytes `EF BB BF`). Power BI Desktop rejects files with a BOM.

**The solution**: 
1. Use `[System.Text.UTF8Encoding]($false)` with `[System.IO.File]::WriteAllText()` to write without BOM
2. After writing all files, scan the entire project and strip any stray BOMs

See [Validation](validation.md) for how we verify this.

---

## Template Tokens

The PBIP structure uses placeholders that we fill in at conversion time.

**Tokens used**:

| Token | Replaced with | Example |
|-------|---|---|
| `{{REPORT_FOLDER}}` | Report item folder name | `MyModel.Report` |
| `{{SEMANTIC_MODEL_FOLDER}}` | Semantic model folder name | `MyModel.SemanticModel` |
| `{{PAGE_NAME}}` | Report page ID (20 hex chars) | `a1b2c3d4e5f6g7h8i9j0` |

The tokens live in `pbip-templates/` — we load those templates, replace the tokens, and write the final files.

---

## Three-Phase Pipeline

The tool runs validation in three phases to ensure a working project.

**Phase 1: Validate Input**
- Does the `.bim` file exist?
- Is it valid TMSL JSON (can parse it)?
- Resolve the dataset name and output folder

**Phase 2: Assemble Project**
- Create folder structure (`<name>.SemanticModel/`, `<name>.Report/`)
- Copy the `.bim` file
- Write metadata files (use templates, substitute tokens)
- Write UTF-8 without BOM

**Phase 3: Validate Output**
- Does the structure match expectations?
- Is the report artifact declared (not dataset)?
- Are all JSON files valid?
- Are relative paths correct?
- Do any files have a BOM?
- Is the project theme configured?

If any phase fails, we stop and report the error. If all three pass, the project is guaranteed to open in Desktop.

See [Validation](validation.md) for the exact checks.

---

## Single Source of Truth

The PBIP structure isn't invented in code. It lives in `pbip-templates/` — four files taken verbatim from a real Power BI Desktop export.

**Why**:
- The structure comes directly from Microsoft (no guessing)
- Every field is documented in `pbip-templates/REFERENCE.md`
- Both PowerShell and C# tools read the same templates
- The internal test uses the same templates
- If the structure needs to change, you change it in one place

See [Templates](templates.md) for details.

---

## Next Steps

- [Glossary](glossary.md) — unfamiliar terms
- [Project Structure](structure.md) — the exact folder layout
- [Templates](templates.md) — how token substitution works
