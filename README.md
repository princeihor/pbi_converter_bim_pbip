# BIM → PBIP converter

Converts a Tabular `model.bim` file into a **Power BI Desktop project (PBIP)**
that opens directly in Power BI Desktop.

A `.bim` file *is* a TMSL model, and a PBIP semantic model can store its model
as `model.bim` directly (the "PBIP with TMSL" layout that Power BI Desktop
itself uses). So the conversion is pure file assembly:

* **no `pbi-tools`**, no model conversion;
* **no `.NET` install**, no admin rights;
* **no internet access**.

The output is a complete project — a semantic model **and** a blank report —
because Power BI Desktop's `.pbip` schema *requires* a report artifact (a
dataset-only project fails to open). Open the `.pbip`, and you get an empty
report already bound to the converted model.

## Two implementations

| Implementation | Folder | Best when |
|----------------|--------|-----------|
| **PowerShell** (recommended) | [`powershell/`](powershell/) | You have no admin rights and cannot install anything. PowerShell ships with Windows, so it just runs. Includes a GUI. |
| **.NET 8 C#** | [`BimToPbipCli/`](BimToPbipCli/) | You want a standalone `.exe` with a browser-based UI. |

Both produce the **identical** project structure, because the structure is
defined once in [`pbip-templates/`](pbip-templates/) — see *Single source of
truth* below.

> **Why the PowerShell version exists.** An unsigned downloaded `.exe` is
> blocked by Windows SmartScreen, and code-signing needs a paid certificate. A
> PowerShell *script* is not an executable, so it runs on a locked-down,
> non-admin machine with nothing to install.

---

## Easiest path — PowerShell, no install

1. Get the [`powershell/`](powershell/) folder **and** the
   [`pbip-templates/`](pbip-templates/) folder (keep the repository layout —
   the script reads the templates from `pbip-templates/`).
2. **Double-click `powershell/BimToPbip.cmd`.** A GUI window opens — no install,
   no admin prompt.
3. In the window: **Browse…** to your `.bim`, optionally set an output folder
   and project name, then click **Convert**.
4. On success, double-click the generated `.pbip` to open it in Power BI Desktop.

### PowerShell — command line

```powershell
.\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"
.\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim" -OutputRoot "C:\PBIP\MyModel" -DatasetName "MyModel"
```

| Parameter      | Description |
|----------------|-------------|
| `-BimPath`     | (required for headless mode) Path to the input `model.bim`. |
| `-OutputRoot`  | PBIP project root. Defaults to a folder next to the `.bim`. |
| `-DatasetName` | Dataset / project name. Defaults to the `.bim` file name. |
| `-NoGui`       | Never open the GUI. |

Requirement: **Windows with PowerShell 5.1+** (built in). Nothing else.

---

## .NET 8 C# version

`BimToPbipCli/` is a .NET 8 console app with the same conversion logic plus a
browser-based UI. Run with no arguments (or double-click the `.exe`) for the
UI, or use the CLI:

```
BimToPbipCli --bim <path> [--out <path>] [--dataset <name>] [--help]
```

A prebuilt, self-contained Windows `.exe` is produced by the
*build-windows-exe* GitHub Actions workflow — download the
**`BimToPbipCli-win-x64`** artifact from the latest successful run.

Build / publish it yourself (needs the .NET 8 SDK on the build machine only):

```powershell
dotnet build BimToPbipCli -c Release
dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

---

## What it produces

The Microsoft "PBIP with TMSL" layout:

```
<output-root>/
  <name>.pbip                       # project entry point — open this
  <name>.SemanticModel/
    definition.pbism                # semantic model item properties
    model.bim                       # the input .bim, copied verbatim
  <name>.Report/
    definition.pbir                 # binds the report to the semantic model
    report.json                     # a blank, openable page
```

After assembly the tool **validates** the project against the rules Power BI
Desktop enforces (correct `report` artifact, relative model reference, all
files parse as JSON, no UTF-8 BOM anywhere). A project that would fail to open
is reported as an error instead of a false success.

---

## Single source of truth

The PBIP file structure is **not** invented in code. It lives in
[`pbip-templates/`](pbip-templates/) — four files taken verbatim from a real
Power BI Desktop export, with three substitution tokens. Every field is
documented, with its rationale and source, in
[`pbip-templates/REFERENCE.md`](pbip-templates/REFERENCE.md).

The PowerShell tool reads these files at runtime; the C# tool embeds them; the
internal test reads them. There is exactly one definition of the structure.

> **Encoding.** Power BI Desktop rejects any PBIP file that starts with a UTF-8
> byte-order mark (BOM). Both tools write every file as UTF-8 without a BOM and
> sweep the finished project to strip a stray BOM (e.g. from a `model.bim` that
> already had one). `powershell/Fix-PbipBom.ps1` repairs an already-built
> project in place.

---

## Internal test

[`tests/internal_test.py`](tests/internal_test.py) builds a deliberately
complex `model.bim` (many tables, measures, relationships, RLS roles,
perspectives, cultures), runs the conversion against the shared
`pbip-templates/`, and asserts the produced project is structurally valid —
including a regression check for the two real failures this project hit
(a UTF-8 BOM, and a `dataset` artifact instead of `report`).

```
python3 tests/internal_test.py
```

It also runs in CI (`.github/workflows/build.yml`) before the `.exe` is built.

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0`  | Success. |
| `1`  | Invalid or missing command-line arguments. |
| `2`  | Input `model.bim` not found. |
| `3`  | Input `model.bim` is not valid TMSL JSON. |
| `4`  | The assembled PBIP project failed structural validation. |
| `5`  | Could not create/write files in the target directory. |
| `99` | Unexpected error. |

---

## Project layout

```
pbip-templates/            # single source of truth for the PBIP structure
  project.pbip
  SemanticModel/definition.pbism
  Report/definition.pbir
  Report/report.json
  REFERENCE.md             # field-by-field rationale

powershell/                # no-install, no-admin implementation
  BimToPbip.cmd            # double-click launcher
  bim-to-pbip.ps1          # the converter: GUI + headless CLI + self-validation
  Fix-PbipBom.cmd          # double-click launcher for the BOM repair tool
  Fix-PbipBom.ps1          # strips a UTF-8 BOM from an existing PBIP project

BimToPbipCli/              # .NET 8 C# implementation
  Program.cs               # entry point
  CliParser.cs/CliOptions.cs
  PbipTemplates.cs         # loads the embedded pbip-templates
  ConversionService.cs     # validate input -> assemble -> validate output
  WebUi/                   # local browser-based UI

tests/internal_test.py     # end-to-end conversion + validation test
.github/workflows/build.yml
```
