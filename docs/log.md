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

## How to Update This Wiki

See `SCHEMA.md` for:
- The three operations: **Ingest** (update when code changes), **Query** (find info), **Lint** (health check)
- Page categories and conventions
- Workflow example

Keep this log current as you update the wiki.
