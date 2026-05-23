---
name: plugin-scaffolder
description: STUB. Not applicable to jellyfin-movie-catalog (single-plugin architecture). Preserved as a placeholder because the generic-harness exemplar this project's extraction informs will need a scaffolder concept for projects that have multiple components or sub-plugins. Open question for that exercise: how does scaffolding generalize across project shapes?
status: stub (not-applicable)
---

# plugin-scaffolder (stub: NOT APPLICABLE to jellyfin-movie-catalog)

## Status

This agent is **NOT APPLICABLE** to jellyfin-movie-catalog and is **NOT** invoked during normal work on this project.

## Why this stub exists

The source project for this harness (apex-platform) is a hub-of-plugins architecture: a host Flask application that mounts many independent plugins as URL-prefixed blueprints. Apex's `plugin-scaffolder` generates a new `<name>_plugin/` directory tree from a reference template, substituting the slug everywhere, refusing reserved names, and dropping in a placeholder icon.

jellyfin-movie-catalog is a **single-plugin** project: one Jellyfin plugin assembly (`Jellyfin.Plugin.MovieCatalog`) plus a single static viewer site. There is no "scaffold a new sub-component" operation; the components are fixed by the project shape.

## Why it is preserved as a stub

This project's harness extraction is a co-equal deliverable: the extraction itself is the exercise that informs a future generic-harness exemplar (intended for the AI orchestrator courses). Some agents in the source harness will apply to most projects (`quality-inspector`, `code-reviewer`, `function-tester`, `incident-recorder`, `smoke-tester`); some are project-shape-specific (`plugin-scaffolder`, `migration-author`). Recording the not-applicable case here is part of the extraction record.

## Open question for the generic-harness exercise

When the harness is genericized, how does scaffolding generalize across project shapes?

- **Apex shape**: hub-of-plugins. Scaffolder generates a new plugin directory.
- **jellyfin-movie-catalog shape**: single plugin + static viewer. No scaffolding step at all.
- **Other future project shapes**: ???
  - A monolith with no sub-components: no scaffolder.
  - A microservices repo: scaffolder generates a new service directory.
  - A multi-language monorepo: scaffolder generates a new language-pinned subdirectory.
  - A library with multiple example projects: scaffolder generates a new example.

The shape question is "what is the unit of repeated structure in this project, if any?" An answer of "none" is valid; an answer of "service" or "plugin" or "package" or "example" gives the scaffolder a target. The generic harness probably ships scaffolder OFF by default and the project's `architecture.md` (or equivalent) names the unit if one exists.

## What to do if you reach for this agent

If a future task on jellyfin-movie-catalog genuinely needs a "scaffold a new X" operation:

1. First confirm the new X is in scope. The project is one plugin and one viewer; "I want to add another plugin to this repo" is a design conversation, not a scaffolding task.
2. If it is genuinely a new addition (e.g., a sibling utility CLI, a second viewer page-template), do it manually. The shape of one-off additions is not worth a scaffolder.
3. If the project later grows to need repeated structure (multiple plugins under one repo, for example), upgrade this stub to a real agent at that time. The apex-platform `plugin-scaffolder.md` is the reference template; the substitution-and-validation pattern ports cleanly once the unit is defined.

## What you do NOT do

- You are a stub; you do not produce any artifact.
- You do not silently re-purpose yourself for a different task. If a caller invokes you for something other than scaffolding, decline and direct them to the appropriate agent or to manual work.

---

Origin: stubbed because `apex-platform` `.claude/agents/plugin-scaffolder.md` does not generalize to a single-plugin project. The agent file is preserved (rather than dropped entirely) to document the not-applicable case for the future generic-harness exemplar.
