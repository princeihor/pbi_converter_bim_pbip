---
title: Installation
last-updated: 2026-05-21
tags: [guide, setup]
---

# Installation

Get the tool running on your machine.

## PowerShell (Recommended)

**Requirements**: 
- Windows (any version that includes PowerShell 5.1+)
- **No admin rights, no install needed**

**Steps**:

1. Download the repository:
   - Clone with git: `git clone https://github.com/princeihor/pbi_converter_bim_pbip.git`
   - Or download as ZIP from GitHub

2. Open File Explorer and navigate to the `powershell/` folder

3. **Double-click `BimToPbip.cmd`** — a GUI window opens

4. Done. Use the GUI or run commands in PowerShell:
   ```powershell
   cd <path-to-powershell-folder>
   .\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"
   ```

**Note**: Both `powershell/` and `pbip-templates/` folders must be present and in the standard layout. The script reads templates from `pbip-templates/`.

---

## C# / .NET 8 (Pre-built .exe)

**Easiest way** if you want a standalone executable:

1. Go to the GitHub Actions [build-windows-exe workflow](../../.github/workflows/build.yml)
2. Click on the latest successful run
3. Download the **`BimToPbipCli-win-x64`** artifact
4. Extract `BimToPbipCli.exe`
5. Run it:
   ```
   BimToPbipCli.exe                          # Browser UI
   BimToPbipCli.exe --bim "C:\Models\MyModel.bim"  # CLI
   ```

**Requirements**: None (the `.exe` is self-contained, includes .NET runtime)

---

## C# / .NET 8 (Build Yourself)

**Requirements**:
- Windows with PowerShell (for the build script)
- .NET 8 SDK (download from [dotnet.microsoft.com](https://dotnet.microsoft.com/))

**Steps**:

1. Clone the repository

2. Open PowerShell in the repository folder

3. Build:
   ```powershell
   dotnet build BimToPbipCli -c Release
   ```

4. Publish to a standalone `.exe`:
   ```powershell
   dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true `
       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
       -p:EnableCompressionInSingleFile=true -o publish
   ```

5. The executable is at `publish/BimToPbipCli.exe`

**Note**: The `pbip-templates/` files are embedded into the executable at build time. You only need the `.exe` to run it (the templates don't need to be present separately).

---

## Verify Installation

### PowerShell

```powershell
.\bim-to-pbip.ps1 -Help
```

Should show the help text.

### C# (.exe)

```
BimToPbipCli.exe --help
```

Should show the CLI options.

---

## Next Steps

- [Quick Start](quick-start.md) — run your first conversion
- [CLI Reference](cli-reference.md) — all parameters
