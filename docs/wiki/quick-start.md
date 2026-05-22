---
title: Quick Start
last-updated: 2026-05-22
tags: [guide, getting-started]
---

# Quick Start — 5 Minutes

The tool ships as a single self-contained Windows `.exe`: `BimToPbipCli.exe`.

## Get the Tool

**Easiest** — download the pre-built `.exe`:
- Go to the GitHub Actions [build workflow](../../.github/workflows/build.yml)
- Open the latest successful run and download the `BimToPbipCli-win-x64` artifact
- Extract `BimToPbipCli.exe`

**Or build it yourself** (needs the .NET 8 SDK):
```
dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

See [Installation](installation.md) for details.

---

## Browser UI (Easiest)

1. Run `BimToPbipCli.exe` with no arguments (or `--ui`)
2. A browser-based UI opens
3. Select your `model.bim`
4. (Optional) Set the output folder and project name
5. Convert
6. Find `MyModel.pbip` in the output folder and double-click it to open in Power BI Desktop

**Done.** Your model loads and you get a blank report.

---

## Command Line

For scripting or CI:

```
BimToPbipCli.exe --bim C:\Models\MyModel.bim
```

Optional parameters:

```
BimToPbipCli.exe --bim C:\Models\MyModel.bim --out C:\PBIP --dataset MyModel
```

| Option | Default | Notes |
|--------|---------|-------|
| `--bim` | (required) | Path to the input `.bim` file |
| `--out` | Folder next to the `.bim` | Where to create the project |
| `--dataset` | `.bim` file name | Project folder names use this (e.g., `MyModel.SemanticModel`) |
| `--ui` | (off) | Open the browser UI instead of running headless |

See [CLI Reference](cli-reference.md) for everything.

---

## What Happens

The tool:

1. **Loads + normalizes** your `.bim` through the Tabular Object Model (TOM), which rebuilds a consistent metadata object graph
2. **Serializes** the model to a TMDL `definition/` folder (a folder of `.tmdl` text files — no `model.bim`)
3. **Creates** the project folder with subfolders for the semantic model and the report
4. **Writes** the wrapper metadata files (`project.pbip`, `definition.pbism`, `definition.pbir`, `report.json`)
5. **Validates** the complete project against Power BI Desktop's schema
6. **Strips** any UTF-8 byte-order marks from all files
7. Reports success or an error code

See [Exit Codes](cli-reference.md#exit-codes) if something fails.

---

## Next Steps

- [Installation](installation.md) — detailed setup
- [CLI Reference](cli-reference.md) — all parameters and exit codes
- [Troubleshooting](troubleshooting.md) — if something goes wrong
