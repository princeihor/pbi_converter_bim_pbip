# BimToPbipCli

A Windows-oriented (cross-platform .NET 8) utility that converts a Tabular
`model.bim` file into a **PBIP-compatible project folder**, driving the existing
[`pbi-tools`](https://pbi.tools/cli/) CLI under the hood.

It runs in **two modes**:

* **Web UI** — start the `.exe` with no arguments (or double-click it in
  Explorer). It opens a local page in your browser where you pick files with
  native dialogs and click **Convert**. See *Quick start* below.
* **Command line** — pass `--bim ...` for scripting / CI. See *Usage* below.

Either way it automates the steps you would otherwise run by hand:

1. Run `pbi-tools convert` to turn `model.bim` into a TMDL model folder.
2. Wrap that TMDL output in a PBIP-style project layout (`dataset/definition/`).
3. Emit the minimal PBIP metadata files (`.pbip`, `.platform`, `definition.pbism`).
4. Clean up the temporary workspace.

The result is a **dataset-only PBIP project** (a semantic model, no report). You
can open the generated `.pbip` in Power BI Desktop and add a report there.

---

## Quick start (Web UI — no command line)

1. Build a standalone executable once (see *Publish a standalone `.exe`* below),
   or ask whoever set this up for the prebuilt `BimToPbipCli.exe`.
2. Make sure `pbi-tools` is installed (see *Requirements*).
3. **Double-click `BimToPbipCli.exe`.** A small console window appears and your
   default browser opens the converter page automatically.
4. In the page:
   * Click **Browse…** next to *model.bim file* and pick your `.bim`.
   * Optionally choose an output folder, dataset name, and `pbi-tools` path
     (leave blank to use the defaults / `PBI_TOOLS_PATH` / `PATH`).
   * Click **Convert**. The live log shows each step.
5. On success, click **Open project folder** to jump to the result in Explorer.
6. To stop the tool, close the small console window.

The UI binds only to `http://localhost:<random-port>` on your own machine — it
is not exposed to the network. The native file/folder pickers are Windows-only;
on other platforms type the paths manually.

---

## What it produces

```
<output-root>/
  <DatasetName>.pbip          # PBIP project entry point (points at the dataset)
  dataset/
    .platform                 # Fabric/Git item metadata: type + display name + GUID
    definition.pbism          # semantic model item properties
    definition/               # TMDL model files produced by pbi-tools convert
      model.tmdl
      tables/
      ...
```

* The **dataset GUID** is generated per run and stored as `config.logicalId` in
  `dataset/.platform`.
* The **dataset name** comes from `--dataset` (or the `.bim` file name) and is
  stored as `metadata.displayName` in `dataset/.platform`.
* The **relative path to the definition** is recorded in `<DatasetName>.pbip`
  under `artifacts[].dataset.path` (`dataset`).

> **Format disclaimer.** The PBIP / "Power BI Project" format is owned by
> Microsoft and is still evolving. The JSON templates emitted by this tool live
> in a single file — [`BimToPbipCli/PbipTemplates.cs`](BimToPbipCli/PbipTemplates.cs) —
> so they are easy to update. If Microsoft changes the schema, adjust that file.
> Always cross-check against the authoritative spec:
> <https://learn.microsoft.com/power-bi/developer/projects/>.
> Note also that native PBIP support in `pbi-tools` itself is still being
> discussed upstream (see the "Can we expect Support for PBIP" issue), so this
> utility composes the PBIP layout manually around `pbi-tools convert`.

---

## Requirements

* **.NET 8 SDK** — to build/run this utility. <https://dotnet.microsoft.com/download/dotnet/8.0>
* **`pbi-tools` CLI** — the conversion engine. <https://pbi.tools/cli/>

### Installing `pbi-tools`

1. Download a release from GitHub: <https://github.com/pbi-tools/pbi-tools/releases>
   (the `pbi-tools` build runs on .NET / works cross-platform; `pbi-tools.exe`
   is the Windows build).
2. Extract the archive to a folder, e.g. `C:\tools\pbi-tools\`.
3. Make the tool discoverable using **one** of:
   * Add the folder to your `PATH`, **or**
   * Set the `PBI_TOOLS_PATH` environment variable to the executable (or its
     folder), **or**
   * Pass `--pbiToolsPath` on every invocation.

```powershell
# Option A: PATH (PowerShell, current session)
$env:PATH += ";C:\tools\pbi-tools"

# Option B: environment variable (persisted for the current user)
setx PBI_TOOLS_PATH "C:\tools\pbi-tools\pbi-tools.exe"
```

The path to `pbi-tools` is **never hard-coded** in this utility.

---

## Build

```powershell
cd BimToPbipCli
dotnet build -c Release
```

---

## Publish a standalone `.exe` (no .NET install needed)

To produce a single self-contained executable that runs on machines **without**
the .NET runtime installed:

```powershell
dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true
```

The resulting executable is at:

```
BimToPbipCli\bin\Release\net8.0\win-x64\publish\BimToPbipCli.exe
```

Copy that single file anywhere and run it directly:

```powershell
.\BimToPbipCli.exe --bim "C:\Models\MyModel.bim" --out "C:\PBIP\MyModel" --dataset "MyModelDataset"
```

Notes:
* `-r win-x64` targets 64-bit Windows. Use `win-arm64` for ARM, or `linux-x64` /
  `osx-x64` for other platforms.
* `--self-contained true` bundles the .NET runtime — the target machine needs
  **no .NET install**. The `.exe` is larger (~15 MB+) as a result.
* For a small `.exe` that *requires* .NET 8 to be installed on the target, use
  `--self-contained false -p:PublishSingleFile=true` instead.
* `pbi-tools` is still an external dependency and must be installed separately
  on the target machine (see Requirements above).

---

## Usage

```
BimToPbipCli --bim <path> [--out <path>] [--dataset <name>]
             [--pbiToolsPath <path>] [--modelSerialization <format>]
             [--keepTemp] [--help]
```

| Option                       | Required | Description |
|-------------------------------|----------|-------------|
| `--bim <path>`                | yes      | Path to the input `model.bim` file. |
| `--out <path>`                | no       | PBIP project root. Defaults to a sub-folder named after the dataset, next to the `.bim` file. |
| `--dataset <name>`            | no       | Dataset name. Defaults to the `.bim` file name without extension. |
| `--pbiToolsPath <path>`       | no       | Path to `pbi-tools(.exe)` (or its folder). Overrides `PBI_TOOLS_PATH` and `PATH`. |
| `--modelSerialization <fmt>`  | no       | Serialization passed to `pbi-tools convert`. Defaults to `Tmdl`. |
| `--keepTemp`                  | no       | Keep the temporary working directory for inspection. |
| `--help`, `-h`                | no       | Show help and exit. |

Both `--name value` and `--name=value` forms are accepted.

### Examples

```powershell
# Run from source with the .NET SDK:
dotnet run --project BimToPbipCli -- --bim "C:\Models\MyModel.bim"

dotnet run --project BimToPbipCli -- --bim "C:\Models\MyModel.bim" --out "C:\PBIP\MyModel" --dataset "MyModelDataset"

dotnet run --project BimToPbipCli -- --bim "C:\Models\MyModel.bim" --out "C:\PBIP\MyModel" --dataset "MyModelDataset" --pbiToolsPath "C:\tools\pbi-tools\pbi-tools.exe"

# Or run the built executable directly:
.\BimToPbipCli\bin\Release\net8.0\BimToPbipCli.exe --bim "C:\Models\MyModel.bim"
```

### Full annotated run

```powershell
# pbi-tools is on PATH (or PBI_TOOLS_PATH is set), so no --pbiToolsPath needed.
dotnet run --project BimToPbipCli -- `
    --bim "C:\Models\Contoso.bim" `      # input Tabular model
    --out "C:\PBIP\Contoso" `            # PBIP project root to create
    --dataset "ContosoSales" `           # semantic model display name
    --keepTemp                           # keep temp folder to inspect raw convert output
```

Expected console output (abbreviated):

```
[ INFO ] BimToPbipCli — BIM -> PBIP project converter
[ INFO ] Input model:   C:\Models\Contoso.bim
[ INFO ] Dataset name:  ContosoSales
[ INFO ] Output root:   C:\PBIP\Contoso
[ STEP ] Checking environment for pbi-tools...
[ INFO ] pbi-tools:     C:\tools\pbi-tools\pbi-tools.exe
[ INFO ] Temp workspace: C:\Users\...\AppData\Local\Temp\bim-to-pbip\<guid>
[ STEP ] Running pbi-tools convert (Tmdl)...
[  OK  ] Model converted to TMDL.
[ STEP ] Assembling PBIP project structure...
[ INFO ] Dataset GUID:  <guid>
[  OK  ] PBIP structure assembled.
[ INFO ] --keepTemp set; temporary workspace kept at: ...
[  OK  ] Conversion complete.
  PBIP project root : C:\PBIP\Contoso
  Project file      : C:\PBIP\Contoso\ContosoSales.pbip
  Dataset folder    : C:\PBIP\Contoso\dataset
  Model definition  : C:\PBIP\Contoso\dataset\definition (TMDL)

  Created: a PBIP dataset (semantic model) project. No report (.Report) part
  was generated — open the .pbip in Power BI Desktop to add a report.
```

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0`  | Success. |
| `1`  | Invalid or missing command-line arguments. |
| `2`  | Input `model.bim` not found. |
| `3`  | `pbi-tools` CLI could not be located. |
| `4`  | `pbi-tools convert` failed. |
| `5`  | Could not create/write files in the target directory. |
| `99` | Unexpected error. |

Every failure prints a clear `[ ERROR ]` message to **stderr** — there are no
silent failures.

---

## Project layout

```
BimToPbipCli/
  BimToPbipCli.csproj      # .NET 8 console project
  Program.cs               # entry point: parse args -> run -> exit code
  CliOptions.cs            # parsed options (data only)
  CliParser.cs             # argument parsing + help text (separate from business logic)
  CliParseException.cs
  ExitCode.cs              # typed exit codes
  ConsoleLogger.cs         # step logging (stdout / stderr)
  PbiToolsLocator.cs       # resolves pbi-tools via --pbiToolsPath / PBI_TOOLS_PATH / PATH
  ProcessRunner.cs         # System.Diagnostics.Process wrapper, captures stdout/stderr
  PbipTemplates.cs         # PBIP JSON templates — single place to update for spec changes
  ConversionService.cs     # the 5-step conversion pipeline
  ConversionResult.cs      # typed result
  ConversionException.cs   # carries an ExitCode for explicit failure handling
  IStepLogger.cs           # logging abstraction shared by CLI and web UI
  ConsoleLogger.cs         # IStepLogger -> stdout/stderr (CLI mode)
  CapturingLogger.cs       # IStepLogger -> in-memory list (web UI mode)
  WebUi/
    WebUiServer.cs         # local HttpListener server: serves the page + JSON API
    IndexPage.cs           # the single-page HTML/CSS/JS UI
    NativeFilePicker.cs    # native Windows file/folder dialogs (via PowerShell)
README.md
```

Argument parsing (`CliParser`) is intentionally kept separate from the
conversion pipeline (`ConversionService`) so each can change and be tested
independently.
