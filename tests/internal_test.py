#!/usr/bin/env python3
"""Internal end-to-end test for the BIM -> PBIP converter.

What this proves
----------------
* A *complex* model.bim (many tables, measures, relationships, roles,
  perspectives, cultures) is converted into a PBIP project by the *real*
  built C# tool (BimToPbipCli).
* The model is normalized through TOM and emitted as a TMDL ``definition/``
  folder — the modern PBIP layout — never copied verbatim as ``model.bim``.
* The produced project has the exact structure Power BI Desktop requires --
  the same rules whose violation produced the real "Cannot read .pbip" errors
  (BOM, and ``artifacts[].dataset`` instead of ``artifacts[].report``).

How it stays honest
-------------------
The test does NOT re-implement the conversion. It builds and runs the shipped
``BimToPbipCli`` tool via ``dotnet`` and validates whatever that tool produced.
The conversion now requires the Tabular Object Model (TOM), so a .NET 8 SDK
must be available. Run:  python3 tests/internal_test.py
"""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
TEMPLATES = REPO / "pbip-templates"
CLI_PROJECT = REPO / "BimToPbipCli"

PASSED: list[str] = []


def ok(msg: str) -> None:
    PASSED.append(msg)
    print(f"  PASS  {msg}")


def fail(msg: str) -> None:
    print(f"  FAIL  {msg}")
    raise AssertionError(msg)


def check(condition: bool, msg: str) -> None:
    if condition:
        ok(msg)
    else:
        fail(msg)


# ---------------------------------------------------------------------------
# A deliberately complex TMSL model.bim
# ---------------------------------------------------------------------------

def _column(name: str, dtype: str, **extra: object) -> dict:
    col = {"name": name, "dataType": dtype, "sourceColumn": name}
    col.update(extra)
    return col


def build_complex_bim() -> dict:
    """A multi-table star schema with measures, a calculated column, a
    hierarchy, relationships, RLS roles, a perspective and a culture."""

    def table(name: str, columns: list[dict], measures: list[dict] | None = None,
              extra: dict | None = None) -> dict:
        t = {
            "name": name,
            "columns": columns,
            "partitions": [{
                "name": f"{name}-Partition",
                "mode": "import",
                "source": {
                    "type": "m",
                    "expression": [
                        "let",
                        f'    Source = Sql.Database("server", "DW", [Query="SELECT * FROM dbo.{name}"])',
                        "in",
                        "    Source",
                    ],
                },
            }],
        }
        if measures:
            t["measures"] = measures
        if extra:
            t.update(extra)
        return t

    dim_date = table("DimDate", [
        _column("DateKey", "int64"),
        _column("Date", "dateTime", formatString="Long Date"),
        _column("Year", "int64"),
        _column("MonthNumber", "int64"),
        _column("MonthName", "string"),
        {"name": "YearMonth", "dataType": "string", "type": "calculated",
         "expression": "FORMAT(DimDate[Date], \"YYYY-MM\")"},
    ], extra={
        "hierarchies": [{
            "name": "Calendar",
            "levels": [
                {"name": "Year", "ordinal": 0, "column": "Year"},
                {"name": "Month", "ordinal": 1, "column": "MonthName"},
            ],
        }],
    })

    dim_product = table("DimProduct", [
        _column("ProductKey", "int64"),
        _column("ProductName", "string"),
        _column("Category", "string"),
        _column("Subcategory", "string"),
        _column("ListPrice", "decimal", formatString="\\$#,0.00"),
    ])

    dim_customer = table("DimCustomer", [
        _column("CustomerKey", "int64"),
        _column("CustomerName", "string"),
        _column("Segment", "string"),
        _column("GeographyKey", "int64"),
    ])

    dim_geography = table("DimGeography", [
        _column("GeographyKey", "int64"),
        _column("Country", "string", dataCategory="Country"),
        _column("Region", "string"),
        _column("City", "string", dataCategory="City"),
    ])

    dim_reseller = table("DimReseller", [
        _column("ResellerKey", "int64"),
        _column("ResellerName", "string"),
        _column("BusinessType", "string"),
    ])

    fact_sales = table("FactSales", [
        _column("SalesKey", "int64"),
        _column("DateKey", "int64"),
        _column("ProductKey", "int64"),
        _column("CustomerKey", "int64"),
        _column("ResellerKey", "int64"),
        _column("Channel", "string"),
        _column("Quantity", "int64"),
        _column("SalesAmount", "decimal", formatString="\\$#,0.00"),
        _column("Cost", "decimal", formatString="\\$#,0.00"),
    ], measures=[
        {"name": "Total Sales", "expression": "SUM(FactSales[SalesAmount])",
         "formatString": "\\$#,0.00"},
        {"name": "Total Cost", "expression": "SUM(FactSales[Cost])",
         "formatString": "\\$#,0.00"},
        {"name": "Margin", "expression": "[Total Sales] - [Total Cost]",
         "formatString": "\\$#,0.00"},
        {"name": "Margin %", "expression": "DIVIDE([Margin], [Total Sales])",
         "formatString": "0.0%"},
        {"name": "Sales YTD",
         "expression": "TOTALYTD([Total Sales], DimDate[Date])",
         "formatString": "\\$#,0.00"},
    ])

    fact_inventory = table("FactInventory", [
        _column("DateKey", "int64"),
        _column("ProductKey", "int64"),
        _column("UnitsOnHand", "int64"),
    ], measures=[
        {"name": "Units On Hand", "expression": "SUM(FactInventory[UnitsOnHand])"},
    ])

    return {
        "name": "ComplexSalesModel",
        "compatibilityLevel": 1567,
        "model": {
            "culture": "en-US",
            "defaultPowerBIDataSourceVersion": "powerBI_V3",
            "tables": [
                dim_date, dim_product, dim_customer, dim_geography,
                dim_reseller, fact_sales, fact_inventory,
            ],
            "relationships": [
                {"name": "Sales_Date", "fromTable": "FactSales",
                 "fromColumn": "DateKey", "toTable": "DimDate", "toColumn": "DateKey"},
                {"name": "Sales_Product", "fromTable": "FactSales",
                 "fromColumn": "ProductKey", "toTable": "DimProduct", "toColumn": "ProductKey"},
                {"name": "Sales_Customer", "fromTable": "FactSales",
                 "fromColumn": "CustomerKey", "toTable": "DimCustomer", "toColumn": "CustomerKey"},
                {"name": "Sales_Reseller", "fromTable": "FactSales",
                 "fromColumn": "ResellerKey", "toTable": "DimReseller", "toColumn": "ResellerKey"},
                {"name": "Customer_Geography", "fromTable": "DimCustomer",
                 "fromColumn": "GeographyKey", "toTable": "DimGeography", "toColumn": "GeographyKey"},
                {"name": "Inventory_Date", "fromTable": "FactInventory",
                 "fromColumn": "DateKey", "toTable": "DimDate", "toColumn": "DateKey"},
                {"name": "Inventory_Product", "fromTable": "FactInventory",
                 "fromColumn": "ProductKey", "toTable": "DimProduct", "toColumn": "ProductKey"},
            ],
            "roles": [
                {"name": "EU Only", "modelPermission": "read",
                 "tablePermissions": [
                     {"name": "DimGeography",
                      "filterExpression": "DimGeography[Region] = \"Europe\""}]},
                {"name": "Admin", "modelPermission": "administrator"},
            ],
            "perspectives": [
                {"name": "Sales Overview", "tables": [
                    {"name": "FactSales", "columns": [], "measures": [
                        {"name": "Total Sales"}, {"name": "Margin"}]},
                    {"name": "DimDate"},
                    {"name": "DimProduct"},
                ]},
            ],
            "cultures": [
                {"name": "uk-UA", "translations": {"model": {"tables": [
                    {"name": "FactSales", "translatedCaption": "Продажі"}]}}},
            ],
            "expressions": [
                {"name": "ServerParam", "kind": "m",
                 "expression": "\"server\" meta [IsParameterQuery=true, Type=\"Text\"]"},
            ],
            "annotations": [
                {"name": "PBI_QueryOrder", "value": "[\"FactSales\"]"},
            ],
        },
    }


# ---------------------------------------------------------------------------
# Run the real shipped converter
# ---------------------------------------------------------------------------

def run_converter(bim_path: Path, out_root: Path, name: str) -> None:
    """Invokes the shipped BimToPbipCli tool via ``dotnet run``."""
    cmd = [
        "dotnet", "run", "--project", str(CLI_PROJECT), "-c", "Release", "--",
        "--bim", str(bim_path), "--out", str(out_root), "--dataset", name,
    ]
    print(f"  > {' '.join(cmd)}")
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.stdout:
        print(proc.stdout)
    if proc.returncode != 0:
        if proc.stderr:
            print(proc.stderr)
        fail(f"converter exited with code {proc.returncode}")


# ---------------------------------------------------------------------------
# Validation -- mirrors ConversionService.validatePbipProject
# ---------------------------------------------------------------------------

def _load_json(path: Path) -> object:
    raw = path.read_bytes()
    check(not raw.startswith(b"\xef\xbb\xbf"), f"no UTF-8 BOM: {path.name}")
    return json.loads(raw.decode("utf-8"))


def validate(out_root: Path, name: str) -> None:
    report_folder = f"{name}.Report"
    sm_folder = f"{name}.SemanticModel"
    pbip_path = out_root / f"{name}.pbip"
    report_dir = out_root / report_folder
    sm_dir = out_root / sm_folder
    definition_dir = sm_dir / "definition"

    required = {
        "<name>.pbip": pbip_path,
        "<name>.Report/": report_dir,
        "<name>.SemanticModel/": sm_dir,
        "definition.pbir": report_dir / "definition.pbir",
        "report.json": report_dir / "report.json",
        "definition.pbism": sm_dir / "definition.pbism",
        "definition/ (TMDL folder)": definition_dir,
    }
    for label, path in required.items():
        check(path.exists(), f"exists: {label}")

    # The semantic model is a TMDL definition/ folder, never a verbatim model.bim.
    check(not (sm_dir / "model.bim").exists(),
          "no verbatim model.bim (model is normalized to TMDL)")
    tmdl_files = list(definition_dir.rglob("*.tmdl"))
    check(len(tmdl_files) >= 1, f"definition/ has .tmdl files ({len(tmdl_files)} found)")
    check((definition_dir / "model.tmdl").exists(), "definition/model.tmdl exists")

    # .pbip : exactly a 'report' artifact, never a 'dataset' artifact.
    pbip = _load_json(pbip_path)
    check(pbip.get("version") == "1.0", ".pbip version is 1.0")
    artifacts = pbip.get("artifacts")
    check(isinstance(artifacts, list) and len(artifacts) >= 1, ".pbip has artifacts")
    art = artifacts[0]
    check("dataset" not in art,
          ".pbip artifact has NO 'dataset' (the key Power BI rejects)")
    check("report" in art, ".pbip artifact has a 'report' entry")
    check(art["report"].get("path") == report_folder,
          f".pbip report.path == '{report_folder}'")

    # definition.pbir : relative reference to the semantic model.
    pbir = _load_json(report_dir / "definition.pbir")
    ref = pbir.get("datasetReference", {}).get("byPath", {}).get("path")
    check(ref == f"../{sm_folder}", f".pbir byPath == '../{sm_folder}'")

    # report.json : parses and has at least one page.
    report = _load_json(report_dir / "report.json")
    sections = report.get("sections")
    check(isinstance(sections, list) and len(sections) >= 1,
          "report.json has >= 1 page")
    check(bool(sections[0].get("name")), "report.json page has a name")

    # report.json : the theme must be present, otherwise Power BI Desktop
    # crashes rendering the report ("...undefined (reading 'customTheme')").
    cfg = json.loads(report["config"])
    base_theme = cfg.get("themeCollection", {}).get("baseTheme", {}).get("name")
    check(bool(base_theme),
          "report.json config has themeCollection.baseTheme (theme render crash guard)")
    pkgs = report.get("resourcePackages", [])
    check(any(p.get("resourcePackage", {}).get("name") == "SharedResources"
              for p in pkgs),
          "report.json has a SharedResources base-theme package")

    # definition.pbism : parses.
    _load_json(sm_dir / "definition.pbism")

    # No file anywhere in the project may carry a UTF-8 BOM.
    for f in out_root.rglob("*"):
        if f.is_file():
            check(not f.read_bytes().startswith(b"\xef\xbb\xbf"),
                  f"no UTF-8 BOM anywhere: {f.relative_to(out_root)}")


# ---------------------------------------------------------------------------
# Consistency: the shipped tool normalizes via TOM and uses the templates
# ---------------------------------------------------------------------------

def check_tool_consistency() -> None:
    check(not (REPO / "powershell").exists(),
          "the retired PowerShell implementation is gone")

    proj = (TEMPLATES / "project.pbip").read_text(encoding="utf-8")
    check('"report"' in proj and '"dataset"' not in proj,
          "template project.pbip uses a 'report' artifact, not 'dataset'")

    csproj = (CLI_PROJECT / "BimToPbipCli.csproj").read_text(encoding="utf-8")
    check("pbip-templates" in csproj,
          "BimToPbipCli.csproj embeds the pbip-templates")
    check("Microsoft.AnalysisServices" in csproj,
          "BimToPbipCli.csproj references the TOM package")

    conv = (CLI_PROJECT / "ConversionService.cs").read_text(encoding="utf-8")
    check("DeserializeDatabase" in conv,
          "ConversionService.cs deserializes the .bim through TOM")
    check("TmdlSerializer" in conv and "SerializeDatabaseToFolder" in conv,
          "ConversionService.cs emits the model as TMDL")
    for marker in ("PbiToolsLocator", "runConvert", "ProcessRunner"):
        check(marker not in conv,
              f"ConversionService.cs no longer depends on pbi-tools ('{marker}' absent)")


# ---------------------------------------------------------------------------

def main() -> int:
    print("BIM -> PBIP internal test")
    print("-" * 60)

    check(TEMPLATES.is_dir(), "pbip-templates/ folder is present")
    check(CLI_PROJECT.is_dir(), "BimToPbipCli/ project is present")
    check_tool_consistency()

    name = "Adventure Works Complex"  # includes a space on purpose
    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        bim_path = tmp_path / "ComplexSalesModel.bim"
        bim_path.write_text(json.dumps(build_complex_bim(), indent=2),
                            encoding="utf-8")
        check(bim_path.stat().st_size > 2000, "complex .bim was generated")

        out_root = tmp_path / "out" / name
        run_converter(bim_path, out_root, name)
        ok("converter completed without error")

        validate(out_root, name)

    print("-" * 60)
    print(f"ALL {len(PASSED)} CHECKS PASSED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as exc:
        print("-" * 60)
        print(f"TEST FAILED: {exc}")
        sys.exit(1)
