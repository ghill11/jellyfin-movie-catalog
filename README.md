# jellyfin-movie-catalog

A Jellyfin Server plugin that mirrors your movie library to a static GitHub Pages catalog, so you can check what you already own from anywhere without exposing your home Jellyfin to the internet.

## What it does

- Plugin subscribes to Jellyfin library events (`ItemAdded` / `ItemUpdated` / `ItemRemoved`) filtered to movies
- Debounces a quiet window (~30s configurable) so a library scan that adds 50 movies pushes once, not 50 times
- PUTs a `movies.json` snapshot to a GitHub repo you own via the Contents API (authenticated with a narrow fine-grained PAT)
- A static viewer page hosted on GitHub Pages fetches that JSON and renders a sortable, title-searchable table
- Open the viewer URL on your phone while you're at a thrift store, used-DVD shop, or library sale; check the title before you buy

## Requirements

- Jellyfin Server 10.11.x
- A GitHub account
- A GitHub repository you own (public, free) that will hold the catalog JSON + viewer
- A fine-grained Personal Access Token scoped to ONLY that one repo with `Contents: read+write`

## Installing (manual sideload)

See [INSTALL.md](INSTALL.md) for the full step-by-step procedure on Unraid + Docker, including PAT generation, path discovery, log verification, and troubleshooting. For local dev environment setup (Windows portable Jellyfin + sacrificial test repo), see [DEV_SETUP.md](DEV_SETUP.md).

Quick version:

1. Grab the latest release zip from [Releases](../../releases).
2. Extract it into your Jellyfin server's plugin directory. On Unraid Docker, the standard host path is:
   ```
   /mnt/user/appdata/jellyfin/data/plugins/MovieCatalog/
   ```
   Note the `/data/` segment. Jellyfin looks for plugins under `<config>/data/plugins/`, NOT `<config>/plugins/`. Confirm your container's `/config` mount with `docker inspect jellyfin --format '{{ range .Mounts }}{{ .Destination }} <-- {{ .Source }}{{ "\n" }}{{ end }}'` if you're unsure.
3. The folder ends up with the `.dll` and `meta.json` directly inside (not nested under another directory). `chown -R nobody:users` so the container user can read them.
4. Restart the Jellyfin container.
5. Dashboard -> Plugins -> Movie Catalog -> configure (use the direct URL `http://<jellyfin-host>/web/index.html#!/configurationpage?name=Movie+Catalog` if the standard plugin page errors).

## Configuring

Open the plugin settings page in Jellyfin's admin dashboard. Fields:

| Field | Description |
|---|---|
| Owner | GitHub username (e.g., `ghill11`) |
| Repo | Target repo name (e.g., `jellyfin-movie-catalog`) |
| Branch | Default `main` |
| JsonPath | Default `docs/movies.json` |
| PatToken | The fine-grained PAT (masked; "Show" toggle for verification) |
| DebounceSeconds | Default 30, min 5 |

Click **Test Connection** before **Save** to verify the PAT and repo path are correct.

Rotation: change the PAT here at any time. The next sync uses the new value immediately.

## Viewing the catalog

Open `https://<your-github-username>.github.io/<repo>/` in any browser. The viewer is plain HTML + JS, no login, no API key. Anyone with the URL can see your movie titles.

If that's a privacy concern, you have two options:
- Use a less-discoverable repo name (security through obscurity; not real security)
- Fork this project, modify the viewer to require a query-string or basic-auth gate, and self-host

## Building from source

Requires .NET 9 SDK.

```bash
dotnet build plugin/Jellyfin.Plugin.MovieCatalog.csproj -c Release
```

The built `.dll` lands at `plugin/bin/Release/net9.0/Jellyfin.Plugin.MovieCatalog.dll`. Drop it (plus `plugin/meta.json`) into your Jellyfin plugins folder.

## License

MIT. See [LICENSE](LICENSE).

## Acknowledgments

This project's Claude Code "harness" (rules, agents, hooks, settings) is ported from `ghill11/apex-platform`. See [EXTRACTION.md](EXTRACTION.md) for the porting log.
