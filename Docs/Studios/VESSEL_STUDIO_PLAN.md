# Vessel Studio — plan and platform map

**Goal.** One studio app: pick a vessel, its studio opens, and you test how it moves, what its abilities
do and how its play-style types feel — from a phone, with no PC needed, and the same studio on a PC when
you want more. The studio **lives in Prisma** (Froglet's own engine, `Port/`), not inside Unity: Unity only
opens Prisma on it. A **studio agent** sits inside it, so a change to a studio or a design idea goes
straight to Claude.

Decided by the user on 2026-10-09: web first; platforms now are **Web, Windows (Prisma.exe) and
Android**, iOS later; Squirrel and Stoat first.

---

## 1. What exists (2026-10-09, branch `vessel-studio`)

| Piece | Where | State |
|---|---|---|
| **Web Vessel Studio** | `Docs/Studios/VesselStudio/` (`index.html` hub, `squirrel.html`, `stoat.html`, `studios.json`, `README.md`); published at https://claude.ai/artifact/EJYgDToG9R2eLzupaQpLgN | **Works.** Hub + Squirrel Studio v1 + the Stoat Flight Studio (round 11). Phone play on every studio. |
| **Squirrel Studio v1** | `squirrel.html` | Flight, drift, skimming boost, Boost Ring, steal, overtake petals, six play-style types over four element levels, all on the shipped numbers (each named in the page). Checked headless: Thief run skims 23 prisms, steals 15, boost 1.68× in 8 s; no console errors; fits 400 px. |
| **Studio agent** | `index.html` § Studio agent | **Ask** (Claude answers with the vessel's spec, through the claude.ai viewer) and **Development requests** (stored in the artifact's database, collection `requests`; a Claude Code session reads them with `ArtifactData`). **Copy as agent prompt** for Prisma's AGENT page. |
| **Prisma STUDIOS page** | `Port/src/CosmicShore.Launcher/LauncherApp.Studios.cs`, `StudioCatalog.cs`; doc `Port/docs/LAUNCHER.md` § STUDIOS | **Works** (built and screenshotted headless). One card per studio from `studios.json`: OPEN (browser), AGENT (a Prisma Agent chat on that studio), DOCS; plus OPEN HUB, WEB LINK, FOLDER. 6 tests. |
| **OPEN IN PRISMA / PLAY IN ENGINE** (2026-10-09, `claude/peaceful-rubin-hhw49n`) | `LauncherApp.Studios.cs`, `StudioCatalog.cs`, `CosmicShore.Player/ArcadeAutoStart.cs` | **Built and checked on Linux** (`PRISMA_TEST_STEPS.md`). The studio opens as its own app window that knows it is in Prisma (`#prisma`). The Stoat's PLAY IN ENGINE boots the game and opens Slingshot by itself (`--arcade Slingshot`); the real Stoat flies and slings in Prisma. The engine gained what this branch's black-hole code needed to compile there (transform jobs, render-graph and `CommandBuffer` API, `Controls.ButtonControl`, `SerializeField`, a GLSL `PrismGravityWarpDeform`). The lens pass is compiled, not drawn. |
| **Unity entry** | `Assets/_Scripts/Editor/LaunchPrisma.cs`: **FrogletTools ▸ Vessels ▸ Vessel Studio** | Opens Prisma (building it first if needed) on the STUDIOS page (`--page studios`). Editor-only, owned by the port. |
| Prisma phone player | `Port/src/CosmicShore.Mobile` | Runs the game itself on Android/iOS; touch feeds the game's own `TouchInputStrategy`. **No device run yet** (`Port/docs/milestones.json` C8). |
| Prisma web build | — | **Does not exist.** Prisma's player is .NET + OpenGL; a browser build is an engine milestone (§4). |

## 2. Two kinds of studio

| | **Web studio** (now) | **Game studio** (next) |
|---|---|---|
| What flies | A JavaScript copy of the vessel, built from the shipped numbers | The real vessel: the game's own code and assets |
| Runs on | Any browser: PC, Android, iPhone; Prisma opens it | Prisma on Windows, Prisma on Android (the player APK); Unity editor for checks |
| Best for | Trying designs and play-style types fast, from a phone | Testing what is actually built, before a release |
| Risk | Can drift from the game: every page names its sources and lists what it does not model | Needs the Prisma phone player proven on a device (C8) |

Both stay. A decision made in a web studio lands in the game; the game studio then tests what landed.

## 3. Platforms

| Platform | Now | Next |
|---|---|---|
| **Web** (desktop browser) | The published Vessel Studio, or the files opened directly | — |
| **Android** | The same link in the phone browser; **Play on phone** (two thumb sticks, two trigger handles that work like a gamepad) | The Prisma player APK with a `VesselStudio` scene: the game's own Squirrel on touch. Built from Prisma's BUILD page, then a cloud Android build so no PC is needed at all |
| **Windows** | Prisma.exe ▸ **STUDIOS** (opens the pages; AGENT chat per studio). From Unity: **FrogletTools ▸ Vessels ▸ Vessel Studio** | STUDIOS gains **PLAY IN ENGINE** for the game studio (`--scene VesselStudio --studio-vessel Squirrel`) |
| **iOS** | The same link in Safari works today | The Prisma `.ipa` from GitHub (`prisma-ios.yml`), installed with Sideloadly (needs a PC weekly) or TestFlight ($99/yr) |
| **Multiplayer** | Not in studios | Web studios can share a session later (the artifact `room` capability); the game studio follows Prisma's netcode |

## 4. The game studio and Prisma on more platforms (later phases)

- **Game studio scene** (Unity content, so Prisma runs it unchanged): `Assets/_Scenes/Studios/VesselStudio.unity`,
  a `VesselStudioCatalogSO` with one `VesselStudioProfileSO` per vessel (class, course, play-style types as
  element-level sets, AI options), a touch-first runtime panel, console verbs (`studio vessel squirrel`,
  `studio type speedster`, `studio element mass 10`, `studio ai hard 2`), runtime views of the vessel audits.
  Rules: no runtime write to a shared ScriptableObject, AI stays input-only, logs on a `Studio` channel.
- **Prisma on Android:** a `cs-build` studio profile (first scene `VesselStudio`), then the first device run
  (closes C8 item 2), then a `prisma-android.yml` cloud build (download the APK on the phone).
- **Prisma in the browser** (a web build of the engine): .NET compiles to WebAssembly and OpenGL ES 3.0
  maps onto WebGL 2, so it is possible; it is a large engine milestone (file access, threads, audio, input,
  load size). Until it exists, the web studios are the browser path. To be scoped as a `Port/docs/ROADMAP.md`
  milestone before any work starts.

## 5. The studio agent

| Where | How |
|---|---|
| Web hub | **Ask**: a question plus the vessel's spec goes to Claude (the viewer's own Claude usage; it asks first). **Development requests**: stored in the artifact's `requests` collection (`vessel`, `kind`, `text`, `status`, `createdAt`, `by`, `reply`). |
| Claude Code session | Reads requests with `ArtifactData` (`list`, collection `requests`, url above), does the work on `vessel-studio`, republishes, then sets `status: done` and a `reply`. |
| Prisma.exe | STUDIOS ▸ **AGENT** opens a Prisma Agent chat on that studio, in plan mode, pointed at this plan and the studio README. |
| Unity | Opens Prisma (above); the agent is Prisma's. |

## 6. Phases

| Phase | Work | Done when | Status |
|---|---|---|---|
| 0 | Web Vessel Studio: hub, Squirrel v1, Stoat, agent | the user flies both on a phone and files a request | **built 2026-10-09**, waiting on the user's phone test |
| 1 | Prisma STUDIOS page + Unity menu item | Prisma.exe on Windows opens both studios; Unity's menu opens Prisma there | **built 2026-10-09**, waiting on a Windows check |
| 2 | Squirrel web studio, round 2, from the user's notes | the notes are in the decision log and each is answered | next |
| 3 | Game studio scene (Squirrel), then Prisma desktop PLAY IN ENGINE | the real Squirrel flies in Prisma with the studio panel | PLAY IN ENGINE **built** (2026-10-09) through the vessel's own arcade mode (`engineMode`); the Stoat flies in Slingshot. The Squirrel needs its `engineMode` (Skim Race), and the studio-panel scene is still to come |
| 4 | Prisma Android: studio APK, device run, cloud build | the APK installs from the phone and the Squirrel flies on touch | after 3 |
| 5 | Stoat in the game studio once its sling design is in the game | both vessels in the game studio | after 4 |
| 6 | iOS; Prisma web build (scoped first) | — | later |

## 7. Branches

The studio work lives on **`vessel-studio`**, cut from `claude/peaceful-rubin-hhw49n` (where the Stoat and
its web studio live). The Prisma changes are confined to `Port/` plus `LaunchPrisma.cs` (the one Unity file
the port owns), so they can go to bleeding-edge as their own port PR; the studio pages are docs, which Unity
never reads.
