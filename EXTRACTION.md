# Harness extraction log

> **Purpose**: this file is the running record of porting the apex-platform Claude Code harness into jellyfin-movie-catalog. Each entry documents what was ported, what was scrubbed, what remained apex-coupled, and what generalized cleanly. The whole file is the proto-template for a future generic-harness exemplar (intended for the AI orchestrator courses).

## Conventions

Each file gets an entry with:

- **Source**: path in apex-platform (or NEW if invented during this exercise)
- **Verdict**: one of:
  - **Port clean** - copied verbatim or with trivial path/name swaps
  - **Port + scrub** - copied with surgical edits to remove apex-specific content while preserving structure
  - **Port + REFACTOR** - structurally rearranged (e.g., split into multiple files)
  - **Port INTACT + dormancy header** - copied unchanged with a "not exercised in this project" header prepended (rule applies to most production projects but not this one)
  - **Rewrite** - apex's content was project-domain-specific; replaced with content for this project's domain
  - **Stub + flag** - apex concept doesn't apply; left as a placeholder for the future generic-harness exercise to address
  - **Drop** - not ported at all (apex-specific operational state)
- **Kept verbatim**: substring or section list that ported unchanged
- **Scrubbed/Rewrote**: what was removed or replaced and why
- **Apex-coupling found**: anything subtle that surfaced during the port (informs the future generic harness)
- **Generic exemplar note**: tier categorization (Tier 1-6 per the plan) plus reusability commentary

## Tier categorization

Building toward the generic-harness exemplar:

- **Tier 1: Universal** - port clean, suitable as-is for any project on this harness
- **Tier 2: Language-namespaced** - new pattern this exercise produces; style_<lang>.md per language a project exercises
- **Tier 3: Project-domain** - file structure ports, content rewritten per project
- **Tier 4: Pre-loaded-but-dormant** - port intact with a dormancy header; rule applies to most production projects, lands the moment the project exercises it
- **Tier 5: Apex-coupled but generic-shaped** - file structure and patterns port, contents project-specific
- **Tier 6: Apex-only** - stub-and-flag; concept doesn't generalize without more thought (future generic-harness exercise question)

## Entries

### .gitignore

- **Source**: D:\Apex-Platform\.gitignore
- **Verdict**: Port + scrub
- **Kept verbatim**: secrets section (.env, .pem, .key), local intermediates (.tmp/, *.log), IDE block (.vscode/, .idea/, swap files), OS metadata block (.DS_Store, Thumbs.db, desktop.ini), Claude Code state block (.claude/worktrees/, .claude/settings.local.json, .claude/scheduled_tasks.lock)
- **Scrubbed**: entire Python section (`__pycache__/`, `*.py[cod]`, `venv/`, `.pytest_cache/`, etc.) - no Python in this project
- **Added**: C# section (bin/, obj/, *.user, *.suo, .vs/, publish/, TestResults/, *.nupkg, .nuget/, packages/); local dev section (jf-dev/, .env.test.local for the dev-Jellyfin PAT file)
- **Apex-coupling found**: the comment header read "Apex Platform - .gitignore"; trivially generalizes.
- **Generic exemplar note**: Tier 1 (Universal) for the secrets / IDE / OS / Claude blocks; Tier 2 (Language-namespaced) for the language sections. The shape of "one .gitignore section per language" is itself the reusable pattern - future projects add their language section(s) and remove the ones they don't exercise.

### LICENSE

- **Source**: NEW (apex doesn't have one yet)
- **Verdict**: NEW
- **Content**: MIT, standard template, Copyright (c) 2026 ghill11
- **Generic exemplar note**: every public-repo project on this harness needs one. MIT is the default; alternative licenses (Apache 2.0, GPL) plug in here.

### .editorconfig

- **Source**: NEW
- **Verdict**: NEW
- **Content**: UTF-8, LF, trim trailing whitespace, final newline. 4-space for C#-family (cs, csproj, sln, props, targets); 2-space for JS/HTML/CSS/JSON/YAML/MD; 2-space for shell + powershell.
- **Generic exemplar note**: Tier 2 (Language-namespaced). Same shape as the .gitignore language sections - one block per language family. Generic harness ships with a baseline; consumers add language families as they pick them up.

### global.json

- **Source**: NEW
- **Verdict**: NEW (C#-specific)
- **Content**: Pin SDK band to 9.0.0 with `rollForward: latestFeature` (uses any 9.0.* feature band that's installed). `allowPrerelease: false`.
- **Generic exemplar note**: Tier 5 (Apex-coupled but generic-shaped). The CONCEPT (pin the dev-tool version for reproducibility) is universal; the SHAPE varies by language. Node has package.json engines; Python has .python-version; Rust has rust-toolchain.toml. Future generic harness should provide a "language version pin" slot per supported language.

### .claude/rules/style.md

- **Source**: D:\Apex-Platform\.claude\rules\style.md
- **Verdict**: Port + REFACTOR
- **Kept verbatim**: the em-dash prohibition (with rephrasing of the closing line so the example reference does not name a specific past project's plugin), the "no phantom stub or pending accounts" cross-reference (now points at database.md), the comment philosophy framing ("explain WHY, not WHAT"), the boundary-vs-internal-code validation principle, the surface-vs-swallow error-handling principle, the forbidden-practices framework as a bulleted list, the forbidden-name token list (with `quality.md` cross-reference).
- **Scrubbed/Rewrote**: project-name "apex-platform" -> "jellyfin-movie-catalog" throughout; the Lantern em-dash-scrubbing anecdote rephrased to "a prior project" without naming Lantern or the specific files; example code blocks switched from Python (`counter += 1`, `_PROCESS_STARTED_AT_ISO`) to C# (`counter += 1;`, the debouncer example); the Flask-handler error-handling subsection generalized to "boundary code" + "background work"; the `logging.getLogger(__name__)` Python-specific example removed; the `redirect(request.args.get(...))` Flask-specific example removed.
- **REFACTOR (lifted out)**: the entire "Python identifiers" subsection was removed; identifier conventions are now per-language in `style_csharp.md` and `style_javascript.md`. style.md now carries a "cross-language summary" stub pointing at the language files.
- **Apex-coupling found**: the original style.md mixed three concerns: cross-language style (em-dash, comments, forbidden practices), Python-language identifier conventions, and Flask-domain logging idioms. The refactor surfaced this mixing. **Backport candidate for apex**: apex itself should do the same lift into a `style_python.md` (out of scope for this session; flagged here for the apex maintainer).
- **Generic exemplar note**: Tier 1 (Universal) for the post-refactor `style.md`. The pattern of "top-level `style.md` for cross-language rules; per-language `style_<lang>.md` siblings" is a Tier 2 architectural pattern this exercise produced. Future generic harness ships with this layout by default.

### .claude/rules/style_csharp.md

- **Source**: NEW
- **Verdict**: NEW (Tier 2: Language-namespaced)
- **Content**: C# identifier conventions (PascalCase for types/methods/properties, camelCase for parameters/locals, _camelCase for private fields, PascalCase for constants); file-per-public-type; mandatory `<Nullable>enable</Nullable>` and discipline around `null!`; async/await conventions including the `Async` suffix rule, the `.Result`/`.Wait()` prohibition, and `ConfigureAwait(false)` defaults; `using` declarations vs blocks; DI registration conventions (constructor injection only, singletons for shared clients); the Jellyfin-specific `ILoggerFactory.CreateLogger(name)` workaround (avoid `ILogger<T>` due to a Jellyfin DI quirk); NuGet pinning; exception-handling specifics; XML-doc conventions; the forbidden list (`dynamic`, `unsafe`, reflection-based access, `Thread.Sleep`, `Task.Run` to fake async, `goto`).
- **Apex-coupling found**: none; written from scratch.
- **Generic exemplar note**: Tier 2. Future generic harness ships an empty `style_<lang>.md` per language the project exercises, with this C# file as the exemplar for what populated content looks like.

### .claude/rules/style_javascript.md

- **Source**: NEW
- **Verdict**: NEW (Tier 2: Language-namespaced)
- **Content**: the "no build step" stance with rationale; IIFE / module-scope conventions and the "no globals" rule; `const`/`let` discipline; DOM access patterns (querySelector / getElementById, no jQuery); the `innerHTML` rule with an explicit `esc(s)` helper for template-string interpolation; `fetch()` conventions (check `response.ok`, optional `AbortController` timeout); inline-script size limit (~30 lines); the configPage.html exception (inline is the Jellyfin norm); no CDN dependencies; no DOM-event-driven navigation interception.
- **Apex-coupling found**: none; written from scratch. Apex's frontend.md had a similar inline-script guideline (~30 lines, scoped, self-contained, IIFE-wrapped) for its account-dropdown JS; that doctrine generalizes and lives here now in language-namespaced form.
- **Generic exemplar note**: Tier 2. Pairs with style_csharp.md as the second populated language file in the exercise.

### .claude/rules/workflow.md

- **Source**: D:\Apex-Platform\.claude\rules\workflow.md
- **Verdict**: Port + scrub
- **Kept verbatim (rule bodies)**: "Checking remote state is read-only" (entire section, including the verb-to-action-class table); "Plan files live in-repo at `.claude/plans/`" (entire section); "Plan file naming" regex `^v\d+\.\d+\.\d+-[a-z0-9-]+\.md$` and the `vNEXT-` placeholder; "Plan-mode first for multi-file work" rule text including the three mechanical trigger questions and the "no obvious pattern carve-out" framing; "Design questions surface via AskUserQuestion" with the mechanical-trigger phrase list verbatim; "Test failures are investigated, not labeled" with the investigation-path numbered list; "Surface outstanding quality debt before giving deploy commands" entire section; "Surface harness debt explicitly after every commit that produces one" entire section.
- **Scrubbed/Rewrote**: example plan filenames switched from apex's `v0.1.25-debt-cleanup.md` etc. to project-neutral examples (`v0.1.0-initial-plugin-skeleton.md`, etc.); apex's "Plan-Ref" trailer example for a Flask-route change generalized; the Test-failure rule's Pass 5a reference to `python -m unittest discover hub_webapp/tests` rewritten to `dotnet test` with a note that the hook implementation needs porting; the Pass 5b `@unittest.skip` AST scan reference rewritten to NUnit `[Ignore]` / xUnit `Skip = ...`; the closing "Pending draft items" section scrubbed (removed apex-specific "feature branches per concern" examples that referenced apex plugin work; kept the discipline categories themselves); apex-specific 2026-05-XX origin dates remain as "apex-platform <date>" footnote credits per the prompt's instructions.
- **Apex-coupling found**: the Pass 5a/5b hook descriptions are language-specific (`python -m unittest discover` and `@unittest.skip` AST). The shape of the rule (run the test suite, AST-scan for undocumented skips) is universal; the parser is per-language. Flagged in the rule text that the hook script implementation will need a per-language port; the doctrine carries across.
- **Generic exemplar note**: Tier 1 (Universal) for the rule prose. The hook implementations are Tier 5 (Apex-coupled but generic-shaped): the SHAPE (Pass 5a parses test output for failures; Pass 5b AST-scans test sources for skip-attributes without reasons) is reusable; the IMPLEMENTATION is per-language. Future generic harness should ship a stub `hooks/` directory and a "to populate per language" note.

### .claude/rules/quality.md

- **Source**: D:\Apex-Platform\.claude\rules\quality.md
- **Verdict**: Port + scrub (kept ~90%, per prompt)
- **Kept verbatim**: the SUBSAFE framing and the Thresher historical anchor; the "Why a program, not a checklist" four-bullet definition; the harness boundary list (with file paths generalized: kept the `.claude/rules/*.md`, `.claude/agents/*.md`, `CLAUDE.md` shape; the `scripts/hooks/*` and `.github/workflows/*.yml` shape with "when ported" annotations since the hooks haven't been ported yet); all 7 phases (1-5 and 7 verbatim; phase 6 rewritten per prompt); all 11 tells verbatim with apex-specific origin events rephrased to "apex-platform <date> incident" footnote credits per the prompt instructions; the "Protocol when a tell fires" four-step verbatim; the Agent ownership section with the agent table verbatim (subagent file paths annotated "preloaded; lands as agents are ported"); the Mechanical triggers list and What does NOT fire list verbatim; Forbidden names verbatim; Pressure resistance verbatim; the originating incident paragraph (the apex_post_logout cookie story) generalized to "a prior project (apex-platform)" framing but kept the doctrine.
- **Scrubbed/Rewrote**: project name throughout ("apex-platform" -> "jellyfin-movie-catalog"); the boundary examples in §"The boundary" rewritten for this project (plugin code + viewer + workflows + Contents API interaction, not Flask/auth/FERPA paths); FERPA wording where it appeared as a constraint generalized to "regulated data" or removed; the phase 6 deploy section completely rewritten for the build-zip-sideload-smoke-recordlog ritual (per prompt); the Objective evidence table phase-6 row rewritten; the audit-log content is NOT in quality.md (it lives in database.md as preloaded doctrine; quality.md just references "regulated data" as the category that triggers it).
- **Apex-coupling found**: the original quality.md's phase-6 section was tightly coupled to the apex one-clone deploy + `scripts/smoke.sh` writes `/data/apex/<env>/smoke.json` pattern + `quality-inspector` reads dev-log evidence. The DOCTRINE (every tag gets a PASS line; no exceptions) is universal. The MECHANISM (how the PASS line is produced) is project-specific. The rewrite split these: doctrine stays, mechanism re-rewrites per project.
- **Generic exemplar note**: Tier 1 (Universal) for the SUBSAFE framing, the boundary concept, the 7 phases at the doctrine level, the 11 tells, the protocol, forbidden names, pressure resistance, the originating incident. Tier 3 (Project-domain) for the boundary-membership examples and the phase 6 deploy mechanism. Tier 4 (Pre-loaded-but-dormant) for the agent-ownership subsection (the rule preloads here; agents land as the harness matures). This is the single largest file in the port and the highest doctrinal density.

### .claude/rules/architecture.md

- **Source**: D:\Apex-Platform\.claude\rules\architecture.md (inspiration only)
- **Verdict**: Rewrite
- **Kept from apex (structural patterns, not content)**: the "inviolable structural rules" section as a category; the "file-organization conventions" framing; the explicit "what this architecture is NOT" disclaimer pattern; the cross-references block at the end.
- **Rewrote**: the two-artifact framing (plugin + viewer) replacing apex's hub-plus-plugins framing; the repo layout reflecting `plugin/` + `docs/` rather than `hub_webapp/` + `*_plugin/`; the data-flow ASCII diagram describing Jellyfin library events -> debouncer -> catalog builder -> GitHub Contents API push -> Pages rebuild -> viewer fetch; the structural rules (event handlers return immediately, no sync I/O blocking the scan thread, no PAT in logs, no inline JS larger than ~30 lines, fixed plugin GUID, no state-changing GET on plugin HTTP routes); the configuration section documenting `PluginConfiguration.cs`; the DI registration pattern via `IPluginServiceRegistrator`; an explicit disclaimer that the apex plugin-of-plugins shape does NOT apply.
- **Apex-coupling found**: apex's architecture file was so plugin-discovery-shaped that almost nothing ported as content. What DID port was the META-pattern: a project's architecture file documents its core structural invariants and the explicit list of things-not-allowed. That pattern is universal.
- **Generic exemplar note**: Tier 3 (Project-domain). The file structure is reusable; the contents are per-project. Future generic harness ships an `architecture.md` template with section headers ("Repo layout", "Data flow", "Inviolable structural rules", "Configuration", "What this architecture is NOT", "Cross-references") and empty bodies for the project to fill.

### .claude/rules/deployment.md

- **Source**: D:\Apex-Platform\.claude\rules\deployment.md (inspiration only)
- **Verdict**: Rewrite
- **Kept from apex (structural patterns, not content)**: the "two deploy targets" framing (apex had per-env clones; this project has two artifacts); the tagging discipline subsection structure (hand-written release notes mandatory, annotated tags, workflow-canonical-creator, title pattern, phase-6 deploy log line per tag); the "bringing up a new env" subsection structure; the "forbidden during deploy" closing list.
- **Rewrote**: replaced the one-clone-pull-restart-smoke ritual with build-zip-sideload-smoke; replaced apex's nginx-listen-8443 / gunicorn-gthread / Postgres-cluster section with the Jellyfin Docker container path conventions (`/mnt/user/appdata/jellyfin/plugins/MovieCatalog/`) and the portable Jellyfin dev path (`D:\jf-dev\`); replaced apex's alembic migration step with no-equivalent (the plugin has no migrations today); the cron section reduced to the plugin's optional `ResyncCronExpression` rather than apex's audit-log-rotation cron; the viewer deploy verification (Pages build watching, curl check) is genuinely new content that apex did not have an analog for.
- **Apex-coupling found**: apex's deployment.md was operationally dense with VM-specific paths and systemd / nginx / alembic specifics. The doctrine ("tag-pushable artifact; smoke before record-as-deployed; record per env in a deploy log") is universal. The mechanics rewrite per project.
- **Generic exemplar note**: Tier 3 (Project-domain). Same shape as architecture.md: future generic harness ships a `deployment.md` template with section headers and the deploy-log convention as a reusable pattern, and projects fill the body.

### .claude/rules/frontend.md

- **Source**: D:\Apex-Platform\.claude\rules\frontend.md (inspiration only)
- **Verdict**: Rewrite
- **Kept from apex (structural patterns, not content)**: the inline-script size limit doctrine (~30 lines, scoped, self-contained, IIFE-wrapped) carried over via reference to `style_javascript.md`; the "no CDN dependencies" rule; the "no DOM-event-driven navigation interception" preservation of browser history; the "what plugins MUST do / MUST NOT do" closing-list pattern (adapted to "forbidden in the viewer" + "forbidden on the configPage").
- **Rewrote**: the whole file is per-project content. Apex had a Jinja template-inheritance pattern (`_base.html`, `_ribbon.html`) that does NOT apply: the viewer is a single `index.html`, no inheritance. Apex's `apex-with-bg` / `apex-card` pre-signin visual pattern does NOT apply: no auth in this project. Apex's plugin-admin-tile pattern does NOT apply: single plugin, single configPage. The mobile-first responsive design content is new for this project (mobile is the use case). The JSON schema documentation is new (the contract between plugin and viewer is documented in this rule file).
- **Apex-coupling found**: apex's frontend.md was the densest plugin-of-plugins frontend file (slot model, context_processor, ribbon depth-tier rule). None of that ports. What did port was the META-pattern: a frontend.md documents the surface(s) the project exposes, the conventions for that surface, and the JSON / API contracts between the surface and its data source. That pattern is universal.
- **Generic exemplar note**: Tier 3 (Project-domain). Future generic harness ships a `frontend.md` template with sections ("The frontend surface(s)", "Layout / structure", "CSS conventions", "DOM access patterns", "Data contract with backend", "Forbidden") and projects fill in per-surface content.

### .claude/agents/quality-inspector.md

- **Source**: D:\Apex-Platform\.claude\agents\quality-inspector.md
- **Verdict**: Port + scrub
- **Kept verbatim**: the doctrine shape (PASS / DISCUSS / BLOCK verdict vocabulary; the seven phases; the harness-boundary scan; the pressure-resistance closing; the JSON output contract on pre-push and the prose output contract on chat invocation; the cite-prior-incidents discipline).
- **Scrubbed**: references to apex-platform; specific Python tooling (`python -m unittest discover -s hub_webapp/tests`, `_pass5b_scan.py`) replaced with `dotnet test plugin/tests/...` and the project's Pass 5b scanner; specific incident filenames (cookie-suppression-c6c69c2.md, etc.) dropped since the project has no incident corpus yet; the apex Pass 6 test-primitive entries section dropped to match this project's pre-commit (Pass 6 deferred until a crypto helper lands).
- **Apex-coupling found**: the agent prose cited specific incident filenames as live examples. Replaced with a generic "cite prior incidents when applicable" so new incident filenames written by `incident-recorder` get picked up naturally.
- **Generic exemplar note**: Tier 1 (Universal). The doctrine shape (phases, tells, boundary, verdicts) is the SUBSAFE-modeled core that ports clean across projects. The language-specific Pass 5 details are the only thing that needs swapping per project.

### .claude/agents/code-reviewer.md

- **Source**: D:\Apex-Platform\.claude\agents\code-reviewer.md
- **Verdict**: Port + scrub
- **Kept verbatim**: the BLOCK/WARN/NIT severity tiers; the walk-the-diff-by-category structure; the output-format spec; the "what you do NOT do" closing; severity discipline.
- **Scrubbed/Rewrote**: every category's contents. The apex categories (SQL injection, CSRF on state-changing routes, FERPA/PII, Fernet encryption coverage, plugin contract against the apex Hub, SQLAlchemy session discipline) were rewritten for this project: credential exposure of the GitHub PAT, untrusted innerHTML in the viewer, C# nullable-refs / async-await / `HttpClient` lifetime, NuGet pinning, JS no-CDN/no-build-step, plugin contract against Jellyfin's `BasePlugin<T>` / `IPluginServiceRegistrator`. The em-dash hard rule kept verbatim.
- **Apex-coupling found**: the section-by-section structure (Security, FERPA, Encryption, Plugin contract, DB discipline, URLs, Style, Tests) was apex-shaped. Adapted to: Security, C# idioms, NuGet pinning, JS idioms, Plugin contract, Style, Tests. The shape persists; the contents are project-specific.
- **Generic exemplar note**: Tier 3 (Project-domain). The agent's STRUCTURE (severity tiers, output shape, review-by-category) is reusable; the CATEGORIES are project-shaped.

### .claude/agents/function-tester.md

- **Source**: D:\Apex-Platform\.claude\agents\function-tester.md
- **Verdict**: Port + scrub
- **Kept verbatim**: pattern A (pure function/predicate) vs pattern B (method with side effects); mock-the-boundary-not-the-SUT discipline; one-mock-per-test guidance; do-not-hit-real-external-services rule; the "what you do NOT do" closing including "do not change the SUT to make the test pass."
- **Scrubbed/Rewrote**: stdlib unittest convention -> NUnit (C# plugin) + vanilla-JS test harness (viewer); env-stub-at-module-top pattern dropped (apex needs it because `hub_config` raises at import; this project does not); Flask test_client -> Moq with HttpMessageHandler interception; `sys.path` insert (Python) dropped; Authlib/DB/HTTP mock specifics -> Jellyfin SDK interfaces + GitHub Contents API mocking.
- **Apex-coupling found**: the agent assumed stdlib unittest as the convention. The "align with existing convention" principle is the right one; the specific convention varies. Adapted to "read the first established test in each side as authoritative."
- **Generic exemplar note**: Tier 3 (Project-domain). Pattern A vs B + boundary-mocking discipline is universal; framework specifics vary.

### .claude/agents/incident-recorder.md

- **Source**: D:\Apex-Platform\.claude\agents\incident-recorder.md
- **Verdict**: Port + scrub
- **Kept verbatim**: the file format template (Symptom / Cause / Band-aid / Why band-aid wrong / Proper solution / Pattern recognition / References); the writing discipline; idempotence rules; the "how to recognize this pattern next time" section flagged as the load-bearing part.
- **Scrubbed**: references to specific apex incidents (`cookie-suppression-c6c69c2.md`) as the structural template; example pattern names swapped from apex flavor (cookie suppression, stub-account-for-FK) to this-project flavor (untrusted innerHTML, blocking result in event handler, floating NuGet version).
- **Apex-coupling found**: only the example pattern names. The template itself is universal.
- **Generic exemplar note**: Tier 1 (Universal). The incident-recording template is project-agnostic and ports clean. Every project on this harness uses this agent essentially verbatim.

### .claude/agents/smoke-tester.md

- **Source**: D:\Apex-Platform\.claude\agents\smoke-tester.md
- **Verdict**: Rewrite
- **Kept verbatim**: the discipline (read the contract first, apply verdict logic, append to deploy log, report to user, never push commits, never bypass); the deploy-log format shape; the quality-inspector phase-6 OQE coupling; the "what you do NOT do" closing.
- **Rewrote**: the verification target entirely. Apex verifies a server-side `/<env>/health` JSON endpoint that a VM smoke script populates; this project verifies a release artifact (the plugin zip) by downloading, MD5-checksumming, sideloading to a portable Jellyfin at `D:\jf-dev\`, restarting, polling `/System/Info/Public`, then hitting authenticated `/Plugins`. The verdict set is entirely new (PASS, VERSION_MISMATCH, PLUGIN_MISSING, AUTH_FAILED, STARTUP_FAILED, SIDELOAD_FAILED, DOWNLOAD_FAILED, NO_API_KEY, NOT_PROVISIONED).
- **Apex-coupling found**: apex's process-freshness check is specific to a long-running server restarted in place. This project's portable Jellyfin restart is part of the sideload itself, so freshness is implicit. The MD5 digest record is new (apex relies on the commit SHA; release zips need their own identifier).
- **Generic exemplar note**: Tier 6 (Apex-coupled but generic-shaped). The discipline shape (download artifact, verify, append to deploy log, never touch prod) ports cleanly; the verification body is highly project-shape-specific.

### .claude/agents/plugin-scaffolder.md

- **Source**: D:\Apex-Platform\.claude\agents\plugin-scaffolder.md
- **Verdict**: Stub + flag
- **Rewrote**: the entire body as a NOT APPLICABLE stub. jellyfin-movie-catalog has one plugin and one viewer; there is no "scaffold a new sub-component" operation.
- **Apex-coupling found**: this is the canonical apex-only agent; its existence pre-supposes the hub-of-plugins shape.
- **Generic exemplar note**: Tier 6 (Apex-only). Open question for the generic-harness exercise recorded in the stub body: how does scaffolding generalize across project shapes? Apex: hub-of-plugins. This project: single plugin. Other shapes: microservices? Multi-language monorepos? Probably ships OFF by default in a generic harness, with the project's architecture rule naming the unit if one exists.

### .claude/agents/migration-author.md

- **Source**: D:\Apex-Platform\.claude\agents\migration-author.md
- **Verdict**: Stub + flag
- **Rewrote**: the entire body as a NOT APPLICABLE stub. This project has no database.
- **Apex-coupling found**: tightly coupled to Alembic-SQLAlchemy-Postgres-Python. Several checks (revision-id length caps, round-trip verification, large-table lock patterns, test-DB-matches-production-DB, seed values use primitives not expression nodes) generalize across stacks.
- **Generic exemplar note**: Tier 6 (Apex-coupled). Open question for the generic-harness exercise: what does a stack-agnostic schema-author look like? Probably one agent per stack (alembic-python, efcore-csharp, prisma-typescript) plus a NOT APPLICABLE stub, sharing a small set of language-agnostic checks.

### .claude/agents/integration-tester.md

- **Source**: D:\Apex-Platform\.claude\agents\integration-tester.md
- **Verdict**: Port + scrub (the stub framing ports clean; the "what this will be" body was rewritten for this project's cross-component target)
- **Kept verbatim**: the stub structure; the distinction from function-tester (mocks) and smoke-tester (only confirms up/down); the "awaiting first real cross-component flow" framing.
- **Rewrote**: the components. Apex's plan awaited Prism plugin; this project awaits the first plugin-to-GitHub-to-viewer flow. Target: real portable Jellyfin + real GitHub Contents API against a test repo + optional Playwright against deployed Pages viewer.
- **Generic exemplar note**: Tier 3 (Project-domain). Stub-framing-with-deferred-body is reusable; the body specifics vary.

### scripts/hooks/pre-commit

- **Source**: D:\Apex-Platform\scripts\hooks\pre-commit
- **Verdict**: Port + scrub
- **Kept verbatim**: Pass 1 (forbidden-name token scan); Pass 2 (harness-doctrine lexicon scan, inline-code-strip allowance for backtick-quoted examples); Pass 3 (plan-file naming convention regex); Pass 4 (design-question phrase scan); the sentinel-file mechanism; the coaching-tier-never-blocks discipline; the append-under-section helper.
- **Rewrote**: Pass 5a switched from `python -m unittest discover` against `hub_webapp/tests`/`*_plugin/tests` to `dotnet test plugin/tests/Jellyfin.Plugin.MovieCatalog.Tests.csproj` against the NUnit test project; the FAIL/ERROR parser was rewritten for the dotnet console-logger output format. Pass 5b switched from the Python AST scanner (`_pass5b_scan.py` walking `@unittest.skip` etc.) to an awk-based substring scan for `[Ignore` / `[Explicit` / `[Skip` / `[Fact(Skip` in `plugin/tests/*.cs`, with an adjacent-comment check as the justification heuristic. This is a placeholder until Roslyn AST parsing lands.
- **Dropped**: Pass 6 (test-code primitive duplication scanning `hashlib`/`hmac`/`secrets`/`cryptography` imports). A TODO comment at the top of the hook flags that Pass 6 should be re-added scanning `System.Security.Cryptography` usage when a crypto helper lands.
- **Apex-coupling found**: the venv-relative-python detection in apex's Pass 5a is project-specific (apex has a shared venv at the clone root). This project relies on a system-installed dotnet on PATH. `HARNESS_PATH_RE` also dropped `scripts/smoke.sh` since the project has no server-side smoke script.
- **Generic exemplar note**: Tier 3 (Project-domain). Multi-pass-with-debt-file structure is universal; Passes 1-4 port verbatim across projects that share the harness boundary concept; Pass 5+ vary per language. Pass 6 is reserved for primitive-duplication scans relevant to a project's specific crypto / helper concerns.

### scripts/hooks/pre-push

- **Source**: D:\Apex-Platform\scripts\hooks\pre-push
- **Verdict**: Port clean
- **Kept verbatim**: the entire orchestration shape (annotated-tag check, release-notes file check, quality-inspector subagent invocation); the Windows path fallback for the `claude` CLI; the 900s timeout; the JSON verdict parsing; PASS / DISCUSS / BLOCK switch; no-bypass and fail-closed-on-tool-brokenness disciplines.
- **Scrubbed**: references to apex-platform in error-message text; stale historical context (v0.1.26/v0.1.27 origin notes for the timeout) generalized.
- **Apex-coupling found**: this hook was already largely project-agnostic; only error-message text needed touching.
- **Generic exemplar note**: Tier 1 (Universal). The pre-push gate (annotated-tag + release-notes + quality-inspector, all fail-closed) is a clean reusable shape; every project on this harness can adopt verbatim with a one-line error-text swap.

### scripts/install-quality-hooks.sh

- **Source**: D:\Apex-Platform\scripts\install-quality-hooks.sh
- **Verdict**: Port + scrub
- **Kept verbatim**: the per-clone install mechanism (copy from `scripts/hooks/` to `.git/hooks/`); `--force`/`--help`/refuse-overwrite-if-differs discipline; worktree-aware HOOK_DIR; install-then-report shape.
- **Scrubbed**: help text updated to the five-pass pre-commit (with TODO for Pass 6 when crypto helper lands); apex references in help text.
- **Generic exemplar note**: Tier 1 (Universal). The install script is a clean reusable shape; only the help text varies.

### .claude/settings.json

- **Source**: D:\Apex-Platform\.claude\settings.json
- **Verdict**: Port + scrub
- **Kept verbatim**: the `statusLine` config; the `permissions.allow` shape; gh CLI read-only allowlist; git read-only allowlist.
- **Scrubbed**: apex-specific test-run permissions (`python -m unittest discover`) replaced with `dotnet build/test/restore/pack/publish`; apex hook invocations replaced with this project's; sudo-apex/systemctl permissions dropped (no VM).
- **Added**: `gh release download` for smoke-tester; `node --version` and `node docs/tests/run.js` for viewer testing; curated PowerShell permissions for smoke-tester's sideload (Get-ChildItem, Test-Path, Get-FileHash, Expand-Archive, Compress-Archive, Get-Process, Stop-Process, Start-Process, Invoke-WebRequest, Invoke-RestMethod, New-Item, and narrowly-scoped Remove-Item permissions limited to `D:\jf-dev\config\plugins\MovieCatalog` and `D:\jf-dev`).
- **Apex-coupling found**: apex embedded VM-management commands which dropped cleanly. PowerShell additions are structurally new (apex is Linux-only; this project is Windows-portable-Jellyfin-deployed).
- **Generic exemplar note**: Tier 5 (Apex-coupled but generic-shaped). Schema is universal; specific allowlist is project- and platform-specific.

### .claude/settings.local.json.example

- **Source**: D:\Apex-Platform\.claude\settings.local.json
- **Verdict**: Port + scrub
- **Kept verbatim**: the gitignored-local-override pattern; the permissions.allow shape.
- **Scrubbed**: apex's D:/Apex-Platform paths -> D:/GitHub/jellyfin-movie-catalog. Added `_comment` and `_purpose` fields documenting the intent (apex's version was bare; the example file gets extra explanation for first-time setup).
- **Generic exemplar note**: Tier 1 (Universal). Every project on this harness needs the local-overrides-not-checked-in pattern.

### .claude/statusline.sh

- **Source**: D:\Apex-Platform\.claude\statusline.sh
- **Verdict**: Port clean
- **Kept verbatim**: the entire script. Branch-fallback discipline, the must-never-exit-without-printing-branch comment, jq-availability check, the field composition.
- **Scrubbed**: comment header changed from "apex-platform statusline" to "jellyfin-movie-catalog statusline". Nothing else was apex-specific.
- **Generic exemplar note**: Tier 1 (Universal). Ports across every Claude Code project verbatim minus a one-line header swap.

### .claude/notes/INDEX.md

- **Source**: D:\Apex-Platform\.claude\notes\INDEX.md (catalog format only; entries dropped)
- **Verdict**: Port + scrub
- **Kept verbatim**: the catalog conventions header; the lifecycle paragraph; the file-naming conventions (`feedback_*`, `project_*`, `reference_*`, incidents/, deploys/).
- **Scrubbed**: every catalog entry. Apex has ~20 specific notes; this project starts with zero. The categories themselves (Architectural background / Operational / Deployment / Workflow / Historical / Plugin-specific) were apex-flavored and were dropped; categories will accumulate as notes do.
- **Generic exemplar note**: Tier 1 (Universal). The convention is universal; contents accumulate per project.

### .claude/notes/incidents/.gitkeep

- **Source**: NEW (apex's directory has real incident files; ours starts empty)
- **Verdict**: NEW
- **Content**: empty file
- **Generic exemplar note**: Tier 1 (Universal). Standard idiom for empty checked-in directories.

### .claude/notes/deploys/.gitkeep

- **Source**: NEW
- **Verdict**: NEW
- **Content**: empty file
- **Generic exemplar note**: Tier 1 (Universal). Same as above.

### .claude/commands/deploy-dev.md

- **Source**: D:\Apex-Platform\.claude\commands\deploy-dev.md
- **Verdict**: Port + scrub
- **Kept verbatim**: the four-step shape (surface quality debt; decide whether a conditional step is needed; emit the command block; tell the user to invoke smoke-tester); the read-only discipline; the "what you do NOT do" closing.
- **Rewrote**: the conditional-step decision (apex: alembic migration if a versions file is in the diff -> this project: confirm a release tag exists with a built artifact); the command block (apex `sudo -u apex git pull` + alembic + `systemctl restart` -> PowerShell gh release download + stop Jellyfin + wipe plugin dir + Expand-Archive + start Jellyfin + poll `/System/Info/Public`).
- **Apex-coupling found**: the four-step shape genuinely ports; command-block contents are entirely project-specific. The smoke-tester follow-up coupling is preserved (both projects have a smoke-tester that produces the OQE deploy log line).
- **Generic exemplar note**: Tier 3 (Project-domain). Four-step shape is reusable; the conditional and command block are per-project.

