# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This repository uses **OpenSpec** — a structured workflow framework for AI-assisted software development. It is not an application codebase; it is a specification and change management workspace. All development work flows through four phases: Explore → Propose → Apply → Archive.

## OpenSpec CLI Commands

The `openspec` CLI must be available on PATH. Core commands:

```bash
openspec list --json                                          # List all active changes
openspec new change "<name>"                                  # Create a new change
openspec status --change "<name>" --json                      # Check artifact completion status
openspec instructions <artifact-id> --change "<name>" --json  # Get instructions for an artifact
openspec instructions apply --change "<name>" --json          # Get implementation instructions
```

## Slash Commands

| Command | Purpose |
|---|---|
| `/opsx:explore` | Investigate an idea or problem without implementing anything |
| `/opsx:propose "description"` | Create a new change with full artifact scaffolding |
| `/opsx:apply` | Implement tasks from an active change |
| `/opsx:archive` | Finalize and archive a completed change |

## Workflow Architecture

### Change Lifecycle

1. **Explore** — discovery, problem mapping, no implementation
2. **Propose** — creates three core artifacts under `openspec/changes/<name>/`:
   - `proposal.md` — what & why
   - `design.md` — how
   - `tasks.md` — implementation steps
3. **Apply** — reads context artifacts, works through tasks, checks off completed items
4. **Archive** — validates completion, syncs delta specs to `openspec/specs/`, moves change to `openspec/changes/archive/YYYY-MM-DD-<name>/`

### Directory Layout

```
openspec/
  changes/          # Active changes (one subdirectory per change)
    archive/        # Completed changes (timestamped)
  specs/            # Main specification library (capabilities and their specs)
.claude/
  commands/opsx/    # Slash command definitions (explore, propose, apply, archive)
  skills/           # Reusable skill implementations invoked by commands
```

### Artifact Dependency Model

Artifacts have dependencies that must be satisfied before downstream artifacts can begin. The `openspec status` command tracks completion state (`done` vs pending). Tasks use markdown checkboxes (`- [ ]` / `- [x]`) for progress tracking.

### Delta Specs

A change may include `openspec/changes/<name>/specs/` containing spec modifications. On archive, these delta specs are synced to the main `openspec/specs/` tree.
