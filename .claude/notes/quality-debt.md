# Quality debt log

Append-only record of findings caught by the `pre-commit` hook.
See `.claude/rules/quality.md` for the doctrine. Pre-push (tag) re-verifies
entries against the current working tree; cleared entries pass through.

## Findings

## Harness doctrine findings
2026-05-23T01:22Z | .claude/rules/workflow.md:76 | harness_doctrine "obvious pattern" | `**There is no "obvious pattern" carve-out.** "This is just two more files like the ones I already drafted" is exactly th`
2026-05-23T01:22Z | .claude/rules/style_csharp.md:85 | harness_doctrine  warrant  | `- `unsafe` blocks in this plugin. None of the work is performance-critical enough to warrant it.`
2026-05-23T01:22Z | .claude/agents/plugin-scaffolder.md:39 | harness_doctrine  genuinely needs  | `If a future task on jellyfin-movie-catalog genuinely needs a "scaffold a new X" operation:`
2026-05-23T01:22Z | .claude/agents/migration-author.md:53 | harness_doctrine  genuinely needs  | `If a future task on jellyfin-movie-catalog genuinely needs schema authorship (the project gains a local SQLite cache, a `
2026-05-23T01:22Z | .claude/agents/function-tester.md:51 | harness_doctrine  genuinely needs  | `If a future feature genuinely needs jsdom or a JS test framework, that is a design decision the user approves explicitly`

## Plan naming findings

## Plan design-question findings

## Failing test findings

## Undocumented skip findings
