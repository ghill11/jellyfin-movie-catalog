---
name: migration-author
description: STUB. Not applicable to jellyfin-movie-catalog (no database). Preserved as a placeholder because the generic-harness exemplar this project's extraction informs will need a schema-author concept for projects that have schemas. Open question for that exercise: what does a stack-agnostic schema-author look like?
status: stub (not-applicable)
---

# migration-author (stub: NOT APPLICABLE to jellyfin-movie-catalog)

## Status

This agent is **NOT APPLICABLE** to jellyfin-movie-catalog and is **NOT** invoked during normal work on this project.

## Why this stub exists

The source project for this harness (apex-platform) uses Postgres with Alembic for schema migrations. Apex's `migration-author` writes Alembic migration files against SQLAlchemy model changes: runs autogenerate, hand-reviews what autogenerate misses (server defaults, check constraints, enum changes), enforces a revision-id length cap, writes real downgrades, flags large-table lock patterns, and verifies forward+reverse against a fresh test DB.

jellyfin-movie-catalog has **no database**. The plugin reads from Jellyfin's library (via the Jellyfin SDK, which is an in-process API) and writes a `movies.json` snapshot to a GitHub repo via the Contents API. There is no persistent local storage that needs schema migration: configuration lives in Jellyfin's own plugin-configuration store (a managed file the plugin contract owns), and the snapshot is a single file replaced atomically per push.

## Why it is preserved as a stub

This project's harness extraction is a co-equal deliverable: the extraction itself is the exercise that informs a future generic-harness exemplar. Some agents in the source harness will apply to most projects (`quality-inspector`, `code-reviewer`, `function-tester`, `incident-recorder`, `smoke-tester`); some are stack-specific (`migration-author` is Alembic-Python-Postgres-coupled). Recording the not-applicable case here is part of the extraction record.

## Open question for the generic-harness exercise

What does a **stack-agnostic** schema-author look like? Apex's `migration-author` is tightly coupled to:

- **Alembic** (the migration tool): revision IDs, autogenerate, `op.add_column`, `op.bulk_insert`.
- **SQLAlchemy** (the ORM): model-introspection-driven diff, expression nodes like `sa.func.now()`.
- **Postgres** (the database): server defaults, large-table lock patterns, varchar(32) revision-id limit, psycopg2 parameter adaptation.
- **Python** (the language): the autogenerate output is a Python file.

Each layer has alternatives:

- Migration tool: Flyway (Java), Liquibase (Java), Diesel (Rust), Prisma Migrate (TS), Entity Framework Migrations (C#), dbmate (Go), sqlx-cli (Rust).
- ORM: Entity Framework Core, GORM, Sequelize, Diesel, Drizzle, raw SQL.
- Database: Postgres, MySQL, SQL Server, SQLite, MongoDB (schema migrations look very different here), DynamoDB.
- Language: any.

A truly generic agent would need to know the project's stack and dispatch to the right tool. A more honest approach is probably **one agent per common stack** in the generic harness, with a router or with naming convention (`migration-author-alembic-python`, `migration-author-efcore-csharp`, etc.), and a stub like this one for projects with no schema.

Additionally, several Apex `migration-author` checks generalize across stacks:

- **Revision-id length / identifier-length caps** (Postgres `varchar(32)` for Alembic, similar in other tools).
- **Round-trip verification** (forward, downgrade, forward again, on a throwaway DB).
- **Large-table lock patterns** (relevant for any DB with concurrent writes).
- **Test DB must match production DB** (the apex SQLite-vs-Postgres incident generalizes).
- **Seed values use static primitives, not expression nodes** (the `sa.func.now()` incident generalizes to anything that compiles to SQL vs. is sent as a bound parameter).

A future generic schema-author could ship those checks as language-agnostic stubs that each stack-specific implementation specializes.

## What to do if you reach for this agent

If a future task on jellyfin-movie-catalog requires schema authorship (the project gains a local SQLite cache, a sidecar service uses Postgres, the snapshot format gains a versioning step that requires structured migration):

1. First confirm the schema is in scope. If it is just "the JSON snapshot's shape is changing," that is a `code-reviewer` review against the existing serializer, not a schema migration.
2. If it is genuinely structured persistent storage, decide the stack (SQLite, LiteDB, JSON-versioned-with-migrator, etc.) and write a real agent against that stack at that time. The apex-platform `migration-author.md` is the reference template; specialize for the actual tool.

## What you do NOT do

- You are a stub; you do not produce any artifact.
- You do not silently re-purpose yourself for a different task. If a caller invokes you for something other than schema migration, decline and direct them to the appropriate agent or to manual work.

---

Origin: stubbed because `apex-platform` `.claude/agents/migration-author.md` is tightly coupled to Alembic-SQLAlchemy-Postgres-Python, none of which apply here. The agent file is preserved (rather than dropped entirely) to document the not-applicable case for the future generic-harness exemplar.
