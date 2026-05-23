# jellyfin-movie-catalog - Agent instructions

> Entry point for AI assistants working on `ghill11/jellyfin-movie-catalog`. Mature rules graduate to `.claude/rules/`; CLAUDE.md stays compact through graduation discipline. The `Current state` section below is the only scratchpad area, and it updates only on user approval after a change has been implemented and tested.

## Project summary

A Jellyfin Server 10.11 plugin (C#, .NET 9.0) plus a static GitHub Pages viewer. Solves a specific personal problem: when away from home and shopping for movies, see what's already in the home library without round-tripping the home network.

**How it works**: the plugin subscribes to Jellyfin's library events (`ItemAdded`, `ItemUpdated`, `ItemRemoved`), debounces a configurable quiet window, and pushes a `movies.json` snapshot to a configured GitHub repo via the Contents API. The viewer is plain HTML + JS hosted on GitHub Pages, fetches that JSON, and renders a sortable + title-searchable table.

**Two artifacts, one repo**: `plugin/` (C# class library, ships as a sideload zip via GitHub Releases) and `docs/` (static site, served by GitHub Pages from `main` branch `/docs` folder).

## Current state

Bootstrapping. No tag yet; pre-v0.1.0. Plan at `.claude/plans/v0.1.0-bootstrap.md` covers the initial scaffold (harness port from apex-platform + viewer + plugin + end-to-end against a sacrificial test GitHub repo). The very first deliverable is the `.claude/` harness itself; the second is the viewer; the third is the plugin; the fourth is end-to-end verification. The harness port is being captured at `EXTRACTION.md` as a co-equal deliverable that will inform a future generic-harness exemplar.

## Reference notes (on-demand)

Project-specific operational, architectural, and historical context lives at `.claude/notes/<file>.md` (catalog: `.claude/notes/INDEX.md`). These are git-tracked reference material consulted on demand. NOT auto-loaded into every session's context.

## IMPORTANT constraints

- **IMPORTANT: Never commit secrets.** `.env`, `.env.test.local`, `*.pem`, `*.key`, `*.pat` are all in `.gitignore`. The GitHub PAT used for the plugin's push capability lives in either (a) the plugin's runtime configuration on the Jellyfin server's disk, or (b) `D:\jf-dev\.env.test.local` during local dev. Never in this repo, never in commit history, never in logs.

- **IMPORTANT: Plan mode first for multi-file work.** Plan mode is required when ANY of: the work creates a new file, the work modifies 2+ files, or the work introduces a new decision not covered by an existing approved plan. Single-line fixes inside one already-opened file proceed without plan mode. See `.claude/rules/workflow.md` §"Plan-mode first for multi-file work".

- **IMPORTANT: Trunk + tags promotion.** `main` is always shippable. Multi-step WIP goes on a feature branch (`feature/<short-name>`). Tags (`v<major>.<minor>.<patch>`) are immutable once pushed. The `release.yml` workflow auto-creates a GitHub Release on tag push.

- **IMPORTANT: No em-dashes (U+2014) or en-dashes (U+2013) anywhere.** See `.claude/rules/style.md`.

- **IMPORTANT: The plugin is sideloaded, not git-pushed.** Jellyfin loads plugins from its own `plugins/<name>/` directory inside the Jellyfin data dir, not from this repo. The release artifact (`.zip` of dll + meta.json) is the deploy artifact; the user downloads it and drops it into the Jellyfin plugins folder. There is no "git pull on the Jellyfin server" deploy pattern here.

- **IMPORTANT: The viewer ships via GitHub Pages auto-deploy.** Any push to `main` that touches `docs/` triggers Pages to rebuild. The plugin's runtime PUTs to `docs/movies.json` are commits to main, so library changes auto-propagate to the live viewer with no manual deploy step.

- **IMPORTANT: PAT scope is narrow.** The plugin's GitHub PAT MUST be a fine-grained PAT scoped to ONLY the target repo, with `Contents: read+write` and nothing else. Blast radius if the Jellyfin server is ever compromised: that one repo can be vandalized. No account-level access, no other repo access. Documented on the plugin's settings page.

- **IMPORTANT: Do not modify the apex-platform repo from here.** This project ports doctrine from apex-platform; it does not modify it. Cross-project changes (e.g., backporting the language-file split to apex itself) happen in apex's own repo as a separate session.

## Essential commands

### Git daily-driver

```bash
git pull --rebase origin main
git push origin main
git status
```

### Building the plugin

```bash
# Add /c/Program Files/dotnet to PATH if not present in current shell:
export PATH="/c/Program Files/dotnet:$PATH"

dotnet build plugin/Jellyfin.Plugin.MovieCatalog.csproj -c Release
dotnet test plugin/tests/Jellyfin.Plugin.MovieCatalog.Tests.csproj
```

### Provisioning a dev Jellyfin instance

```powershell
# Download portable Jellyfin (~80MB)
Invoke-WebRequest `
  https://repo.jellyfin.org/files/server/windows/stable/v10.11.6/jellyfin_10.11.6_windows-amd64.zip `
  -OutFile D:\jf-dev\jellyfin.zip
Expand-Archive D:\jf-dev\jellyfin.zip -DestinationPath D:\jf-dev\jellyfin\
New-Item -ItemType Directory D:\jf-dev\data, D:\jf-dev\cache, D:\jf-dev\media\Movies

# Start (port 8097 avoids default 8096)
D:\jf-dev\jellyfin\jellyfin.exe `
  --datadir D:\jf-dev\data `
  --cachedir D:\jf-dev\cache `
  --port 8097
```

### Sideloading the plugin (dev loop)

```bash
# Stop jellyfin.exe first (Stop-Process or kill the PID), then:
mkdir -p /d/jf-dev/data/plugins/MovieCatalog
cp plugin/bin/Release/net9.0/Jellyfin.Plugin.MovieCatalog.dll /d/jf-dev/data/plugins/MovieCatalog/
cp plugin/meta.json /d/jf-dev/data/plugins/MovieCatalog/

# Start jellyfin.exe again; poll for ready:
until curl -sf http://localhost:8097/System/Info/Public > /dev/null; do sleep 1; done
```

### Promoting a build (tag-based)

```bash
# 1. Write release notes (MANDATORY; pre-push hook enforces).
$EDITOR release-notes/v0.x.y.md

# 2. Commit and push the notes file.
git add release-notes/v0.x.y.md
git commit -m "release notes: v0.x.y"
git push origin main

# 3. Create the annotated tag (title is the first line).
git tag -a v0.x.y -m "v0.x.y - <short subject>"

# 4. Push the tag; release.yml builds, zips, checksums, and creates
#    the GitHub Release with the dll artifact attached.
git push origin v0.x.y

# 5. Smoke against the local portable Jellyfin via the smoke-tester
#    agent; appends a PASS line to .claude/notes/deploys/dev-log.md.
```

### Purging the dev Jellyfin instance (end of session)

```powershell
# Stop jellyfin.exe first, then:
Remove-Item -Recurse -Force D:\jf-dev\
```

Zero residue: no registry entries, no AppData, no Program Files install.

## Rules

@.claude/rules/style.md
@.claude/rules/style_csharp.md
@.claude/rules/style_javascript.md
@.claude/rules/workflow.md
@.claude/rules/quality.md
@.claude/rules/architecture.md
@.claude/rules/deployment.md
@.claude/rules/frontend.md
@.claude/rules/database.md
@.claude/rules/auth.md

## Subagents

Six functional agents plus two stubs. Each has its own spec at `.claude/agents/<name>.md`.

- `quality-inspector` - Enforces `.claude/rules/quality.md` across all 7 phases. Read-only verdicts (PASS / DISCUSS / BLOCK). Wired into `pre-push` on tag refspecs as the hard gate.
- `code-reviewer` - Technical review of diffs: C# discipline (nullable refs, async/await, NuGet pinning), JS discipline (no untrusted innerHTML, no CDNs, no build step), no PAT in logs. Severity-tagged findings (BLOCK / WARN / NIT).
- `function-tester` - Per-handler tests using NUnit (C#) or vanilla JS test harness (viewer). Mocks `ILibraryManager` / `IHttpClientFactory` and the GitHub API at the boundary.
- `incident-recorder` - Captures structured triage records into `.claude/notes/incidents/<slug>.md` when a tell fires and the proper solution lands.
- `smoke-tester` - Post-release artifact verifier. Downloads the GitHub release zip, checksums it, sideloads into the local portable Jellyfin, restarts, GETs /Plugins, confirms load. Appends to `.claude/notes/deploys/dev-log.md`.
- `integration-tester` - **stub** until end-to-end tests across plugin + viewer + real GitHub become meaningful.
- `plugin-scaffolder` - **stub-and-flag**. Apex's hub-of-plugins scaffolder concept doesn't generalize cleanly to a single-plugin project. Preserved as a generic-harness-exemplar question.
- `migration-author` - **stub-and-flag**. Apex's Alembic SQL migration author is stack-specific. Preserved for the same reason.

### Quality gate (active once hooks are installed)

Three tiers of enforcement plus an assistant-side surfacing pair.

- **pre-commit** (`scripts/hooks/pre-commit`; mechanical, coaching, never blocks). Pass 1 forbidden-name scan, Pass 2 harness-doctrine lexicon, Pass 3 plan filename regex, Pass 4 plan design-question lexicon, Pass 5 `dotnet test` runner + skip-attribute scan. Appends findings to `.claude/notes/quality-debt.md`.
- **pre-push** (`scripts/hooks/pre-push`; tag refspecs only; hard gate). Annotated-tag check, release-notes file check, quality-inspector subagent invocation.
- **Assistant surfacing** (per `.claude/rules/workflow.md`): surface harness debt explicitly after every commit that produces one; surface outstanding quality debt before giving deploy commands.

Install hooks per clone: `bash scripts/install-quality-hooks.sh`.

## Project layout

```
jellyfin-movie-catalog/
├── CLAUDE.md
├── README.md
├── LICENSE
├── EXTRACTION.md            (the harness-extraction log; co-equal deliverable)
├── .gitignore, .editorconfig, global.json
├── plugin/                  (C# class library)
│   ├── Jellyfin.Plugin.MovieCatalog.csproj
│   ├── *.cs (Plugin, PluginConfiguration, Debouncer, ...)
│   ├── Web/configPage.html  (embedded resource)
│   ├── meta.json
│   └── tests/
├── docs/                  (static GitHub Pages)
│   ├── index.html, app.js, style.css
│   └── movies.json          (plugin overwrites this)
├── release-notes/           (one file per tag)
├── .claude/                 (harness: rules, agents, notes, plans, hooks)
├── .github/workflows/       (dotnet.yml, release.yml)
└── scripts/hooks/           (pre-commit, pre-push, install-quality-hooks.sh)
```
