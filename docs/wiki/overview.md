---
title: Overview
last-updated: 2026-05-22
tags: [guide, introduction]
---

# BIM-to-PBIP Converter — Overview

A command-line tool (with an optional browser UI) that converts Tabular `model.bim` files into **Power BI Desktop projects (PBIP)** that open directly in the desktop app.

## What It Does

You give it a `.bim` file (a TMSL model) and a folder path. It produces a complete PBIP project ready to open in Power BI Desktop:

```
<output>/
  MyModel.pbip                          ← open this file
  MyModel.SemanticModel/
    definition.pbism                    ← model item metadata
    definition/                         ← the model, in TMDL text files
      database.tmdl
      model.tmdl
      tables/*.tmdl
      relationships.tmdl
      ...
  MyModel.Report/
    definition.pbir                     ← report-to-model binding
    report.json                         ← blank report, one empty page
```

Double-click `MyModel.pbip` → Power BI Desktop opens with your model loaded and a blank report ready to edit.

## Why You'd Use It

**Scenario**: You have a Tabular model (`.bim` file) and you want to use Power BI Desktop's project editor (PBIP format) instead of the file-based `.pbix` format.

**The problem**: Power BI Desktop has no built-in "import model" option. You can't just open a `.bim` file.

**The solution**: This tool normalizes the model and assembles a valid PBIP project structure, then validates it before reporting success. The result opens in Desktop immediately.

## How the Model Is Handled

The tool does **not** copy the input `.bim` byte-for-byte. It loads the `.bim` through the **Tabular Object Model (TOM)** library, which rebuilds a consistent metadata object graph, and re-serializes it as **TMDL** — a `definition/` folder of `.tmdl` text files. This is the modern PBIP layout.

**Why this matters**: A verbatim `model.bim` copy caused Power BI Desktop to fail on edit/refresh with the internal error *"Model object-map is not consistent with the metadata-object graph"*. Normalizing through TOM rebuilds a clean object graph and eliminates that failure. See [Concepts](concepts.md#tom-normalization) for details.

## No External Tool Dependencies

- No `pbi-tools` install required — the round-trip happens in-process via the bundled TOM library
- No internet access
- No admin rights

The TOM library is bundled into the self-contained `.exe` (NuGet package `Microsoft.AnalysisServices.NetCore.retail.amd64`, Windows-x64).

## One Implementation

The tool ships **only** as the self-contained C# (.NET 8) `.exe`. There is no longer a PowerShell version. The `.exe` works in CLI mode or opens a browser-based UI when run with no `--bim` argument.

## Validation

The tool doesn't generate-and-hope. It validates:

1. **Input**: Can TOM load the `.bim` as a valid Tabular model?
2. **Assembly**: Did we build the TMDL `definition/` folder and all wrapper files?
3. **Output**: Does the project meet Power BI Desktop's schema requirements?

If validation fails, you get an error message and exit code, not a broken project that fails silently in Desktop.

## Next Steps

- [Quick Start](quick-start.md) — get running
- [Installation](installation.md) — setup
- [CLI Reference](cli-reference.md) — all parameters
- [Concepts](concepts.md) — understand the pieces (PBIP, TMSL, TMDL, TOM, tokens, etc.)
