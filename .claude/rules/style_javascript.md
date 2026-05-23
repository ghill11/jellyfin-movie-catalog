# JavaScript style rules for jellyfin-movie-catalog

Language-specific conventions for the JavaScript half of the project (the static GitHub Pages viewer and any plugin-side configPage.html inline scripts). The language-agnostic conventions live in `style.md`; this file extends them with the JS-specific layer.

## No build step

The viewer is plain HTML + CSS + JavaScript, served as static files from GitHub Pages. There is no bundler, no transpiler, no module loader, no framework, no TypeScript compile step.

Adding any of those is a design decision the user approves explicitly before any tooling lands. The "no build step" stance is load-bearing for two reasons:

1. **GitHub Pages serves what the repo contains.** No build hook means the source IS the deployed artifact; what is in `viewer/` on `main` is what users see. Debugging in the browser is debugging the actual source.
2. **The viewer is intentionally small.** Movie catalog rendering is a sortable, filterable table over a JSON file. The cost of pulling in a framework far exceeds the value at this scale; the project is also a counter-example to default-reaching for one.

When the viewer grows past the point where vanilla JS is straining (likely indicators: more than ~500 lines of viewer JS, more than three pages, or interactive widgets too complex for plain DOM), revisit. Until then, no build step.

## Module scope and global pollution

- Wrap the viewer's main script in an IIFE (immediately invoked function expression) or use ES modules (`<script type="module">`):
  ```javascript
  (() => {
      "use strict";
      // viewer code here
  })();
  ```
- No assignment to `window.*` from viewer code. No implicit globals (every variable declared with `const` or `let`, never bare assignment).
- Function declarations inside the IIFE are scoped to the IIFE; event handlers can reference them without polluting the global namespace.
- `"use strict";` at the top of every script file. Strict mode catches a class of bugs at parse time that sloppy mode silently allows.

## Variable declaration

- `const` by default. `let` only when the binding genuinely changes. `var` is forbidden.
- One declaration per line. Do not chain declarations with commas.

## DOM access

- `document.querySelector(selector)` and `document.querySelectorAll(selector)` for general selection. `document.getElementById(id)` is fine for single-id lookups and is slightly faster.
- jQuery is forbidden. The DOM API is the API.
- Cache DOM references in `const`s near the top of the IIFE if used in more than one handler. Re-querying the same element on every event is wasteful.

## Inserting content into the DOM

- `textContent` for plain text. Always safe.
- `innerHTML` is forbidden for any value that includes user-provided or fetched data unless every interpolated value is run through an `esc()` helper.
- The escape helper:
  ```javascript
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#39;",
  }[c]));
  ```
- Acceptable pattern for rendering a list of movies into the DOM:
  ```javascript
  const rows = movies.map((m) => `
      <tr>
          <td>${esc(m.title)}</td>
          <td>${esc(m.year)}</td>
      </tr>
  `).join("");
  tbody.innerHTML = rows;
  ```
  Every interpolated value passes through `esc`. A template-string with `${someRawValue}` outside `esc(...)` is a code review BLOCK.
- Alternative: build elements via `document.createElement` + `textContent`. Slower to write, safer by construction. Use for one-off elements; use the templated approach for repetitive lists.

## Fetching data

- `fetch()` only. `XMLHttpRequest` is forbidden in new code.
- Always check `response.ok` before parsing:
  ```javascript
  const response = await fetch(url);
  if (!response.ok) {
      throw new Error(`fetch failed: ${response.status} ${response.statusText}`);
  }
  const data = await response.json();
  ```
- Wrap `response.json()` in a try/catch when the response source is not under our control. The viewer's `movies.json` IS under our control (the plugin generates it), so a JSON parse error is a real bug and propagating the throw is correct.
- Set a timeout via `AbortController` if a slow remote could hang the page:
  ```javascript
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 10_000);
  try {
      const response = await fetch(url, { signal: controller.signal });
      // ...
  } finally {
      clearTimeout(timeoutId);
  }
  ```

## Inline scripts in HTML

Inline `<script>` blocks in HTML are acceptable when ALL are true:

- The script is under approximately 30 lines.
- It is self-contained (no shared utilities; does not reference a global from another script).
- It progressively enhances rather than gates functionality (the page works without it, just less interactively).
- It is wrapped in an IIFE.

Anything larger goes to a separate `.js` file loaded via `<script src="...">`.

The plugin's `configPage.html` (the Jellyfin admin settings panel) has different rules: it lives inside the Jellyfin web UI's shell and follows Jellyfin's own plugin-config conventions for talking to the plugin's API endpoints. Inline `<script>` is the norm there because it is the only practical way to embed JS in a Jellyfin config page. The 30-line guideline still applies; larger logic moves to a separate file referenced from the embedded resources.

## No CDN dependencies

Every script, stylesheet, font, and image the viewer uses is served from the same origin (the GitHub Pages site). No `<script src="https://cdn.example.com/...">`, no Google Fonts `<link>`, no remote analytics tags.

Rationale: same-origin loads remove a class of failure modes (CDN outage, mixed-content blocks, privacy-extension blocks, CORS surprises) at trivial cost for a viewer this small.

## No DOM-event-driven navigation interception

Anchor tags (`<a href="...">`) and form submissions do what the browser says they do. The viewer does NOT call `event.preventDefault()` on navigation to swap content via `fetch` + DOM swap (the pseudo-single-page-app pattern). Browser history (back/forward buttons) belongs to the user.

If the viewer ever grows enough to want client-side routing, that is a design decision and the "no build step" stance gets revisited.
