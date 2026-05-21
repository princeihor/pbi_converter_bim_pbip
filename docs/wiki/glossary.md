---
title: Glossary
last-updated: 2026-05-21
tags: [reference]
---

# Glossary

Terms used throughout the documentation.

## .bim

A file containing a Tabular model in TMSL (JSON) format. Produced by SQL Server Analysis Services, Power BI, or similar tools. Human-readable (you can open it in a text editor).

**See**: [TMSL](#tmsl)

## BOM

Byte-Order Mark. An invisible leading byte sequence (UTF-8: `0xEF 0xBB 0xBF`) that marks a file's encoding. Power BI Desktop rejects PBIP files with a BOM.

**See**: [UTF-8 BOM](concepts.md#utf-8-bom-byte-order-mark), [Validation](validation.md)

## CLI

Command-Line Interface. The PowerShell script and C# tool both support CLI mode (no GUI).

## PBIP

Power BI Project. A folder structure that Power BI Desktop can open and edit. Contains a semantic model and reports.

**See**: [PBIP](concepts.md#pbip-power-bi-project), [Project Structure](structure.md)

## PBIX

Power BI Interactive Report (old format). A single compressed file (`.zip`). Power BI Desktop can open both `.pbix` and `.pbip` formats.

## Relative Path

A file path relative to a reference point (not absolute). Example: `../MyModel.SemanticModel` (go up one folder, then into `MyModel.SemanticModel`). Power BI Desktop requires relative paths in report-to-model references.

**Opposite**: Absolute path (e.g., `C:\Users\Me\MyModel.SemanticModel`)

## TMDL

Tabular Model Definition Language. An alternative to TMSL: instead of a single `.bim` JSON file, a model is stored as a folder of `.tmdl` text files. Supported by Power BI Desktop but not yet by this tool.

**See**: [TMDL](concepts.md#tmdl-tabular-model-definition-language)

## TMSL

Tabular Model Scripting Language. A JSON format for describing Tabular models. A `.bim` file is TMSL.

**See**: [TMSL](concepts.md#tmsl-tabular-model-scripting-language), [TMDL](#tmdl)

## Token

A placeholder string (e.g., `{{REPORT_FOLDER}}`) in template files that the tool replaces with actual values at conversion time.

**See**: [Template Tokens](concepts.md#template-tokens), [Templates](templates.md)

## UTF-8

A character encoding standard. UTF-8 without BOM is required by Power BI Desktop.

**See**: [UTF-8 BOM](concepts.md#utf-8-bom-byte-order-mark), [BOM](#bom)

## Validation

The process of checking that the input `.bim` is valid and the output PBIP project meets Power BI Desktop's schema requirements. The tool validates in three phases.

**See**: [Three-Phase Pipeline](concepts.md#three-phase-pipeline), [Validation](validation.md)

---

**Don't see a term?** Open an issue or check [Concepts](concepts.md) for longer explanations.
