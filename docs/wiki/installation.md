---
title: Installation
last-updated: 2026-05-22
tags: [guide, setup]
---

# Installation

Get the tool running on your machine. The tool ships as a single self-contained C# (.NET 8) Windows `.exe`.

## Pre-built `.exe` (Recommended)

The easiest way — no .NET install required.

1. Go to the GitHub Actions [build workflow](../../.github/workflows/build.yml)
2. Click on the latest successful run
3. Download the **`BimToPbipCli-win-x64`** artifact
4. Extract `BimToPbipCli.exe`
5. Run it:
   ```
   BimToPbipCli.exe                                  # Browser UI
   BimToPbipCli.exe --bim "C:\Models\MyModel.bim"    # CLI
   ```

**Requirements**: None. The `.exe` is self-contained — it bundles the .NET runtime, the PBIP wrapper templates, and the Tabular Object Model (TOM) library.

---

## Build It Yourself

**Requirements**:
- Windows
- .NET 8 SDK (download from [dotnet.microsoft.com](https://dotnet.microsoft.com/))

**Steps**:

1. Clone the repository:
   ```
   git clone https://github.com/princeihor/pbi_converter_bim_pbip.git
   ```

2. Build:
   ```
   dotnet build BimToPbipCli -c Release
   ```

3. Publish to a standalone `.exe`:
   ```
   dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true ^
       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
       -p:EnableCompressionInSingleFile=true -o publish
   ```

4. The executable is at `publish/BimToPbipCli.exe`

**Note**: The `pbip-templates/` files and the TOM library are bundled into the executable at build time. You only need the `.exe` to run it.

---

## Verify Installation

```
BimToPbipCli.exe --help
```

Should show the CLI options.

---

## Next Steps

- [Quick Start](quick-start.md) — run your first conversion
- [CLI Reference](cli-reference.md) — all parameters
