# Jellyfin Movie Catalog - dev environment setup

> Step-by-step guide for recreating the local dev environment used to build, test, and iterate on the plugin on a Windows machine. The dev environment is a Windows-portable Jellyfin Server running from a single folder (`D:\jf-dev\`); purging it is `Remove-Item -Recurse -Force D:\jf-dev\` with zero registry / AppData residue.
>
> This is the autonomous dev loop. Once it's set up, you can build the plugin, sideload to the dev Jellyfin, exercise it against a sacrificial test GitHub repo, and tear everything down in seconds.
>
> Companion to `INSTALL.md`. INSTALL.md is for production sideload on Unraid; this file is for the local Windows dev instance.

## Prerequisites

| Tool | Version | Install command (winget on Windows) |
|---|---|---|
| .NET SDK | 9.0.x | `winget install Microsoft.DotNet.SDK.9` |
| git | 2.x | usually pre-installed; otherwise `winget install Git.Git` |
| GitHub CLI (`gh`) | 2.x | `winget install GitHub.cli` |
| Node.js (optional, for `npx serve` on the viewer) | 18+ | `winget install OpenJS.NodeJS.LTS` |
| PowerShell | 5.1 (built-in) or 7.x | built-in is fine |

After install, verify in a fresh shell:
- `dotnet --version` -> `9.0.x`. If "command not found" in your existing shell, the System PATH was updated but your shell session captured PATH at startup. Either open a new shell or `export PATH="/c/Program Files/dotnet:$PATH"` temporarily.
- `gh auth status` should show you logged in to your GitHub account.
- `git --version`.

## Workspace layout

| Path | Purpose |
|---|---|
| `D:\GitHub\jellyfin-movie-catalog\` | Source repo (clone of `ghill11/jellyfin-movie-catalog`) |
| `D:\jf-dev\jellyfin\jellyfin\` | Extracted portable Jellyfin binaries (~380 MB) |
| `D:\jf-dev\data\` | Jellyfin's data/config dir (database, logs, plugins) |
| `D:\jf-dev\cache\` | Jellyfin's cache dir |
| `D:\jf-dev\media\Movies\` | Stub media library Jellyfin scans |
| `D:\jf-dev\.smoke-api-key` | Local admin API key for scripting Jellyfin (gitignored) |
| `D:\jf-dev\.env.test.local` | Sacrificial-test-repo PAT (gitignored; outside the repo tree) |

`D:\jf-dev\` is gitignored; nothing under it ever lands in a commit.

## Step 1: Clone the repo

```bash
mkdir -p "/d/GitHub"
cd "/d/GitHub"
gh repo clone ghill11/jellyfin-movie-catalog
cd jellyfin-movie-catalog
```

If you're on a fresh machine, also install the quality hooks (per-clone):

```bash
bash scripts/install-quality-hooks.sh
```

## Step 2: Create the sacrificial test GitHub repo

The dev loop pushes to a throwaway public repo so dev iterations never overwrite the real `docs/movies.json` on the production repo.

```bash
gh repo create ghill11/jellyfin-movie-catalog-test --public \
  --description "Sacrificial test repo for jellyfin-movie-catalog dev iterations. Safe to delete after dev." \
  --add-readme
```

Generate a fine-grained PAT for this test repo:

1. https://github.com/settings/personal-access-tokens/new
2. Resource owner: ghill11
3. Repository access: **Only select repositories** -> `jellyfin-movie-catalog-test` ONLY
4. Repository permissions: **Contents: Read and write** (NOT Read-only; the plugin needs write). Metadata: Read-only is auto-selected.
5. Expiration: 30-90 days
6. Generate token; immediately store it locally:

```bash
mkdir -p /d/jf-dev
cat > /d/jf-dev/.env.test.local <<'EOF'
# Sacrificial test-repo PAT for jellyfin-movie-catalog dev iterations.
# Never copy this file into the repo. Revoke at https://github.com/settings/tokens once dev is done.
JELLYFIN_MOVIE_CATALOG_TEST_PAT=<paste-the-token-here>
EOF
chmod 600 /d/jf-dev/.env.test.local
```

## Step 3: Provision the portable Jellyfin

```powershell
# Run in PowerShell (the back-ticks are PowerShell line continuations)
New-Item -ItemType Directory D:\jf-dev\jellyfin, D:\jf-dev\data, D:\jf-dev\cache, D:\jf-dev\media\Movies -Force

# Find the latest stable Jellyfin Windows portable ZIP at:
# https://repo.jellyfin.org/files/server/windows/stable/
# Browse to the latest version dir -> amd64/ -> grab the file ending in -amd64.zip
$VERSION = "10.11.9"
$URL = "https://repo.jellyfin.org/files/server/windows/stable/v${VERSION}/amd64/jellyfin_${VERSION}-amd64.zip"

Invoke-WebRequest $URL -OutFile D:\jf-dev\jellyfin.zip
Expand-Archive D:\jf-dev\jellyfin.zip -DestinationPath D:\jf-dev\jellyfin\ -Force
Remove-Item D:\jf-dev\jellyfin.zip
```

The ZIP extracts with a nested `jellyfin/` folder; the binary ends up at `D:\jf-dev\jellyfin\jellyfin\jellyfin.exe`.

## Step 4: Seed stub media

Empty `.mkv` files (zero bytes) are sufficient for plugin testing. Jellyfin's media scanner will fail `ffprobe` on each and may not add them to the library on the first scan, but a `Refresh All Libraries` call after configuration usually picks them up by filename.

```bash
cd /d/jf-dev/media/Movies
for title in \
  "Big Buck Bunny (2008)" \
  "Sintel (2010)" \
  "Tears of Steel (2012)" \
  "Elephants Dream (2006)" \
  "Cosmos Laundromat (2015)"
do
  mkdir -p "$title"
  : > "$title/$title.mkv"
done
ls -la
```

(If you want a more realistic test with non-empty media, download tiny real `.mkv` files instead. The above stubs are enough to exercise the plugin's library-event firing path.)

## Step 5: Start Jellyfin

```bash
# From Git Bash or any shell that resolves Windows paths through /c, /d
"/d/jf-dev/jellyfin/jellyfin/jellyfin.exe" --datadir "D:\jf-dev\data" --cachedir "D:\jf-dev\cache" &
disown
```

**Gotcha**: Jellyfin's CLI does NOT accept `--port`. The port is set via a config file (auto-generated on first run; default 8096). If port 8096 is taken on your machine, stop Jellyfin, edit `D:\jf-dev\data\network.xml` to change `<HttpServerPortNumber>`, and restart.

Poll for readiness:

```bash
until curl -sf http://localhost:8096/System/Info/Public > /dev/null 2>&1; do
  echo "waiting..."
  sleep 2
done
echo "ready"
```

## Step 6: Script the Jellyfin setup wizard

Jellyfin's setup wizard runs on first start. We script it via REST so the dev environment can be torn down and rebuilt without GUI clicks.

**Critical sequence note**: `GET /Startup/User` auto-creates a default user. `POST /Startup/User` then UPDATES that user's name/password. Do NOT post `Complete` before `User`; once Complete is sent the wizard is locked and `/Startup/User` requires auth.

```bash
# Step 6.1: Set culture
curl -sw "HTTP %{http_code}\n" -X POST http://localhost:8096/Startup/Configuration \
  -H "Content-Type: application/json" \
  -d '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}'

# Step 6.2: GET /Startup/User triggers the auto-create
curl -s http://localhost:8096/Startup/User
# expected output: {"Name":"<your-windows-username>"}

# Step 6.3: POST /Startup/User to set the dev admin name + password
curl -sw "HTTP %{http_code}\n" -X POST http://localhost:8096/Startup/User \
  -H "Content-Type: application/json" \
  -d '{"Name":"dev","Password":"devdev"}'

# Step 6.4: Remote access (off for local dev)
curl -sw "HTTP %{http_code}\n" -X POST http://localhost:8096/Startup/RemoteAccess \
  -H "Content-Type: application/json" \
  -d '{"EnableRemoteAccess":false,"EnableAutomaticPortMapping":false}'

# Step 6.5: Finalize
curl -sw "HTTP %{http_code}\n" -X POST http://localhost:8096/Startup/Complete
```

All five should return HTTP 204.

## Step 7: Authenticate and capture an admin API token

```bash
TOKEN=$(curl -s -X POST http://localhost:8096/Users/AuthenticateByName \
  -H "Content-Type: application/json" \
  -H "Authorization: MediaBrowser Client=\"smoke\", Device=\"smoke\", DeviceId=\"smoke\", Version=\"0.1\"" \
  -d '{"Username":"dev","Pw":"devdev"}' \
  | python -c "import json,sys; print(json.load(sys.stdin)['AccessToken'])")
echo "$TOKEN" > /d/jf-dev/.smoke-api-key
echo "admin token captured (len $(echo -n $TOKEN | wc -c))"
```

This token is reused across the rest of the dev session for any authenticated REST calls.

## Step 8: Add a movies library

**Path-encoding gotcha**: the JSON body cannot use backslashes in the path (`D:\jf-dev\...` would be `\j` = invalid JSON escape). Use forward slashes; Jellyfin/.NET handles them fine on Windows.

```bash
TOKEN=$(cat /d/jf-dev/.smoke-api-key)
curl -sw "HTTP %{http_code}\n" -X POST \
  "http://localhost:8096/Library/VirtualFolders?name=Movies&collectionType=movies&refreshLibrary=true" \
  -H "X-Emby-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"LibraryOptions":{"PathInfos":[{"Path":"D:/jf-dev/media/Movies"}]}}'
```

Expect HTTP 204. Verify:

```bash
curl -s "http://localhost:8096/Library/VirtualFolders" -H "X-Emby-Token: $TOKEN" | python -c "import json,sys; [print(f['Name'], '->', f['Locations']) for f in json.load(sys.stdin)]"
```

## Step 9: Build the plugin

```bash
cd /d/GitHub/jellyfin-movie-catalog
export PATH="/c/Program Files/dotnet:$PATH"   # only if dotnet not on shell PATH
dotnet build plugin/Jellyfin.Plugin.MovieCatalog.csproj -c Release
```

Expect zero warnings, zero errors. The .dll lands at `plugin/bin/Release/net9.0/Jellyfin.Plugin.MovieCatalog.dll`.

## Step 10: Sideload the plugin

```bash
# Stop Jellyfin (PowerShell):
powershell.exe -Command "Get-Process -Name 'jellyfin' -ErrorAction SilentlyContinue | Stop-Process -Force"
sleep 2

# Copy .dll + meta.json into the dev Jellyfin's plugins dir
# (note the data/plugins/ path, same convention as the production Unraid install):
mkdir -p /d/jf-dev/data/plugins/MovieCatalog
cp plugin/bin/Release/net9.0/Jellyfin.Plugin.MovieCatalog.dll /d/jf-dev/data/plugins/MovieCatalog/
cp plugin/meta.json /d/jf-dev/data/plugins/MovieCatalog/

# Restart
"/d/jf-dev/jellyfin/jellyfin/jellyfin.exe" --datadir "D:\jf-dev\data" --cachedir "D:\jf-dev\cache" &
disown

until curl -sf http://localhost:8096/System/Info/Public > /dev/null 2>&1; do sleep 2; done
echo "ready"
```

Verify the load:

```bash
LATEST_LOG="$(ls -t /d/jf-dev/data/log/*.log | head -1)"
grep -E "MovieCatalog|Movie Catalog" "$LATEST_LOG" | tail -5
```

Expect:

```
PluginManager: Loaded assembly Jellyfin.Plugin.MovieCatalog, Version=0.1.0.0...
PluginManager: Loaded plugin: Movie Catalog 0.1.0.0
JellyfinMovieCatalog: LibraryEventListener subscribed (ItemAdded/Updated/Removed, filtered to Movie)
```

## Step 11: Configure the plugin against the test repo

The standard "Dashboard -> Plugins -> Movie Catalog" navigation may show "An error occurred while getting the plugin details from the repository" - this is Jellyfin's plugin-catalog lookup failing for sideloaded plugins. The plugin itself is fine. Use the direct settings URL:

```
http://localhost:8096/web/index.html#!/configurationpage?name=Movie+Catalog
```

Or skip the UI entirely and POST the config via REST:

```bash
TOKEN=$(cat /d/jf-dev/.smoke-api-key)
PLUGIN_ID="7476643a-a7aa-47eb-8116-5856ce955bb2"
PAT="$(grep '^JELLYFIN_MOVIE_CATALOG_TEST_PAT=' /d/jf-dev/.env.test.local | cut -d= -f2)"

cat > /tmp/jf-plugin-config.json <<EOF
{
  "Owner": "ghill11",
  "Repo": "jellyfin-movie-catalog-test",
  "Branch": "main",
  "JsonPath": "docs/movies.json",
  "PatToken": "$PAT",
  "DebounceSeconds": 30
}
EOF

curl -sw "HTTP %{http_code}\n" -X POST "http://localhost:8096/Plugins/$PLUGIN_ID/Configuration" \
  -H "X-Emby-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  --data-binary "@/tmp/jf-plugin-config.json"
rm /tmp/jf-plugin-config.json
```

Expect HTTP 204. Verify (with PAT redacted in display):

```bash
curl -s "http://localhost:8096/Plugins/$PLUGIN_ID/Configuration" -H "X-Emby-Token: $TOKEN" \
  | python -c "
import json, sys
c = json.load(sys.stdin)
c['PatToken'] = '<redacted ' + str(len(c.get('PatToken',''))) + ' chars>'
print(json.dumps(c, indent=2))
"
```

## Step 12: Trigger Resync and verify end-to-end

```bash
TASK_ID="16528f0fb1b00786ae04f1b1eac3e2a0"   # stable across restarts; lookup once via GET /ScheduledTasks
curl -sw "HTTP %{http_code}\n" -X POST "http://localhost:8096/ScheduledTasks/Running/$TASK_ID" \
  -H "X-Emby-Token: $TOKEN"
sleep 5

# Plugin log:
LATEST_LOG="$(ls -t /d/jf-dev/data/log/*.log | head -1)"
grep "JellyfinMovieCatalog" "$LATEST_LOG" | tail -5

# GitHub side:
PAT="$(grep '^JELLYFIN_MOVIE_CATALOG_TEST_PAT=' /d/jf-dev/.env.test.local | cut -d= -f2)"
curl -s -H "Authorization: Bearer $PAT" -H "Accept: application/vnd.github+json" \
  "https://api.github.com/repos/ghill11/jellyfin-movie-catalog-test/contents/docs/movies.json" \
  | python -c "
import json, base64, sys
d = json.load(sys.stdin)
print('sha:', d['sha'][:7])
print('size:', d['size'], 'bytes')
print('content sample:')
print(base64.b64decode(d['content']).decode('utf-8')[:400])
"
```

Expect:
- Plugin log: `Manual resync triggered` -> `GitHub push OK (200 or 201); payload N bytes` -> `Manual resync complete`
- Test repo: a fresh commit titled `movie catalog sync <UTC-O>` with `docs/movies.json` containing the 5 stub movies.

## Step 13: Iteration loop

Once the dev environment is set up, the typical inner loop while developing the plugin is:

```bash
# 1. Edit C# source under plugin/
# 2. Build
cd /d/GitHub/jellyfin-movie-catalog
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build plugin/Jellyfin.Plugin.MovieCatalog.csproj -c Release

# 3. Stop Jellyfin, copy new .dll, restart, wait for ready
powershell.exe -Command "Get-Process -Name 'jellyfin' -ErrorAction SilentlyContinue | Stop-Process -Force"
sleep 2
cp plugin/bin/Release/net9.0/Jellyfin.Plugin.MovieCatalog.dll /d/jf-dev/data/plugins/MovieCatalog/
"/d/jf-dev/jellyfin/jellyfin/jellyfin.exe" --datadir "D:\jf-dev\data" --cachedir "D:\jf-dev\cache" &
disown
until curl -sf http://localhost:8096/System/Info/Public > /dev/null 2>&1; do sleep 2; done

# 4. Trigger Resync against the test repo
TOKEN=$(cat /d/jf-dev/.smoke-api-key)
curl -X POST "http://localhost:8096/ScheduledTasks/Running/$TASK_ID" -H "X-Emby-Token: $TOKEN"

# 5. Watch logs
tail -f /d/jf-dev/data/log/*.log | grep JellyfinMovieCatalog
```

## Tear-down

When dev is done:

```bash
# Stop Jellyfin
powershell.exe -Command "Get-Process -Name 'jellyfin' -ErrorAction SilentlyContinue | Stop-Process -Force"

# Remove the entire dev tree (Jellyfin binaries + data + cache + media + PAT file)
powershell.exe -Command "Remove-Item -Recurse -Force D:\jf-dev\"
```

Zero residue on the host. No registry entries, no AppData, no Program Files install.

To also clean up GitHub:

```bash
# Delete the sacrificial test repo
gh repo delete ghill11/jellyfin-movie-catalog-test --yes

# Revoke the dev PAT at:
# https://github.com/settings/personal-access-tokens
# (Find the PAT named for the dev test repo, click "Revoke".)
```

## Optional: install the Claude Code CLI for full pre-push gate

The pre-push hook on tag refspecs invokes `claude -p` to run the `quality-inspector` agent (a hard gate). For day-to-day work this isn't needed. For tag-push work, the CLI must be on PATH. The Windows install lives at something like `C:\Users\<you>\AppData\Roaming\Claude\claude-code\<version>\claude.exe`; add that directory to PATH or export it inline before pushing a tag:

```bash
LATEST_CLAUDE_DIR="$(ls -d /c/Users/$USER/AppData/Roaming/Claude/claude-code/*/ 2>/dev/null | sort -V | tail -1)"
export PATH="$LATEST_CLAUDE_DIR:$PATH"
```

(The hook itself could be made resilient to different install paths; that's a tracked v0.1.1+ follow-up.)

## File-location cheat-sheet (dev)

| Thing | Path |
|---|---|
| Source repo | `D:\GitHub\jellyfin-movie-catalog\` |
| Plugin binaries (after `dotnet build`) | `D:\GitHub\jellyfin-movie-catalog\plugin\bin\Release\net9.0\` |
| Portable Jellyfin binary | `D:\jf-dev\jellyfin\jellyfin\jellyfin.exe` |
| Jellyfin data + config | `D:\jf-dev\data\` |
| Jellyfin plugins dir | `D:\jf-dev\data\plugins\MovieCatalog\` |
| Jellyfin logs | `D:\jf-dev\data\log\log_YYYYMMDD.log` |
| Jellyfin SQLite DB | `D:\jf-dev\data\data\jellyfin.db` |
| Dev admin API token | `D:\jf-dev\.smoke-api-key` |
| Dev test-repo PAT | `D:\jf-dev\.env.test.local` |
