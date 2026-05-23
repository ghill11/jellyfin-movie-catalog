#!/usr/bin/env bash
# jellyfin-movie-catalog statusline.
#
# Reads the Claude Code statusline JSON from stdin and emits a single line:
#
#   <branch>[ @ <worktree>] | <model> | ctx <N>%[ | <cost>]
#
# Fields shown:
#   - git branch (or "(no-git)" outside a repo)
#   - worktree name when the session is inside a Claude Code worktree
#   - model display name (e.g. "Opus")
#   - context-window used percentage (pre-calculated by Claude Code)
#   - session cost in USD when > $0.01 (suppressed below to reduce noise)
#
# JSON contract: https://code.claude.com/docs/en/statusline (verified 2026-05-19).
#
# Discipline: this script must NEVER exit without printing at least the
# branch. Claude Code renders whatever the script prints; printing nothing
# is invisible to the user, so a silent failure looks like "the statusline
# is gone." `set -eu` was tried initially and silently killed the script
# on empty stdin (Claude Code occasionally re-runs the statusline before
# the next JSON tick); removed in favor of explicit defensive handling.

# Branch first, since it's the one field we want even when everything
# else fails. Always succeeds (falls back to "(no-git)").
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "(no-git)")

# Read stdin; if it's empty, skip JSON parsing entirely and just print
# the branch. Claude Code does sometimes call the script outside its
# normal flow with no input.
input=$(cat 2>/dev/null || true)

if [ -z "$input" ] || ! command -v jq >/dev/null 2>&1; then
  if [ -z "$input" ]; then
    printf '%s' "$branch"
  else
    printf '%s | (install jq for full statusline)' "$branch"
  fi
  exit 0
fi

# Each jq call is wrapped so an individual field failure does not kill the
# whole script. The `// empty` and `// default` jq expressions handle
# missing fields. The 2>/dev/null on each jq + `|| true` on the pipeline
# handle a JSON parse error (returns empty string for the variable).
model=$(printf '%s' "$input" | jq -r '.model.display_name // "claude"' 2>/dev/null || echo "claude")
pct=$(printf '%s' "$input" | jq -r '.context_window.used_percentage // 0' 2>/dev/null | cut -d. -f1)
[ -z "$pct" ] && pct="?"
worktree=$(printf '%s' "$input" | jq -r '.workspace.git_worktree // .worktree.name // empty' 2>/dev/null || echo "")
cost=$(printf '%s' "$input" | jq -r '.cost.total_cost_usd // 0' 2>/dev/null || echo "0")

# Compose the line.
out="$branch"
if [ -n "$worktree" ]; then
  out="$out @ $worktree"
fi
out="$out | $model | ctx ${pct}%"

# Only show cost when meaningful (> 1 cent), to reduce visual noise.
show_cost=$(printf '%s' "$cost" | awk '{ if ($1 + 0 > 0.01) print "yes" }' 2>/dev/null || echo "")
if [ "$show_cost" = "yes" ]; then
  cost_fmt=$(printf '%s' "$cost" | awk '{ printf "$%.2f", $1 }' 2>/dev/null || echo "")
  if [ -n "$cost_fmt" ]; then
    out="$out | $cost_fmt"
  fi
fi

printf '%s' "$out"
