# Vessel Studio — plan (Squirrel and Stoat first, phone first, in Prisma)

**Goal.** Open one app on a phone, pick a vessel, and its studio opens: fly it with the game's own
controls, switch its play-style types, turn its abilities and element levels up and down, race it
against its AI, and read its live numbers — with no PC. The same studio runs in the Unity editor and in
Prisma on a desktop, so a phone finding can be reproduced at a desk. Squirrel and Stoat go first; every
other vessel joins by adding one data asset.

Status: **plan, nothing built yet** (2026-10-09). Measured on `claude/peaceful-rubin-hhw49n` at
`65fd65001` (Stoat Flight Studio round 11).

---

## 1. What exists today (measured, so nothing is built twice)

| Thing | Where | What it gives the studio |
|---|---|---|
| **Stoat Flight Studio** | `Docs/Studios/StoatFlightStudio.html` (264 KB, three.js, 11 rounds; live: https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc) | A **design** studio: it re-implements the Stoat's sling in JavaScript from the shipped numbers, with five play styles, five types per setting group, 102 illustrated tooltips, a decision log and a **Play on phone** mode (two thumb sticks, two trigger handles). Runs in any phone browser today. |
| **Prisma phone player** | `Port/src/CosmicShore.Mobile` (`MobileHost`, `TouchBridge`) | The game's own boot on a full-screen GL ES 3.0 view. Touch goes into the game's own `TouchInputStrategy`, and the first finger clicks uGUI. So a studio built as game content gets phone controls for free. |
| **Prisma phone builds** | Launcher **BUILD** page; `cs-build` (`Port/src/CosmicShore.Build`) | Android APK/AAB built on the PC; iOS `.ipa` built on GitHub's Mac (`.github/workflows/prisma-ios.yml`), unsigned, installed with Sideloadly. **No device run yet** (`Port/docs/milestones.json` C8 "Platforms" = todo). No cloud Android build. |
| **Prisma launch into a scene** | `CosmicShore.Player --scene NAME` | A studio scene can be opened directly, skipping menus. |
| **Prisma model viewer** | `--view-model FILE`, EDITOR > MODELS | Looks at a hull's mesh; does not fly it. |
| **Vessel editor tools** | `FrogletTools/Vessels/*` (17: audits of ability rows, construction, morphs, skimmers, tails and jets, corridor radii, vision band, speed-tunnel law; wirers) | Editor-only checks of the PREFAB. The studio shows their runtime counterparts (§3.4). |
| **In-game tools** | Vessel Changer toy, Spawn Matrix, freestyle flight in the menu, dev console (`blackhole`, `prisms`, `bench`, `diag`, `prof`, `freeze`, `ab`, `fps`), the Black Hole tool (B, runtime uGUI) | The studio reuses these instead of re-making them. |
| **The Squirrel** | `R_VesselActions/SQUIRREL_*.md`, `Assets/Resources/ElementalAbilityMaps/Squirrel.asset` | Fully built: drift on LT (analog), the Oak Trunk tube on RT, the omni-crystal ring morph, and four element rows — Charge: Crystal Joust steal ×1→×2.5; Mass: Boost Ring cooldown ×1→×0.5; Space: Steal reach 15→30; Time: Skimming energy ×1→×2. Touch controls already exist (right-thumb lift = full drift; keep-right-finger = tube). Its Skim Race AI has Easy / Medium / Hard (`Docs/SKIM_RACE_AI.md`). |
| **A Squirrel studio** | — | **Does not exist** on any branch (every remote branch searched 2026-10-09). The `cece/lab-*` branches are creature labs (NCA), not vessel tools. |

## 2. Two kinds of studio — both are kept, for different jobs

| | **Design studio** (web page) | **Test studio** (game content, runs in Unity and Prisma) |
|---|---|---|
| Question it answers | "Which design should we build?" | "Does the built vessel feel right?" |
| What runs | A JavaScript re-implementation from the shipped numbers | The real vessel: the game's own code, physics, abilities, AI |
| Phone today | Yes: open the page in the phone browser | After the first Prisma phone build (§5, phase 2) |
| Fits | A vessel still being designed: **the Stoat** | A vessel already built: **the Squirrel** |

Rule: a design studio's decision lands in the game; the test studio then tests what landed. **No web
studio for the Squirrel**: it would re-implement a vessel that already exists and drift from it.

## 3. The test studio (game side) — architecture

### 3.1 One scene, one profile per vessel

- **Scene** `Assets/_Scenes/Studios/VesselStudio.unity`: one open cell with a short test course (rings or
  a waypoint track from the existing course generators), crystals to collect, and a spawn point. Boots
  straight into it (no login, no menu), single player, offline.
- **`VesselStudioCatalogSO`** (one asset in `Resources/`): the list of profiles the hub shows.
- **`VesselStudioProfileSO`**, one per vessel: the vessel class, its course, its **play-style types**, its
  AI options, the readouts and tools it shows. **Adding a vessel = adding one profile asset.** Opening a
  studio = loading the scene with that profile.

### 3.2 Play-style types are element levels (the game's own model)

Both vessels already tie their styles to the four elements: the Squirrel's element rows each own one
ability (§1), and the Stoat's play-styles plan maps Time → Comet, Space → Anchor, Mass → Maelstrom,
Charge → Flare (`STOAT_PLAY_STYLES.md` §4). So a **type is a named set of four element levels**, applied
through the same element-level API the game uses — never by editing a shared ScriptableObject at
runtime (one config asset drives every hull of a class).

Starting types for the Squirrel (names to confirm):

| Type | Charge | Mass | Space | Time | Feels like |
|---|---|---|---|---|---|
| Shipped | 0 | 0 | 0 | 0 | today's start state |
| Thief | 5 | 0 | 5 | 0 | long-reach steals, big petal grabs |
| Speedster | 0 | 5 | 0 | 5 | fast Boost Ring cycle, double skim energy |
| Skimmer | 0 | 0 | 2 | 5 | lives on the trail |
| Brawler | 5 | 3 | 2 | 0 | overtakes and steals |
| Maxed | 5 | 5 | 5 | 5 | every L5 upgrade on (Shepherd, Twin Rings, Iron Grip, Live Wire) |

Plus a **drift style** switch the Squirrel already supports: feathered (half pull) vs buried (full pull),
so a touch player can feel both.

### 3.3 The studio panel (touch first)

Runtime uGUI, a collapsible drawer, big targets, readable on a 6-inch screen:

1. **Vessel** — the hub's list (Squirrel, Stoat, more as profiles are added).
2. **Type** — the play-style buttons above, plus four element sliders (0–5) for a custom set.
3. **Abilities** — the vessel's four-icon ability row, live (cooldowns, holds, L5 on/off).
4. **AI** — off, or 1–3 opponents at Easy / Medium / Hard (the Squirrel's Skim Race pilot).
5. **Readouts** — speed, drift amount and grip, boost, skim energy, element levels; FPS and frame time.
6. **Session** — reset to start, slow motion (0.25× / 0.5× / 1×), freeze ecology (`freeze on`), restart course.

The same verbs are dev-console commands, so Prisma's MCP and scripted tests drive them too:
`studio vessel squirrel`, `studio type speedster`, `studio element mass 5`, `studio ai hard 2`,
`studio slowmo 0.5`, `studio reset`.

### 3.4 The vessel tools inside the studio

The editor audits check the prefab. In the studio, each gets a **runtime view** of the same thing on the
flying vessel:

| Editor tool | Studio view |
|---|---|
| Audit Vessel Ability Rows | the live ability row, with a warning chip if a slot is unbound |
| Audit Vessel Skimmers / Corridor Radii | a toggleable gizmo of the skimmer sphere (Space scales it 15 → 30) |
| Audit Vessel Elemental Morphs | the hull morph following the element sliders |
| Audit Vessel Tails and Jets | the jets and trail visible from a chase or side camera |
| Validate Vessel Vision Band / Speed Tunnel Law | the vision band and speed tunnel drawn at the current speed |
| `diag` / `fps` instruments | a one-tap "record 30 s" that writes the `diag` report |

### 3.5 Rules the studio keeps

- Every number comes from the game's assets; types are data in the profile SO, never code.
- No runtime write to a shared ScriptableObject (per-vessel runtime state only).
- AI stays input-only; the studio only chooses how many and which difficulty.
- Logs on a `CSLogChannel` (a new `Studio` channel), nothing per frame.
- Editor-only code stays out of the studio (it must run in a phone build).

## 4. Prisma's part

| Where | What | Who builds it |
|---|---|---|
| Desktop | A **STUDIOS** page in the launcher: the catalog's vessels as cards; Play opens the player with `--scene VesselStudio --studio-vessel Squirrel` | engine session (`Port/`) |
| MCP | `studio_open vessel=Squirrel type=Speedster`, so an agent can fly a scripted lap and screenshot it | engine session |
| Phone build | A `cs-build` **studio profile**: an APK/`.ipa` whose first scene is `VesselStudio` (smaller, quicker boot) | engine session |
| **Cloud Android build** | A `prisma-android.yml` workflow, like `prisma-ios.yml`: push a request file or press Run → GitHub builds the studio APK → download it on the phone and install. **This is what makes "test from the phone, no PC" true on Android.** iOS still needs a PC once a week (Sideloadly, 7-day signature) or the $99/yr TestFlight. | engine session |
| First device run | Closes C8 item 2 ("Android device pass") with the studio as the test content | the user, with the APK |

What "Prisma on the phone" means here: the **Prisma player** with the studio, not the Prisma launcher. The
launcher (Dear ImGui: git, agents, editor pages) is a desktop tool; the phone runs the game.

## 5. Phases (each ends with a check that proves it)

| Phase | Work | Done when | Who |
|---|---|---|---|
| 0 | Phone-test the Stoat **design** studio now: open its artifact link in the phone browser, tap **Play on phone** | the user flies it sideways on the phone and logs decisions | user |
| 1 | **Squirrel test studio** in the game: scene, catalog + Squirrel profile, studio panel, the six types, AI toggle, readouts, console verbs | in the Unity editor, picking Speedster sets Mass 5 / Time 5 and the Boost Ring cooldown halves; an AI Squirrel races the course on Hard | Unity session (+ user check in editor) |
| 1b | Same studio running in **Prisma desktop** | `CosmicShore.Player --scene VesselStudio` shows the panel; a scripted lap and screenshot via MCP | engine session |
| 2 | **Phone**: studio build profile + cloud Android build | the APK installs from a phone browser and the Squirrel flies with touch | engine session, then user |
| 3 | **Stoat profile**, once the Stoat's chosen sling design is in the game (`Docs/Studios/STOAT_PLAY_STYLES.md` §4) — its five styles as types | the hub shows both; the Stoat flies its Slingshot course with LT/RT handles | Unity session |
| 4 | Hub and launcher STUDIOS page; more vessels as profiles | adding a vessel is one profile asset and a catalog entry | both |

## 6. Decisions for the user

1. **Which phone first:** Android (cloud APK, no PC at all) or iPhone (needs a PC weekly, or TestFlight at
   $99/yr)?
2. **Squirrel's types:** keep the six above, or rename / change them?
3. **Phase 1 in Unity or straight in Prisma:** the studio is game content either way; building it in a
   Unity session and checking it in the Unity editor first is the safer order.
4. **Where it lives:** this plan sits on `vessel-studio` (cut from `claude/peaceful-rubin-hhw49n`). The
   Squirrel studio needs nothing from the Stoat branch, so phase 1 could also start from `bleeding-edge`
   and meet the Stoat in phase 3.
