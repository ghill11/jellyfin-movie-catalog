# Database rules

> **Dormancy notice**: jellyfin-movie-catalog has no persistent database. This file is dormant - the rules below do not exercise on any code path in the current project, and the pre-commit / pre-push hooks do not enforce them yet.
>
> The rules are kept INTACT (not gutted) because they capture hard-won doctrine that any future project on this harness benefits from inheriting pre-loaded. The moment a project on this harness introduces a database (local cache, server-side variant, plugin-local SQLite, anything that persists across restarts), these rules land active. Pre-loading them is the right default for a generic-exemplar harness.
>
> Examples reference apex-platform's Flask/SQLAlchemy/Alembic stack. When this project's first database arrives, generalize the language. Doctrine generalizes across stacks; mechanical details (Alembic-specific 32-char revision id cap, etc.) are stack-specific.

---

# Database rules (apex-platform doctrine, ported intact)

The data-modeling rules that govern schema design, migrations, and PII handling on apex-platform. Naming conventions for PII columns (`_enc` suffix, `LargeBinary` type, `email_hash` String(64)) live in `style.md` §"PII columns"; this file covers the structural, threat-model, and lifecycle rules that go beyond naming.

## PII column pairing

### The rule

Any model column that stores identifying student data is encrypted at rest in a `*_enc` column (Fernet, `LargeBinary`). Plaintext storage of identifying data is BLOCK by `code-reviewer`.

If the identifying data is also a search axis (the column appears in `WHERE` clauses for indexed lookup, or is the right-hand side of an `email_hash_for(...)` call), the row additionally carries a paired `*_hash` column (SHA-256, `String(64)`, lowercase-trimmed). The hash enables deterministic indexed lookup; the encrypted column is the source of truth.

The pair MUST be added together in the same migration. Adding a `*_email_enc` column in one migration and a `*_email_hash` column in a later migration is BLOCK by `migration-author` because the intermediate state (encrypted column with no lookup key) is an outage shape: any code path that needs to look the row up by email cannot.

### Registered search axes

At v1, the only search axis on apex-platform is `email`. The User model has `email_enc` + `email_hash`; the AuditEvent model has `actor_email_enc` + `actor_email_hash` and `target_email_enc` + `target_email_hash`.

Adding a new search axis (e.g., `student_id`, `orcid`, `external_username`) is a design decision documented here before the migration lands. The procedure: open a plan that names the new axis, justifies why it is a search axis (i.e., names the queries that need to filter on it), and updates this section. Then the migration follows.

### Threat model

Why the pair, not one or the other:

- **Fernet alone fails the search requirement.** Fernet is non-deterministic by design: encrypting `"alice@uca.edu"` twice produces two different ciphertexts. A column of Fernet ciphertexts cannot be used as an index for "find the row matching this email." So `*_enc` alone forces every email lookup to be a full-table scan with per-row decrypt, which is O(n) and operationally untenable.

- **SHA-256 alone fails the at-rest-encryption requirement under FERPA.** SHA-256 is deterministic: `hash("alice@uca.edu")` always produces the same output. An attacker with a database dump and the institutional directory (which is effectively public for university populations) can hash every directory entry and reverse the table in seconds. The hash by itself is not encryption; it is a fingerprint that identifies the row when the population is known.

- **The pair gives both properties.** The `*_hash` column is the indexed lookup key (deterministic, fast). The `*_enc` column is the FERPA-compliant at-rest storage (non-deterministic, key-protected). A database dump without the Fernet key reveals only ciphertext for the email itself; the hash still identifies which rows are about which directory entries, but the actual email content stays encrypted.

This combined property is why the pair is mandatory, not a nice-to-have.

### Where the pattern applies

Every table storing user PII, not only the User model. As of v0.1.23 the second consumer of the pattern is the AuditEvent model (which denormalizes actor and target emails so the audit row survives user deletion). Future tables with PII (placeholder-row whitelisting, external roster subscribers, any plugin's PII-bearing model) follow the same pattern.

When a feature tracks an identifying email without minting a User row (the "no phantom stub or pending accounts" rule, below), the table gets a nullable `email_enc` + `email_hash` pair. The nullable pair stays in scope.

### Detection

- **`migration-author`** BLOCKs any new `*_email_enc` column without a matching `*_email_hash` column on the same table in the same migration. Regex target: `*_email_enc\b` on a column definition.
- **`code-reviewer`** BLOCKs a SQLAlchemy model definition with the same shape: an `_enc` column on a search axis without a paired `_hash` column.
- Both agents also BLOCK any plaintext column whose name matches a known identifier (`email`, `email_address`, anything an identifier) without `_enc` / `_hash` suffix.

## Encrypted field decryption

### The rule

Decrypt encrypted columns for display ONLY via `hub_webapp/execution/hub_decrypt.py::decrypt_record(instance)`. Routes and templates MUST NOT call `crypto.decrypt(...)` against a `*_enc` column inline.

The helper introspects the model instance for any column whose name ends in `*_enc`, decrypts each via `crypto.decrypt`, and returns a dict keyed by the column name with the `_enc` suffix stripped. Caller assembles the display dict with non-encrypted fields explicitly:

```python
from hub_decrypt import decrypt_record

display = {
    "id": user.id,
    "role": user.role,
    "disabled": user.disabled,
    **decrypt_record(user),  # adds email, name, picture_url
}
```

For columns whose decrypted value is JSON (today: `details_enc` on AuditEvent), the helper returns the JSON string; the caller parses `json.loads(...)`. No magic suffix in v1 - one JSON column does not justify a convention.

### Why generic, not per-model

Prototype evidence: the platform is gaining encrypted fields beyond User PII (NLM cookies, Prism source metadata, Marquee GitHub tokens - all in the prototype, all coming back as plugins land). A single generic helper scales to 5+ plugins with 3-5 encrypted columns each; per-model display helpers would balloon to ~25 call sites by year-end and fragment the decryption boundary.

### Detection

- `code-reviewer` BLOCKs any `crypto.decrypt(` call against a `*_enc` column outside `hub_decrypt.py` (and outside `crypto.py` itself, where the primitive lives). Mechanical regex match on `crypto\.decrypt\(.*_enc\b`.
- Tests live at `hub_webapp/tests/test_decrypt.py`. Add round-trip tests for any new `*_enc` column at the same time as the column is added.

### FERPA isolation interaction

`hub_decrypt.py` imports `crypto` and SQLAlchemy. It MUST NOT be imported from `hub_webapp/execution/hub_health.py` (the test_health_isolation.py AST contract enforces that hub_health does not import `crypto` or `hub_db`; importing hub_decrypt from hub_health would route around that contract). The FERPA-isolation rule from CLAUDE.md generalizes: any future public unauthenticated endpoint must be decrypt-free, including via transitive imports.

## Alembic revision id length

### The rule

Alembic revision ids on apex-platform are capped at **32 characters**, including the leading `NNNN_` sequence number. This matches the `alembic_version.version_num` column's `varchar(32)` (the alembic default; we never customized it).

### Conventions for staying under the cap

The `NNNN_` prefix consumes 5 characters, leaving 27 for the descriptive part. Plugin-scoped revisions get a plugin slug prefix consuming another 5-8 characters, leaving 19-22 for description.

Drop redundant words:

- `0015_marquee_gh_published_at` (28 chars) NOT `0015_marquee_github_last_published_at` (38 chars).
- `0002_audit_events` (18 chars) NOT `0002_audit_events_with_actor_target_columns` (45 chars).

### Failure mode if exceeded

Alembic runs `upgrade head` in a single transaction by default. Inside that transaction:

1. The migration body executes (e.g., `op.create_table(...)`).
2. Alembic issues `UPDATE alembic_version SET version_num='<id>'`.
3. If `<id>` exceeds 32 characters, Postgres raises `psycopg2.errors.StringDataRightTruncation`.
4. The transaction rolls back. **Every migration in the chain is undone**, including any that succeeded before the failing one.
5. `alembic_version` stays at whatever it was before `upgrade head` started.

Recovery procedure: rename the revision (file name AND the `revision: str = "..."` value), then `alembic stamp <last-known-good-id>` followed by `alembic upgrade head`. Do NOT `alembic stamp <new-id>` and assume the schema is at that state; the transaction rolled back, so the schema is at the pre-failure state.

### Cross-reference

The full recovery procedure with the 2026-04-27 worked example lives at `~/.claude/rules/reference_alembic_revision_length.md` (user-level rule, cross-project). The apex-side `migration-author` agent enforces the cap at migration-authoring time so the failure mode never reaches production.

## No phantom stub or pending accounts

### The rule

When a feature needs to track an email that may not belong to an Apex account holder (external roster subscribers, future Marquee external collaborators, any "email-only" use case), add a nullable `email_enc` + `email_hash` pair on the existing table. Do NOT mint a stub User row, and do NOT borrow PendingUser, to satisfy a foreign-key constraint.

A row in such a table represents either an Apex user (the FK to `users.id` is populated) OR an external email (the FK is NULL and the `email_enc`+`email_hash` pair carries identity). Never both.

### Why

Stub or PendingUser rows that exist only to satisfy a foreign key create three problems:

1. **Phantom rows in `users`** that have no login-time purpose. PendingUser was designed for super-admin pre-grant of faculty roles, with a real lifecycle (the row is "popped" at first login and replaces the placeholder semantics with real identity). Borrowing it for "we need an email to mail" breaks that lifecycle: the row never gets popped because there is no first login.

2. **Schema complexity.** Every consumer of the table now has to handle the dual state of "real user vs stub" with branching logic.

3. **User-table bloat.** The user table becomes a directory of "people who might log in someday" mixed with "people who actually use the platform." Operational queries (count of active users, role distribution, etc.) require filtering out the phantoms.

A nullable email pair on the consuming table avoids all three. The audit_events table follows this pattern by denormalizing `actor_email_*` and `target_email_*` so the row survives user deletion.

### Reconciliation

If a row was created with a nullable email pair and later that email's user signs up, reconciliation is a separate optional concern. Two valid approaches:

- **Manual**: a super-admin route that scans for rows with NULL `user_id` AND populated `email_hash`, and offers to bind them to a matching `users.id` if one exists.
- **Automatic at user creation**: on `upsert_user`, after the new user row lands, scan the relevant external tables for rows matching the new user's `email_hash` and update their FK.

Neither is load-bearing for the feature to ship. Pick one when it becomes operationally useful; until then, the nullable column is the contract.

### Cross-reference

Origin: 2026-04-30 Beacon manual-add-subscriber design discussion. The user-level rule `~/.claude/rules/feedback_no_phantom_accounts.md` encodes the rationale at the cross-project level. The pattern's first apex-platform consumer is the placeholder-row whitelisting feature (super-admin pre-authorizing an external email), described in CLAUDE.md "Active focus."

## Audit log retention

### The rule

Rows in `audit_events` are not kept in the hot table forever. A monthly cron runs a TWO-STEP rotation: (1) rows older than `audit_archival_age_days` (default 2555 = 7 years, matching institutional FERPA-aligned educational records retention) move from `audit_events` to `audit_events_archive`; (2) if `audit_archive_retention_days > 0`, rows older than that horizon are DELETED from `audit_events_archive`. The default for archive retention is 0, which means never purge - preserving the archive indefinitely until the operator tightens that knob.

Both knobs live in the `hub_settings` table (seeded by migration `0004_hub_settings`), editable by a hub super_admin via the Hub Admin tile at `/admin/settings`. The cron runs as the `apex` OS user, invokes `scripts/rotate_audit.py`, and logs to `/var/log/apex/<env>-rotate-audit.log`.

### Mechanism

- Cron: monthly at 02:00 UTC on day 1 (`0 2 1 * * ...`). Crontab entry documented in `.claude/rules/deployment.md` §"Cron".
- Script: `scripts/rotate_audit.py`. Calls `hub_audit.rotate()` with no args; the function reads both knobs from `hub_settings`. Exits non-zero on failure.
- Two-step rotation runs in a single transaction:
  1. SELECT rows from `audit_events` with `created_at < now() - archival_age_days`, INSERT them into `audit_events_archive`, DELETE them from `audit_events`.
  2. If `archive_retention_days > 0`: DELETE rows from `audit_events_archive` with `created_at < now() - archive_retention_days`. If 0: skip step 2 entirely.
  If either step fails, neither table is mutated.
- Schema: `audit_events_archive` matches `audit_events` column-for-column. PII pairing (actor_email_hash + actor_email_enc, target_email_hash + target_email_enc) survives into the archive intact.
- Importable: the rotation logic lives in `hub_webapp/execution/hub_audit.py` as `rotate(archival_age_days=None, archive_retention_days=None)`. None args trigger the `hub_settings` read; tests pass explicit values to bypass the DB lookup. Returns `{"moved": N, "purged": M, "archive_purge_skipped": bool}`.

### The two knobs (relationship matters)

`audit_archival_age_days` is the age at which a row leaves the hot table. `audit_archive_retention_days` is the absolute age at which a row leaves the archive (NOT the time since archival). Both are measured against `created_at`, the original event timestamp.

If `archive_retention_days < archival_age_days`, every newly-archived row is immediately eligible for purge on the next rotation pass - the archive is effectively empty. The form does not prevent this misconfiguration; the operator chooses.

Sensible configurations:
- `archival_age=2555, retention=0` (default): archive grows indefinitely. Safe for low-volume systems.
- `archival_age=2555, retention=3650`: hot 0-7y, archive 7-10y, deleted >10y.
- `archival_age=365, retention=2555`: hot last year, archive 1-7y, deleted >7y. Tighter hot table for query speed.

### Threshold-triggered review

`rotate()` reads row counts on BOTH tables after the rotation. Each emits a `WARNING` log line when its count exceeds 1,000,000:
- `audit_events > 1M` suggests tightening `audit_archival_age_days`.
- `audit_events_archive > 1M` suggests tightening `audit_archive_retention_days` (currently 0 = never purge).

Operator picks these up in the journal (or in `/var/log/apex/<env>-rotate-audit.log`) and decides. The thresholds are soft signals, not actions; the script does not auto-purge or auto-tighten.

### What this is NOT

- Not cold-storage. Both tables live in the same cluster on `/data/postgres/16/main`. Cold-storage tier (S3, glacier-equivalent, or pg_dump-to-disk on `/data/apex/<env>/archive/audit/`) is deferred until a separate tag.
- Not GDPR-style right-to-erasure. The archive preserves the encrypted PII pair, not purges it (the archive-purge step DELETES the whole row, not just the PII columns). A future feature (super-admin "forget this email") would null the PII columns selectively in both tables; that is a separate decision.
- Not auto-tuning. The threshold metrics are for the operator to read, not for the script to act on.

### Cross-reference

- Migration (table): `alembic/versions/0003_audit_events_archive.py`.
- Migration (settings backing): `alembic/versions/0004_hub_settings.py` (seeds both knobs).
- Script: `scripts/rotate_audit.py`.
- Test: `hub_webapp/tests/test_audit_rotation.py`.
- Crontab: `.claude/rules/deployment.md` §"Cron".
- Admin tile: `/admin/settings` (hub_admin blueprint; gated by `@require_super_admin`).
- Vestigial env knob: `AUDIT_RETENTION_DAYS` in `.env.example`. No longer read by the script. Remove from `.env` if desired.
