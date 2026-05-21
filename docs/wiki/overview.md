---
title: Overview
last-updated: 2026-05-21
tags: [guide, introduction]
---

# BIM-to-PBIP Converter — Overview

A Windows tool that converts Tabular `model.bim` files into **Power BI Desktop projects (PBIP)** that open directly in the desktop app.

## What It Does

You give it a `.bim` file (a TMSL model) and a folder path. It produces a complete PBIP project ready to open in Power BI Desktop:

```
<output>/
  MyModel.pbip                 ← open this file
  MyModel.SemanticModel/
    definition.pbism           ← model metadata
    model.bim                  ← your input model, verbatim
  MyModel.Report/
    definition.pbir            ← report-to-model binding
    report.json                ← blank report, one empty page
```

Double-click `MyModel.pbip` → Power BI Desktop opens with your model loaded and a blank report ready to edit.

## Why You'd Use It

**Scenario**: You have a Tabular model (`.bim` file) and you want to use Power BI Desktop's graphical project editor (PBIP format) instead of the file-based `.pbix` format.

**The problem**: Power BI Desktop has no built-in "import model" option. You can't just open a `.bim` file.

**The solution**: This tool assembles a valid PBIP project structure and validates it before reporting success. The result opens in Desktop immediately.

## No Dependencies

- ✅ No `pbi-tools` install required
- ✅ No `.NET 8 SDK` needed (unless building the C# tool yourself)
- ✅ No internet access
- ✅ PowerShell version needs Windows PowerShell 5.1 or Core (ships with Windows)
- ✅ No admin rights

The tool takes your `.bim` (which is already TMSL JSON) and wraps it in PBIP project metadata files. No model conversion, no external tools.

## Two Implementations

| | PowerShell | C# (.NET 8) |
|---|---|---|
| **Installation** | None — ships with Windows | .NET 8 SDK (or download pre-built `.exe`) |
| **Interface** | GUI (point-and-click) or command-line | CLI or browser-based UI |
| **Best for** | No admin rights, quick one-off conversion | Automation, integration into pipelines |

Both produce **identical project structure**. The structure itself is defined once in `pbip-templates/`, which is the single source of truth.

## Validation

The tool doesn't generate-and-hope. It validates:

1. **Input**: Is the `.bim` valid TMSL JSON?
2. **Assembly**: Did we build all the required files?
3. **Output**: Does the project meet Power BI Desktop's schema requirements?

If validation fails, you get an error message, not a broken project that fails silently in Desktop.

## Next Steps

- [Quick Start](quick-start.md) — get running
- [Installation](installation.md) — setup for your platform
- [CLI Reference](cli-reference.md) — all parameters
- [Concepts](concepts.md) — understand the pieces (PBIP, TMSL, tokens, etc.)
