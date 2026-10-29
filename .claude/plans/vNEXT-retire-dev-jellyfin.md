# Retire the dev Jellyfin instance at D:\jf-dev

## Context

After the 2026-10-28 consolidation of the D:\ repos into D:\GitHub, the user asked to move `D:\jf-dev` into the repo. On learning that `D:\jf-dev` is the throwaway portable Jellyfin set up during v0.1.0 development, the user chose to retire it instead of moving it (decided by user 2026-10-28: "we can kill that now").

Observed state (read-only survey, 2026-10-28):

- 544 MB total: portable Jellyfin binaries (374 MB), a leftover `jellyfin.zip` (160 MB), data (10 MB), 5 zero-byte stub movies.
- `jellyfin.exe` is not running. The dev instance last ran on 2026-05-22.
- The dev plugin config targets `ghill11/jellyfin-movie-catalog-test`, the sacrificial repo. The `movie catalog sync` commits landing on this repo today come from the Unraid production server, so retiring the dev instance does not affect the live catalog.
- The folder holds two secrets: the dev PAT (`.env.test.local` and `data\plugins\configurations\Jellyfin.Plugin.MovieCatalog.xml`) and the local admin API key (`.smoke-api-key`).
- Residue outside the folder: two inbound Allow firewall rules named `Jellyfin.Server` for `D:\jf-dev\jellyfin\jellyfin\jellyfin.exe`. There are no scheduled tasks, startup entries or AppData folders.

## Steps (Claude)

1. Copy this plan into the repo as `.claude/plans/vNEXT-retire-dev-jellyfin.md` (per `workflow.md` "Plan files live in-repo"). Leave it uncommitted until the user says to commit.
2. Check again that no `jellyfin.exe` is running. If one is, stop and report instead of deleting anything.
3. Send `D:\jf-dev` to the Recycle Bin with `Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, OnlyErrorDialogs, SendToRecycleBin)`. This is a recoverable delete, not a hard delete: the D: Recycle Bin has a 97 GB cap and `NukeOnDelete=0`, so 544 MB fits. The user empties the bin when ready.
4. No other repo files change. The docs keep describing `D:\jf-dev` as the place to provision a dev instance, and DEV_SETUP.md stays the recipe for rebuilding it.

## Steps (user, outside what Claude can do)

- **Revoke the dev PAT** on GitHub (Settings > Developer settings > Fine-grained tokens, the token scoped to `jellyfin-movie-catalog-test`). Deleting the files does not revoke it, and until the bin is emptied the files still hold it in plain text.
- **Remove the two firewall rules.** Changing security settings is the user's job. In an elevated PowerShell:
  `Get-NetFirewallApplicationFilter -Program 'D:\jf-dev\jellyfin\jellyfin\jellyfin.exe' | Get-NetFirewallRule | Remove-NetFirewallRule`
- **Keep or delete** the `ghill11/jellyfin-movie-catalog-test` repo. Not touched by this plan.

## Consequences to know before approving

- **Phase-6 gate.** `quality.md` requires a `smoke-tester` PASS line against `D:\jf-dev` for every tag. Once the folder is gone, `smoke-tester` returns `NOT_PROVISIONED`, so the next tag needs the instance rebuilt from DEV_SETUP.md first (about 10 minutes), unless the doctrine changes.
- **Known doc drift, not fixed here.** `smoke-tester.md` and `/deploy-dev` describe a layout that differs from the real one: `config\plugins` instead of `data\plugins`, `jellyfin.exe` at the root, `--datadir config`. CLAUDE.md passes `--port 8097`, which the CLI rejects. `deployment.md` names the wrong log folder and PAT file. CLAUDE.md's "zero residue" claim misses the firewall rules. All of this bites on the next rebuild. It is a separate follow-up.

## Verification

- `Test-Path D:\jf-dev` returns False.
- The Recycle Bin (Shell namespace 10) lists `jf-dev` with its original location `D:\`.
- `git status` in the repo shows only the new plan file.
