# SteamPipe delivery — ready, not yet armed

Everything here is written and reviewable, but **nothing can run until the Steamworks apps exist**.
Under the Playtest model there are **two** of them, and one script serves both from one set of
templates by naming a target:

| Target | What it is | Ids come from | Environment |
|---|---|---|---|
| `--target base` *(default)* | The store app. The future **paid** build. | Checklist **A2** (create the Steamworks app) | `STEAM_APPID` / `STEAM_DEPOTID` |
| `--target playtest` | The **Playtest child app**. The live invite channel. | Checklist **A4** (create the Playtest child app — `Docs/STEAM_PLAYTEST_RUNBOOK.md`) | `STEAM_PLAYTEST_APPID` / `STEAM_PLAYTEST_DEPOTID` |

The upload script fails fast with a message naming the checklist item that produces the missing id
rather than half-executing, so it is safe to have in the repo before either app exists. **No app id,
depot id or credential is stored in the repository** — they live in the build machine's environment.

## What is here

| File | Purpose |
|---|---|
| `templates/app_build.vdf` | SteamPipe app build script template. Tokens are substituted at upload time from whichever target was named. |
| `templates/depot_build.vdf` | Depot mapping + the exclusion list that keeps debug symbols out of the shipped depot. |
| `upload.sh` | Resolves the target's ids, renders the templates, validates the build folder, and drives `steamcmd`. |
| `work/` | Generated VDFs and SteamPipe's chunk cache. Git-ignored. |

## Turning it on (after A2 for the base app, after A4 for the Playtest app)

1. In Steamworks, note the **app id** and the **depot id** (App Admin → Depots) **of the app you are
   targeting**. The Playtest child app has its own pair; it is never the base app's.
2. Create a **builder account** — a dedicated Steamworks account with build permission on both
   apps. Do not use a personal account; the credentials end up on the build machine.
3. On the build machine, establish a cached session once, interactively, so Steam Guard is satisfied:
   ```bash
   steamcmd +login <builder-account>
   ```
4. Export the environment for the target and upload:
   ```bash
   # Playtest child app — the invite channel (ids from A4)
   export STEAM_PLAYTEST_APPID=<playtest-appid>
   export STEAM_PLAYTEST_DEPOTID=<playtest-depotid>
   export STEAM_USER=<builder-account>
   ./upload.sh --target playtest --build-dir ../../Builds/Windows64 --branch internal

   # Base app — the future paid build (ids from A2). --target base is the default.
   export STEAM_APPID=<appid>
   export STEAM_DEPOTID=<depotid>
   ./upload.sh --target base --build-dir ../../Builds/Windows64 --branch internal
   ```
   Both pairs may be exported at once. The script reads only the pair for the chosen target, and
   refuses outright if the two pairs share an id — that is the paste error that publishes a build
   to the wrong audience.

## Branch convention under two apps

The model is stated once, in full, in `Docs/STEAM_PLAYTEST_RUNBOOK.md` § *Branch model under two
apps*. The short form — note that the **same branch name has a different audience on each app**:

| Branch | Base app (`--target base`) | Playtest app (`--target playtest`) |
|---|---|---|
| `internal` | Team only, password protected. Every build lands here first. | Team only, password protected. Smoke-test the exact child-app build from Steam before it reaches testers; the overlay check (**B7**) runs here now. |
| `beta` | Revision-1's closed-playtest branch. Under the Playtest model nobody outside the team owns the base app, so it has **no audience** until the paid conversion. Keep it password protected; do not point testers at it. | Not defined. The tester population is managed by signup grants and friend invites, not by a branch password. |
| `default` | **Everyone who owns the game.** The paid release and its patches, nothing before. | **Every tester who has been granted access** — signup grants and friend invites alike. This is the live invite channel (**E7**). |

`upload.sh` will not pass `setlive` for `default` on either app unless you pass `--set-live` *and*
type the target's app id back. Two things to know about that:

- Valve's SteamPipe documentation says the `default` branch **cannot be set live from a build
  script at all** — *"That must be done through the App Admin panel"*
  (<https://partner.steamgames.com/doc/sdk/uploading>). So the normal path is to upload *without*
  `--set-live` and publish from App Admin → Builds. The confirmation is the last line of defence,
  not the publishing mechanism.
- On the Playtest app, `default` is what every granted tester downloads. Setting a build live there
  is a wave-wide ship, and the wave policy (`Docs/STEAM_PLAYTEST_RUNBOOK.md` § A6) says when that
  is allowed.

The build description stamps the target (`[base]` / `[playtest]`) beside the version and commit, so
the two apps' build lists in Steamworks are never ambiguous.

## Notes

- **No Steamworks SDK is integrated into the game**, by decision. The overlay, wishlists, reviews,
  and forums all work on a plain Windows build. Achievements and Steam Input come post-launch.
- The build description is stamped automatically from `build_manifest.txt`, which
  `CosmicShoreBuildPipeline` writes next to the player, plus the target. That is how a Steam build
  record on either app traces back to a commit.
- Depot exclusions drop `.pdb`, `.debug`, Burst debug information, and the IL2CPP
  `*_BackUpThisFolder_ButDontShipItWithYourGame*` folder. Shipping those wastes hundreds of MB and
  hands out the symbol table.
- Two Valve rules the runbook carries and the uploader cannot enforce: Playtest participants cannot
  review the base game and Playtest metrics never touch it; and charging for Playtest access in any
  form is prohibited (<https://partner.steamgames.com/doc/features/playtest>).
