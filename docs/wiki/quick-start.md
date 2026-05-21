---
title: Quick Start
last-updated: 2026-05-21
tags: [guide, getting-started]
---

# Quick Start — 5 Minutes

## PowerShell (Easiest)

**Requirements**: Windows PowerShell 5.1+ (built in). No install, no admin rights.

1. Download the `powershell/` folder from the repository.
2. **Double-click `powershell/BimToPbip.cmd`**
3. A small GUI window opens
4. **Browse…** → select your `model.bim`
5. (Optional) Set output folder and project name
6. **Convert**
7. On success, the output folder opens. Find `MyModel.pbip` and double-click it to open in Power BI Desktop

**Done.** Your model loads and you get a blank report.

---

## PowerShell (Command-line)

For scripting or CI:

```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"
```

Optional parameters:

```powershell
.\bim-to-pbip.ps1 `
    -BimPath "C:\Models\MyModel.bim" `
    -OutputRoot "C:\PBIP" `
    -DatasetName "MyModel"
```

| Parameter | Default | Notes |
|-----------|---------|-------|
| `-BimPath` | (required) | Path to input `.bim` file |
| `-OutputRoot` | Folder next to `.bim` | Where to create the project |
| `-DatasetName` | `.bim` file name | Project folder names use this (e.g., `MyModel.SemanticModel`) |

---

## C# (If You Have .NET 8)

**Requirements**: .NET 8 SDK installed, or download the pre-built Windows `.exe`.

**Download pre-built .exe**:
- Go to the GitHub Actions [build workflow](../../.github/workflows/build.yml)
- Download the latest `BimToPbipCli-win-x64` artifact
- Run it with no arguments for the browser UI, or use CLI

**Build yourself**:

```powershell
dotnet build BimToPbipCli -c Release
dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Run**:

```
BimToPbipCli.exe                                    # Browser UI
BimToPbipCli.exe --bim C:\Models\MyModel.bim       # CLI
BimToPbipCli.exe --help                             # All options
```

---

## What Happens

The tool:

1. **Validates** your `.bim` is valid TMSL JSON
2. **Creates** the output folder with subfolders for the semantic model and report
3. **Copies** your `.bim` into the semantic model folder
4. **Writes** the metadata files (`definition.pbism`, `definition.pbir`, `report.json`)
5. **Validates** the complete project against Power BI Desktop's schema
6. **Strips** any UTF-8 byte-order marks from all files
7. Reports success or an error code

See [Exit Codes](cli-reference.md#exit-codes) if something fails.

---

## Next Steps

- [Installation](installation.md) — detailed setup
- [CLI Reference](cli-reference.md) — all parameters and exit codes
- [Troubleshooting](../troubleshooting.md) — if something goes wrong
