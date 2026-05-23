#!/usr/bin/env bash
# Install jellyfin-movie-catalog quality hooks into this repo's hooks directory.
#
# Per-clone install: hooks live in .git/hooks (or git-common-dir for worktrees)
# which is not version-controlled, so each clone runs this script once. The
# canonical source for the hooks lives at scripts/hooks/ in the repo.
#
# Usage:
#   scripts/install-quality-hooks.sh           # install (refuses to overwrite)
#   scripts/install-quality-hooks.sh --force   # overwrite existing hooks
#   scripts/install-quality-hooks.sh --help

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOK_DIR="$(git rev-parse --git-common-dir)/hooks"
SRC_DIR="$REPO_ROOT/scripts/hooks"
FORCE=0

for arg in "$@"; do
  case "$arg" in
    --force|-f)
      FORCE=1
      ;;
    -h|--help)
      cat <<'EOF'
Usage: scripts/install-quality-hooks.sh [--force]

Installs jellyfin-movie-catalog quality hooks (pre-commit, pre-push) into this
repo's hooks directory by copying from scripts/hooks/. Refuses to overwrite an
existing hook with different content unless --force is given.

Hooks installed:
  pre-commit  Five-pass mechanical scan over staged additions:
                Pass 1: forbidden-name tokens in code files.
                Pass 2: harness-doctrine lexicon on rule files, agent
                        prompts, hooks, and workflow YAML. Catches
                        judgment-call language. Inline-code spans
                        inside single backticks are stripped before
                        matching so quoted examples are not flagged.
                Pass 3: plan-file naming convention scan against
                        .claude/plans/*.md additions.
                Pass 4: design-question phrases in plan-file additions.
                Pass 5a: failing tests via `dotnet test`.
                Pass 5b: undocumented [Ignore]/[Explicit]/[Skip]
                         attributes (substring scan placeholder; full
                         Roslyn AST parsing can land later).
              All passes append to .claude/notes/quality-debt.md under
              separate H2 sections, print to stderr, and write a
              sentinel at .git/.harness-debt-this-commit with the count
              of new findings. Never blocks the commit (coaching tier).
              The assistant MUST surface new entries in the response
              following the commit per workflow.md "Surface harness
              debt explicitly".
  pre-push    On tag refspecs only:
                1. Verifies each tag refspec resolves to an annotated tag
                   object (`git for-each-ref ... %(objecttype)` == `tag`).
                   Refuses the push if any tag is lightweight. Mechanical.
                2. Verifies release-notes/<tag>.md exists at HEAD and is
                   non-empty (mechanical). Refuses the push if missing.
                3. Invokes the quality-inspector subagent via the Claude
                   Code CLI; refuses the push on any hard finding.
              All three gates fail closed. No bypass.

Doctrine: .claude/rules/quality.md and CLAUDE.md "Promoting a build
(tag-based)".
EOF
      exit 0
      ;;
    *)
      echo "unknown arg: $arg" >&2
      exit 2
      ;;
  esac
done

mkdir -p "$HOOK_DIR"

install_one() {
  local name="$1"
  local src="$SRC_DIR/$name"
  local dst="$HOOK_DIR/$name"

  if [ ! -f "$src" ]; then
    echo "skip: source missing: $src" >&2
    return 0
  fi

  if [ -e "$dst" ] && [ "$FORCE" -eq 0 ]; then
    if cmp -s "$src" "$dst"; then
      echo "ok: $name already installed (identical content)"
      return 0
    fi
    echo "refuse: $dst exists and differs from source." >&2
    echo "        Re-run with --force to overwrite, or inspect the difference:" >&2
    echo "          diff $src $dst" >&2
    return 1
  fi

  cp "$src" "$dst"
  chmod +x "$dst"
  echo "installed: $dst"
}

install_one pre-commit
install_one pre-push

cat <<EOF

Quality hooks installed at: $HOOK_DIR

Notes:
  - pre-commit runs five passes (forbidden-name + harness-doctrine lexicon +
    plan-naming + plan-design-question + failing-tests/undocumented-skips),
    appends to .claude/notes/quality-debt.md, prints to stderr, and writes a
    sentinel at .git/.harness-debt-this-commit with the new-finding count.
    Never blocks a commit.
  - pre-push enforces the hard gate ONLY when the push includes a tag refspec
    (refs/tags/*). Non-tag pushes pass through.
  - pre-push runs three gates on tag pushes: (1) annotated-tag check (each
    tag refspec must resolve to a tag object, not a commit); (2) release-
    notes file existence check; (3) quality-inspector via the 'claude' CLI.
    All three fail closed.
  - The 'claude' CLI is required on PATH at tag-push time. If absent, the
    push is refused (fails closed).
  - No bypass. See .claude/rules/quality.md and CLAUDE.md "Promoting a build".
EOF
