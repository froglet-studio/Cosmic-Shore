# SteamPipe delivery — ready, not yet armed

Everything here is written and reviewable, but **nothing can run until the Steamworks app exists**
(launch checklist item **A2**). The upload script fails fast with an explanatory message rather than
half-executing, so it is safe to have in the repo before the account is approved.

## What is here

| File | Purpose |
|---|---|
| `templates/app_build.vdf` | SteamPipe app build script template. Tokens are substituted at upload time. |
| `templates/depot_build.vdf` | Depot mapping + the exclusion list that keeps debug symbols out of the shipped depot. |
| `upload.sh` | Renders the templates, validates the build folder, and drives `steamcmd`. |
| `work/` | Generated VDFs and SteamPipe's chunk cache. Git-ignored. |

## Turning it on (after A2)

1. In Steamworks, note the **app id** and the **depot id** (App Admin → Depots).
2. Create a **builder account** — a dedicated Steamworks account with build permission. Do not use a
   personal account; the credentials end up on the build machine.
3. On the build machine, establish a cached session once, interactively, so Steam Guard is satisfied:
   ```bash
   steamcmd +login <builder-account>
   ```
4. Export the environment and upload:
   ```bash
   export STEAM_APPID=<appid>
   export STEAM_DEPOTID=<depotid>
   export STEAM_USER=<builder-account>
   ./upload.sh --build-dir ../../Builds/Windows64 --branch internal
   ```

## Branch convention

| Branch | Who sees it | Used for |
|---|---|---|
| `internal` | Team only, password protected | Every build. Smoke test before anything wider. |
| `beta` | Invited playtesters, password protected | The closed playtest (checklist **E7**). |
| `default` | **Everyone who owns the game** | Launch and patches only. |

`upload.sh` will not publish to `default` unless you pass `--set-live` *and* type the app id back to
confirm. Uploading without `--set-live` puts the build in Steamworks where it can be reviewed and
published from the web UI — that is the normal path.

## Notes

- **No Steamworks SDK is integrated into the game**, by decision. The overlay, wishlists, reviews,
  and forums all work on a plain Windows build. Achievements and Steam Input come post-launch.
- The build description is stamped automatically from `build_manifest.txt`, which
  `CosmicShoreBuildPipeline` writes next to the player. That is how a Steam build record traces back
  to a commit.
- Depot exclusions drop `.pdb`, `.debug`, Burst debug information, and the IL2CPP
  `*_BackUpThisFolder_ButDontShipItWithYourGame*` folder. Shipping those wastes hundreds of MB and
  hands out the symbol table.
