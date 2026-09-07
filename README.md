# Jellyfin.Plugin.ArrProfileSwitcher

Lets any Jellyfin user switch a movie's or show's Radarr/Sonarr quality profile
(e.g. 1080p → 4K) from an admin-curated list, right from the item detail page or
three-dot menu, and trigger a search — no Radarr/Sonarr login, and users never see
an API key or URL. Scoped to movies, series, and seasons (not episodes).

## Requirements

- Jellyfin **10.11.x**
- Radarr and/or Sonarr (v3 API), reachable from the Jellyfin server
- [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) —
  needed for the in-page picker to appear in Jellyfin Web; without it, the admin
  config page and API still work. Not supported on Android TV.

## Installation

1. **Dashboard → Plugins → Repositories → Add Repository**
2. Manifest URL: `https://raw.githubusercontent.com/sdvaletone/jellyfin-arr-profile-switcher/main/manifest.json`
3. **Catalog** → install **Arr Profile Switcher** → restart Jellyfin
4. Open the plugin's settings, set your Radarr/Sonarr URLs, and curate which
   quality profiles users can switch to

Not affiliated with or endorsed by Jellyfin; not in the official plugin catalog.

## Security

`RADARR_API_KEY`/`SONARR_API_KEY` are read from environment variables and never
exposed through the config page, API responses, or the browser. The two
user-facing endpoints only let a caller pick from a closed set of admin-curated
options — the real profile id is always resolved server-side — and check that
the caller can actually see the target item. A per-item cooldown throttles
repeat requests.

## API

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /ArrProfileSwitcher/Profiles` | Admin | Real Radarr/Sonarr profiles, for the option-mapping editor. |
| `GET /ArrProfileSwitcher/Status?itemId=` | Any user | Whether an item is tracked, its current profile, and the options. |
| `POST /ArrProfileSwitcher/Upgrade` | Any user | `{ ItemId, OptionId }` — applies the option and triggers a search. |

## Building from source

```bash
dotnet build Jellyfin.Plugin.ArrProfileSwitcher.sln -c Release
dotnet test Jellyfin.Plugin.ArrProfileSwitcher.sln -c Release
```

## Contributing

This repo is a synced mirror of a private monorepo, not a standalone project —
PRs are welcome but land here after being merged on the private side, so they
won't show a normal GitHub merge commit.

## License

MIT — see [LICENSE](LICENSE).
