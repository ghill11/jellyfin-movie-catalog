# Jellyfin Movie Catalog - Unraid installation and configuration

> Step-by-step guide for installing and configuring `jellyfin-movie-catalog` on a Jellyfin server running in a Docker container on Unraid.
>
> This file is meant to be downloaded, kept in a secure location (e.g., a password manager attachment or a private encrypted backup), and used as a personal reference when reinstalling the plugin in the future. The bottom of the file has a placeholder section where you can append your own GitHub Personal Access Token (PAT) for your records. **Do not commit a copy of this file with a real PAT in it. Do not share that copy publicly.**

## What the plugin does

- Subscribes to your Jellyfin server's library events (movie added / updated / removed).
- After a 30-second quiet window with no further changes, builds a JSON snapshot of your movie library (title, year, runtime, genres, date added, TMDB id).
- PUTs that JSON to a configured GitHub repo via the Contents API.
- A static viewer page hosted on GitHub Pages fetches the JSON and renders a sortable, title-searchable table. Open it on any device with internet access to check what movies you already own.

Plugin GUID: `7476643a-a7aa-47eb-8116-5856ce955bb2` (frozen for the life of the plugin).

## Prerequisites

- Unraid host with the Docker plugin and a working Jellyfin container.
- A public GitHub repository to hold the catalog JSON and serve the viewer. Default repo name: `jellyfin-movie-catalog`. The viewer URL will be `https://<github-username>.github.io/<repo-name>/`.
- A fine-grained GitHub Personal Access Token (PAT) scoped to that one repo with **Contents: Read and write**.

## Step 1: Generate a fine-grained Personal Access Token

1. Open https://github.com/settings/personal-access-tokens/new (logged in to the GitHub account that owns the target repo).
2. Token name: `Jellyfin-Movie-Catalog` (or any name you'll recognize later).
3. Expiration: pick a rotation cadence you're comfortable with. 90 days is reasonable. Calendar-remind yourself to rotate before expiry.
4. Resource owner: your GitHub username.
5. Repository access: **Only select repositories** -> choose your `jellyfin-movie-catalog` repo (NOT "All repositories").
6. Repository permissions:
   - **Contents: Read and write** (this is the one the plugin needs)
   - Metadata: Read-only (selected automatically; required)
   - Leave everything else at "No access".
7. Generate token. Copy it immediately. You will not see it again on the GitHub UI.
8. Paste the token into the "Appendix: PAT storage" section at the bottom of this file (your local copy only) for safekeeping.

Why this scoping matters: if your Jellyfin server is ever compromised, the blast radius is bounded to this one repo. The attacker can vandalize your movie catalog. They cannot touch any other repo on your account.

## Step 2: Sideload the plugin

### 2a. Confirm the host path that maps to the container's `/config`

Different Jellyfin Docker images mount `/config` from different host paths. To avoid placing the plugin in a directory that isn't visible to the container, confirm the mapping first.

Open an Unraid host shell (the `>_` icon in the top-right of the Unraid web UI) and run:

```bash
# Replace 'jellyfin' with your container name if it differs.
# To list container names: docker ps --format "{{.Names}}"
docker inspect jellyfin --format '{{ range .Mounts }}{{ .Destination }} <-- {{ .Source }}{{ "\n" }}{{ end }}'
```

Look for the line that starts with `/config <-- ...`. The path on the right is your host-side `/config`. For example, on a stock Unraid install it's usually `/mnt/user/appdata/jellyfin`.

For the rest of this guide, replace `<CONFIG_HOST>` with whatever that path is.

### 2b. Determine the plugins directory

Jellyfin stores plugins under `<config>/data/plugins/` inside the container. So the host path is:

```
<CONFIG_HOST>/data/plugins/MovieCatalog/
```

NOT `<CONFIG_HOST>/plugins/MovieCatalog/`. The intermediate `data/` directory is important. If you skip it the plugin file is in a directory the container can't see, and Jellyfin will not load it.

### 2c. Download and extract the release artifact

From the same Unraid host shell:

```bash
# Choose the version you want to install. Latest is at:
# https://github.com/ghill11/jellyfin-movie-catalog/releases/latest
VERSION="v0.1.0"

# Replace <CONFIG_HOST> with the path you confirmed in step 2a.
CONFIG_HOST="/mnt/user/appdata/jellyfin"

# Create the plugin directory (the trailing 'MovieCatalog' folder name is what
# Jellyfin's plugin loader will display; keep it consistent across reinstalls).
mkdir -p "$CONFIG_HOST/data/plugins/MovieCatalog"
cd "$CONFIG_HOST/data/plugins/MovieCatalog"

# Download the release zip.
wget "https://github.com/ghill11/jellyfin-movie-catalog/releases/download/$VERSION/jellyfin-movie-catalog-$VERSION.zip"

# Extract it. The zip contains two files at the root: the .dll and meta.json.
unzip -o "jellyfin-movie-catalog-$VERSION.zip"
rm "jellyfin-movie-catalog-$VERSION.zip"

# Set ownership to the user the Jellyfin container runs as. On most Unraid
# Jellyfin images this is nobody:users (uid 99, gid 100).
chown -R nobody:users .

# Confirm the result.
ls -la
```

You should see exactly two files: `Jellyfin.Plugin.MovieCatalog.dll` and `meta.json`, both owned by `nobody:users`.

### 2d. Restart the Jellyfin container

Use the Unraid Docker tab (click the Jellyfin icon -> Restart) or from the shell:

```bash
docker restart jellyfin
```

Wait ~20 seconds for Jellyfin to come back up.

## Step 3: Verify the plugin loaded

In the Unraid shell:

```bash
# Latest log file from Jellyfin
ls -lt "$CONFIG_HOST"/log/log_*.log | head -1

# Filter for our plugin's load lines
grep -E "MovieCatalog|Movie Catalog" "$CONFIG_HOST"/log/log_*.log | tail -10
```

You should see three lines from the most recent restart:

```
PluginManager: Loaded assembly "Jellyfin.Plugin.MovieCatalog, Version=0.1.0.0..." from "/config/data/plugins/MovieCatalog/Jellyfin.Plugin.MovieCatalog.dll"
PluginManager: Loaded plugin: "Movie Catalog" "0.1.0.0"
JellyfinMovieCatalog: LibraryEventListener subscribed (ItemAdded/Updated/Removed, filtered to Movie)
```

If you DON'T see those lines:
- Re-check the path in step 2b. The most common failure is the plugin being at `<config>/plugins/` instead of `<config>/data/plugins/`.
- Run `ls -la "$CONFIG_HOST/data/plugins/MovieCatalog/"` and confirm both files are present and owned by `nobody:users`.
- Check the log around the restart time for any error lines mentioning `MovieCatalog`.

## Step 4: Configure the plugin

### 4a. Don't panic if you see "An error occurred while getting the plugin details from the repository"

When you navigate to **Dashboard -> Plugins** and click on **Movie Catalog**, Jellyfin may show an error banner:

> An error occurred while getting the plugin details from the repository.

**This is a cosmetic UI error, not a plugin problem.** Jellyfin tries to fetch update info for every installed plugin by querying the plugin-catalog repositories it knows about (the official Jellyfin plugin repo and any others you've added). Sideloaded plugins like Movie Catalog aren't in any of those catalogs, so the lookup fails and Jellyfin surfaces the error in the UI. The plugin itself is loaded and functional - you verified that via the log in step 3.

The standard catalog-clicks navigation also doesn't reliably open the settings page for sideloaded plugins. Use the direct settings-page URL instead.

### 4b. Open the settings page via the direct URL

```
http://<your-jellyfin-url>/web/index.html#!/configurationpage?name=Movie+Catalog
```

For example: `http://192.168.4.175:8096/web/index.html#!/configurationpage?name=Movie+Catalog`. Bookmark this URL; you'll want it again for PAT rotations or to tweak settings.

### 4c. Fill in the settings form

On the settings page, fill in:

| Field | Value |
|---|---|
| Owner | Your GitHub username (e.g., `ghill11`) |
| Repo | Repository name (e.g., `jellyfin-movie-catalog`) |
| Branch | `main` |
| JSON Path | `docs/movies.json` (this is the path inside the repo where the catalog JSON is written; the default value works with GitHub Pages serving from `main /docs`) |
| Personal Access Token | The PAT from step 1 (paste; the field is masked with a "Show" toggle for verification) |
| Debounce Seconds | `30` (raise this if your library scans produce especially noisy events) |

### Click "Test Connection" before saving

Expected responses:
- `OK - file exists, current SHA <hash>...` -> Auth works, file is reachable, ready to push.
- `OK - file does not yet exist; first sync will create it.` -> Auth works; the first push will create the file. Also fine.
- `Auth failed (HTTP 401 or 403) - check your PAT.` -> Re-check PAT scope and expiry (step 1).
- `Repo not found - check owner/repo.` -> Typo in Owner or Repo.

### Save

The Save button persists the config. PAT rotation: simply paste a new PAT on this page and Save; the very next sync uses the new value (no Jellyfin restart needed).

## Step 5: Trigger the initial sync via Scheduled Tasks

After saving the configuration in step 4, the plugin is armed but hasn't pushed anything yet. You need to fire one manual sync to push the current library state to GitHub. After that, future library changes (movies added, removed, or updated) will auto-trigger syncs without intervention.

### 5a. Run the scheduled task from the web UI

1. From the Jellyfin dashboard, click the **Scheduled Tasks** entry in the left sidebar (under "Advanced").
2. The page lists every scheduled task Jellyfin knows about (some from Jellyfin itself, some from plugins). Scroll or search the list for the task named **`Resync Movie Catalog Now`** (it's under the "Movie Catalog" category).
3. Hover over the task row; a **Run** button (right-pointing triangle, like a media play button) appears at the right end of the row.
4. Click **Run**.

Jellyfin shows a small progress indicator while the task is running. The task typically completes in 1-3 seconds for libraries under ~5,000 movies. The "Last result" column updates to show "Completed" or "Failed".

If the result is **Completed**, push succeeded; proceed to step 6 to verify on GitHub.

If the result is **Failed**, open the log (step 6) and look for the most recent `JellyfinMovieCatalog` lines. The error message identifies the cause; usually a PAT scope or expiry problem.

### 5b. When to manually run this task (vs. auto-sync)

The plugin runs the sync automatically every time Jellyfin's library detects a change (movie added, updated, removed), after a 30-second quiet window. You normally don't need to touch the scheduled task. Manual trigger is useful for:

- **First sync after install or reconfiguration** - this step.
- **After rotating the PAT** - to confirm the new PAT works immediately.
- **After a manual edit to the JSON in GitHub** - e.g., if you ever hand-edit `docs/movies.json` and want the plugin to overwrite it with the current library state.
- **Troubleshooting** - if the auto-sync seems to not be firing on library changes and you want to force a known-good push to compare against.

### 5c. Alternative: trigger via REST API

If you prefer scripts to clicks, the same trigger is reachable via REST. The task id is stable across restarts (it's a hash of the task's Name + Key). To look it up once:

```bash
# Get an admin API token (substitute your admin username/password)
TOKEN=$(curl -s -X POST http://<jellyfin-url>/Users/AuthenticateByName \
  -H "Content-Type: application/json" \
  -H "Authorization: MediaBrowser Client=\"smoke\", Device=\"smoke\", DeviceId=\"smoke\", Version=\"0.1\"" \
  -d '{"Username":"<admin>","Pw":"<password>"}' \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['AccessToken'])")

# Look up the Resync task id
TASK_ID=$(curl -s "http://<jellyfin-url>/ScheduledTasks" \
  -H "X-Emby-Token: $TOKEN" \
  | python3 -c "
import json, sys
for t in json.load(sys.stdin):
    if t.get('Key') == 'MovieCatalogResync':
        print(t['Id']); break
")
echo "Resync task id: $TASK_ID"

# Trigger the task (returns HTTP 204 on success)
curl -sw "HTTP %{http_code}\n" -X POST \
  "http://<jellyfin-url>/ScheduledTasks/Running/$TASK_ID" \
  -H "X-Emby-Token: $TOKEN"
```

Save `$TASK_ID` once; it survives Jellyfin restarts as long as the plugin's GUID and task Key don't change (they're frozen).

## Step 6: Verify end-to-end

Watch the plugin log:

```bash
tail -f "$CONFIG_HOST"/log/log_*.log | grep JellyfinMovieCatalog
```

Successful sync looks like:

```
JellyfinMovieCatalog: Manual resync triggered
JellyfinMovieCatalog: GitHub push OK (200); payload <N> bytes
JellyfinMovieCatalog: Manual resync complete
```

If you see `GitHub push got 403` or `GitHub push got 401`, the PAT is wrong or has insufficient scope. See step 1.

Then verify on GitHub:

1. Open `https://github.com/<github-username>/jellyfin-movie-catalog/commits/main` - you should see a fresh commit titled `movie catalog sync 2026-MM-DDTHH:mm:ss...`.
2. Wait ~30 seconds for GitHub Pages to rebuild.
3. Open `https://<github-username>.github.io/jellyfin-movie-catalog/` on any device. The page should show your movie library with sortable columns and a title search.

## Daily operation: what to expect

- After configuration, the plugin runs in the background. No manual triggering needed.
- Every time you add, remove, or update a movie in Jellyfin (rip a DVD, delete an old file, etc.), the plugin schedules a sync.
- A 30-second quiet window must elapse with no further changes before the sync fires. This coalesces bursts (e.g., a library scan that adds 50 movies pushes once, not 50 times).
- Each successful sync produces one commit on `main` of your GitHub repo and one Pages rebuild (~30 seconds later).
- You can always force an immediate sync via **Scheduled Tasks -> Resync Movie Catalog Now**.

## Troubleshooting

### Plugin doesn't appear in Dashboard -> Plugins

Most common cause: wrong install path. Re-do step 2b.

### Plugin appears but settings page won't open

Use the direct URL from step 4. Jellyfin's plugin catalog lookup can fail for sideloaded plugins.

### `GitHub push got 403; PAT bad or missing scope`

The PAT does not have `Contents: write` on the configured repo. Go to https://github.com/settings/personal-access-tokens and confirm:
- The PAT is not expired.
- Its "Repository access" includes your target repo.
- Its "Repository permissions" has Contents set to "Read and write" (not Read-only).

### `GitHub push got 401`

The PAT is invalid, expired, or revoked. Generate a new PAT (step 1) and paste it into the plugin settings; click Save.

### Push 409 retried once and still fails

A concurrent edit collision. Rare in normal use (only you and the plugin push to this file). Wait a minute and trigger Resync again, or investigate any other tool that might be writing to `docs/movies.json`.

### Plugin loaded but no syncs happen on library changes

- Confirm the library scan actually detected the change. Check the log for `ItemAdded` / `ItemUpdated` / `ItemRemoved` lines around the time of the change.
- Confirm `LibraryEventListener subscribed` appeared in the log after the most recent restart.
- Confirm Debounce Seconds is set to something reasonable (5-60).

### Pages site does not update

- Confirm a fresh `movie catalog sync` commit landed on `main`.
- GitHub Pages has a small lag (~30 seconds typical, occasionally up to a few minutes). Wait, then hard-refresh (Ctrl+Shift+R).
- Confirm GitHub Pages is enabled in your repo (Settings -> Pages -> Source: `main` /docs).

## File locations cheat-sheet (Unraid)

| Thing | Host path |
|---|---|
| Plugin install dir | `<CONFIG_HOST>/data/plugins/MovieCatalog/` |
| Plugin config (XML, contains PAT in plaintext) | `<CONFIG_HOST>/data/plugins/configurations/Jellyfin.Plugin.MovieCatalog.xml` |
| Jellyfin logs | `<CONFIG_HOST>/log/log_YYYYMMDD.log` |
| Jellyfin database (SQLite) | `<CONFIG_HOST>/data/data/jellyfin.db` |

The plugin config XML contains the PAT in plaintext. This is a known Jellyfin behavior, not specific to this plugin. The narrow PAT scoping from step 1 is the mitigation.

## Updating the plugin to a new version

1. Stop the Jellyfin container (or have the file in-use; replacement on a live container usually works, but a stop is safer).
2. From the Unraid shell:
   ```bash
   VERSION="vX.Y.Z"   # the new version
   cd "$CONFIG_HOST/data/plugins/MovieCatalog"
   rm Jellyfin.Plugin.MovieCatalog.dll meta.json
   wget "https://github.com/ghill11/jellyfin-movie-catalog/releases/download/$VERSION/jellyfin-movie-catalog-$VERSION.zip"
   unzip -o "jellyfin-movie-catalog-$VERSION.zip"
   rm "jellyfin-movie-catalog-$VERSION.zip"
   chown -R nobody:users .
   ```
3. Restart the container.
4. Verify the new version loaded via the log (step 3).
5. Your existing configuration (Owner, Repo, Branch, JsonPath, PatToken, DebounceSeconds) persists across upgrades. No reconfiguration needed unless the new version adds new settings.

## Uninstalling the plugin

```bash
docker stop jellyfin
rm -rf "$CONFIG_HOST/data/plugins/MovieCatalog"
rm -f "$CONFIG_HOST/data/plugins/configurations/Jellyfin.Plugin.MovieCatalog.xml"
docker start jellyfin
```

The viewer site at `https://<github-username>.github.io/jellyfin-movie-catalog/` will still exist with the last-synced catalog. To stop serving the catalog publicly, either delete the repo or disable Pages in repo settings.

---

# Appendix: PAT storage

> The section below is for your private records only. Do NOT commit a copy of this file with the PAT filled in. Append your PAT details, save this file to a secure location (password manager attachment, encrypted backup, etc.), and shred any other copies.

```
GitHub Personal Access Token for Jellyfin Movie Catalog

Token name (as it appears on GitHub):
Created on:
Expires on:
Repository scope:
Permissions:

Token value:


-- end --
```
