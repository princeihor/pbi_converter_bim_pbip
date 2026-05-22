# Documentation Changelog

An append-only record of documentation updates (ingests, updates, lint passes).

## [2026-05-21] ingest | Initial Documentation Wiki Created

**Summary**: Created comprehensive LLM Wiki documentation for the BIM-to-PBIP converter.

**Pages created**:
- `SCHEMA.md` — wiki maintenance instructions and conventions
- `index.md` — catalog of all wiki pages
- `wiki/overview.md` — what the tool does and why
- `wiki/concepts.md` — core concepts (PBIP, TMSL, TMDL, BOM, tokens, pipeline)
- `wiki/glossary.md` — terminology reference
- `wiki/quick-start.md` — get running in 5 minutes
- `wiki/installation.md` — setup for PowerShell and C#
- `wiki/cli-reference.md` — command-line parameters and exit codes
- `wiki/structure.md` — exact project folder layout and file formats
- `wiki/templates.md` — template files and token substitution
- `wiki/validation.md` — what the tool validates at each phase
- `wiki/troubleshooting.md` — common errors and fixes

**Structure**: 
- Concepts + Reference pages for deep dives
- Guides for getting started and troubleshooting
- All pages cross-referenced with links
- YAML frontmatter for tracking relationships

**Why**: Previous sessions completed the converter implementation (PBIP projects now open in Power BI Desktop). Documentation needed to be created using the LLM Wiki pattern — a persistent, interlinked markdown wiki that grows incrementally as the tool evolves.

**Next steps**:
- As the tool is updated (bug fixes, features, refactors), update the relevant wiki pages
- Periodically lint the wiki for stale claims, orphans, gaps, contradictions
- Use the index to navigate; follow cross-references to deepen understanding

---

## [2026-05-22] ingest | TOM normalization + TMDL output; PowerShell implementation retired

**Pages updated**: index.md, overview.md, concepts.md, structure.md, templates.md, validation.md, architecture.md, cli-reference.md, installation.md, quick-start.md, troubleshooting.md, testing.md, glossary.md

**Why**: The converter was substantially reworked. Three changes:

1. **TOM normalization** — the model is no longer copied verbatim. The converter now deserializes the input `.bim` through the Tabular Object Model (`Microsoft.AnalysisServices.Tabular.JsonSerializer.DeserializeDatabase`), which rebuilds a consistent metadata object graph. This fixes the Power BI Desktop edit/refresh failure *"Model object-map is not consistent with the metadata-object graph"* that the old verbatim copy caused.
2. **TMDL output** — the semantic model is no longer a single `model.bim`. It is a `<name>.SemanticModel/definition/` folder of TMDL text files written by TOM (`TmdlSerializer.SerializeDatabaseToFolder`).
3. **PowerShell retired** — the `powershell/` folder (`bim-to-pbip.ps1`, `Fix-PbipBom.ps1`, `.cmd` launchers) was deleted. The tool ships only as the self-contained C# `.exe`, which bundles the TOM library (`Microsoft.AnalysisServices.NetCore.retail.amd64`, Windows-x64).

**Details**:
- `index.md`: removed dangling entries (`powershell-impl.md`, `powershell-vs-csharp.md`, `csharp-impl.md`, `examples.md`, `changelog.md`); catalog now matches the 12 pages on disk.
- `concepts.md`: TMSL is now described as input-only; added a first-class TMDL concept and a new "TOM normalization" concept.
- `glossary.md`: added TOM; rewrote the TMDL entry (it is now the output format, not "not yet supported").
- `troubleshooting.md`: added an entry for the object-map error explaining the current tool fixes it; removed the "PowerShell Script Doesn't Run" and "GUI Doesn't Open (PowerShell)" sections and the `Fix-PbipBom.ps1` references.
- `architecture.md`: removed the obsolete "Two Implementations" and "Why we avoid pbi-tools" sections; the tool now round-trips the model through TOM in-process; the TMDL "future support" note is now realized.
- `cli-reference.md`, `installation.md`, `quick-start.md`: only the C# `.exe` remains; documented options `--bim`, `--out`, `--dataset`, `--ui`, `--help`.
- `validation.md`, `testing.md`: the pipeline is now load+normalize / assemble / validate; the internal test runs the real C# converter via `dotnet run` and validates the TMDL project (no byte-identical `model.bim` assertion).
- Bumped `last-updated` to 2026-05-22 on every updated page and fixed `relates-to` lists that pointed to removed pages.

---

## How to Update This Wiki

See `SCHEMA.md` for:
- The three operations: **Ingest** (update when code changes), **Query** (find info), **Lint** (health check)
- Page categories and conventions
- Workflow example

Keep this log current as you update the wiki.
