# BIM → PBIP converter

Converts a Tabular `model.bim` file into a **Power BI Desktop project (PBIP)**
that opens directly in Power BI Desktop.

The converter does **not** copy the `.bim` verbatim. It deserializes the model
through the **Tabular Object Model (TOM)** and re-serializes it as **TMDL**
into the project's `definition/` folder — the modern PBIP layout. The TOM
round-trip rebuilds a consistent metadata object graph, which is what stops
Power BI Desktop from failing on edit/refresh with *"Model object-map is not
consistent with the metadata-object graph"*.

TOM is bundled into the self-contained `.exe`, so there is still:

* **no separate install**, no admin rights;
* **no internet access** needed to run it.

The output is a complete project — a semantic model **and** a blank report —
because Power BI Desktop's `.pbip` schema *requires* a report artifact (a
dataset-only project fails to open). Open the `.pbip`, and you get an empty
report already bound to the converted model.

---

## Getting the tool

`BimToPbipCli/` is a .NET 8 console app with a browser-based UI. A prebuilt,
self-contained Windows `.exe` is produced by the *build-windows-exe* GitHub
Actions workflow — download the **`BimToPbipCli-win-x64`** artifact from the
latest successful run. Nothing needs to be installed to run it.

Build / publish it yourself (needs the .NET 8 SDK on the build machine only):

```powershell
dotnet build BimToPbipCli -c Release
dotnet publish BimToPbipCli -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

> The TOM package (`Microsoft.AnalysisServices.NetCore.retail.amd64`) is
> Windows-x64, matching the `-r win-x64` publish target. It is bundled into the
> single-file `.exe`.

---

## Usage

Run with no arguments (or double-click the `.exe`) for the browser UI, or use
the CLI:

```
BimToPbipCli --bim <path> [--out <path>] [--dataset <name>] [--help]
```

| Option       | Description |
|--------------|-------------|
| `--bim`      | (required) Path to the input `model.bim`. |
| `--out`      | PBIP project root. Defaults to a folder next to the `.bim`. |
| `--dataset`  | Dataset / project name. Defaults to the `.bim` file name. |
| `--ui`       | Force the browser-based UI. |
| `--help`     | Show help. |

On success, double-click the generated `.pbip` to open it in Power BI Desktop.

---

## What it produces

```
<output-root>/
  <name>.pbip                       # project entry point — open this
  <name>.SemanticModel/
    definition.pbism                # semantic model item properties
    definition/                     # the model as TMDL (database.tmdl,
                                     #   model.tmdl, tables/*.tmdl, ...)
  <name>.Report/
    definition.pbir                 # binds the report to the semantic model
    report.json                     # a blank, openable page
```

The conversion is a three-phase pipeline:

1. **Load + normalize** — deserialize the `.bim` through TOM (invalid models
   are rejected here with a precise error).
2. **Assemble** — write the model as TMDL via TOM's `TmdlSerializer`, plus the
   wrapper files from the embedded templates.
3. **Validate** — check the project against the rules Power BI Desktop enforces
   (a non-empty TMDL `definition/` folder, correct `report` artifact, relative
   model reference, all metadata files parse as JSON, no UTF-8 BOM anywhere).

A project that would fail to open is reported as an error, not a false success.

---

## Single source of truth

The PBIP *wrapper* files are **not** invented in code. They live in
[`pbip-templates/`](pbip-templates/) — four files taken from a real Power BI
Desktop export, with three substitution tokens. Every field is documented, with
its rationale and source, in
[`pbip-templates/REFERENCE.md`](pbip-templates/REFERENCE.md). The C# tool embeds
them; the internal test reads them.

The semantic model itself is not a template — TOM generates it at conversion
time.

> **Encoding.** Power BI Desktop rejects any PBIP file that starts with a UTF-8
> byte-order mark (BOM). The tool writes every file as UTF-8 without a BOM and
> sweeps the finished project to strip a stray BOM.

---

## Internal test

[`tests/internal_test.py`](tests/internal_test.py) builds a deliberately
complex `model.bim` (many tables, measures, relationships, RLS roles,
perspectives, cultures), runs the **real built converter** against it, and
asserts the produced project is structurally valid — including a regression
check for the real failures this project hit (a UTF-8 BOM, and a `dataset`
artifact instead of `report`).

```
python3 tests/internal_test.py
```

It requires the .NET 8 SDK (it invokes the converter via `dotnet`) and runs in
CI (`.github/workflows/build.yml`) before the `.exe` is built.

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0`  | Success. |
| `1`  | Invalid or missing command-line arguments. |
| `2`  | Input `model.bim` not found. |
| `3`  | Input `model.bim` is not a valid Tabular model (TOM could not load it, or it could not be serialized to TMDL). |
| `4`  | The assembled PBIP project failed structural validation. |
| `5`  | Could not create/write files in the target directory. |
| `99` | Unexpected error. |

---

## Project layout

```
pbip-templates/            # single source of truth for the PBIP wrapper files
  project.pbip
  SemanticModel/definition.pbism
  Report/definition.pbir
  Report/report.json
  REFERENCE.md             # field-by-field rationale

BimToPbipCli/              # .NET 8 C# implementation
  Program.cs               # entry point
  CliParser.cs/CliOptions.cs
  PbipTemplates.cs         # loads the embedded pbip-templates
  ConversionService.cs     # load + normalize -> assemble -> validate
  WebUi/                   # local browser-based UI

tests/internal_test.py     # end-to-end conversion + validation test
.github/workflows/build.yml
```
