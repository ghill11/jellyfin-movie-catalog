# Deployment rules for jellyfin-movie-catalog

The discipline for getting a tag onto its two deploy targets: a Jellyfin server with the plugin installed, and a GitHub Pages site serving the viewer.

## The two deploy targets

This project has two independently-deployable artifacts; each has its own release path.

### 1. Plugin (GitHub release on tag)

- Source: `plugin/` on the tagged commit.
- Build: `dotnet publish -c Release` against the plugin project; zip the resulting DLL(s) plus `meta.json` into `Jellyfin.Plugin.MovieCatalog_<version>.zip`. Compute MD5 of the zip.
- Artifact: the zip attached to a GitHub Release; the MD5 published in the release notes for sideload verification.
- Workflow: `.github/workflows/release-on-tag.yml` builds and uploads on tag push.
- Install: user downloads the zip, unzips into Jellyfin's plugins directory at `<config>/data/plugins/MovieCatalog/` (NOT `<config>/plugins/` - the `data/` segment is required; Jellyfin scans `<data-path>/plugins/` where `<data-path>` defaults to `<config>/data/`), restarts the Jellyfin server.

### 2. Viewer (GitHub Pages from main)

- Source: `docs/` on `main`.
- Build: none. Pages serves the source files as-is.
- Trigger: any push to `main` that touches `docs/`.
- Pages rebuilds automatically; the live site reflects `main` after the build completes (usually under a minute).

The plugin's PUT to the catalog data repo (separate repo, see `architecture.md`) counts as a push for THAT repo's Pages rebuild, not this code repo's. The viewer in this code repo is the UI shell; the data it fetches lives in the separate repo.

## Tagging discipline

### Hand-written release notes for every tag

Every tag MUST have a `release-notes/v<X.Y.Z>.md` file at HEAD before the tag is pushed. The pre-push hook (when ported) refuses the push if missing or empty.

No "the release is trivial" carve-out: the smallest release gets at least a short paragraph naming what changed and why. The apex-platform precedent (a several-tag cycle that drifted into "opt-in" release notes that never got written) is the reason this is mandatory, not advisory.

### Annotated tags, not lightweight

```bash
title="v0.1.0 - initial plugin skeleton"
git tag -a v0.1.0 -m "$title"
```

`git tag -a` attaches the title to the tag itself. The release-on-tag workflow reads the first line of the annotated message as the release title. Lightweight tags (no `-a`) cause the workflow to fall back to the bare tag name with a warning.

### The release-on-tag workflow is the canonical release creator

When the workflow runs, it:

1. Builds the plugin (`dotnet publish -c Release`).
2. Assembles `Jellyfin.Plugin.MovieCatalog_<version>.zip` containing the DLL(s) and `meta.json`.
3. Computes the MD5 of the zip.
4. Reads `release-notes/v<X.Y.Z>.md` as the release body.
5. Reads the first line of the annotated tag message as the release title.
6. Creates the GitHub Release and uploads the zip.

Do NOT run `gh release create` manually. The workflow is the canonical create path; manual creates fight the workflow's idempotency. If the workflow fails, fix the workflow and re-run (`gh workflow run`); don't bypass with a manual create.

### Title pattern

`vX.Y.Z - <descriptive subject>`. Examples:

- `v0.1.0 - initial plugin skeleton + viewer table`
- `v0.1.2 - debouncer window now configurable`
- `v0.2.0 - viewer filter UI + sort by year`

To fix a typo on an already-published release, `gh release edit <tag> --notes-file release-notes/v<X.Y.Z>.md` is fine. That is editing, not creating.

### Phase-6 deploy log line per tag

Every tag, including harness-only or docs-only tags, gets a phase-6 PASS line in `.claude/notes/deploys/dev-log.md`. The discipline and the rationale are documented in `quality.md` §"6. Deploy" and §"Objective evidence". The cost is small (build verification + sideload + a one-line log entry); the cost of a missing chain link is doctrine erosion.

## Sideload procedure (Unraid Docker target)

The intended production target is a Jellyfin instance running in a Docker container on Unraid. Paths inside the container map to host paths under `/mnt/user/appdata/jellyfin/`.

### Stop, install, restart

```bash
# On the Unraid host (or via the Unraid web UI):
docker stop jellyfin

# Confirm the container's /config host mount first; different images mount it
# from different host paths. The plugins directory is at <config-host>/data/plugins/:
#   docker inspect jellyfin --format '{{ range .Mounts }}{{ .Destination }} <-- {{ .Source }}{{ "\n" }}{{ end }}'
#
# The path below assumes the standard Unraid mapping of /config <-- /mnt/user/appdata/jellyfin/.
# Substitute your actual /config source if it differs.

# Drop the unzipped plugin into the plugins folder.
# NOTE: the path is /mnt/user/appdata/jellyfin/DATA/plugins/, NOT /mnt/user/appdata/jellyfin/plugins/.
# Jellyfin scans <config>/data/plugins/; a plugin folder placed at <config>/plugins/ is invisible to the server.
mkdir -p /mnt/user/appdata/jellyfin/data/plugins/MovieCatalog
unzip -o ~/Jellyfin.Plugin.MovieCatalog_v0.1.0.zip -d /mnt/user/appdata/jellyfin/data/plugins/MovieCatalog

# Permissions: Jellyfin Docker containers typically run as nobody:users (uid 99, gid 100) on Unraid.
chown -R nobody:users /mnt/user/appdata/jellyfin/data/plugins/MovieCatalog

docker start jellyfin
```

The plugin folder name (`MovieCatalog` here) is not load-bearing; Jellyfin discovers plugins by the assemblies they contain. Using a stable name aids manual housekeeping.

### Dev sideload (portable Jellyfin)

For local development, the project uses a portable Jellyfin instance at `D:\jf-dev\` (Windows host). Sideload there:

```powershell
# Stop the dev instance (Ctrl-C in its terminal, or its launcher script's stop verb)
# Drop the unzipped plugin into:
#   D:\jf-dev\data\plugins\MovieCatalog\
# Start the dev instance again
```

`D:\jf-dev\` is excluded from the repo via `.gitignore`. The dev instance is the local test target before any release reaches the Unraid host.

## Sideload smoke procedure

After restart, verify the plugin loaded and works end-to-end. This is the phase-6 OQE for the deploy.

### 1. Plugin appears in admin UI

Navigate to Dashboard -> Plugins. "Movie Catalog" appears in the installed list. Open it; the configuration page renders without errors.

### 2. No startup errors in the Jellyfin log

```bash
# Unraid:
docker logs jellyfin --tail 200 | grep -i "moviecatalog\|movie catalog\|error"
# Dev:
# Tail D:\jf-dev\data\logs\log_<date>.log
```

No exceptions naming the plugin; if any, capture and investigate before recording the deploy.

### 3. Test Connection action

In the plugin's configPage.html, the Test Connection button:

- Sends the configured PAT to the configured repo's `/contents/<MoviesJsonPath>` GET endpoint.
- Reports success/failure inline.
- Does NOT write anything.

A successful Test Connection confirms: the PAT is valid, the repo exists, the path is reachable. Run this against the dev test repo (NOT the production data repo) for the smoke.

### 4. Trigger Resync Movie Catalog Now

Either via the configPage button (if implemented) or via Dashboard -> Scheduled Tasks -> Resync Movie Catalog -> Run Now.

Verify:
- The Jellyfin log shows a single push attempt (debounced from any concurrent library activity).
- The log line indicates HTTP 200 / 201 from GitHub.
- The test repo's `movies.json` now reflects the dev library state (open the raw file URL, confirm the JSON contains the expected movie titles).
- GitHub Actions on the data repo (if Pages is configured there) shows a successful Pages build for the new commit.

### 5. Record evidence

Append the phase-6 line to `.claude/notes/deploys/dev-log.md`:

```
<UTC timestamp> | dev | v0.1.0 | PASS | build=ok sideload=ok testconn=ok resync=ok movies_count=42
```

Field meaning: build (the zip artifact verified), sideload (plugin loaded without error), testconn (Test Connection succeeded), resync (Resync produced a valid movies.json in the test repo), movies_count (sanity-check count from the produced JSON).

A FAIL line states which step failed and what the observed error was, in the same one-line shape.

## Configuration of the dev environment

The dev Jellyfin instance (`D:\jf-dev\`) is configured to:

- Point at a small test movie library on the dev host (not the production media share).
- Use a dedicated test GitHub repo for the plugin's pushes. The test repo lives under the developer's GitHub user, separate from the production data repo, so pushes during development do not pollute production data.
- Use a PAT scoped to ONLY the test repo (a fine-grained PAT with `Contents: read and write` on just that repo). The production PAT (different value, different scope) lives only on the Unraid host's installed plugin config; it is never copy-pasted into the dev instance.

The dev PAT is not committed. It lives in the dev Jellyfin instance's on-disk config under `D:\jf-dev\data\plugins\MovieCatalog\config.xml` (Jellyfin's plugin-config file format). That path is inside the gitignored `jf-dev/` directory, so it cannot accidentally end up in a commit.

## Viewer deploy verification

When a tag includes changes to `docs/`, the deploy verification extends:

1. Push the tag (also implicitly pushes any associated commit to `main` if the tag is on `main`).
2. Watch the Pages build complete:
   ```bash
   gh api repos/<owner>/<repo>/pages/builds/latest
   ```
   The response's `status` field reads `building` then `built`. Expected duration: under 60 seconds for a viewer this small.
3. Curl the Pages URL and confirm the new content is live:
   ```bash
   curl -s https://<owner>.github.io/<repo>/ | grep -o '<title>[^<]*</title>'
   ```
4. Open the live site in a browser, confirm the new feature/fix is visible.

Record the Pages verification in the same dev-log line:

```
<UTC timestamp> | dev | v0.2.0 | PASS | build=ok sideload=ok testconn=ok resync=ok movies_count=42 pages_built=ok pages_visible=ok
```

## Per-environment install considerations

This is a personal-use project, so today there is one "production" environment: the Unraid Docker Jellyfin instance, plus the dev Windows portable instance. The discipline (dev sideload first, evidence recorded, then production install) ports directly if the project ever runs on additional Jellyfin instances:

- Each environment gets its own deploy-log file under `.claude/notes/deploys/<env>-log.md`.
- Each environment uses its own GitHub data repo (and its own PAT) so configs do not cross-contaminate.
- Tags are immutable; once `v0.1.0` is on the dev Jellyfin, the same `v0.1.0` zip is what goes to the production Jellyfin (no "rebuild for prod" step that could produce a different bit-for-bit artifact).

## Bringing up a new dev environment

When a new dev machine joins (different developer, or a clean Windows install on the existing developer's box):

1. Clone the repo.
2. Install .NET 9 SDK (the version pinned by `global.json`).
3. Install a portable Jellyfin Server 10.11.6 at `<dev-jellyfin-path>/` (the path is local convention; matches `D:\jf-dev\` for the current developer).
4. In the dev Jellyfin, create a fresh dummy library pointing at a small set of test movies.
5. Create a fine-grained GitHub PAT scoped to a private test repo, set up the test repo (empty `movies.json` is fine), and record the PAT only in the dev Jellyfin's plugin config (never in any file that gets committed).
6. Build the plugin (`dotnet publish -c Release`), zip, sideload to the dev Jellyfin.
7. Run the smoke procedure above. Confirm the test repo's `movies.json` reflects the dev library.

This procedure is the per-developer setup. The eventual automation (a `scripts/dev-setup.ps1` or similar) will codify it; until then it stays manual.

## Cron / scheduled resync

The plugin's `ResyncScheduledTask` runs:

- On manual trigger from the Jellyfin admin UI ("Run Now").
- On the schedule defined by `PluginConfiguration.ResyncCronExpression` if set (otherwise no automatic full resync; library events drive incremental updates).

The full resync is the backstop for the event-driven debouncer: if Jellyfin restarts mid-event-handling, or an event is dropped, the next scheduled resync re-syncs the full library state. Recommended schedule for production: nightly at a low-traffic hour. Default if no cron set: no schedule (manual only). This is documented on configPage.html so the user knows the tradeoff.

## Forbidden during deploy

- Manual `gh release create` (use the workflow).
- Sideloading a build that did not come from a tagged release zip (no "I'll just copy the bin/Release/ output over"). The release zip is the artifact contract.
- Editing `movies.json` in the data repo by hand. The plugin owns that file; manual edits will be overwritten on the next push and may confuse a future debug session.
- Committing a PAT, ever. The pre-commit secrets-scan layer (when added) catches the obvious cases; the discipline of "PAT lives only in the running Jellyfin's plugin config, never in source control" is the load-bearing rule.

## Cross-references

- The deploy-log entry format and the phase-6 evidence requirement: `quality.md` §"6. Deploy" and §"Objective evidence".
- The PAT-not-in-logs rule: `architecture.md` §"Inviolable structural rules" and `style_csharp.md` §"Logging".
- The plugin's runtime behavior (event listener, debouncer, push semantics): `architecture.md` §"Data flow".
- The viewer's structure: `frontend.md`.
