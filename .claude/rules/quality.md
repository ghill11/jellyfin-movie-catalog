# Quality discipline for jellyfin-movie-catalog

> The project's analog of the U.S. Navy SUBSAFE program: a quality discipline applied at every phase of work, with a defined boundary, objective evidence requirements, and a no-bend protocol under cost, schedule, and authority pressure.

## Why a program, not a checklist

In April 1963, USS Thresher imploded during deep-diving trials. 129 dead. The investigation traced contributing causes to undocumented silver-brazed joints in seawater systems and material control failures that no checklist had caught. The Navy stood up SUBSAFE within ~60 days. In the decades since, exactly one submarine has been lost (Scorpion, 1968, not SUBSAFE-certified).

SUBSAFE works because it is a program, not a checklist:
- It defines a **boundary** (which systems are covered).
- It requires **objective quality evidence** (OQE): documented, traceable proof that every requirement was met. "I'm pretty sure" is disqualifying.
- It is enforced by **independent verification**: the people verifying are not the people who did the work.
- It does not bend for schedule, cost, or rank.

This project (a Jellyfin plugin plus a static viewer) is personal-scale, but it lives on a public repo, talks to a public GitHub API with a credential, and produces an artifact a Jellyfin server loads at process startup. A bug in the plugin can break a self-hosted media server's startup; a leaked PAT in a log is a real credential exposure. The cost of careless code compounds across versions: a fragile fix becomes load-bearing for the next feature built on top. The discipline is the same regardless of scale.

This file is the operating doctrine. It is `@`-imported into every Claude Code session in this repo and is non-negotiable.

## The boundary

**Inside the boundary** (full discipline applies): every line of code, configuration, and infrastructure that ships to either deploy target. This includes:

- C# plugin code, embedded resources (configPage.html, the plugin meta.json), build scripts producing the zipped release artifact.
- Static viewer code (HTML, CSS, JavaScript) served from GitHub Pages.
- GitHub Actions workflows (build, release-on-tag).
- The plugin's interaction with the GitHub Contents API (PAT handling, request shape, retry logic).
- Anything that lands in a tagged release artifact or in `viewer/` on `main`.

**Outside the boundary** (lower bar, but still cared for): dev tooling, `.claude/` harness, notes, exploratory scripts, one-off experiments that never ship to a release.

If you are unsure which side a change is on, assume inside.

## The harness boundary

A subset of the "inside the boundary" set that the program watches with extra suspicion: the files that define the boundary, the rules, the agents, and the enforcement hooks themselves. Changes to these files cannot be reviewed by the same agents they govern without circular reasoning; the doctrine treats them as a special case.

**Inside the harness boundary**:

- `.claude/rules/*.md`
- `.claude/agents/*.md` (when the agent set is introduced)
- `.claude/commands/*.md` (when commands are introduced)
- `CLAUDE.md`
- `scripts/hooks/*` (when the hook set is ported)
- `scripts/install-quality-hooks.sh` (when ported)
- `.github/workflows/*.yml`

Additional rules apply to harness-boundary diffs:

- The pre-commit lexicon scan (Tier 1 coaching) flags judgment-call phrases. See `scripts/hooks/pre-commit` Pass 2 once ported.
- `quality-inspector` (when ported) runs the harness-diff check at tag push as the hard gate.
- A workflow YAML change that modifies release artifact assembly paths, fetch behavior, or hook orchestration is a hard finding (BLOCK) absent verification evidence in the commit message body. Acceptable evidence: an explicit `Verified locally with: <command>`, `Verified in workflow run <id>`, or test-pass link.
- A hook script change that adds a new fail-open path (logs but does not block, when blocking would be appropriate at that gate) is BLOCK. Pre-commit is intentionally fail-open as Tier 1 coaching; extending that is fine. Adding fail-open to pre-push (the hard gate) is BLOCK.
- A rule that asks the assistant to make a judgment call (the phrases in the lexicon, or other interpretive escape valves) is BLOCK per tell #9.

The reasoning: any of these slipping into the doctrine erodes the discipline that protects the rest of the boundary. The cost of a tightened harness is a slightly more deliberate edit cycle; the cost of an eroded harness is the kind of multi-anti-pattern session the originating apex-platform incidents catalog (see tell #9 origin).

## The phases

Quality is applied at every phase. Each phase produces evidence (see "Objective evidence" below) that the discipline was followed.

### 1. Brainstorm / scope

Distinguish symptom from cause before any solution is discussed. A request like "make X work" or "fix Y" is interrogated:
- What is the symptom (what does the user see)?
- What is the cause (what is actually wrong)?
- What evidence will let us know we have removed the cause, not just hidden the symptom?

Output a problem statement in **symptom / cause / desired-evidence** form before proposing approaches.

### 2. Plan

Plans must:
- Name the root cause explicitly. Symptom-only plans are rejected before code is written.
- Cite prior patterns searched (the current codebase, public Jellyfin plugin exemplars, this project's own `.claude/notes/`, the doctrine in `.claude/rules/`). "I didn't find one" requires evidence of looking.
- State which side of the boundary the change is on. Inside-boundary changes carry a verification section that names the end-to-end test.
- Be approved (by the user) before code is generated.

### 3. Code

Before generating code, run the tells checklist (below). If any tell fires, code is not generated until the protocol is followed.

While coding:
- Reuse existing functions, helpers, patterns. Inventing a new helper requires evidence that no existing one fits.
- Comments explain WHY (constraint, gotcha, prior incident, link to context). Comments never restate WHAT.
- Names are honest. See "Forbidden names" below.

### 4. Test

Tests verify cause-removal, not symptom-absence.
- Integration tests run against real boundaries where feasible (a real Jellyfin server for plugin smoke, a real test GitHub repo for the Contents API push). Mocked boundaries are a known failure mode: when the mock and the real boundary drift, the tests pass while the production behavior breaks.
- Tests cover the golden path AND the failure mode the change addresses.
- Verification covers what a human reviewer would manually check.

### 5. Fix / debug

The fix protocol is the strictest application of the discipline because it is where band-aids historically slip through:

1. Write **symptom / cause / proposed change** before any patch.
2. If the proposed change does not eliminate the cause, it is not a fix. Stop. Find the proper solution.
3. Present the proper solution with implementation cost, scope, risk, and verification. If the proper solution is genuinely too large for the moment, that is a **scoping conversation** (descope, defer, split the work), not a band-aid conversation. Band-aids are not offered as the alternative to a proper solution.
4. The user decides scope. The user does not decide between "proper" and "band-aid" because that choice is not on the table.

### 6. Deploy

Every tag MUST produce a phase-6 PASS line in `.claude/notes/deploys/dev-log.md`. There is no "harness-only" or "docs-only" exemption: the chain of PASS lines is the OQE the `quality-inspector` checks against, and a missing line is a hard finding even if the diff would not have affected the running artifact. The cost of a smoke is small (build + sideload + restart + spot-check); the cost of a missing chain link is doctrine erosion.

The phase-6 ritual for this project:

1. **Build the release artifact.** Either locally (`dotnet publish -c Release` plus the zip step) or by pushing the tag and letting the GitHub Actions `release-on-tag` workflow build it. Verify the released zip contains exactly the expected files (the plugin DLL, any dependency DLLs, `meta.json`) and that the MD5 in the release matches what Jellyfin will see.
2. **Sideload to the dev Jellyfin instance.** The portable Jellyfin lives at `D:\jf-dev\` for local development. Procedure: stop the dev instance, drop the unzipped plugin into the plugins folder, restart, confirm the plugin appears in the admin UI.
3. **Smoke-trigger.** Open the plugin configuration page. Run the "Test Connection" action against a known-empty test GitHub repo. Then trigger "Resync Movie Catalog Now" and confirm the push happens (movies.json updated in the test repo, log line in Jellyfin's log indicating success).
4. **Record the evidence.** Append a one-line entry to `.claude/notes/deploys/dev-log.md` with: tag, timestamp, build verdict (PASS / FAIL / partial), the test-repo state observed, and any anomaly. This line IS the phase-6 OQE.

For the viewer side specifically: every tag that touches `viewer/` ALSO requires confirming GitHub Pages rebuilt and the new content is live. Steps: push, wait for the Pages deploy to complete (`gh api repos/<owner>/<repo>/pages/builds/latest` confirms `status: built`), curl the Pages URL and sanity-check the response. Record the evidence in the same dev-log line.

When the project grows additional environments beyond dev (production-target Jellyfin, a separate prod GitHub repo for movie data), each gets its own deploy-log file under `.claude/notes/deploys/`. The shape of the rule does not change; the per-env file is just where the evidence lands.

### 7. Maintain / refactor

When touching old code, evaluate whether prior decisions still hold. If a band-aid is found in the tree (rare, but possible from pre-discipline work), surface it for graduation to a proper solution. Do not silently preserve it; do not silently extend it.

## Tells that quality is slipping

These apply at every phase: code, plan, fix, refactor, infrastructure, configuration. If any tell fires, stop and run the protocol below.

**All tells apply to test code unless explicitly noted otherwise.** A test reimplementing a production primitive, a test with a special-case branch that exists only to avoid a real bug, a test whose only justification is a comment: all are the same anti-patterns as in production code. The mechanical Pass scans already cover test files (Pass 1 forbidden-name, Pass 5a failing tests, Pass 5b undocumented skips, Pass 6 test-code primitive duplication); the LLM-judgment scans (tells #1-6, #8) MUST extend the same scrutiny to test files that they apply to production. The originating apex-platform v0.1.26 ship-then-catch incident is the worked example: tell #6 caught a production duplicate and missed the test duplicate because the inspector's prose focused on production code.

1. You are adding state (cookie, flag, header, session key, sentinel file, env var, on-disk marker) whose only job is to alter the next request's or next run's behavior.
2. You are adding a special-case branch to accommodate state, input, or behavior elsewhere that you believe shouldn't exist as it does.
3. You are adding a sleep, retry, debounce, or suppress-once gate to paper over a race or ordering bug. (The plugin's library-event debouncer is a legitimate debouncer that collapses bursts; this tell catches the OTHER use, where a sleep is hiding a race.)
4. The code needs a comment to explain to a future reader why it exists at all. Real code usually reads as obvious in hindsight; only constraints and gotchas need provenance.
5. Correctness depends on transient state staying put: a cookie not being cleared, a cache not being flushed, a file not being deleted, a process not restarting at the wrong moment.
6. There is a prior pattern (a sibling module, the same codebase elsewhere, `.claude/notes/`, an established library convention) that solved the same problem differently. You did not look. **Test code is in scope**: a test reimplementing a production primitive (a hash, an HTTP retry, lookup logic, etc.) is the same drift risk as production-side duplication. Use the production helper in tests; reserve direct primitive calls for the helper's own dedicated test file (which Pass 6 maintains on an allowlist).
7. A variable, flag, function, or file name contains `_post_`, `_just_`, `_suppress_`, `_skip_`, `_bypass_`, `_hack_`, `_temp_`, `_workaround_`. The name is the code admitting it is suspect.
8. You are about to edit a second file in this session without an approved plan that covers both. The pattern is plan-momentum bias: a prior plan finished, the work feels like it continues, and the second file gets touched without surfacing the new scope. The triggering rationalization sounds like "this is obvious enough to skip planning" or "this is just N more of the same thing I already did." The mitigation: stop, name the new decision, propose the plan, wait for approval. See `workflow.md` §"Plan-mode first for multi-file work" for the originating incident and the mechanical trigger questions.
9. The diff under review touches a file inside the harness boundary (see §"The harness boundary"), and the added text contains interpretive escape-valve language. The pre-commit Pass 2 lexicon catches the common phrases mechanically; the quality-inspector reviews structural cases at tag push. The phrases historically associated with this anti-pattern: `obvious`, `warrants`, `deserves`, `as appropriate`, `when necessary`, `feel free`, `you may`, `nice to have`, `if you feel/need/want to`, `should consider`, `unless you have a reason`, `worth it`, `usually proceed`, `often proceed`, and any phrase that turns a rule into a judgment call the author makes at the moment of acting. The originating apex-platform 2026-05-19 cycle found five instances in one session. The mitigation: rewrite the rule with a mechanical trigger (count, file existence, regex match, hook gate) or remove the carve-out entirely. Quoting a bad phrase as an EXAMPLE inside a rule file is fine if the quote is inside backticks (the Pass 2 scan strips inline-code spans before matching); the lexicon-as-prohibition is about authored rule text, not quoted examples.

10. The diff under review touches a file in `.claude/plans/`, and the added text contains design-question phrases the assistant typed alongside their own preferred answer. The pre-commit Pass 4 lexicon catches the phrases mechanically (`my recommendation`, `i recommend`, `i lean toward`, `i lean`, `open question`, `design question`, `question to resolve`, `i'll go with`, `resolved here`). The originating apex-platform 2026-05-22 v0.1.26 planning session is the worked example. The mitigation: use `AskUserQuestion` to surface the question BEFORE writing the plan section that depends on the answer; record the user's resolved answer in the plan (attributed to them, e.g., `(decided by user 2026-MM-DD)`). Doctrine in `workflow.md` §"Design questions surface via AskUserQuestion, not buried in plan text".

11. A test failure or test-runner error is being applied a label (`pre-existing`, `environment issue`, `infrastructure issue`, `known failure`, `flaky`, `intermittent`) without an accompanying investigation of the actual error. The label is the anti-pattern; the investigation is the rule. The pre-commit Pass 5 hook catches every failure and error mechanically (Pass 5a) plus every undocumented skip attribute (Pass 5b); the quality-inspector re-verifies entries at tag push and BLOCKs on any unresolved. The originating apex-platform 2026-05-22 incident: a mock leak caused 10 of 19 tests to fail across three tags, each tag labeled "pre-existing environment issues"; root cause was visible in five minutes once actually investigated. The mitigation: read the actual error first line BEFORE applying any label; fix the test infrastructure or the system under test; convert genuinely-intentional skips to a skip-with-documented-reason form. Doctrine in `workflow.md` §"Test failures are investigated, not labeled".

## Protocol when a tell fires

1. **Stop and write the triage in chat before proposing anything:**
   - **Symptom:** what the user sees.
   - **Cause:** what is actually wrong.
   - **Why the current draft does not address the cause:** one sentence.

2. **Find the proper solution.** Search the current codebase, public Jellyfin plugin exemplars, and `.claude/notes/` for an existing pattern. Investigate until the proper solution is concrete enough to estimate.

3. **Present the proper solution with:**
   - One-paragraph description.
   - Files touched and rough size of diff.
   - Risks and verification plan.
   - Recommendation: ship the proper solution as-is, or scope it (descope, defer, split). Executive-summary length; detail on request.

4. **Do not propose band-aids as an alternative.** If the proper solution is too large for the moment, that is a scoping conversation: descope the feature, defer the work to its own ticket, or split the change into stages where each stage is itself a proper solution at a smaller scope. The user decides scope. The user is not offered a "ship a kludge" path because that path is not on the table.

## Objective evidence

Each phase produces evidence that the discipline was applied. If you cannot produce the evidence, the work did not meet the standard.

| Phase | Evidence |
|---|---|
| Brainstorm | Symptom/cause/desired-evidence statement in the chat or a note. |
| Plan | A plan file naming the cause, citing prior-art search, with a verification section for boundary changes. User-approved before code starts. |
| Code | Commit message that names what changed and WHY (not what; diff already shows that). Linked plan reference (or `Plan-Ref:` trailer) for every inside-boundary commit. |
| Test | Passing test output. For boundary changes, integration-level evidence (real Jellyfin instance for plugin smoke, real test GitHub repo for the Contents API push). |
| Fix | The symptom/cause/change writeup, followed by the proper-solution presentation, before any patch. |
| Deploy | A PASS line in `.claude/notes/deploys/dev-log.md` matching the tag: build verdict, sideload outcome, test-trigger result, viewer-rebuild confirmation when `viewer/` was touched. Required for every tag, no exceptions (including harness-only or docs-only tags). |

For projects on this harness that introduce regulated-data handling (PII, FERPA-style educational records, HIPAA-style health records, financial credentials beyond a single API token), additional evidence categories land: an audit log of identity-bearing events, a retention rotation discipline, a decryption-helper boundary. The full pattern is captured in `database.md` §"Audit log retention" as preloaded doctrine; it does not exercise in the current project (no regulated data, no persistent identity store), but the shape is there when it does.

## Agent ownership

The quality program is enforced not only by rule files and hooks but by functional agents. Each owns a specific artifact set, and each encodes verdict logic, structural invariants, and format conventions that the main assistant cannot see from the call site. When an agent owns the artifact, only the agent produces it.

### The owned artifact set (preloaded; lands as agents are ported)

| Agent | Owned artifact(s) |
|---|---|
| `quality-inspector` | PASS / DISCUSS / BLOCK verdicts at any phase; pre-push hard gate on tag refspecs. |
| `code-reviewer` | BLOCK / WARN / NIT findings on diffs. |
| `smoke-tester` | Lines in `.claude/notes/deploys/<env>-log.md`. |
| `function-tester` | Per-handler test files. |
| `incident-recorder` | Files under `.claude/notes/incidents/`. |

The main assistant invokes these agents (once ported into `.claude/agents/`). The main assistant does NOT produce their owned artifacts inline.

### Mechanical triggers (any one fires the rule)

1. **Artifact ownership match.** The next thing the main assistant is about to write to disk matches an owned artifact pattern in the table above. The main assistant invokes the agent instead of writing the file.

2. **Transient API error from an invoked agent.** Errors named `Overloaded`, `ServiceUnavailable`, `RateLimited`, `Timeout`, `InternalServerError`, or any 5xx-class response from the API. These are retryable by definition. The main assistant retries the agent invocation. The main assistant does NOT produce the artifact inline.

3. **Verdict disagreement.** The agent returned a structured verdict and the main assistant reads the underlying data differently. The main assistant surfaces both views to the user with the evidence for each. The main assistant does NOT overwrite the agent's verdict in the artifact.

### What does NOT fire the rule

- **User override.** The user explicitly directs the main assistant to write the artifact inline (e.g., "just go append the line yourself"). The directive overrides the rule for that turn.
- **Ad-hoc work outside any owned artifact set.** A `git log`, a `grep`, a `git diff`, an exploratory read. None of these touch an owned artifact.
- **Pre-invocation scoping decisions.** Before any agent is invoked, the main assistant judges in the plan that the task is small enough to do inline. This is a planning decision recorded in the plan file, not a runtime substitution. Once an agent is invoked, the rule fires; this exclusion applies only to the pre-invocation window.

### Origin

Apex-platform 2026-05-20 v0.1.21 phase-6 deploy. The main assistant invoked `smoke-tester` to write the PASS line for a release tag. The first invocation returned `API Error: Overloaded`. The main assistant treated that as terminal and produced the line inline. The user caught it before push, reverted, and re-invoked the agent. The retry succeeded.

The substitution bypassed: (a) the agent's verdict logic; (b) the freshness invariant check; (c) the payload whitelist; (d) the audit-trail uniformity that the `quality-inspector` reads at tag push.

The rule preloads here so that when agents land on this harness, the substitution pattern is already disallowed.

## Forbidden names

These tokens in identifiers are the code naming its own suspicion:

`_post_`, `_just_`, `_suppress_`, `_skip_`, `_bypass_`, `_hack_`, `_temp_`, `_workaround_`, `_quick_`, `_fixme_`, `_todo_<verb>_`

If a draft reaches for any of these, the name is the tell. Rename and re-examine; usually the rename forces a better design.

## Pressure resistance

The discipline holds under:
- "We have a release deadline."
- "The user is in a hurry."
- "It is the end of the session."
- "I have already invested in this approach and rewriting feels expensive."
- "It is just a small change."
- "Nobody will notice."

These are exactly the moments the discipline exists for. SUBSAFE was created because schedule pressure under bureaucratic incentives is what killed Thresher. Cost pressure under deadline incentives is what ships fragile platforms.

If a moment feels like an exception to the discipline, it is not. It is the moment the discipline is doing its job.

## The originating incident

This file exists because of a specific failure mode on a prior project (apex-platform): the assistant proposed an `apex_post_logout` cookie to suppress auto-OAuth on unauthenticated revisits to the application root after sign-out. The user pushed back. The proper solution was to stop auto-OAuthing on the root at all: make it public, render a welcome page when unauthenticated, require an explicit "Sign in" click. No cookie, no flag, no state. The pattern was already in the prototype the project succeeded. The assistant had not looked.

The band-aid would have shipped. It would also have been fragile (cookie expiry, private browsing, manual clear), invisible to future maintainers (a callback inspecting a cookie for reasons not in the code), and load-bearing for correctness in a way that is untestable without simulating cookie loss. Every one of those costs compounds across a platform that intends to run for years.

The doctrine in this file is the response. The next time a similar moment arrives on jellyfin-movie-catalog or any future project on this harness, the protocol above is the answer, not the inventiveness that produced the cookie.
