# Workflow rules for jellyfin-movie-catalog

## Checking remote state is read-only

When the user asks to "check," "look at," "see if there's anything new," or otherwise inspect the remote (GitHub) state of the repo, use only read-only queries against the GitHub API: `gh api`, `gh release list`, `gh pr list`, `gh issue list`, `gh run list`, `gh repo view`, and the like.

**Never** `git fetch`, `git pull`, `git remote update`, or any other command that writes to local git state, when the user's request was a check. Those commands mutate `FETCH_HEAD`, remote-tracking branches, and tags. Even a silent `git fetch` that pulls no new refs has written to the local repo; the next time the user runs `git describe` or `git log origin/main`, they may see state they did not authorize.

Bringing remote changes into the local repo is a separate action that requires the user to explicitly ask: "pull main", "fetch the new tag", "bring down v0.1.x." The two operations are kept distinct: *checking what is out there*, and *bringing it down*. Only the second is allowed to write.

If a check requires authentication that `gh` provides but a raw HTTP call would not (e.g., querying private-repo state), `gh api` is still the correct tool. If `gh` is genuinely unavailable for a needed query, surface that to the user rather than reaching for `git fetch` as a substitute.

**How to apply:** Map the user's verb to the action class.

| User verb | Action class | Allowed commands |
|---|---|---|
| check, look, see, is there anything new, what's on the remote, what tags exist on github | read-only | `gh api`, `gh release list`, `gh pr list`, `gh issue list`, `gh run list`, `gh repo view`, `git ls-remote` (read-only by design) |
| pull, fetch, bring down, sync, update local | mutating | `git fetch`, `git pull`, `git remote update`, after the user said so |

When in doubt about which class a request falls in, ask before mutating. Never frame a `git fetch` as "checking."

Origin: cross-project user instruction; the apex-platform 2026-05-18 incident (a `git fetch --tags origin` was issued in response to "check gh for updates") motivated the explicit framing.

## Plan files live in-repo at `.claude/plans/`

When Claude Code's plan mode produces a plan file, it lands at `~/.claude/plans/<auto-slug>.md` on the user's machine by default. That path is not under version control and does not sync via GitHub. For jellyfin-movie-catalog every harness artifact MUST be in-repo so:

1. The plan is visible to anyone reading the repo on any machine.
2. Co-workers and future Claude sessions on a fresh clone see the same plan history.
3. The harness can later be genericized as an exemplar for other projects.

**Rule:** As the first step of implementation (immediately after the user approves a plan via `ExitPlanMode`), move the plan file from the global path into `.claude/plans/<descriptive-name>.md`. Rename the auto-generated slug to a name that conforms to the §"Plan file naming" rule below. The global copy can be deleted; the in-repo copy is canonical.

**Why this rule and not "move plans before approval":** plan mode is a Claude Code feature that writes to its own configured location; we do not interfere with it during the planning loop. The move happens at the boundary between planning and implementation, which is a moment we control.

**How to apply:**

1. Read the plan file from `~/.claude/plans/<auto-slug>.md`.
2. Choose a descriptive filename per the naming rule below.
3. `Write` the content to `.claude/plans/<descriptive-name>.md`. While doing so, scrub em-dashes per `style.md`.
4. Update any references (CLAUDE.md, commit messages drafted earlier, other plan files) to point at the in-repo path.
5. Delete the global copy (optional but tidier).

This rule also applies to ad-hoc design docs that come up mid-implementation: if the work product is "a plan for future work," it lives at `.claude/plans/`, not in a chat scratchpad or a user's local notes.

## Plan file naming

Plan filenames in `.claude/plans/` MUST match the regex `^v\d+\.\d+\.\d+-[a-z0-9-]+\.md$`. The leading `vX.Y.Z` is the target tag the plan ships under; the slug is 2-5 hyphenated lowercase-alphanumeric words describing the work.

Examples that pass: `v0.1.0-initial-plugin-skeleton.md`, `v0.1.3-debouncer-tuning.md`, `v0.2.0-viewer-filter-ui.md`.

Examples that fail: `debouncer-tuning.md` (no version), `viewer-filter.md` (no version), `plugin-skeleton-v0.1.0.md` (version trailing, not leading).

If a plan starts before a tag is assigned (exploratory work, design discussion), use the placeholder `vNEXT-<slug>.md`. Rename to the real version once the tag is decided. This is a placeholder, not a permanent shape; a plan that ships with `vNEXT-` in its name at tag time is non-conforming.

Mechanical trigger: the pre-commit hook's Pass 3 scans new files in `.claude/plans/` against the regex and appends non-conforming names to `.claude/notes/quality-debt.md` under §"Plan naming findings". The pre-push hook on tag refspecs blocks via `quality-inspector` if any unresolved entry remains. There is no judgment-call override.

How to apply: when moving a plan from `~/.claude/plans/<auto-slug>.md` into the repo, the in-repo name MUST already match the regex. Do not commit a plan with the auto-generated global slug as its filename; rename at move time.

## Plan-mode first for multi-file work

Before generating code or content for non-trivial work, enter plan mode (Claude Code's plan mode if available; otherwise produce a plan file at `.claude/plans/<descriptive-name>.md` and get user approval) and wait for the user to approve before executing.

**Plan mode is required when ANY of the following is true:**

- The work creates a new file (any file, anywhere in the repo or harness).
- The work modifies 2 or more files.
- The work introduces a new decision not covered by an existing approved plan, even if it touches only one file.

**Plan mode is NOT required when ALL of the following are true:**

- The work is a single-line or few-line fix.
- The fix is inside one file you have already opened in this session for an approved task.
- No new decisions are being made (the change is the mechanical execution of something already agreed).

**There is no `obvious pattern` carve-out.** `This is just two more files like the ones I already drafted` is exactly the rationalization the prior version of this rule permitted, and exactly the rationalization the new rule version forbids. If you find yourself thinking `this is obvious enough to skip planning`, that is itself the tell that planning is needed: the work has crossed enough threshold that you are constructing a defense for skipping the discipline.

**How to apply:** Before starting any work, ask three yes/no questions:

1. Will this create a new file?
2. Will this modify 2 or more files?
3. Does this introduce a decision not in the current approved plan?

If any answer is yes, enter plan mode. The questions are mechanical to defeat momentum bias.

When in doubt, plan. The cost of a small plan that turns out to have been overkill is minutes; the cost of multi-file work without a plan is the kind of drift the user has to catch and the assistant has to retract.

See also `quality.md` §"Tells that quality is slipping" tell #8 (plan-momentum bias), which gives the `quality-inspector` agent license to flag a diff that touches 2 or more files without an approved plan.

Origin: apex-platform 2026-05-19 incident. Immediately after shipping one tag, the assistant began drafting two new rule files without entering plan mode. The triggering rationalization was "I have momentum from the prior plan and these are just two more rule files." The prior plan explicitly did not cover those files; the work was a new decision. The user caught it; the rule was rewritten to remove the "obvious patterns" carve-out that had permitted the rationalization.

## Design questions surface via AskUserQuestion, not buried in plan text

In plan mode, when a design decision requires user judgment, surface it via `AskUserQuestion` BEFORE writing the plan section that depends on the answer. Forbidden pattern: surfacing the question in plan text AND embedding your preferred answer in the same text or in the plan file ("here's my recommendation: X; see plan for details"). The user reviewing the plan should not have to spot embedded decisions they did not explicitly approve.

**Mechanical triggers** (the pre-commit Pass 4 hook scans `.claude/plans/*.md` for these phrases on staged adds; appearance is a finding that contributes to the post-commit sentinel and gets re-verified at tag push):

- `my recommendation` / `i recommend`
- `i lean toward` / `i lean`
- `open question` / `design question` / `question to resolve`
- `i'll go with`
- `resolved here` (catches the `(resolved here)` framing too)

**What does NOT require asking** (decide inline; mechanical or established-pattern choices):

- Naming following an established convention (`PascalCase.cs` filename per `style_csharp.md`, viewer files per `frontend.md`).
- Test file location, import order, other style-mechanical choices.
- Choices the user has already decided in `CLAUDE.md`, a rule file, or a prior session's plan.

**Mitigation when uncertain**: ask. The cost of an unnecessary `AskUserQuestion` is a small interrupt. The cost of an unsurfaced decision is the user catching it later, at which point trust is burned and the plan needs revision.

**How to apply**: when drafting plan-mode text, if a sentence begins with "my recommendation" / "i lean" / "open question" / "design question", stop. Call `AskUserQuestion` instead. Only after receiving the user's answer, return to the plan text and write the resolved decision (attributed to the user where relevant, e.g., `(decided by user 2026-MM-DD)`).

Origin: apex-platform 2026-05-22 v0.1.26 planning. The assistant surfaced a design question alongside their preferred answer in the same response, then wrote the plan assuming the preferred answer. The user caught it: "why did you surface the design question then answer it yourself without asking me?" The rule and its mechanical Pass 4 enforcement shipped together so the doctrine and the hook ride out at the same time (per `quality.md` tell #9 lineage).

**Why prose AND hook ship together**: deferring the mechanical enforcement to a later tag is the same anti-pattern this rule exists to catch: a rule without a hook is a documents-and-collects-debt asset, exactly the pattern the harness is supposed to defeat. Both ship in the same tag.

## Test failures are investigated, not labeled

`pre-existing`, `environment issue`, `infrastructure issue`, `known failure`, `flaky`, `intermittent` are NOT triage categories. They are labels that excuse not investigating. Every persistent test failure either has a root cause (find it) or has been explicitly investigated and accepted with a documented exception (write it). The label-without-investigation pattern is forbidden.

**Mechanical triggers** (the pre-commit Pass 5 hook scans on every commit; entries get re-verified at tag push):

- **Pass 5a (failing tests)**: every test reported as `FAIL` or `ERROR` by `dotnet test` against the plugin's test project becomes an entry under `## Failing test findings` in `.claude/notes/quality-debt.md`. Entry format: `<timestamp> | <test name> | <FAIL|ERROR> | <first line of error>`. (Implementation note: the Pass 5 hook script needs to be authored for `dotnet test`'s output format; the apex-platform original was written for Python's `unittest discover`. The shape of the rule is identical; the parser changes.)
- **Pass 5b (undocumented skips)**: every test decorated with NUnit's `[Ignore("...")]` (or xUnit's `Skip = "..."` on `[Fact]` / `[Theory]`, or whatever skip mechanism the chosen test framework uses) MUST have a documented reason in the attribute itself (not a bare `[Ignore]`). The pass scans the test source files for skip-shaped attributes lacking a reason argument; undocumented skips become entries under `## Undocumented skip findings`.

**Soft at commit, hard at tag** (matches the Pass 1-4 pattern):

- Pre-commit Pass 5 NEVER blocks the commit. WIP commits to feature branches, mid-refactor breakage, intentional spikes: all proceed. The hook logs findings to `quality-debt.md` and surfaces via the sentinel; the assistant enumerates them in the response per §"Surface harness debt explicitly after every commit that produces one".
- Pre-push on tag refspecs invokes `quality-inspector`, which re-verifies every failing-test entry by re-running the test and every undocumented-skip entry by re-scanning the source. Unresolved entries block the tag push. No bypass.

**Investigation path when a failure surfaces**:

1. Read the actual error first line. Do NOT label.
2. If the cause is in the test infrastructure (mock leak, missing dep, env-dependent path): fix the test setup, not the system under test.
3. If the cause is in the system under test: fix it or revert the change that broke it.
4. If the failure is genuinely intentional (e.g., known-broken feature under refactor): convert to a skip with a documented reason (`[Ignore("waiting on v0.2.x source-rewrite")]`). The reason is the exception that documents itself.
5. NEVER apply a label like "pre-existing" and move on. The label is the anti-pattern; the investigation is the rule.

**Cross-reference**: same shape as §"Design questions surface via AskUserQuestion, not buried in plan text". Both are evasion-by-labeling. That rule catches the planning version; this rule catches the runtime version. Together they cover the two surfaces where the assistant tends to apply a category in order to avoid examining its contents.

Origin: apex-platform 2026-05-22 v0.1.26 cleanup. An engine-mock leak in a test class caused 10 of 19 tests to fail across three tags; each tag, the failures were labeled "pre-existing environment issues" and shipped uninvestigated. Root cause was visible in five minutes once actually examined.

## Surface outstanding quality debt before giving deploy commands

When you finish a task that produced commits, and before you give the user any deployment commands (build, package, sideload, GitHub release, etc.), you MUST explicitly summarize outstanding entries from `.claude/notes/quality-debt.md` that touch any commit in the current task's diff.

The format:

```
Quality debt outstanding (before you deploy):
  - <file>:<line> - <category> `<token>` (<timestamp>)
  - ...

These will hard-block the next tag push. Recommend resolving before deploy, or accept the block at tag time.
```

If `quality-debt.md` is empty, or no entries reference files touched by this task's diff, state that explicitly:

```
Quality debt: none outstanding for this task.
```

Silence is not auditable. Stating "none outstanding" makes the absence verifiable; omitting the surfacing step makes it ambiguous whether you forgot to check.

**Why:** The `pre-commit` hook (Tier 1 of `quality-inspector` enforcement) appends findings to `quality-debt.md` and prints them to stderr at commit time. Stderr scrolls away. The `@`-import of `quality-debt.md` into `CLAUDE.md` keeps the file in the session's context, but passive presence does not equal active acknowledgement. Restating debt at task completion, in the same response as the deploy commands, gives it the visibility it needs at the moment it matters most: just before code crosses the boundary into a deployed artifact.

This is the third tier of `quality-inspector` enforcement (after `pre-commit` coaching and `pre-push` hard gate on tags). See the doctrine at `quality.md`.

**How to apply:**

1. After the task's last commit, before composing your wrap-up text, read `.claude/notes/quality-debt.md`.
2. Filter entries to those whose file is in the diff of commits made during this task (`git log --name-only <task-start>..HEAD`).
3. Re-verify each filtered entry: does the recorded token still appear in the file? If not, it is stale; do not surface it.
4. Render the remaining entries in the format above, ahead of the deploy commands. If the filtered-plus-verified set is empty, render the "none outstanding" line.

## Surface harness debt explicitly after every commit that produces one

When the pre-commit hook appends a finding to `.claude/notes/quality-debt.md` (forbidden-name scan, harness-doctrine lexicon scan, or any future debt category), the assistant MUST explicitly enumerate the new entry in the response following the commit. Format:

```
Harness debt logged this commit:
  - <file>:<lineno> - <category> `<token-or-phrase>` (<timestamp>)
  - ...
```

Stderr is not surfacing. The `@`-import of `quality-debt.md` into `CLAUDE.md` is not surfacing for the current turn (the import reflects file state at session start, not mid-session appends). Saying "see quality-debt.md" is not surfacing. The entry must appear in conversation, in the response immediately following the commit, with no omission or paraphrase that obscures what was logged.

There is no "the entry is minor" exception, no "it is a known quote in an example" exception, no "the user can see stderr" exception. The user decides what to do with the debt; the assistant's job is to make sure they see it.

This pairs with §"Surface outstanding quality debt before giving deploy commands": that rule fires only at deploy time and only against the current task's diff. This rule fires at commit time, against every commit, surfacing any new entry produced by that commit. Both rules apply; neither replaces the other.

**How to detect:** the pre-commit hook writes a sentinel file at the worktree's git dir containing the count of new findings produced by that commit. The path is `$(git rev-parse --git-dir)/.harness-debt-this-commit`. In a normal clone this resolves to `.git/.harness-debt-this-commit`; in a Claude Code worktree it resolves to something like `<main-repo>/.git/worktrees/<wt-name>/.harness-debt-this-commit`. The hook resets the sentinel at the start of every run, so its content always reflects the most recent hook invocation. After every commit:

1. Read the sentinel. If absent or `0`, no surfacing is required.
2. If non-zero, read the last N entries of `.claude/notes/quality-debt.md` (N matches the sentinel value, split between H2 sections based on the count breakdown the hook printed to stderr).
3. Enumerate them in the response using the format above.
4. Optionally adjudicate in the same response: "These are quoted examples and not real rule additions; safe to leave" or "this is a real escape valve and needs to be rewritten before the next tag." But surfacing comes first; adjudication is optional.

Origin: apex-platform 2026-05-19. The user pointed out that "prints to stderr" is the same shape of carve-out we had just removed from the plan-mode rule (a "soft enforcement, the assistant can see it if they want" framing). "Logged" was being treated as equivalent to "surfaced," when those are different things.

## Pending draft items

Topics this rule file does not yet encode but should, once the relevant workflow has been exercised enough on this project to know what is canonical:

- Trunk + tags promotion: full rules, not just an IMPORTANT summary in CLAUDE.md. The discipline (annotated tags, hand-written release notes per tag, pre-push gate verifying both) ports from the apex-platform precedent.
- The "main is shippable" discipline: direct-commit-vs-feature-branch heuristic, the "ship-right-now" mental test, the one-feature-branch-per-concern guideline.
- Multi-machine push/pull rituals: pull-before-work, push-before-EOD, rebase-on-diverge.
- Pre-commit checks beyond quality-debt: format check (`dotnet format`), build, tests, secret-scan (when the secret-scan layer is added).
- **Graduation discipline**: rule of thumb for when CLAUDE.md content has stabilized enough to move to `.claude/rules/`. The cross-project version: a week without modification suggests it has stabilized.

Plan-mode-first and state-update discipline are already covered above (the §"Plan-mode first for multi-file work" rule; the CLAUDE.md IMPORTANT for state updates) and graduated out of this TODO when they did.
