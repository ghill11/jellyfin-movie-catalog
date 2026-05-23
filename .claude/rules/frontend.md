# Frontend rules for jellyfin-movie-catalog

The template, CSS, and JavaScript conventions that govern the project's two frontend surfaces:

1. **The viewer** (`docs/index.html` + `app.js` + `style.css`): the public GitHub Pages site that renders the catalog.
2. **The plugin admin page** (`configPage.html`, embedded in the plugin as a resource): the Jellyfin-side settings panel.

The two surfaces have different constraints (the viewer is fully owned, the configPage lives inside Jellyfin's admin shell), so their rules differ where the constraints differ. They share the language-agnostic and JavaScript-specific rules in `style.md` and `style_javascript.md`.

## The viewer

### Layout: one HTML file, no inheritance

The viewer is `docs/index.html`. There is no template engine, no server-side rendering, no `_base.html` inheritance pattern (the apex-platform Jinja precedent does not apply: there is no Flask, no Jinja, and only one page).

Structure:

```html
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Movie Catalog</title>
    <link rel="stylesheet" href="style.css">
</head>
<body>
    <header>
        <h1>Movie Catalog</h1>
        <input type="search" id="search" placeholder="Filter...">
    </header>
    <main>
        <table id="catalog">
            <thead>...</thead>
            <tbody></tbody>
        </table>
    </main>
    <footer>
        <p>Last updated: <span id="updated"></span></p>
    </footer>
    <script src="app.js"></script>
</body>
</html>
```

The single `<script src="app.js">` at the end of `<body>` is the only JavaScript inclusion. No inline `<script>` blocks in the viewer (the configPage has different conventions; see below).

### CSS conventions

**Class naming.** No prefix is used (the entire viewer is one tiny page; namespacing classes provides no value here). Class names are descriptive: `.movie-row`, `.movie-title`, `.movie-year`, `.year-filter-active`.

If the viewer ever grows enough that a class-name collision feels plausible (more than one logical component on the page, or shared classes across multiple pages), introduce a `.jmc-*` prefix at that point. Until then, no prefix.

**CSS custom properties (variables)** for theme values:

```css
:root {
    --bg: #1a1a1a;
    --fg: #e0e0e0;
    --muted: #888;
    --accent: #4a9eff;
    --border: #333;
}
```

The viewer's default appearance is dark (Jellyfin's default is dark; users coming from the Jellyfin UI expect continuity). Light mode is not implemented in v1; if the user wants light, they can override CSS in their browser. Adding a theme toggle is a design decision.

**Mobile-first responsive design.** The viewer must work on a phone screen. The whole point of the project is "browse my catalog when away from home"; if it does not render usably on a phone, it fails the use case.

Default styles target narrow viewports. Wider screens get progressive enhancement via `@media (min-width: 720px)` rules. The table converts to a card-list layout below ~600px (the columns become stacked rows inside each card) so a movie's information stays readable without horizontal scroll.

**No CDN dependencies.** Per `style_javascript.md` §"No CDN dependencies", everything served same-origin. The viewer does not link to Google Fonts, does not pull a CSS framework, does not load an analytics tag.

### DOM access patterns

`document.querySelector` and `getElementById` per `style_javascript.md`. No jQuery.

Cache DOM references near the top of the IIFE for elements referenced from multiple handlers:

```javascript
(() => {
    "use strict";
    const tbody = document.querySelector("#catalog tbody");
    const searchInput = document.getElementById("search");
    const updatedEl = document.getElementById("updated");
    // ...
})();
```

### Data fetching

The viewer fetches `movies.json` from the data repo's GitHub Pages URL (or directly from raw.githubusercontent.com if the data repo is not Pages-enabled). The URL is hardcoded in `app.js` as a `const` near the top:

```javascript
const MOVIES_JSON_URL = "https://<owner>.github.io/<data-repo>/movies.json";
```

If the data repo's URL changes, this constant changes. There is no config file for the viewer; the URL is part of the viewer's source.

### Same-origin and CORS

The viewer is served from `<owner>.github.io/<code-repo>/` (the code repo). The data lives at `<owner>.github.io/<data-repo>/movies.json` (the data repo). These are different origins.

GitHub Pages serves files with `Access-Control-Allow-Origin: *` by default, so `fetch()` from the viewer to the data repo's `movies.json` works without CORS preflight. If GitHub ever changes that default, the viewer will start failing to load; document that dependency here so the failure mode is recognizable.

### JSON schema the viewer consumes

The viewer expects `movies.json` to be a JSON object with this shape:

```json
{
    "generated_at": "2026-MM-DDTHH:MM:SSZ",
    "plugin_version": "0.1.0",
    "movies": [
        {
            "id": "<jellyfin-item-id>",
            "title": "Movie Title",
            "year": 2024,
            "imdb_id": "tt1234567",
            "tmdb_id": 12345,
            "runtime_minutes": 110,
            "genres": ["Action", "Drama"],
            "added_at": "2026-MM-DDTHH:MM:SSZ"
        }
    ]
}
```

Field rules:

- `generated_at` and `plugin_version`: viewer reads `generated_at` to render the "Last updated" footer; `plugin_version` is for debug visibility.
- `movies[*].id`: opaque Jellyfin identifier. The viewer treats it as an opaque string and uses it only as a stable key for rendering; it never tries to look anything up by it (the viewer has no API back to Jellyfin).
- `movies[*].title`, `year`: required. The viewer renders these.
- `movies[*].imdb_id`, `tmdb_id`: optional. When present, the viewer renders title as a link to the corresponding external page.
- `movies[*].runtime_minutes`, `genres`, `added_at`: optional. Used for sort/filter affordances when present.

The plugin's `MovieCatalogBuilder` is the authoritative producer of this shape. When the viewer needs a new field, the plugin's builder is updated first, the new movies.json is pushed, and only then does the viewer start reading the field (graceful degradation: missing field renders as empty, not as an error).

### Forbidden in the viewer

- CDN dependencies (per the universal rule).
- Frameworks (React, Vue, etc.). No build step.
- A build step.
- GET-request-driven state mutation. The viewer does not write anywhere; this rule is preloaded for the moment the viewer grows interactive features.
- Inserting untrusted-data via `innerHTML` without `esc(...)` (per `style_javascript.md`).
- jQuery. The DOM is the API.
- Inline `<style>` blocks in `index.html` larger than ~5 lines. CSS goes in `style.css`.
- Inline `<script>` blocks in `index.html`. JS goes in `app.js`.

## The plugin admin page (`configPage.html`)

The plugin's configuration page is an embedded resource served by Jellyfin's plugin web-page mechanism. It renders inside Jellyfin's admin shell, which sets the surrounding chrome (header, navigation, theme).

### Constraints from Jellyfin

`configPage.html` is not a standalone page. It is a fragment that Jellyfin injects into its admin layout. Implications:

- Do not include `<html>`, `<head>`, or `<body>` tags. The Jellyfin shell provides those.
- Do not include `<link rel="stylesheet">` for project styles. Jellyfin styles the admin chrome; the configPage's content inherits sensible defaults. Limited inline `<style>` or scoped class names for plugin-specific layout are acceptable.
- Inline `<script>` is the normal way to wire interactivity here (per `style_javascript.md` §"Inline scripts in HTML"). The conventional Jellyfin plugin shape is a single inline script that wires `onload` -> `loadConfiguration()` and a submit handler -> `saveConfiguration()`.

### Form fields

The configPage form binds to `PluginConfiguration.cs`'s properties one-for-one. Field set:

- **GitHub repo settings**:
  - `txtGitHubToken` (password input): the PAT. The disclosure text under the field explicitly states "stored in plain text in Jellyfin's plugin config file; create a fine-grained PAT scoped only to the target repo to limit blast radius."
  - `txtGitHubRepoOwner` (text input).
  - `txtGitHubRepoName` (text input).
  - `txtGitHubBranch` (text input, default `main`).
  - `txtMoviesJsonPath` (text input, default `movies.json`).
- **Behavior settings**:
  - `numDebounceMilliseconds` (number input, min 5000, max 600000, default 30000). Inline hint: "How long to wait for library activity to settle before pushing (5-600 seconds)."
  - `txtResyncCronExpression` (text input, optional). Inline hint: "Cron expression for scheduled full resync. Leave blank for event-driven only."

### The Test Connection button

The configPage exposes a "Test Connection" affordance that verifies the configured PAT can read the configured `movies.json` location. Behavior:

- Button calls a plugin HTTP endpoint (POST, not GET; this is a request triggered by the user but it touches credentials and so is not idempotent in the safety-relevant sense).
- The plugin endpoint reads the current saved config (NOT the values in the form; the user must Save first), constructs an authenticated GET to GitHub's Contents API, returns a small result (`{ok: true, sha: "..."}` or `{ok: false, error: "..."}`).
- The configPage renders the result inline below the button. On error, the error text MUST NOT include the PAT or any header containing it. The plugin's endpoint sanitizes the response before returning.

The PAT-disk-storage disclosure (the "stored in plain text" callout) lives directly below the token field. It is mandatory in the UI; users have a right to know how their credential is handled. The plugin DOES NOT encrypt the PAT at rest because Jellyfin's plugin-config infrastructure does not provide an encryption surface; rolling our own would mean storing a key somewhere, which only moves the problem.

### Forbidden on the configPage

- Any inline `<style>` block larger than ~30 lines (move to a separate embedded resource if needed).
- Loading external resources (CDNs, fonts, analytics).
- Any DOM read or write of values from other plugin configs. The configPage is scoped to this plugin only.
- Logging the PAT to the browser console (the plugin's HTTP endpoint never returns the PAT; the configPage never displays it after save except as masked dots).

## Cross-references

- JavaScript conventions across both surfaces: `style_javascript.md`.
- The viewer's data-flow position in the system: `architecture.md` §"Data flow".
- The configPage's relationship to the plugin's C# config class and DI: `architecture.md` §"Configuration" and `style_csharp.md` §"Identifier conventions".
- Deploy verification for viewer changes (Pages build + curl): `deployment.md` §"Viewer deploy verification".
