# Make the dev, release and config docs match reality

## Context

While retiring the dev Jellyfin on 2026-10-28, a survey showed that several harness files describe a dev layout, a release artifact and a config schema that differ from what actually exists. As written, `smoke-tester` cannot download the release zip, and if it could it would sideload into the wrong folder and start Jellyfin against an empty data dir. The phase-6 gate depends on that agent. The cause: most of these files were written before the dev instance was provisioned and before the plugin's config shape settled, while DEV_SETUP.md and the code were written from the real run. The fix makes the docs match the code and DEV_SETUP.md. It does not change any code.

Scope (decided by user 2026-10-28): dev layout, zero-residue claim, release zip name, smoke-tester's plugin check, config field names. CLAUDE.md is corrected in place (decided by user 2026-10-28), not reduced to a pointer. The dev install pins Jellyfin 10.11.11, the version the Unraid production server runs (decided by user 2026-10-28), replacing the 10.11.9 pin (404 on repo.jellyfin.org) and the 10.11.6 in `deployment.md`.

## Safety: the production install is untouched

Only docs and harness files change. No file under `plugin/`, no `configPage.html`, no `meta.json`, no `.github/workflows/`, nothing under `docs/`. No tag is created, so no new artifact reaches Unraid. The config-name fix moves the docs to the code's names; the code's property names (`Owner`, `Repo`, `Branch`, `JsonPath`, `PatToken`, `DebounceSeconds`) stay as they are, so the settings Jellyfin saved on Unraid keep binding.

## Facts the docs are corrected to

- **Dev layout** (DEV_SETUP.md, confirmed by the 2026-10-28 survey): exe `D:\jf-dev\jellyfin\jellyfin\jellyfin.exe`; start with `--datadir D:\jf-dev\data --cachedir D:\jf-dev\cache` (no `--port`; the port lives in `data\config\network.xml` as `<InternalHttpPort>`, default 8096); plugins `data\plugins\MovieCatalog\`; plugin config holding the PAT `data\plugins\configurations\Jellyfin.Plugin.MovieCatalog.xml`; logs `data\log\log_YYYYMMDD.log`.
- **Host residue:** accepting the Windows Firewall prompt on first run creates two inbound Allow rules named `Jellyfin.Server`. Tear-down removes them from an elevated PowerShell.
- **Release artifact** (`.github/workflows/release.yml`): `jellyfin-movie-catalog-<tag>.zip`, with the DLL and `meta.json` at the zip root. Matches INSTALL.md and the published v0.1.0 asset.
- **Plugin identity** (`plugin/Plugin.cs`, `plugin/meta.json`): Name `Movie Catalog`, Id `7476643a-a7aa-47eb-8116-5856ce955bb2`, version `0.1.0.0` for tag `v0.1.0`.
- **Config schema** (`plugin/PluginConfiguration.cs`, `plugin/Web/configPage.html`): `Owner`, `Repo`, `Branch` (`main`), `JsonPath` (`docs/movies.json`), `PatToken`, `DebounceSeconds` (30); form ids `owner`, `repo`, `branch`, `jsonPath`, `patToken`, `debounceSeconds` (min 5). There is no cron setting; `ResyncScheduledTask.GetDefaultTriggers()` is empty, so a full resync is manual (the "Run" button on the `Resync Movie Catalog Now` task) or user-scheduled in Jellyfin's task UI.

## Changes

1. Copy this plan to `.claude/plans/vNEXT-dev-docs-match-reality.md`.
2. `.claude/agents/smoke-tester.md`: zip pattern and the line 21 artifact name to `jellyfin-movie-catalog-$tag.zip`; plugin dir to `data\plugins\MovieCatalog`; start command to the nested exe with `--datadir`/`--cachedir`; `PLUGIN_MISSING` log hint to `data\log\`; replace `<stable-plugin-id>` with the literal GUID; state the version rule (strip the leading `v`, append `.0`, compare to the reported `Version`, so `v0.1.0` matches `0.1.0.0`).
3. `.claude/commands/deploy-dev.md`: same zip name, plugin dir and start command.
4. `CLAUDE.md`: provisioning block takes DEV_SETUP.md Step 3's URL shape and version, and the nested exe path; drop `--port 8097`, add `--cachedir`, poll port 8096; replace the zero-residue line with the firewall-rule note and its removal command.
5. `.claude/rules/deployment.md`: zip name (artifact description, workflow step list, Unraid `unzip` line); workflow file `release.yml`; dev log dir `data\log\`; PAT file path; `<MoviesJsonPath>` to `<JsonPath>`; rewrite the cron section to the manual-only behavior above.
6. `.claude/rules/architecture.md`: config schema list to the real properties; the PAT-in-logs rules (forbidden example and the `code-reviewer` BLOCK trigger) name `PatToken`.
7. `.claude/rules/frontend.md`: form field list to the real ids, with seconds and min 5; drop the cron field.
8. `.claude/settings.json`: the two `Remove-Item` permissions from `D:\jf-dev\config\plugins\MovieCatalog` to `D:\jf-dev\data\plugins\MovieCatalog`.
9. `DEV_SETUP.md`: residue claims (header and Tear-down) corrected; Tear-down gains the firewall-rule removal; the port gotcha points at `data\config\network.xml` and `<InternalHttpPort>`.

Left alone: EXTRACTION.md and past plans (historical records), INSTALL.md (already correct), the descriptive phrase "release-on-tag workflow" in hook messages and quality.md (a role name, not a file path).

10. Folded in from the `quality-inspector` review (verdict DISCUSS, no hard findings): `architecture.md` repo-tree comments (`debounce seconds`, `manual resync (no default trigger)`); `deployment.md` task name `Resync Movie Catalog Now` and button label `Run`; the version pin above in `CLAUDE.md`, `DEV_SETUP.md` and `deployment.md`.

## Findings recorded, not fixed here

- `.claude/notes/deploys/dev-log.md` has never existed, so v0.1.0 shipped without a phase-6 PASS line, and `scripts/hooks/pre-push` never checks the deploy log. Repairing the line needs the dev instance rebuilt and a real smoke run.
- `architecture.md:9` and `code-reviewer.md:50` say the plugin targets Jellyfin Server 10.11.6; production runs 10.11.11 and the csproj references `Jellyfin.Controller` 10.11.3.
- `frontend.md` contradicts `configPage.html` on PAT display (the page shows it via `showPatBtn`), Test Connection (the page calls GitHub from the browser with unsaved form values), and the `<html>`/`<body>` prohibition (the page has them).
- `architecture.md`'s repo tree shows subfolders and a test project; the real plugin files sit flat in `plugin/` and there is no test project.
- The `settings.json` `Remove-Item` allow entries are literal paths, while `smoke-tester` passes the path through `$pluginDir`, so they may never match.

## Verification

- Grep the live docs (excluding EXTRACTION.md and `.claude/plans/`) for each stale token: `jf-dev\config`, `Jellyfin.Plugin.MovieCatalog-`, `MovieCatalog_`, `release-on-tag.yml`, `8097`, `GitHubToken`, `GitHubRepo`, `MoviesJsonPath`, `DebounceMilliseconds`, `ResyncCronExpression`, `txtGitHub`, `zero residue`, `10.11.9`, `10.11.6` in the dev-install docs. Expect one hit only: the `.github/workflows/release.yml:48` comment naming apex-platform's own `release-on-tag.yml`.
- HEAD request on the pinned download URL returns success (not 404).
- `gh release view v0.1.0 --json assets` (read-only): the asset name matches the new pattern.
- `.claude/settings.json` parses as JSON.
- No em or en dashes in any changed file.
- `git diff --stat` lists only the files above; nothing under `plugin/`, `docs/` or `.github/`.
- Run `quality-inspector` on the diff (harness-boundary change) and report its verdict verbatim.
- After the commit, read the hook's debt sentinel and surface any entries. Commit and push only when the user says so.
- Not verifiable now: a live smoke run, since the dev instance is retired.
