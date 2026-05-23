# Release notes

Hand-written body for each tagged release of `ghill11/jellyfin-movie-catalog`. One file per tag, named `<tag>.md` (e.g., `v0.1.0.md`).

## Format

- Plain markdown.
- No front-matter, no required headers.
- First content line is whatever fits. The release title comes from the annotated tag's message (`git tag -a vX.Y.Z -m "vX.Y.Z - <subject>"`), NOT from this file.
- The `release.yml` workflow reads the entire file as the release body and appends a `**Full Changelog**` link at the bottom; do NOT include a Full Changelog link in the file itself.

## Enforcement

`scripts/hooks/pre-push` refuses to push a tag `vX.Y.Z` unless `release-notes/vX.Y.Z.md` exists at HEAD and is non-empty. No bypass. See `.claude/rules/quality.md` §"Pressure resistance" for why.

This is the second tier of release-notes enforcement after the doctrine in `CLAUDE.md` §"Promoting a build (tag-based)". The doctrine says "every release gets hand-written notes"; the hook is what makes that mechanical.

## Conventions for the body

- Open with one or two sentences explaining what this release does and why.
- Use H2 (`##`) section headers for sub-topics ("Plugin additions", "Viewer changes", "Bug fixes", "Doctrine updates", etc.).
- Reference plan files at `.claude/plans/` when applicable: "See `.claude/plans/<name>.md` for the approved plan."
- Reference relevant commits, files, or rule sections inline.
- Conclude with a "Verification" section naming the tests + smoke that confirm the tag is healthy.
- Do NOT include marketing language. The audience is future-you and a teammate reading the release page months later.
- Em-dashes are forbidden per `.claude/rules/style.md`. Use a hyphen, colon, parentheses, or rewrite.

## How to write notes for a tag

1. Decide the tag name (e.g., `v0.1.0`).
2. Create `release-notes/v0.1.0.md` with the body content.
3. Commit it AS PART OF the release commit (or as a separate commit that immediately precedes the tag), so HEAD at the tag contains the file.
4. Tag and push as usual; the workflow handles the rest.

If the file is missing at push time, the pre-push hook refuses the push. Recovery: write the file, commit it, push the commit, then re-push the tag.
