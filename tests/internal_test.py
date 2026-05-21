#!/usr/bin/env python3
"""Internal end-to-end test for the BIM -> PBIP converter.

What this proves
----------------
* A *complex* model.bim (many tables, measures, relationships, roles,
  perspectives, cultures) is converted into a PBIP project.
* The produced project has the exact structure Power BI Desktop requires --
  the same rules whose violation produced the real "Cannot read .pbip" errors
  (BOM, and `artifacts[].dataset` instead of `artifacts[].report`).

How it stays honest
-------------------
The PBIP structure is NOT redefined here. The test reads the *same*
`pbip-templates/` folder that the shipped tools (`powershell/bim-to-pbip.ps1`
and `BimToPbipCli`) read, and applies the *same* assembly recipe (copy the
.bim as model.bim, write four token-substituted metadata files). It also
greps the PowerShell tool to confirm it uses those same templates and tokens,
so the test and the tool cannot silently drift apart.

This container has only Python, so the PowerShell/C# tools and Power BI
Desktop cannot be launched here; this test validates the structure/contract
they all share. Run:  python3 tests/internal_test.py
"""

from __future__ import annotations

import json
import re
import secrets
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
TEMPLATES = REPO / "pbip-templates"

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
# Conversion recipe -- the same steps the shipped tools perform
# ---------------------------------------------------------------------------

def convert(bim_path: Path, out_root: Path, name: str) -> None:
    report_folder = f"{name}.Report"
    sm_folder = f"{name}.SemanticModel"
    report_dir = out_root / report_folder
    sm_dir = out_root / sm_folder
    out_root.mkdir(parents=True, exist_ok=True)
    report_dir.mkdir(parents=True, exist_ok=True)
    sm_dir.mkdir(parents=True, exist_ok=True)

    # The .bim IS the TMSL model -> stored verbatim as model.bim.
    # encoding="utf-8" (not utf-8-sig) guarantees no BOM is written.
    (sm_dir / "model.bim").write_text(
        bim_path.read_text(encoding="utf-8"), encoding="utf-8")

    pbip = (TEMPLATES / "project.pbip").read_text(encoding="utf-8")
    (out_root / f"{name}.pbip").write_text(
        pbip.replace("{{REPORT_FOLDER}}", report_folder), encoding="utf-8")

    pbism = (TEMPLATES / "SemanticModel" / "definition.pbism").read_text(encoding="utf-8")
    (sm_dir / "definition.pbism").write_text(pbism, encoding="utf-8")

    pbir = (TEMPLATES / "Report" / "definition.pbir").read_text(encoding="utf-8")
    (report_dir / "definition.pbir").write_text(
        pbir.replace("{{SEMANTIC_MODEL_FOLDER}}", sm_folder), encoding="utf-8")

    report = (TEMPLATES / "Report" / "report.json").read_text(encoding="utf-8")
    (report_dir / "report.json").write_text(
        report.replace("{{PAGE_NAME}}", secrets.token_hex(10)), encoding="utf-8")


# ---------------------------------------------------------------------------
# Validation -- mirrors Test-PbipStructure in bim-to-pbip.ps1
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

    required = {
        "<name>.pbip": pbip_path,
        "<name>.Report/": report_dir,
        "<name>.SemanticModel/": sm_dir,
        "definition.pbir": report_dir / "definition.pbir",
        "report.json": report_dir / "report.json",
        "definition.pbism": sm_dir / "definition.pbism",
        "model.bim": sm_dir / "model.bim",
    }
    for label, path in required.items():
        check(path.exists(), f"exists: {label}")

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

    # definition.pbism : parses.
    _load_json(sm_dir / "definition.pbism")

    # model.bim : parses, is the complex model, byte-identical to the input.
    model = _load_json(sm_dir / "model.bim")
    check("model" in model, "model.bim has a 'model' member")
    check(len(model["model"]["tables"]) == 7, "model.bim kept all 7 tables")

    # No file anywhere in the project may carry a UTF-8 BOM.
    for f in out_root.rglob("*"):
        if f.is_file():
            check(not f.read_bytes().startswith(b"\xef\xbb\xbf"),
                  f"no UTF-8 BOM anywhere: {f.relative_to(out_root)}")


# ---------------------------------------------------------------------------
# Consistency: the shipped PowerShell tool uses these same templates/tokens
# ---------------------------------------------------------------------------

def check_tool_consistency() -> None:
    ps1 = (REPO / "powershell" / "bim-to-pbip.ps1").read_text(encoding="utf-8")
    for token in ("pbip-templates", "{{REPORT_FOLDER}}",
                  "{{SEMANTIC_MODEL_FOLDER}}", "{{PAGE_NAME}}",
                  ".SemanticModel", ".Report", "Test-PbipStructure"):
        check(token in ps1, f"bim-to-pbip.ps1 references '{token}'")
    for marker in ("pbi-tools.exe", "Resolve-PbiTools", "Install-PbiToolsAuto",
                   "PbiToolsExe"):
        check(marker not in ps1,
              f"bim-to-pbip.ps1 no longer depends on pbi-tools ('{marker}' absent)")

    proj = (TEMPLATES / "project.pbip").read_text(encoding="utf-8")
    check('"report"' in proj and '"dataset"' not in proj,
          "template project.pbip uses a 'report' artifact, not 'dataset'")

    # The C# tool consumes the same templates and shares the structure.
    cs_dir = REPO / "BimToPbipCli"
    csproj = (cs_dir / "BimToPbipCli.csproj").read_text(encoding="utf-8")
    check("pbip-templates" in csproj,
          "BimToPbipCli.csproj embeds the pbip-templates")
    conv = (cs_dir / "ConversionService.cs").read_text(encoding="utf-8")
    for marker in ("PbiToolsLocator", "runConvert", "ProcessRunner"):
        check(marker not in conv,
              f"ConversionService.cs no longer depends on pbi-tools ('{marker}' absent)")
    check("validatePbipProject" in conv,
          "ConversionService.cs validates the assembled project")


# ---------------------------------------------------------------------------

def main() -> int:
    print("BIM -> PBIP internal test")
    print("-" * 60)

    check(TEMPLATES.is_dir(), "pbip-templates/ folder is present")
    check_tool_consistency()

    name = "Adventure Works Complex"  # includes a space on purpose
    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        bim_path = tmp_path / "ComplexSalesModel.bim"
        bim_path.write_text(json.dumps(build_complex_bim(), indent=2),
                            encoding="utf-8")
        check(bim_path.stat().st_size > 2000, "complex .bim was generated")

        out_root = tmp_path / "out" / name
        convert(bim_path, out_root, name)
        ok("conversion completed without error")

        validate(out_root, name)

        # The input .bim must survive the round-trip byte-for-byte.
        check((out_root / f"{name}.SemanticModel" / "model.bim").read_text("utf-8")
              == bim_path.read_text("utf-8"),
              "model.bim is byte-identical to the input .bim")

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
