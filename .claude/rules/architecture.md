# Architecture rules for jellyfin-movie-catalog

The structural and design decisions every change must respect.

## The two artifacts

The project ships two artifacts from one repo:

1. **The plugin** (C# class library, .NET 9.0). Loaded by Jellyfin Server 10.11.6 at process start. Lives in `plugin/`.
2. **The viewer** (static HTML + CSS + JavaScript site). Served from GitHub Pages, source path `docs/` on `main`. Renders a sortable, filterable table over the movies.json snapshot the plugin pushes.

The two artifacts share one repo but have separate deploy targets, separate release cadences (a viewer-only tweak doesn't require a new plugin release), and separate failure modes. The repo layout enforces the separation.

## Repo layout

```
jellyfin-movie-catalog/
|-- plugin/
|   |-- Jellyfin.Plugin.MovieCatalog/
|   |   |-- Jellyfin.Plugin.MovieCatalog.csproj
|   |   |-- Plugin.cs                            # BasePlugin<TConfiguration> + IHasWebPages
|   |   |-- PluginServiceRegistrator.cs          # IPluginServiceRegistrator (DI registrations)
|   |   |-- Configuration/
|   |   |   |-- PluginConfiguration.cs           # config schema (PAT, repo, branch, debounce ms, ...)
|   |   |   |-- configPage.html                  # embedded resource, the Jellyfin admin settings page
|   |   |-- Listening/
|   |   |   |-- LibraryEventListener.cs          # IHostedService; subscribes to library events
|   |   |   |-- Debouncer.cs                     # collapses event bursts into one push
|   |   |-- Catalog/
|   |   |   |-- MovieCatalogBuilder.cs           # iterates the movie library; produces movies.json
|   |   |-- Github/
|   |   |   |-- GitHubPusher.cs                  # Contents API: GET sha + PUT new content
|   |   |-- Scheduled/
|   |   |   |-- ResyncScheduledTask.cs           # IScheduledTask: manual + cron resync
|   |-- Jellyfin.Plugin.MovieCatalog.Tests/      # test project
|-- docs/
|   |-- index.html
|   |-- app.js
|   |-- style.css
|   |-- movies.json                              # dev-only; production movies.json lives in the data repo
|-- release-notes/                               # hand-written per-tag notes
|-- scripts/                                     # build helpers, hook installers (when added)
|-- .claude/                                     # harness: rules, plans, notes, agents (when added)
|-- .github/workflows/                           # CI: build, release-on-tag, Pages deploy
|-- .gitignore
|-- .editorconfig
|-- LICENSE
|-- global.json
|-- CLAUDE.md
|-- README.md
```

A separate GitHub repo holds the produced `movies.json` (the catalog data, written by the plugin's PAT). The viewer fetches from that data repo via GitHub Pages (or directly from raw.githubusercontent.com if the data repo is not Pages-enabled). Keeping the data repo separate from the code repo means a movies.json update does not pollute the code repo's git history.

## Data flow

```
Jellyfin library scan (or admin action)
    -> ILibraryManager fires ItemAdded / ItemUpdated / ItemRemoved events
    -> LibraryEventListener (IHostedService) receives event
    -> Debouncer.Bump() (fire-and-forget; resets a delay timer)
    -> [debounce window elapses with no new bumps]
    -> dispatched task: MovieCatalogBuilder.BuildAsync()
        -> iterates Movie items from the library, projects to a small DTO
        -> serializes to JSON
    -> GitHubPusher.PushAsync(json)
        -> GET /repos/<owner>/<repo>/contents/movies.json (capture sha)
        -> PUT /repos/<owner>/<repo>/contents/movies.json (new content + sha)
    -> log result (count, duration, HTTP status)

(asynchronously, on push)
GitHub Pages rebuilds the data repo's site
    -> users visiting the viewer URL fetch the new movies.json
    -> viewer app.js re-renders the table
```

The debouncer is load-bearing for correctness: a library scan publishes dozens to hundreds of events in seconds. Without debouncing, the plugin would issue dozens of PUTs to the GitHub Contents API and likely hit rate-limit (the unauthenticated limit is 60/hr, the PAT-authenticated limit is 5000/hr but bursty sustained writes still cost). Default debounce: 30 seconds of quiet before dispatch.

## Inviolable structural rules

These protect the plugin from causing Jellyfin startup failures, deadlocks, or leaked credentials.

### Event handlers return immediately

Methods registered as library event handlers MUST return without doing any I/O. The handler's job is to call `Debouncer.Bump()` and return. The actual catalog build and the GitHub push happen on a separate dispatched task that the debouncer schedules.

A handler that does network I/O blocks the event-publishing thread. Jellyfin's library scan publishes events synchronously on the scan thread; blocking that thread slows the scan, can starve other event subscribers, and risks Jellyfin marking the listener as faulty.

The `code-reviewer` agent BLOCKs any library-event handler whose body contains an `await` of an I/O operation, a `.Result` / `.Wait()` call, or a synchronous file/network call.

### No synchronous I/O blocking the library scan thread

Even outside of event handlers, any code that runs in a context shared with the library scan (a startup hook, a background service that polls library state) MUST do I/O asynchronously and never block the calling thread.

### No PAT in logs

The GitHub Personal Access Token is the only secret in the plugin. It is read from `PluginConfiguration` at the point of use, passed to the HTTP client via the `Authorization` header, and otherwise never referenced.

Forbidden:
- Logging `_configuration.GitHubToken` at any level.
- Logging the `Authorization` header value.
- Logging an HTTP request object that contains the header (some serializers include headers).
- Logging the configuration object as a whole.

Allowed:
- Logging a boolean ("token configured" / "token missing").
- Logging the token's character length (sanity check for an empty paste).

The `code-reviewer` agent BLOCKs any log statement that interpolates `GitHubToken` (any casing) or `Authorization`. The pre-commit forbidden-name scan (per `quality.md`) does not cover this specifically yet; a Pass N detector for PAT-in-logs is on the future TODO.

### No inline JavaScript larger than ~30 lines

The plugin's `configPage.html` is an embedded resource that Jellyfin renders inside its admin shell. Small inline `<script>` blocks for wiring the Test Connection button and the form-submit handler are fine, per `style_javascript.md` §"Inline scripts in HTML". Anything substantial goes to a separate `.js` file referenced from the embedded resources.

### The plugin GUID is fixed at first commit

Jellyfin identifies plugins by GUID. Once the plugin has been installed by any user, changing the GUID would re-register the plugin as a different one, losing settings. The GUID is set in `Plugin.cs` (or wherever the BasePlugin override lives) and never changes after the first published release.

If the project ever needs a fundamentally different plugin (a hard incompatibility with prior versions), that is a NEW plugin with a NEW GUID, distributed alongside or as a successor, not a GUID change on this one.

### No state-changing GET on plugin HTTP routes

When the plugin exposes HTTP endpoints (e.g., the Test Connection action invoked from configPage.html), state-changing operations are POST / PUT / PATCH / DELETE. A GET that triggers a config write or a GitHub push is a `code-reviewer` BLOCK.

The HTTP semantics: GET is safe and idempotent by definition. Browsers, web crawlers, and Jellyfin's own routing can issue GETs without expecting side effects. A state-changing GET is the bug, not a missing-CSRF problem.

## Configuration

`PluginConfiguration.cs` defines the schema:

- `GitHubToken` (string, secret). The PAT.
- `GitHubRepoOwner` (string).
- `GitHubRepoName` (string).
- `GitHubBranch` (string, default `main`).
- `MoviesJsonPath` (string, default `movies.json`).
- `DebounceMilliseconds` (int, default `30000`).
- `ResyncCronExpression` (string, optional). When set, the scheduled task uses this cron expression for periodic full resync (independent of library events).

Jellyfin's config UI persists this to disk under the plugin's data folder. The PAT is stored as plain text in that file (Jellyfin does not offer encryption at rest for plugin configs out of the box). The configPage.html disclosure surface MUST state this explicitly so the user knows what they are saving.

## DI registration

`PluginServiceRegistrator.cs` registers:

- `LibraryEventListener` as a hosted service (so Jellyfin starts and stops it with the server).
- `Debouncer` as a singleton (one debouncer per plugin instance).
- `MovieCatalogBuilder` as a singleton.
- `GitHubPusher` as a singleton (shares one HttpClient).
- `ResyncScheduledTask` as a scheduled task (Jellyfin's IScheduledTask discovery handles this).

`ILoggerFactory` is the logger source (per `style_csharp.md` §"Logging"). Each service obtains its logger via `loggerFactory.CreateLogger(nameof(ServiceType))` in the constructor.

## What this architecture is NOT

The apex-platform precedent had a plugin-of-plugins shape: a central hub auto-discovered sibling plugins via a NAME constant, registered each at a URL prefix, and rendered a tile grid. That pattern does NOT apply to this project.

This is a SINGLE Jellyfin plugin. There is no plugin-discovery mechanism, no NAME-keyed routing, no per-plugin admin segregation, no role system, no blueprint context_processor pattern. If the project later spawns a second related plugin (e.g., a TV-show catalog), it would be a separate project on the same harness, not a sub-plugin of this one.

## File-organization conventions

- **New types** go in a folder named for the responsibility area (`Listening/`, `Catalog/`, `Github/`). One public type per file.
- **New helpers** go alongside the type that uses them, in the same folder, UNLESS more than one folder needs them. Cross-folder helpers move to a `Common/` folder.
- **New HTTP endpoints** (if the plugin grows them beyond configPage.html's Test Connection) go in a controller class under an `Api/` folder, following Jellyfin's `[ApiController]` + `[Route]` conventions.
- **New scheduled tasks** go under `Scheduled/`, each in its own file, each implementing `IScheduledTask`.
- **Tunable parameters** go in `PluginConfiguration.cs`, NOT in new env vars or hardcoded constants. The config surface is the user's tunable layer; constants in code are for invariants.

## Cross-references

- C# coding conventions: `style_csharp.md`
- JavaScript conventions (for configPage.html and the viewer): `style_javascript.md`
- Frontend (viewer + configPage.html): `frontend.md`
- Deploy procedures: `deployment.md`
- Quality discipline (where the structural rules above get enforced at code review): `quality.md`
