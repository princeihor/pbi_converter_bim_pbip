---
title: CLI Reference
last-updated: 2026-05-22
tags: [reference]
---

# Command-Line Reference

Complete reference for `BimToPbipCli.exe` options and exit codes.

## Signature

```
BimToPbipCli.exe [--bim <path>] [--out <path>] [--dataset <name>] [--ui] [--help]
```

## Parameters

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--bim <path>` | Yes* | (none) | Path to the input `model.bim` file. |
| `--out <path>` | No | Folder next to the `.bim` | PBIP project root folder. |
| `--dataset <name>` | No | `.bim` file name (without extension) | Dataset / project name. Used in folder names. |
| `--ui` | No | (off) | Open the browser-based UI. |
| `--help` | No | (none) | Show help and exit. |

**\* "Required for CLI mode"**: If you don't provide `--bim`, the browser-based UI opens. With `--bim`, the tool runs headless in CLI mode.

## Examples

**Browser UI** (interactive):
```
BimToPbipCli.exe
BimToPbipCli.exe --ui
```

**CLI with defaults**:
```
BimToPbipCli.exe --bim C:\Models\MyModel.bim
```
→ Output folder: `C:\Models\MyModel\`
→ Project name: `MyModel`

**Custom output and name**:
```
BimToPbipCli.exe --bim C:\Models\MyModel.bim --out C:\PBIP\MyProject --dataset CustomName
```
→ Output: `C:\PBIP\MyProject\CustomName.pbip`

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success. PBIP project created and validated. |
| `1` | Invalid or missing command-line arguments. |
| `2` | Input `model.bim` not found. |
| `3` | Input `model.bim` is not a valid Tabular model — TOM could not load it, or could not serialize it to TMDL. |
| `4` | The assembled PBIP project failed structural validation. |
| `5` | Could not create/write files in the target directory. |
| `99` | Unexpected error (bug, not user error). |

### Example

```
BimToPbipCli.exe --bim C:\Models\NonExistent.bim
# Output: [ ERROR ] File not found: C:\Models\NonExistent.bim
# Exit code: 2
```

---

## Logging

In CLI mode the tool produces console output:
- `[ ERROR ]` for failures
- `[ WARN ]` for warnings
- `[ STEP ]` for pipeline progress
- `[ OK ]` for success

In UI mode, results are shown in the browser.

---

## Common Patterns

### Automation / CI

Check the exit code:
```bat
BimToPbipCli.exe --bim "C:\Models\Model.bim" --out "C:\Build"
if %ERRORLEVEL% NEQ 0 (
    echo Failed with code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
echo Success
```

### Batch Processing

Convert multiple models:
```bat
for %%f in (C:\Models\*.bim) do BimToPbipCli.exe --bim "%%f"
```

---

## Next Steps

- [Quick Start](quick-start.md) — usage examples
- [Troubleshooting](troubleshooting.md) — if something fails
