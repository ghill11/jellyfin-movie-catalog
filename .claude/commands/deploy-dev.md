---
description: Emit the canonical sideload-to-local-Jellyfin command sequence after a release tag (read-only; does not execute)
---

Emit the canonical command form for sideloading a freshly-published release zip into the local portable Jellyfin at `D:\jf-dev\`. Do NOT execute anything. Do NOT touch the user's Unraid Jellyfin. Just produce the command block the user runs locally, in the format below.

## Step 1: surface outstanding quality debt

Per `.claude/rules/workflow.md` §"Surface outstanding quality debt before giving deploy commands", before emitting any commands you MUST summarize outstanding entries from `.claude/notes/quality-debt.md` that touch files in the current diff. If `quality-debt.md` is empty, or no entries reference files touched by the current task's diff, state that explicitly.

Format:

```
Quality debt outstanding for this sideload:
  - <file>:<line> - <category> `<token>` (<timestamp>)
  - ...

These will hard-block the next tag push.
```

Or, if no debt outstanding:

```
Quality debt: none outstanding for this task.
```

## Step 2: confirm a release tag is the target

The sideload pulls a published GitHub release artifact, not local source. Confirm:

- A release tag (`v0.X.Y`) exists on GitHub and has a built artifact attached (`Jellyfin.Plugin.MovieCatalog-vX.Y.Z.zip`).
- If the user is mid-build and the release does not exist yet, this command is premature. Tell them the build/release pipeline must finish first.

You can use `gh release list --limit 5` to inspect the most recent releases.

## Step 3: emit the command block

Format the output as a single fenced PowerShell block the user can copy-paste:

```powershell
# Sideload Jellyfin.Plugin.MovieCatalog into the portable Jellyfin at D:\jf-dev\
$tag = "vX.Y.Z"  # set this to the release tag being smoked

# 1. Download the release zip.
gh release download $tag --pattern "Jellyfin.Plugin.MovieCatalog-*.zip" --dir "$env:TEMP\jf-mc-smoke" --clobber

# 2. Stop the portable Jellyfin so DLLs are not locked.
Get-Process -Name jellyfin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 3. Wipe and recreate the plugin folder so stale DLLs never win the load.
$pluginDir = "D:\jf-dev\config\plugins\MovieCatalog"
if (Test-Path $pluginDir) { Remove-Item -Recurse -Force $pluginDir }
New-Item -ItemType Directory -Path $pluginDir | Out-Null

# 4. Expand the release zip into the plugin folder.
$zip = Get-ChildItem "$env:TEMP\jf-mc-smoke\Jellyfin.Plugin.MovieCatalog-*.zip" | Select-Object -First 1
Expand-Archive -Path $zip.FullName -DestinationPath $pluginDir -Force

# 5. Start the portable Jellyfin.
Start-Process -FilePath "D:\jf-dev\jellyfin.exe" `
    -WorkingDirectory "D:\jf-dev" `
    -ArgumentList "--datadir","D:\jf-dev\config" `
    -WindowStyle Hidden

# 6. Wait for Jellyfin to come back up (poll /System/Info/Public).
$deadline = (Get-Date).AddSeconds(90)
while ((Get-Date) -lt $deadline) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:8096/System/Info/Public" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($r.StatusCode -eq 200) { Write-Host "Jellyfin is up."; break }
    } catch { Start-Sleep -Seconds 2 }
}
```

Annotate the tag line with the specific tag the user just published, if known from the current task context.

## Step 4: tell the user to invoke smoke-tester after

After they run the block, the next step is to invoke the `smoke-tester` agent (or say "smoke that" / "verify the release") so it confirms the plugin loaded via `/Plugins`, records the MD5 digest, and appends a verdict line to `.claude/notes/deploys/dev-log.md`.

That deploy log line is the phase-6 OQE artifact the `quality-inspector` looks for at the next tag push.

## What you do NOT do

- You do not execute anything. No `Bash` or `PowerShell` calls. Read-only.
- You do not touch the user's Unraid Jellyfin. The portable instance at `D:\jf-dev\` is the only target.
- You do not invoke `smoke-tester` yourself. Tell the user to.
- You do not skip the quality-debt preamble. It is a workflow.md requirement.

---

Origin: ported from `apex-platform` `.claude/commands/deploy-dev.md`. The shape (surface quality debt, decide on conditional step, emit a single fenced command block, then instruct on the smoke-tester follow-up) ports clean; the apex VM-deploy form (`sudo -u apex git pull`, alembic migration, `systemctl restart`) was rewritten for this project's sideload-to-portable-Jellyfin form.
