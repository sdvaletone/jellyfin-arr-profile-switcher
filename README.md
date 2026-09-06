# Jellyfin.Plugin.ArrProfileSwitcher

Lets any Jellyfin user switch a movie's or show's Radarr/Sonarr quality profile —
from an admin-curated list of options (e.g. 1080p / 4K) — right from the item detail
page or three-dot menu, and triggers a search. No separate Radarr/Sonarr login needed.

## Target

| | |
|---|---|
| Jellyfin server | **10.11.11** (`linuxserver/jellyfin` on `nuc`) |
| SDK packages | `Jellyfin.Controller` / `Jellyfin.Model` / `Jellyfin.Data` **10.11.11** |
| TFM | `net9.0` |
| Plugin GUID | `6556f718-0762-48ec-bc64-bac5b86ed0c6` |

The GUID appears in four places and they must stay identical: `Plugin.cs`
(`Plugin.Id`), `meta.json` (`guid`), `build.yaml` (`guid`), and the config page's
`pluginUniqueId`.

The version appears in three places: `Directory.Build.props`, `meta.json`, `build.yaml`.

## Security model

Radarr/Sonarr API keys never touch this plugin's config page, its API responses, or
the browser:

- `RADARR_API_KEY` / `SONARR_API_KEY` are read from environment variables on the
  Jellyfin container (see `ansible/roles/svc_jellyfin_arr_profile/`) — never entered
  through the admin UI, never echoed back by any endpoint.
- `GET /ArrProfileSwitcher/Profiles` (lists real profile ids/names, used only by the
  admin config page) requires `Policies.RequiresElevation` (admin).
- `GET /ArrProfileSwitcher/Status` and `POST /ArrProfileSwitcher/Upgrade` require only
  `[Authorize]` (any logged-in user), per design — but the client can only ever choose
  from a closed set of admin-curated `OptionId`s; the real Radarr/Sonarr profile id is
  resolved server-side and is never accepted from a request.
- Every accepted `/Upgrade` call is logged (`{User, ItemId, PreviousProfile,
  NewProfile}`) via `ILogger`, which flows to Loki like every other container log.
- A per-item cooldown (`ThrottleMinutes`, default 10) prevents double-click / repeat
  search-command floods.
- Radarr (`:7878`) and Sonarr (`:8989`) are reached over the internal `media_stack`
  Docker network by container DNS name — this traffic never goes through Traefik or
  leaves the host.

## Client-side injection

The item detail page and three-dot menu integration is injected into
`index.html` via the **File Transformation** plugin (IAmParadox27,
[jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)),
already installed on the NUC's Jellyfin (it's what WhisperSubs and HomeScreenSections
use too). `Services/StartupService.cs` registers the patch via reflection at server
startup (plugins load into separate `AssemblyLoadContext`s, so it can't be referenced
directly — this is File Transformation's own documented integration pattern).
`Services/TransformationPatches.IndexHtml` inserts `<script>`/`<link>` tags pointing at
this plugin's own config-page-served JS/CSS (`configurationpage?name=arrProfileSwitcher.js`
— same mechanism WhisperSubs uses to ship `whisperSubs.js`, confirmed live on this server).

If File Transformation isn't installed, the plugin logs a warning at startup and the
admin config page + API still work — only the client-side picker never appears.

**Android TV**: out of scope. This injection technique only reaches Jellyfin Web (and
any client that's a WebView of it); the native Android TV app has no hook for custom
UI from a server plugin.

## Build

No CI, no GitHub Release — built locally and the output is committed straight to
`dist/`, which `ansible/roles/svc_jellyfin_arr_profile` copies onto the NUC. Same
process as `src/jellyfin-loudness-normalizer/`:

```bash
# From the NUC (has Docker; avoids needing a local .NET SDK):
scp -r src/jellyfin-arr-profile-switcher nuc:/tmp/aps-build
ssh nuc "docker run --rm -v /tmp/aps-build:/src -w /src mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -c 'dotnet restore Jellyfin.Plugin.ArrProfileSwitcher/Jellyfin.Plugin.ArrProfileSwitcher.csproj && \
           dotnet publish Jellyfin.Plugin.ArrProfileSwitcher/Jellyfin.Plugin.ArrProfileSwitcher.csproj -c Release -o /src/out --no-restore'"
scp nuc:/tmp/aps-build/out/Jellyfin.Plugin.ArrProfileSwitcher.dll dist/
cp meta.json dist/meta.json
ssh nuc "docker run --rm -v /tmp/aps-build:/scratch alpine sh -c 'rm -rf /scratch/*'"  # container wrote as root
```

Then commit `dist/Jellyfin.Plugin.ArrProfileSwitcher.dll` and `dist/meta.json` together
(keep `meta.json`'s `version` and `Directory.Build.props`'s `<Version>` in sync) — the
next `svc_jellyfin_arr_profile` deploy picks up the change and restarts Jellyfin.

## API

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /ArrProfileSwitcher/Profiles` | Admin | Real Radarr/Sonarr quality profiles, for the option-mapping editor. |
| `GET /ArrProfileSwitcher/Status?itemId=` | Any user | Whether an item is tracked, its current profile, and the selectable options. |
| `POST /ArrProfileSwitcher/Upgrade` | Any user | `{ ItemId, OptionId }` — applies the chosen admin-curated option and triggers a search. |

## Not in this project

The Ansible deploy role (`ansible/roles/svc_jellyfin_arr_profile/`) and the
`RADARR_API_KEY`/`SONARR_API_KEY` wiring into `docker/nuc/jellyfin/docker-compose.yml`
live elsewhere in the repo.
