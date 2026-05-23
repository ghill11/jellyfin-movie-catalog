---
title: auth
type: rule
status: active
created: 2026-05-19
---

# Auth rules

> **Dormancy notice**: jellyfin-movie-catalog has no user-facing authentication. The Jellyfin plugin runs inside the user's own Jellyfin server (which has its own auth); the static GitHub Pages viewer is public-by-design. This file is dormant - the rules below do not exercise on any code path in the current project.
>
> The rules are kept INTACT because OAuth boundary discipline, gate predicates, session.clear() rules, and role-promotion-only upsert patterns are universal to any authenticated system. The moment a project on this harness adds a login flow (even a simple one), these rules land active. Pre-loading them is the right default for a generic-exemplar harness.
>
> Examples reference apex-platform's Google OAuth / FERPA / hub-plugin architecture. When this harness's first authenticated project arrives, generalize the language. Doctrine (the three-gate callback, session.clear() before identity transition, never-trust-the-callback's-claimed-identity) generalizes; mechanical specifics are stack-specific.

---

# Auth rules (apex-platform doctrine, ported intact)

The Hub owns authentication. Every plugin sits behind it. This rule file documents the patterns that already ship in `hub_webapp/execution/hub_auth.py` and `hub_webapp/execution/hub_landing.py` so plugins (and future Hub changes) follow them consistently.

## The auth boundary

All identity work happens in two files:

- `hub_webapp/execution/hub_auth.py`: OAuth flow (`/auth/login`, `/auth/callback`, `/auth/logout`, `/auth/signed-out`, `/auth/denied`) and the gate predicate `is_permitted(email)`.
- `hub_webapp/execution/hub_landing.py`: the `require_login` and `require_super_admin` decorators and the public `/` dispatch.

These two modules are the only place auth logic lives. Plugins import `require_login` from `hub_landing` but do NOT touch `hub_auth`. If a plugin needs to call the gate predicate (rare), it imports `is_permitted` from `hub_auth` and does not duplicate the logic.

## The gate predicate: `is_permitted`

```python
def is_permitted(email):
    """Returns (allowed: bool, is_super_admin: bool)."""
```

Single source of truth for "is this email allowed in." Returns a tuple so the callback can both decide AND promote in one call without re-checking elsewhere.

Allow logic:

1. Empty or missing email: `(False, False)`.
2. No `@` in the email: `(False, False)`.
3. Email matches an entry in `hub_config.SUPER_ADMIN_EMAILS` (case-insensitive, exact match after lowercase + strip): `(True, True)`. This is the super-admin bootstrap; explicit super-admins can come from any domain.
4. Domain matches an entry in `hub_config.ALLOWED_DOMAINS` (case-insensitive): `(True, False)`. This is the institutional allow-list.
5. Anything else: `(False, False)`.

Plugins MUST NOT reimplement this predicate. If a plugin needs a tighter check (e.g., "only faculty," "only roster members"), it builds on TOP of `is_permitted`, not parallel to it: first call `is_permitted` to confirm the user is allowed in Apex at all, then apply the plugin-specific filter.

## The OAuth callback discipline

`hub_webapp/execution/hub_auth.py::callback` is the most security-sensitive route in the platform. The discipline:

### `session.clear()` before EVERY identity transition

Every code path in the callback that changes identity state begins with `session.clear()`. This includes:

- **Email unverified** -> clear -> set `session["denied_email"]` -> redirect to `/auth/denied`.
- **Not permitted** -> clear -> set `denied_email` -> redirect.
- **Disabled user** -> clear -> set `denied_email` -> redirect.
- **Success** -> clear -> set new identity (`user_id`, `email`, `name`, `picture`, `role`) -> redirect to `/`.

`session.clear()` BEFORE setting new identity prevents cross-identity leaks: if the user was previously signed in as someone else (e.g., a denied flow that set `denied_email` from an attacker's session), the previous session keys are wiped before any new key lands.

Logout also calls `session.clear()`. `/auth/signed-out` calls it defensively in case the page is reached without `/auth/logout` having run first.

### Three gates, in order

The callback applies three gates, each of which short-circuits to `/auth/denied`:

1. **Email verified.** `profile.get("email_verified", False)` must be True. Google sometimes returns unverified email addresses (rare, but possible); they are refused.
2. **`is_permitted`.** The allow-list check above.
3. **`user.disabled`.** Even an allowed email can be disabled in the DB by a super-admin. The check happens AFTER `upsert_user` because `disabled` is a row-level flag, not a config-level decision. This third gate cannot be skipped or reordered.

Adding a fourth gate (e.g., MFA, IP allow-list) goes here, in the same shape: short-circuit to `/auth/denied` with `denied_email` set.

### Email normalization

Email arrives from Google as `profile["email"]`. The callback normalizes it once at the top (`(profile.get("email") or "").strip().lower()`) and writes the normalized form back into `profile["email"]` before calling `hub_db.upsert_user`. The lookup index (`email_hash`) is SHA-256 of this same normalized form, so the normalization must be done in lockstep on both sides. `is_permitted` does its own internal strip+lower; the redundancy is intentional.

## The role bootstrap: `SUPER_ADMIN_EMAILS` + role-promotion-only upsert + startup re-apply

The single privileged role at the Hub level is `super_admin`. There is no admin UI for promoting users at the Hub; promotion happens via the `SUPER_ADMIN_EMAILS` env var, applied via TWO mechanisms:

1. **At OAuth callback time** (per-login, existing behavior). An email in `SUPER_ADMIN_EMAILS` that signs in for the first time is upserted with `is_super_admin=True`, and `hub_db.upsert_user` sets `role="super_admin"`.
2. **At service startup** (new v0.1.26). `hub_auth.reapply_super_admin_from_env()` runs in `create_app()` after DB init. For each email in `SUPER_ADMIN_EMAILS`, find the matching User row by `email_hash` and ensure `role='super_admin'` AND `disabled=false`. Idempotent; no-op when state is consistent or when the email has no User row yet (first login still creates it).

The startup re-apply is Layer 2 of the last-super-admin protection (see §"Last super-admin protection" below). It is the self-bricking safety net: even if a future demote/disable UI accidentally bricks the system, a service restart un-bricks it as long as the email is in `SUPER_ADMIN_EMAILS`.

**Role-promotion-only.** Neither mechanism ever demotes. `upsert_user` promotes a role (`student` -> `super_admin`) when `is_super_admin=True`, but it does NOT demote. `reapply_super_admin_from_env` promotes and un-disables ONLY users whose email is in `SUPER_ADMIN_EMAILS`; it never touches anyone else. Removing an email from `SUPER_ADMIN_EMAILS` does not strip the role from existing users; that requires an explicit DB action by a super-admin (and lands with the v0.1.27 demote/disable UI, gated by §"Last super-admin protection" and §"Env-derived super_admin accounts are non-demotable from the UI" below).

Plugins MAY define their own role vocabularies that are independent of the Hub role. The ribbon's `current_role` template variable defaults to the Hub role from `session["role"]`; a plugin with its own roles overrides this via a blueprint-level `context_processor` returning `{"current_role": ...}`. See `.claude/rules/architecture.md` §"Plugin-scoped roles".

## Last super-admin protection

Three layers of defense against bricking the system by demoting or disabling the last super_admin. The rule lands now (v0.1.26); the UI it gates lands in v0.1.27.

**Layer 1 (rule for v0.1.27 routes):**
- Any route that would set `role != 'super_admin'` on a user whose current role IS `'super_admin'` MUST first verify that ≥1 OTHER user has `role='super_admin'` AND `disabled=false`. If not, refuse the operation with a clear error.
- Any route that would set `disabled=true` on a super_admin MUST apply the same check.
- A super_admin demoting/disabling themselves is allowed only when another active super_admin exists.
- UI MUST hide the demote/disable affordance from a super_admin acting on their own row when no other active super_admin exists.
- Helper `count_active_super_admins(exclude_user_id=None)` MUST live in `hub_db.py`. Pre-specified here; implementation lands with the demote/disable route in v0.1.27.

**Layer 2 (ships in v0.1.26):**
- `hub_auth.reapply_super_admin_from_env()` runs at every service start (see §"The role bootstrap" above).
- Net effect: a single misconfigured demote is recoverable. The operator notices, edits `.env` if needed, restarts the service, and the system un-bricks.

**Layer 3 (UI prevention; lands v0.1.27):**
- The demote/disable UI suppresses the affordance entirely when self-acting AND no other active super_admin exists. Server-side enforcement (Layer 1) is the load-bearing check; UI suppression is the affordance.

## Env-derived super_admin accounts are non-demotable from the UI

A user whose `email_hash` matches the SHA-256 of any entry in `SUPER_ADMIN_EMAILS` is an **env-derived super_admin**. Their super_admin role is re-applied at every service start (Layer 2 above). Demoting one from the admin UI is wasted work - the next service restart reinstates the role.

The v0.1.27 demote/disable UI MUST:
- Detect env-derived status and disable the demote/disable affordance for those rows, with a tooltip naming `.env` (`SUPER_ADMIN_EMAILS`) as the source.
- Surface text in `hub_admin_users.html`: "Role managed by SUPER_ADMIN_EMAILS - edit `.env` and restart to change."
- Also enforce server-side: the demote/disable POST handler MUST call `is_env_super_admin` and refuse the operation if true. Defense in depth - UI disable alone is bypassable via crafted POST.

To remove an env-derived super_admin: edit `.env` on the VM to drop the email from `SUPER_ADMIN_EMAILS`, restart the service, THEN demote via the UI. The row is no longer env-derived; UI now permits the action; Layer 1 rule still applies (must not be the last active super_admin).

Helper `is_env_super_admin(email_hash) -> bool` MUST live in `hub_auth.py`. Pre-specified here; implementation lands with the demote/disable UI in v0.1.27. Implementation hashes each entry in `SUPER_ADMIN_EMAILS` once at module-import (memoized) and does set-membership against `email_hash`. No decryption needed; no DB hit.

## The `require_super_admin` decorator

`hub_webapp/execution/hub_landing.py` exports `require_super_admin` alongside `require_login`. It is the gate for hub-scoped admin routes (currently just the `hub_admin` settings page).

Semantics, in order:

1. If `session["user_id"]` is missing, redirect to `/auth/login` (same as `require_login`).
2. If `session["role"] != "super_admin"`, render `hub_admin_denied.html` with HTTP 403.
3. Otherwise call through to the view.

The 403 page follows the `apex-with-bg` + `apex-card` pattern per `.claude/rules/frontend.md` §"The pre-signin visual pattern". It does NOT show sign-in buttons (the user is already logged in, just under-privileged); it offers a "Back to Apex" link instead.

**Plugin admin routes MUST NOT use this decorator.** Each plugin defines its own admin gate named `require_<plugin>_admin` (lives in the plugin's blueprint module) that checks both (a) hub super_admin (operational godmode) AND (b) the plugin's own admin list in a `<PLUGIN>_ADMINS` env var (hashed and compared against `email_hash`). See `.claude/rules/architecture.md` §"Hub settings vs plugin settings" for the full pattern and `placeholder_plugin/` for the exemplar.

## The denied flow

A denied sign-in attempt lands the user on `/auth/denied` with the offending email passed via `session["denied_email"]`. The page renders `hub_denied.html` (UCA backdrop + apex-card; see `.claude/rules/frontend.md`) and returns HTTP 403.

The denied email is NOT persisted anywhere; it lives only in the session until the user signs in successfully (which calls `session.clear()`) or closes the browser. Logging the denied email is a FERPA risk if the user has a name-bearing email; do not add it to access logs or journal output.

## The signed-out flow and `prompt=select_account`

The default Google OAuth flow auto-grants for a user who is already signed into Google. After a user signs out of Apex, hitting `/auth/login` would immediately re-sign them in as the same Google identity. This is sometimes wanted (the user signed out by accident) and sometimes not (the user wants to sign in as a different account, e.g., switching between a personal and an institutional Google account).

The pattern: `/auth/login?switch=1` adds `prompt=select_account` to the Google authorize URL, which forces Google to show the account chooser. Without `switch=1`, Google's SSO auto-grants.

`hub_welcome.html`, `hub_signed_out.html`, and `hub_denied.html` each expose two sign-in affordances: "Sign in" (no switch param) and "Sign in with a different account" (`?switch=1`). New auth screens MUST do the same.

`/auth/login` short-circuits to `/` if `session["user_id"]` is already set. This prevents a signed-in user from triggering a redundant OAuth round-trip by clicking a stale "Sign in" link.

## The public root and `@require_login`

`hub_webapp/execution/hub_landing.py::index` handles `/`. It is intentionally public:

- When `session["user_id"]` is set: render the plugin tile grid (`hub_index.html`).
- When `session["user_id"]` is NOT set: render the welcome page (`hub_welcome.html`).

This dispatch closes the post-logout auto-OAuth loop without any cookie or flag plumbing. The incident that motivated this design is recorded at `.claude/notes/incidents/cookie-suppression-c6c69c2.md`.

**`require_login` lives in `hub_landing`, NOT `hub_auth`.** The decorator is colocated with the public-root logic because both implement the auth dispatch pattern. Plugins import it as:

```python
from hub_landing import require_login
```

The decorator redirects unauthenticated requests to `/auth/login`. It does NOT inspect roles; role checks live inside the route function (or in additional decorators like a hypothetical `@require_super_admin`).

## What plugins MUST do

- Apply `@require_login` to every plugin route that touches user-scoped data. The Hub does NOT gate plugins at the blueprint level; the decorator is opt-in per route.
- Read identity from `session["user_id"]`, `session["email"]`, `session["name"]`, `session["picture"]`, `session["role"]`. These are populated by the Hub callback and are the contract.
- Use `hub_db.email_hash_for(email)` (SHA-256 of lowercase-trimmed email) for any user lookup by email. Plain-text email columns cannot be looked up because Fernet is non-deterministic.
- Call `is_permitted` (imported from `hub_auth`) if a plugin route needs to check whether an arbitrary email would be allowed in Apex. Do not reimplement the logic.

## What plugins MUST NOT do

- Do NOT override `session.clear()` discipline. If a plugin route needs to invalidate a session, POST to `/auth/logout`, do not clear and continue. (The logout route is POST-only as of v0.1.21; a stray `<a href="/auth/logout">` returns 405. See `architecture.md` §"Inviolable structural rules" for the no-state-changing-GET contract.)
- Do NOT add an OAuth flow. The Hub is the only OAuth client. Plugins that need third-party auth (e.g., Marquee's GitHub integration) treat that as feature-scoped, not identity-scoped: the user is already an Apex identity; the third-party token is associated with that identity, not a replacement for it.
- Do NOT log raw email addresses, names, picture URLs, or session contents. PII in logs is a FERPA risk. See `.claude/rules/style.md` §"Logging".
- Do NOT decrypt `User.email_enc` / `name_enc` / `picture_url_enc` on a public route. The FERPA-isolation contract for `hub_health.py` (no `crypto`/`hub_db` imports) generalizes: any future public route must be decrypt-free by construction.
- Do NOT mint a stub `User` row to satisfy a FK when the feature only needs an email. Add a nullable `email_enc` + `email_hash` column to the plugin's own table instead. See `.claude/rules/style.md` §"No phantom stub or pending accounts".
- Do NOT gate plugin routes at the blueprint level (e.g., a `before_request` that checks `session["user_id"]`). Use `@require_login` per route. Blueprint-level gating prevents plugins from exposing legitimate public routes (e.g., a future Beacon QR check-in landing page that students hit before signing in).
- Do NOT use GET for state changes (logout, role promotion/demotion, account disable, anything that writes). State changes are POST forms with the CSRF token. See `architecture.md` §"Inviolable structural rules" and `frontend.md` §"What plugins MUST NOT do (templates)" for the CSRF contract.

## Future: placeholder-row whitelisting

Per CLAUDE.md's "Active focus": super-admins will eventually be able to pre-authorize external (non-uca.edu) emails by inserting a placeholder `User` row with `google_sub IS NULL` and a populated `email_hash`. The OAuth callback graduation logic will detect a first-time signer whose `email_hash` matches a placeholder row and bind the placeholder to the real Google identity, preserving any pre-assigned role.

When that lands:
- `is_permitted` likely gains a fourth allow path: "is there a placeholder row with this `email_hash`?" That keeps the predicate as the single source of truth.
- The callback's `session.clear()` discipline stays exactly as-is.
- The "role-promotion-only upsert" rule extends to "placeholder role survives binding": the pre-assigned role on the placeholder MUST be preserved through the upsert, not overwritten by a default.

These bullets are the contract for the work; the actual implementation lands in a separate plan.
