---
title: CLI Reference
last-updated: 2026-05-21
tags: [reference]
---

# Command-Line Reference

Complete reference for CLI options and exit codes.

## PowerShell

### Signature

```powershell
.\bim-to-pbip.ps1 [-BimPath <path>] [-OutputRoot <path>] [-DatasetName <name>] [-NoGui]
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `-BimPath` | string | Yes* | (none) | Path to input `model.bim` file. Required for headless mode. |
| `-OutputRoot` | string | No | Folder next to `.bim` | PBIP project root folder. |
| `-DatasetName` | string | No | `.bim` file name (without extension) | Dataset / project name. Used in folder names. |
| `-NoGui` | switch | No | false | Never show the GUI. Without `-BimPath` this reports an error. |

**\* "Required for headless mode"**: If you don't provide `-BimPath`, the GUI opens. If you provide `-BimPath`, the tool runs headless and `-NoGui` has no effect.

### Examples

**GUI mode** (interactive):
```powershell
.\bim-to-pbip.ps1
```

**Headless with defaults**:
```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"
```
→ Output folder: `C:\Models\MyModel\`
→ Project name: `MyModel`

**Custom output and name**:
```powershell
.\bim-to-pbip.ps1 `
    -BimPath "C:\Models\MyModel.bim" `
    -OutputRoot "C:\PBIP\Projects\MyProject" `
    -DatasetName "CustomName"
```
→ Output: `C:\PBIP\Projects\MyProject\CustomName.pbip`

---

## C# (BimToPbipCli.exe)

### Signature

```
BimToPbipCli.exe [--bim <path>] [--out <path>] [--dataset <name>] [--help]
```

### Parameters

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--bim <path>` | Yes* | (none) | Path to input `model.bim` file. |
| `--out <path>` | No | Folder next to `.bim` | PBIP project root folder. |
| `--dataset <name>` | No | `.bim` file name (without extension) | Dataset / project name. |
| `--help` | No | (none) | Show help and exit. |

**\* "Required unless GUI"**: If you don't provide `--bim`, the browser-based UI opens. If you provide `--bim`, the tool runs in CLI mode.

### Examples

**Browser UI** (interactive):
```
BimToPbipCli.exe
```

**CLI with defaults**:
```
BimToPbipCli.exe --bim C:\Models\MyModel.bim
```

**Custom output and name**:
```
BimToPbipCli.exe --bim C:\Models\MyModel.bim --out C:\PBIP\MyProject --dataset CustomName
```

---

## Exit Codes

Both implementations use the same exit codes:

| Code | Meaning |
|------|---------|
| `0` | Success. PBIP project created and validated. |
| `1` | Invalid or missing command-line arguments. |
| `2` | Input `model.bim` not found. |
| `3` | Input `model.bim` is not valid TMSL JSON. |
| `4` | The assembled PBIP project failed structural validation. |
| `5` | Could not create/write files in the target directory. |
| `99` | Unexpected error (bug, not user error). |

### Example

```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\NonExistent.bim"
# Output: [ ERROR ] File not found: C:\Models\NonExistent.bim
# Exit code: 2
```

---

## Logging

**PowerShell** produces color-coded console output:
- `[ ERROR ]` in red
- `[ WARN ]` in yellow
- `[ STEP ]` in cyan
- `[ OK ]` in green

**C# (CLI)** produces similar output to stderr/stdout.

**C# (UI)** shows results in the browser.

---

## Common Patterns

### Automation / CI

Check the exit code:
```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\Model.bim" -OutputRoot "C:\Build"
if ($LASTEXITCODE -eq 0) {
    Write-Host "Success"
} else {
    Write-Host "Failed with code $LASTEXITCODE"
    exit $LASTEXITCODE
}
```

### Batch Processing

Convert multiple models:
```powershell
Get-ChildItem "C:\Models" -Filter "*.bim" | ForEach-Object {
    .\bim-to-pbip.ps1 -BimPath $_.FullName
}
```

---

## Next Steps

- [Quick Start](quick-start.md) — usage examples
- [Troubleshooting](../troubleshooting.md) — if something fails
