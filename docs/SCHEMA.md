# BIM-to-PBIP Converter Documentation Schema

This is a wiki of the BIM-to-PBIP converter tool — a structured, interlinked collection of markdown pages that stays current as the codebase evolves.

## Purpose

This wiki documents **what the tool does, how it works, and how to use it**. It's a single source of truth alongside the code itself. When the tool changes, the wiki changes too.

## Operations

### Ingest
When you update the converter (new features, bug fixes, architectural changes):
1. Identify what changed (code, functionality, constraints)
2. Update relevant wiki pages (concepts, guides, reference)
3. Update the index if new pages were created
4. Append an entry to `log.md` with date, change summary, and affected pages

### Query
When you have a question about the converter:
1. Check `index.md` to find relevant wiki pages
2. Read the pages; they cross-reference each other
3. If the answer isn't in the wiki, it's a gap — file it in log.md for a future lint pass

### Lint
Periodically health-check the wiki:
- Look for stale claims (has the code changed since this page was written?)
- Check for orphan pages (no inbound links)
- Find missing concepts (something important mentioned but not documented?)
- Verify cross-references still point to valid pages
- Check for contradictions between pages

## Page Categories

### Architecture & Concepts
- **Concepts** — core ideas (PBIP, TMSL, TMDL, TOM normalization, tokens, UTF-8 BOM, three-phase pipeline)
- **Structure** — the exact folder layout and file format (from pbip-templates/)

### User Guides
- **Quick Start** — get running in 5 minutes
- **Installation** — setup for the C#/.NET tool
- **Command-line Reference** — all CLI parameters explained
- **Troubleshooting** — common errors and fixes

### Implementation Details
- **Architecture** — the C# tool's design, classes, and pipeline
- **Validation** — what the tool checks before reporting success
- **Testing** — internal_test.py and what it validates

### Reference
- **Changelog** — history of breaking changes and features
- **Glossary** — unfamiliar terms explained
- **Templates** — the pbip-templates/ folder structure and token substitution

## Conventions

- **Links**: Use relative markdown links (e.g. `[Concepts](wiki/concepts.md)`)
- **Code blocks**: Always include the language (e.g. ` ```powershell ` or ` ```csharp `)
- **Cross-references**: When describing a concept that has its own page, link to it (e.g. "See [Three-Phase Pipeline](wiki/three-phase-pipeline.md)")
- **Frontmatter**: Wiki pages include YAML frontmatter for tracking:
  ```yaml
  ---
  title: Page Title
  last-updated: YYYY-MM-DD
  relates-to: [other-page, another-page]
  tags: [category, subtopic]
  ---
  ```

## Files

- `index.md` — catalog of all wiki pages
- `log.md` — append-only record of updates (ingests, lint passes, etc.)
- `wiki/` — the actual markdown pages organized by category
- `raw/` — source materials referenced by the wiki (original README, REFERENCE.md, etc.)
- `SCHEMA.md` — this file (the instructions)

## Workflow Example

**Scenario**: You fix a bug where relative paths in `definition.pbir` used backslashes instead of forward slashes, breaking the report reference on Unix. The fix is simple: always use `/` in that template.

**Ingest steps**:
1. Update `wiki/validation.md` to note that forward slashes are validated
2. Update `wiki/templates.md` to document the exact requirement
3. Update `wiki/troubleshooting.md` with an entry about this failure mode if it's user-facing
4. Update `index.md` if any of those pages are new
5. Append to `log.md`:
   ```
   ## [2026-05-21] ingest | Fixed: relative paths use forward slashes in definition.pbir
   
   **Pages updated**: validation.md, templates.md, troubleshooting.md
   
   **Why**: Bug fix in C# ConversionService where path separators weren't normalized. Now always uses forward slashes per PBIP spec.
   ```

## How to Keep the Wiki Current

Each time you touch the code:
- **For features**: add new concept pages, update architecture pages, update CLI reference
- **For bugs**: add troubleshooting entries, update validation docs if the check changed
- **For refactors**: update implementation detail pages, verify cross-references still work
- **For test additions**: update testing.md

The goal: someone reading the wiki + looking at the code should fully understand what's happening. The wiki is not a replacement for code review — it's a layer on top that captures the *why*, the *how*, and the *what to watch out for*.
