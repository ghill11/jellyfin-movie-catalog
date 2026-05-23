# Style rules for jellyfin-movie-catalog

Language-agnostic style and discipline rules. Language-specific conventions live in their own files:

- C# conventions: `style_csharp.md`
- JavaScript conventions: `style_javascript.md`

When a new language enters the project (Python helper script, Go tool, etc.), it gets its own `style_<lang>.md`. This top-level file stays language-agnostic.

## Hard rules (load-bearing)

### No em-dashes anywhere

Em-dashes (U+2014, the long one) are forbidden everywhere in jellyfin-movie-catalog text: code, comments, docstrings, templates, commit messages, configuration files, rule files, notes. En-dashes (U+2013) likewise.

Use one of:

- a plain hyphen `-`
- a colon `:`
- parentheses
- rewrite the sentence

If a dash is genuinely needed for parenthetical phrasing, use a spaced hyphen ` - ` but prefer restructuring.

Origin: standing user preference (cross-project). Em-dashes read as a tell that AI wrote the text and are stylistically unwelcome. Treat as a hard lint rule. The originating apex-platform incident (a prior project) involved scrubbing 70+ em-dashes across a codebase including two database migrations and a user-facing template string; do not repeat that cost.

### No phantom stub or pending accounts

When a feature needs to track an external identifier (an email, an external username, an ID from a system that may not have a local account) that may not belong to a local account holder, add a nullable identifier column on the existing table rather than minting a stub account row to satisfy a foreign-key constraint.

This rule does not exercise on the current project (jellyfin-movie-catalog has no user accounts; the Jellyfin server owns auth). It is preloaded for any future feature on this harness that introduces a user table. The full rule, threat model, and worked patterns live in `database.md` §"No phantom stub or pending accounts".

Origin: cross-project rule validated on a prior project's external-subscriber feature design. See `database.md` for the full record.

## Naming conventions

### File and directory names

- One artifact per file at the project root: directories `plugin/` and `viewer/` separate the two deployables.
- Inside `plugin/`: C# follows `style_csharp.md`'s one-public-type-per-file rule.
- Inside `viewer/`: a small fixed set (`index.html`, `app.js`, `style.css`, `movies.json` when present in dev).
- Test files mirror the unit under test name in their language's convention.
- Plan files in `.claude/plans/` follow `^v\d+\.\d+\.\d+-[a-z0-9-]+\.md$` (the `vNEXT-<slug>.md` placeholder is allowed before the tag is decided). See `workflow.md` §"Plan file naming" for the mechanical enforcement.

### Identifier conventions (cross-language summary)

Each language file documents its own identifier rules:

- **C#**: `PascalCase` for types/methods/properties, `camelCase` for parameters/locals, `_camelCase` for private fields. See `style_csharp.md` §"Identifier conventions".
- **JavaScript**: `camelCase` for variables/functions, `PascalCase` for constructors, `UPPER_SNAKE_CASE` for module-level constants. See `style_javascript.md`.

The reason this top-level file does not enumerate them: identifier conventions are a language-property, not a project-property. When a third language enters the project, it gets its own conventions file, not a third bullet here.

### Identifying-data columns (preloaded from database.md)

When the project introduces persistent storage, identifying-data columns follow this pattern:

- Encrypted-at-rest columns end in `_enc` and are typed as the language's bytes-equivalent (e.g., C# `byte[]`, Python `LargeBinary`).
- The lookup-key paired with an `_enc` column is named `<base>_hash` typed as a fixed-width string (e.g., 64 chars for SHA-256).

Full structural rule (which `_enc` columns require a paired `_hash`, the threat model, the prohibition on plaintext identifying fields) lives in `database.md` §"PII column pairing". The bullets above are the naming-convention half; the rule file is the structural half.

This rule does not exercise on the current project (no persistent storage). It is preloaded so the moment a database lands, the naming is right by default.

## Comment philosophy

Comments explain WHY, not WHAT. Reserve comments for:

- Hidden constraints (a vendor API quirk, a platform-specific limitation, a third-party SDK gotcha)
- Subtle invariants (the order of operations that callers depend on)
- Workarounds for a specific bug (link to the issue or incident note)
- Behavior that would surprise a reader

If removing the comment would not confuse a future reader, do not write it. A comment that restates the next line's code is noise.

Module / class / type docstrings at the top of non-trivial files summarize the responsibility in 2-4 sentences. Trivial helpers and single-purpose scripts do not need them.

Bad:

```csharp
// increment the counter by 1
counter += 1;
```

Good:

```csharp
// Jellyfin's library event publisher does not de-duplicate adjacent change
// notifications; multiple item-added events for the same scan are common.
// We debounce to coalesce a burst into one push.
private readonly Debouncer _debouncer;
```

## Error handling

### At the boundary vs. internal code

Validate at the boundary: external input (HTTP responses, file contents, deserialized JSON), environment variables, configuration values. Trust internal code and framework guarantees.

Do NOT add validation that can never trigger (`if (alreadyValidatedArg is null)` when the parameter is annotated non-nullable and the caller is internal). That is noise. Do NOT add fallbacks for scenarios that cannot occur in the program's actual usage patterns; that is dead code.

### Surface vs. swallow

Catch exceptions only when you can do something useful with them:

- Log and re-raise (so the caller sees the failure with context attached)
- Log and return an explicit error result (when the boundary has a defined error shape)
- Log and convert (to a domain exception that callers expect)

A bare `catch { }` (or `except: pass` in Python, or `catch (Exception) { }` in C#) is a `code-reviewer` BLOCK.

### Background work and scheduled tasks

Log loudly. Exit / fail non-zero on hard failures so the scheduler records it. Do not retry indefinitely inside the task; the scheduler will run it again at the next interval.

For the Jellyfin plugin specifically: the debouncer's dispatched work (the GitHub Contents API push) catches and logs its own failures at the top, then exits the dispatched task. A failed push is not retried inside the dispatch; the next library event triggers a new debounce window which produces a fresh attempt.

## Logging

The language-specific files document the local logger conventions:

- C# logging: `style_csharp.md` §"Logging" (use `ILoggerFactory.CreateLogger(name)` not `ILogger<T>` due to a Jellyfin DI workaround; structured logging with named parameters; never log secrets).
- JavaScript logging: not formalized in this project (the viewer is a static page; `console.log` for diagnostics during development is fine; production browser-console output should be minimal).

Cross-language principles:

- Use `info` for normal lifecycle events, `warning` for recoverable anomalies, `error` for failures that block work.
- Never log secrets. The GitHub PAT is the project's only secret today; the rule extends to any future credential.
- Never log entire framework-supplied objects (a Jellyfin BaseItem with paths and metadata, a full HTTP response body). Log identifiers and counts.
- Diagnostic `print` / `Console.WriteLine` in shipping code is a `code-reviewer` WARN. Use the logger.

## Forbidden practices

- Committing secrets: `*.pem`, `*.key`, `.env*` with real values, any file containing a real PAT or API key. All matching paths are in `.gitignore`; keep them there. `.env.example` style placeholder files (committed, documenting required keys with empty values) are fine.
- Hardcoding paths or URLs in code. Configuration belongs in the appropriate config surface (the plugin's `Configuration`, environment variables for tools, named constants at the top of small scripts).
- Blocking I/O in a request, event handler, or hot loop without a timeout. Every HTTP call, file read on remote storage, and inter-process call has a timeout argument.
- Em-dashes (U+2014) or en-dashes (U+2013) anywhere in project text. See "Hard rules" above.
- Unsafe deserialization on untrusted input (`pickle.loads`, `yaml.load` without `SafeLoader`, BinaryFormatter in .NET, etc.). The viewer parses one JSON file from a known same-origin source; that boundary uses `JSON.parse` (the safe default).
- `eval`, `exec`, or any language equivalent on any input that could be influenced by user data.
- Forbidden-name tokens in identifiers: `_post_`, `_just_`, `_suppress_`, `_skip_`, `_bypass_`, `_hack_`, `_temp_`, `_workaround_`, `_quick_`, `_fixme_`, `_todo_<verb>_`. The name is the code admitting it is suspect. See `quality.md` §"Forbidden names" for the doctrine and the pre-commit hook for the mechanical scan.

## Cross-references

- C#-specific extensions and overrides: `style_csharp.md`
- JavaScript-specific extensions and overrides: `style_javascript.md`
- The forbidden-name doctrine and the broader tells-of-slipping-quality framework: `quality.md`
- File-creation discipline, plan-mode rules, design-question surfacing: `workflow.md`
- Persistent-storage naming conventions (preloaded, dormant for this project): `database.md`
