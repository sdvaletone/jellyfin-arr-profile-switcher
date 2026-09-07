# Jellyfin.Plugin.ArrProfileSwitcher

Profile-switch menu item on the item detail page / three-dot menu, scoped to movies,
series, and seasons (not episodes — Sonarr has no per-season quality profile, so a
season page switches/displays the same series-wide profile a series page would).
Options are admin-curated per Radarr/Sonarr instance (e.g. 1080p / 4K); switching
applies the profile and triggers a search. No Radarr/Sonarr login needed — your users
never see a Radarr/Sonarr API key or URL.

## Requirements

- Jellyfin **10.11.x**
- Radarr and/or Sonarr (v3 API), reachable from the Jellyfin server
- Optional, for the in-page picker: [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) —
  without it, the admin config page and API still work, but the client-side picker
  won't appear in Jellyfin Web (see "Client-side injection" below)

## Installation

1. In Jellyfin, go to **Dashboard → Plugins → Repositories → Add Repository**.
2. Add this repository's manifest URL: `https://raw.githubusercontent.com/sdvaletone/jellyfin-arr-profile-switcher/main/manifest.json`
3. Go to **Catalog**, find **Arr Profile Switcher**, install it, and restart Jellyfin.
4. Open the plugin's settings page and set your Radarr/Sonarr base URLs (e.g.
   `http://radarr:7878`) and cooldown, then curate which quality profiles users can
   switch to.

This plugin is not affiliated with or endorsed by Jellyfin and isn't in the official
plugin catalog — install it via the repository URL above.

## Security model

Radarr/Sonarr API keys never touch this plugin's config page, its API responses, or
the browser:

- `RADARR_API_KEY` / `SONARR_API_KEY` are read from environment variables on the
  Jellyfin process — never entered through the admin UI, never echoed back by any
  endpoint.
- `GET /ArrProfileSwitcher/Profiles` (lists real profile ids/names, used only by the
  admin config page) requires `Policies.RequiresElevation` (admin).
- `GET /ArrProfileSwitcher/Status` and `POST /ArrProfileSwitcher/Upgrade` require only
  `[Authorize]` (any logged-in user), per design — but the client can only ever choose
  from a closed set of admin-curated `OptionId`s; the real Radarr/Sonarr profile id is
  resolved server-side and is never accepted from a request. Both endpoints also check
  that the requesting user can actually see the target item (parental controls /
  restricted libraries), so an item id alone isn't enough to probe or act on a
  restricted item.
- Every accepted `/Upgrade` call is logged (`{User, ItemId, PreviousProfile,
  NewProfile}`) via the server's standard logging.
- A per-item cooldown (`ThrottleMinutes`, default 10) prevents double-click / repeat
  search-command floods; the cooldown check is atomic under concurrent requests.

## Client-side injection

The item detail page and three-dot menu integration is injected into
`index.html` via the **File Transformation** plugin (IAmParadox27,
[jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)).
`Services/StartupService.cs` registers the patch via reflection at server
startup (plugins load into separate `AssemblyLoadContext`s, so it can't be referenced
directly — this is File Transformation's own documented integration pattern).
`Services/TransformationPatches.IndexHtml` inserts `<script>`/`<link>` tags pointing at
this plugin's own config-page-served JS/CSS (`configurationpage?name=arrProfileSwitcher.js`).

If File Transformation isn't installed, the plugin logs a warning at startup and the
admin config page + API still work — only the client-side picker never appears.

**Android TV**: out of scope. This injection technique only reaches Jellyfin Web (and
any client that's a WebView of it); the native Android TV app has no hook for custom
UI from a server plugin.

## Building from source

```bash
dotnet build Jellyfin.Plugin.ArrProfileSwitcher.sln -c Release
dotnet test Jellyfin.Plugin.ArrProfileSwitcher.sln -c Release
```

The build output (`Jellyfin.Plugin.ArrProfileSwitcher.dll`) plus `meta.json` is what
Jellyfin's `PluginManager` expects in a directory named `ArrProfileSwitcher_<version>`
under your Jellyfin data directory's `plugins/` folder, if installing manually instead
of via the repository above.

## API

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /ArrProfileSwitcher/Profiles` | Admin | Real Radarr/Sonarr quality profiles, for the option-mapping editor. |
| `GET /ArrProfileSwitcher/Status?itemId=` | Any user | Whether an item is tracked, its current profile, and the selectable options. |
| `POST /ArrProfileSwitcher/Upgrade` | Any user | `{ ItemId, OptionId }` — applies the chosen admin-curated option and triggers a search. |

## Contributing

This repository is a synced mirror of a private monorepo — it's the full,
authoritative plugin source (not a submodule or a stub), but changes here are
merged in on the private side and re-synced out, rather than merged directly
on GitHub. Issues and pull requests are welcome; a PR may take a bit longer to
land than usual and won't show a normal GitHub merge commit when it does, but
it will get reviewed and credited.

## License

MIT — see [LICENSE](LICENSE).
