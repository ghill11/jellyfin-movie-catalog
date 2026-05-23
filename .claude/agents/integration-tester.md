---
name: integration-tester
description: Cross-component integration tests for jellyfin-movie-catalog with real Jellyfin, real GitHub Contents API, and optional Playwright against the deployed viewer. NOT YET IMPLEMENTED - functional prompt to be written after the plugin and viewer are both live and there is a real end-to-end flow to exercise.
status: stub
---

# integration-tester (stub)

## Purpose

Writes and runs integration tests that exercise the full stack with real components:

- **Real (portable) Jellyfin**: the local `D:\jf-dev\` portable instance with a test library populated. The plugin is sideloaded; the test exercises library events fired by the actual Jellyfin process (not mocked `ILibraryManager`).
- **Real GitHub Contents API**: a dedicated test repo (separate from the user's real catalog repo) that the plugin pushes snapshots to. The test verifies the push happened, the file landed, and the response shape matches the plugin's expectations.
- **Real debouncer**: end-to-end exercise of the event-to-snapshot path including the debounce window, coalescing of multiple events, and the back-off behavior on API failure.
- **Optional Playwright tests**: full browser against the deployed GitHub Pages viewer, verifying the viewer renders the test repo's snapshot correctly, filters work, the API does not leak any sensitive metadata, and the page works without JS (graceful degradation, if that becomes a project goal).

Distinct from `function-tester` (which mocks dependencies and runs locally) and `smoke-tester` (which only confirms the released zip sideloads cleanly).

## Status

**Stub** - no functional prompt yet. Will be filled in after:

1. The plugin is live and successfully pushing snapshots to a real GitHub repo (autonomous end-to-end test repo, before the user's real Unraid handoff).
2. The viewer is live on GitHub Pages and rendering snapshots from at least one repo.
3. There is a real cross-component flow to exercise.

The stub framing follows the `apex-platform` precedent: that project's `integration-tester` was a stub awaiting the first real plugin (Prism). Ours is a stub awaiting the first real plugin-to-GitHub-to-viewer round-trip.

## Invocation triggers (proposed)

- User says "write an integration test for the snapshot push" or "verify the debouncer coalesces events end-to-end against the test repo."
- Before publishing a release tag, to gate on integration tests passing in addition to the smoke-tester's sideload check.
- After a change that touches the event subscription path, the debouncer, the snapshot serializer, or the GitHub Contents API client.

## What this agent will need when filled in

- A reserved test GitHub repo with its own PAT, isolated from the user's real catalog repo.
- A test Jellyfin library (a small set of dummy movie files, or a library pointed at a small fixture directory) so events fire deterministically.
- A way to drive Jellyfin's library-scan from a test (probably via the `/Library/Refresh` admin API, with the smoke-tester's `.smoke-api-key`).
- A teardown that wipes the test repo's snapshot file between runs so each test starts clean.
- Playwright (optional) installed against a headless browser, hitting the deployed viewer.

The agent must explicitly NOT touch the user's real catalog repo or production Jellyfin. The test repo and the `D:\jf-dev\` portable instance are the only allowed targets.

---

Origin: stub modeled on `apex-platform` `.claude/agents/integration-tester.md`, which is itself a stub awaiting that project's first real plugin. The shape (cross-component, real services in a contained test environment, distinct from function-tester and smoke-tester) ports clean; the specifics (real Jellyfin + real GitHub Contents API + optional Playwright against GitHub Pages) are this project's analog of apex's (real Postgres + full Flask + real OAuth + optional Playwright against apex-dev).
