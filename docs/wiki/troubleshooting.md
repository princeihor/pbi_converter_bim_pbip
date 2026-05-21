---
title: Troubleshooting
last-updated: 2026-05-21
relates-to: [validation, cli-reference]
tags: [guide]
---

# Troubleshooting

Common issues and how to fix them.

## File Not Found (Exit Code 2)

**Error**:
```
[ ERROR ] File not found: C:\Models\MyModel.bim
[ ERROR ] Exit code: 2
```

**Causes**:
- File path is wrong (typo, missing drive letter, etc.)
- File was moved or deleted
- Path contains spaces or special characters (not escaped)

**Fixes**:
- Double-check the path: `dir "C:\Models"` or use File Explorer to find the file
- If using PowerShell, wrap paths in quotes: `-BimPath "C:\My Models\MyModel.bim"`
- Try the absolute path (full path, not relative)

---

## Invalid JSON / TMSL (Exit Code 3)

**Error**:
```
[ ERROR ] BIM file is not valid TMSL JSON
[ ERROR ] Exit code: 3
```

**Causes**:
- The `.bim` file is corrupted (truncated, incomplete)
- It's not actually a `.bim` file (wrong file type)
- The file contains invalid TMSL

**Fixes**:
- Open the `.bim` file in a text editor. Does it start with `{`? Does it end with `}`?
- If the file is truncated or obviously corrupted, try regenerating it from your model source
- If it looks valid but the error persists, the TMSL might have an issue — try validating it with `jq` or another JSON parser:
  ```powershell
  Get-Content "C:\Models\MyModel.bim" | ConvertFrom-Json  # If valid, no error
  ```

---

## Permission Denied / Output Folder Not Writable (Exit Code 5)

**Error**:
```
[ ERROR ] Could not create/write files in the target directory
[ ERROR ] Exit code: 5
```

**Causes**:
- Output folder is read-only
- You don't have write permission (on a shared drive, OneDrive, etc.)
- Another process has the folder open

**Fixes**:
- Check folder permissions (right-click → Properties → Security)
- Try a different folder (e.g., `C:\PBIP\` instead of `OneDrive:/Projects/`)
- Close any applications that might have the folder open (File Explorer, Visual Studio, etc.)
- Run PowerShell as Administrator (if needed): right-click PowerShell → "Run as administrator"

---

## Project Validation Failed (Exit Code 4)

**Error**:
```
[ ERROR ] Project validation failed: <details>
[ ERROR ] Exit code: 4
```

**Possible causes**:
- `report.json missing required theme` — tool bug (shouldn't happen with current version)
- `Report artifact not found` — tool bug
- `Relative path uses backslashes` — tool bug
- `BOM detected in file` — tool bug (shouldn't happen, we strip BOMs)

**What to do**:
- This usually indicates a tool bug, not a user error
- Check the error message for specific details
- If you see this consistently, [open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with:
  - The error message
  - Your `.bim` file size and name
  - Which tool (PowerShell or C#) and version

---

## PBIP Opens But Fails in Power BI Desktop

**Symptoms**:
- The tool reports success (exit code 0)
- Double-clicking `<name>.pbip` opens Power BI Desktop
- Desktop shows an error like:
  ```
  Property 'dataset' has not been defined and the schema does not allow additional properties.
  ```
  or
  ```
  Cannot read properties of undefined (reading 'customTheme')
  ```

**Causes**:
- The tool is outdated or has a bug
- The `.pbip` folder structure is incomplete or incorrect

**Fixes**:
- Make sure you're using the latest version of the tool
- Check that the output folder contains all required files:
  ```
  <output>/
    <name>.pbip                 ← exists?
    <name>.SemanticModel/       ← folder exists?
      definition.pbism          ← file exists?
      model.bim                 ← file exists?
    <name>.Report/              ← folder exists?
      definition.pbir           ← file exists?
      report.json               ← file exists?
  ```
- Check that no files have a UTF-8 BOM. Use the `powershell/Fix-PbipBom.ps1` tool to scan:
  ```powershell
  .\Fix-PbipBom.ps1 -PbipPath "C:\PBIP\MyModel"  # If 0 files reported, no BOM
  ```
- [Open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with the error message and the output folder (or a minimal reproduction)

---

## PowerShell Script Doesn't Run

**Error**:
```
.\bim-to-pbip.ps1 : File cannot be loaded because running scripts is disabled on this system.
```

**Cause**: Execution policy is too restrictive.

**Fix**: Set the execution policy for the current user:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try again:
```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"
```

---

## C# Tool / .exe Doesn't Run

**Error**:
```
'BimToPbipCli.exe' is not recognized as an internal or external command
```

**Cause**: The `.exe` is not in the PATH, or you're running it from the wrong directory.

**Fix**:
```powershell
cd <folder-with-exe>
.\BimToPbipCli.exe --bim "C:\Models\MyModel.bim"
```

Or use the full path:
```powershell
C:\Path\To\BimToPbipCli.exe --bim "C:\Models\MyModel.bim"
```

---

## GUI Doesn't Open (PowerShell)

**Error**:
```
.\bim-to-pbip.ps1
# (no GUI appears)
```

**Cause**: GUI requires Windows Forms support (not available in PowerShell Core on non-Windows).

**Fix**:
- On Windows: Use Windows PowerShell (built-in), not PowerShell Core
- On other OS: Use the C# tool instead, or run headless with `-BimPath`
- Check your PowerShell version:
  ```powershell
  $PSVersionTable
  ```
  You should see `PSVersion : 5.1.xxxxx` (Windows PowerShell) or `7.x.x` (Core, no GUI)

---

## Can't Find the Tools

**Problem**: You downloaded the repository but can't find `BimToPbip.cmd` or `BimToPbipCli.exe`.

**Solution**:
- For **PowerShell**: Navigate to the `powershell/` folder. You should see `BimToPbip.cmd` and `bim-to-pbip.ps1`
- For **C# pre-built**: [Download from GitHub Actions](../../.github/workflows/build.yml) (latest successful run, `BimToPbipCli-win-x64` artifact)
- For **C# building yourself**: Run `dotnet publish` and look in the `publish/` folder for `BimToPbipCli.exe`

---

## Still Stuck?

1. Check the [Glossary](glossary.md) for unfamiliar terms
2. Review [Validation](validation.md) to understand what the tool checks
3. Read [Concepts](concepts.md) for background on PBIP, TMSL, tokens, etc.
4. [Open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with:
   - Your error message (exact text)
   - Which tool and version
   - Steps to reproduce (if possible)
   - Your Windows/PowerShell version (`$PSVersionTable`)

---

## Next Steps

- [Validation](validation.md) — what the tool checks
- [Concepts](concepts.md) — background on the tool's architecture
- [CLI Reference](cli-reference.md) — command-line options
