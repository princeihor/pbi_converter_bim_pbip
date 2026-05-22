---
title: Troubleshooting
last-updated: 2026-05-22
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
- Path contains spaces or special characters (not quoted)

**Fixes**:
- Double-check the path with File Explorer or `dir "C:\Models"`
- Wrap paths with spaces in quotes: `--bim "C:\My Models\MyModel.bim"`
- Use the absolute path (full path, not relative)

---

## Not a Valid Tabular Model (Exit Code 3)

**Error**:
```
[ ERROR ] BIM file is not a valid Tabular model
[ ERROR ] Exit code: 3
```

**Causes**:
- The `.bim` file is corrupted (truncated, incomplete)
- It's not actually a Tabular model `.bim` file (wrong file type)
- The Tabular Object Model (TOM) library could not deserialize the model, or could not serialize it to TMDL

**What it means**: The tool loads the `.bim` through TOM (`JsonSerializer.DeserializeDatabase`) and then writes it as TMDL. Exit code `3` means one of those two steps failed.

**Fixes**:
- Open the `.bim` in a text editor. Does it start with `{` and end with `}`?
- If it is truncated or corrupted, regenerate it from your model source
- If it looks valid but TOM still rejects it, the model may contain a construct TOM cannot serialize — [open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with the model details

---

## Permission Denied / Output Folder Not Writable (Exit Code 5)

**Error**:
```
[ ERROR ] Could not create/write files in the target directory
[ ERROR ] Exit code: 5
```

**Causes**:
- Output folder is read-only
- You don't have write permission (shared drive, OneDrive, etc.)
- Another process has the folder open

**Fixes**:
- Check folder permissions (right-click → Properties → Security)
- Try a different folder (e.g., `C:\PBIP\`)
- Close any applications that might have the folder open

---

## Project Validation Failed (Exit Code 4)

**Error**:
```
[ ERROR ] Project validation failed: <details>
[ ERROR ] Exit code: 4
```

**Possible causes**:
- `definition/ folder missing or empty` — tool bug
- `Report artifact not found` — tool bug
- `Relative path uses backslashes` — tool bug
- `BOM detected in file` — tool bug (shouldn't happen, the tool strips BOMs)

**What to do**:
- This usually indicates a tool bug, not a user error
- Check the error message for specific details
- If you see this consistently, [open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with the error message and your `.bim` file size and name

---

## "Model object-map is not consistent with the metadata-object graph"

**Symptom**: Power BI Desktop opens the project, but on edit or refresh it fails with the internal error:

> *Model object-map is not consistent with the metadata-object graph*

**Cause**: This was caused by an **older version** of the tool that copied the input `.bim` byte-for-byte into the project as `model.bim`. A raw `.bim` can carry an object map that does not line up with what Desktop's metadata engine expects.

**Fix**: The current tool **fixes this**. It no longer copies the `.bim` verbatim. It loads the model through the Tabular Object Model (TOM) library — which rebuilds a consistent metadata object graph — and re-serializes it as a TMDL `definition/` folder. The re-serialized model is internally consistent, so the error no longer occurs.

**What to do**: Re-run the conversion with the current `BimToPbipCli.exe`. The new output has a `SemanticModel/definition/` folder of `.tmdl` files instead of a `model.bim`. See [TOM normalization](concepts.md#tom-normalization).

---

## PBIP Opens But Fails in Power BI Desktop

**Symptoms**:
- The tool reports success (exit code 0)
- Double-clicking `<name>.pbip` opens Power BI Desktop
- Desktop shows an error such as:
  ```
  Property 'dataset' has not been defined and the schema does not allow additional properties.
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
      definition/               ← folder of .tmdl files exists?
    <name>.Report/              ← folder exists?
      definition.pbir           ← file exists?
      report.json               ← file exists?
  ```
- [Open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with the error message and a minimal reproduction

---

## `BimToPbipCli.exe` Doesn't Run

**Error**:
```
'BimToPbipCli.exe' is not recognized as an internal or external command
```

**Cause**: The `.exe` is not in the PATH, or you're running it from the wrong directory.

**Fix**:
```
cd <folder-with-exe>
BimToPbipCli.exe --bim "C:\Models\MyModel.bim"
```

Or use the full path:
```
C:\Path\To\BimToPbipCli.exe --bim "C:\Models\MyModel.bim"
```

---

## Can't Find the Tool

**Problem**: You downloaded the repository but can't find `BimToPbipCli.exe`.

**Solution**:
- For the **pre-built** `.exe`: [download from GitHub Actions](../../.github/workflows/build.yml) — open the latest successful run and download the `BimToPbipCli-win-x64` artifact
- To **build it yourself**: run `dotnet publish` and look in the `publish/` folder for `BimToPbipCli.exe` (see [Installation](installation.md))

---

## Still Stuck?

1. Check the [Glossary](glossary.md) for unfamiliar terms
2. Review [Validation](validation.md) to understand what the tool checks
3. Read [Concepts](concepts.md) for background on PBIP, TMSL, TMDL, TOM, tokens, etc.
4. [Open an issue](https://github.com/princeihor/pbi_converter_bim_pbip/issues) with:
   - Your error message (exact text)
   - The tool version
   - Steps to reproduce (if possible)

---

## Next Steps

- [Validation](validation.md) — what the tool checks
- [Concepts](concepts.md) — background on the tool's architecture
- [CLI Reference](cli-reference.md) — command-line options
