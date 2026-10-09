# `claude/peaceful-rubin-hhw49n`: start here

The handoff for anyone picking this branch up: what is on it, where each piece is documented, what is
decided, what is open, and how to check it. Written 2026-10-09, after merging the latest
**bleeding-edge** and **Ys-bleeding-edge** (neither has a commit this branch lacks).

**In one paragraph.** This branch builds the **Stoat** (vessel 14), whose triggers lay an
attractor–repulsor (black hole–white hole) pair to sling round. Around it:

- **The black-hole system:** physics, prism mover, tides, lens, white holes, pairs, a console and a tool.
- **The Slingshot arcade mode** (`GameModes.Slingshot = 64`).
- **The crystal-wormhole style**, merged from `cece/charming-cerf-alf1j1`.
- **A browser design studio** that went through 14 rounds: dipole sling, play styles, AI sim lab, course
  ladder, editor layout.
- **The Vessel Studio hub** (Squirrel + Stoat), merged from `vessel-studio`.
- **Amoebius support:** the game on this branch now compiles and runs in Amoebius, and Amoebius's STUDIOS page
  opens the studio and plays the Stoat in the engine.

**The game still ships the round-4 orbit sling.** The studio's newer dipole sling, momentum and play
styles are designs that have not been ported yet.

---

## 1. Where everything is

| Area | Read | Code / assets |
|---|---|---|
| **Black holes** (physics, prism mover, tides, lens, white holes, pairs, console, tool) | `Docs/BLACK_HOLE.md`, §0.1 "Where it stands" first | `Assets/_Scripts/Controller/Environment/BlackHole/`, `Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl`, `BlackHoleConfigSO`, test scene `Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity` |
| **Crystal wormhole** (the other pair style, from charming-cerf) | `Docs/CRYSTAL_WORMHOLE.md` (§5.1 is this branch's addition) | `Controller/Environment/CrystalWormhole/`, `Controller/Environment/WarpField/` |
| **The Stoat vessel** (prefab, sling executor, hull, lope) | `Assets/_Scripts/Controller/Vessel/R_VesselActions/STOAT.md` | `Stoat.prefab`, `StoatSlingExecutor`, `StoatSlingMath`, `StoatSlingConfigSO` (+ `_SO_Assets/VesselActions/Stoat/`), `StoatHullForm` / `StoatHullBuilder`, `StoatLopeMath` / `StoatAnimation`, generator `Tools/Build/author_stoat_assets.py` |
| **Slingshot mode** | `Assets/_Scripts/Controller/Arcade/SLINGSHOT.md` | `Controller/Arcade/Slingshot/`, scene `MinigameSlingshot.unity`, card `ArcadeGameSlingshot.asset` |
| **The design studio** (rounds 4–14) | `Docs/Studios/README.md` (one section per round, newest first) | `Docs/Studios/StoatFlightStudio.html` (the source) |
| **Play styles** (Comet, Needle, Anchor, Maelstrom, Flare) | `Docs/Studios/STOAT_PLAY_STYLES.md` | the studio's `STYLES` table |
| **AI sim lab: results, max speeds, game-mode idea, studio vs Amoebius, AI port plan** | `Docs/Studios/STOAT_SIM_LAB_PLAN.md` | the studio's Sim lab tab |
| **Vessel Studio hub** (Squirrel v1 + Stoat) | `Docs/Studios/VESSEL_STUDIO_PLAN.md`, `Docs/Studios/VesselStudio/README.md` | `Docs/Studios/VesselStudio/` (`studios.json` is the catalog every surface reads) |
| **Amoebius: STUDIOS page, OPEN IN AMOEBIUS, PLAY IN ENGINE, the engine gaps filled** | `Docs/Studios/PRISMA_TEST_STEPS.md`, `Port/docs/LAUNCHER.md` § STUDIOS | `Port/src/CosmicShore.Launcher/LauncherApp.Studios.cs`, `StudioCatalog.cs`, `Port/src/CosmicShore.Player/ArcadeAutoStart.cs`, `Port/src/CosmicShore.Engine/Jobs/`, `Rendering/RenderGraph.cs` |
| **Amoebius prompt** (Stoat + both pair styles in the engine) | `Docs/Studios/PRISMA_WORMHOLE_SESSION_PROMPT.md` | — |
| **Starting, publishing or syncing a studio** (settled decisions D1-D15, build order, Sync panel) | the `/vessel-studio` skill, `Docs/Studios/VesselStudio/SYNC_PANEL.md` | `.claude/skills/vessel-studio/build_artifact.py`, `Docs/Studios/VesselStudio/sync.js` |
| **What the labs taught** (reusable across labs) | `.claude/skills/labmaker/LEARNINGS.md` § STU (L-STU-1…18), `CATALOG.md` | the `/labmaker` skill |
| **Editor checks still owed** | `Docs/UNITY_VERIFICATION_CHECKLIST.md`: the Slingshot and Stoat entries (🔴), and the black-hole entries `BLACK_HOLE.md` §0.1 lists | — |

**Live pages:**

- **Vessel Studio (the one artifact):** https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa. The hub, the Squirrel AI sim lab, the Stoat
  Flight Studio (round 15), the studio agent and the Sync panel. Decisions: `decisions`; development requests:
  `requests`; Sync jobs: `jobs`. It is **private** until shared from its Share menu. Every earlier studio
  artifact is retired (`/vessel-studio` §0).

## 2. Decided (with the designer)

| Decision | Where recorded |
|---|---|
| The dipole sling: squeeze depth = strength (mostly mass), hold time = rotation, release = slingshot; static holes; one pair at a time | `Docs/Studios/README.md` rounds 5–8 |
| Five play styles: Time→Comet, Space→Anchor, Mass→Maelstrom, Charge→Flare; Needle earned by clean releases; weights rise and drift back to 0.5; sling and holes only | `STOAT_PLAY_STYLES.md` (top) |
| Momentum carry (`dpCarry` 0.4, `dpFlowFade` 5 s, `dpFlowCap` 1.5 × cruise) | `STOAT_SIM_LAB_PLAN.md` §2 |
| Each style wins one column of the scorecard (Comet avg speed, Flare top speed, Needle ring error, Anchor rookie catch rate, Maelstrom prisms per sling) | `STOAT_PLAY_STYLES.md`, `STOAT_SIM_LAB_PLAN.md` §2 |
| Prototype in the studio until the numbers settle, then port once; per-vessel studios on a shared shell | `STOAT_SIM_LAB_PLAN.md` §4 |
| The studio lives in Amoebius; web first, then Windows and Android | `VESSEL_STUDIO_PLAN.md` |

## 3. Open: decisions for the designer

1. **The tap sling.** The fastest play is caught-and-release at 0°, because the kick ignores how far round
   you went. `dpKickSweep` (off by default) makes the kick earned, but then slinging loses to not
   slinging. Choose: keep the tap, or make the swing earn the kick and retune.
   - Measured: `STOAT_SIM_LAB_PLAN.md` §2 "A design hole: the tap sling".
2. **The four-abilities contract.** The fleet rule is four abilities, one per element. The plan makes all
   four elements lean the one sling. Either those are the sling's four facets (four icons, four weights),
   or Hold Still keeps Time.
   - Recorded: `STOAT_PLAY_STYLES.md` §4. Blocks any port of the play styles.
3. **The game mode.** The proposal is one race where each style can win somewhere: speed traps, needle
   gates, prism fields, a broken-sling penalty. Five separate modes is the alternative.
   - Recorded: `STOAT_SIM_LAB_PLAN.md` §3. The next studio round would add these sections to the stand-in
     course.
4. **Porting the dipole sling into the game.** After 1–2. Port plan: `STOAT_SIM_LAB_PLAN.md` §5, which
   follows the ai-system branch's rules (input-only pilot, personas as config, benchmark plus
   cross-entropy tuning).

## 4. Known issues (not fixed, on purpose)

| Issue | Where it shows | Note |
|---|---|---|
| Studio layout is 912 px tall in a 900 px window: the bottom dock's last ~12 px are clipped | `StoatFlightStudio.html` at 1600 × 900 | Found by `verify_lab.cjs` (L-STU-13) |
| The round-13 platform chip pushes the header past the edge at 390–400 px wide (portrait phone) | the page header | L-STU-13; landscape fits |
| The studio is not on the `/labmaker` `__lab` contract (`SHIPPED`, `SPEC`, `reset`, `score`, `state`, `runBatch()` with no args) | `window.__stoatStudio` | L-STU-14 |
| The black hole's lens is not drawn in Amoebius | Amoebius only | The engine compiles URP's render-graph API but does not execute passes yet (`Port/src/CosmicShore.Engine/Rendering/RenderGraph.cs`) |
| `CrystalFlipWave.LateUpdate` throws a NullReferenceException every frame in Amoebius | menu and races | Pre-existing: the file is identical on bleeding-edge |
| None of the game-side Stoat / black-hole / Slingshot work has been opened in the Unity editor | — | No `/verify-unity` was available in these sessions. The editor steps are in `UNITY_VERIFICATION_CHECKLIST.md` |
| `Stoat.prefab` shows the Squirrel's HUD icons | ability row | Prototype debt, `STOAT.md` §4 |

## 5. How to check it

**Studio (no install):** open the live page, or `Docs/Studios/StoatFlightStudio.html` in Chrome (where a
gamepad works).

- Course tab ▸ intensities 1–4.
- Sim lab ▸ **AI flies the Stoat**.
- **Score all five styles**: each style should win its own column.

**Amoebius:** `Docs/Studios/PRISMA_TEST_STEPS.md`, step by step.

- In short: Unity ▸ **FrogletTools ▸ Vessels ▸ Vessel Studio** ▸ STOAT ▸ **OPEN IN AMOEBIUS** (the studio)
  or **PLAY IN ENGINE** (the game's own Stoat in Slingshot).

**Headless, from a cloud session** (what the previous sessions ran):

```bash
# .NET 10 SDK: bash dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet ; export DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH
dotnet test Port/tests/CosmicShore.Tests            # engine, ~2 min (1,913 passed on 2026-10-09, after merging both bleeding-edges)
dotnet test Port/tests/CosmicShore.Launcher.Tests   # 30 passed (after the merge)
dotnet test Port/tests/CosmicShore.Tests.Ported     # the game's edit-mode tests on Amoebius (352 passed after the merge; launcher 30/30; player 0 errors)
dotnet build Port/src/CosmicShore.Player            # live-compiles Assets/_Scripts (0 errors after the merge)
COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off COSMIC_SHORE_PROFILE=studiotest \
  Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore --headless --frames 2400 --arcade Slingshot
node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/StoatFlightStudio.html   # the lab verifier
```

A first-time profile stops at the birth-year, consent and username prompts; a reused profile skips them.
The studio's own headless checks (batch, recorder, platform and layout probes) were run with Playwright
against `window.__stoatStudio`. Its hooks:

- `runBatch(qs, {presets, seeds})`, `batchSummary`;
- `tick` / `sim` with `manualClock` for frame-exact recording;
- `intensity`, `courseKind`, `PLATFORM`, `openWindow`.

## 6. Branch history in one table

| When | What |
|---|---|
| 2026-10-07 – 08 | Black-hole system; white holes and pairs; tool and console; crystal-wormhole merge; the Stoat prefab, sling, hull and lope; Slingshot mode |
| 2026-10-08 – 09 | Studio rounds 4–11: orbit sling → dipole sling, strength/hold, hole settings + illustrated tooltips, birth/annihilation, five play styles, types per group, phone play |
| 2026-10-09 | Rounds 12–14: AI sim lab and scorecard, momentum carry, platform detection, course ladder, editor layout with pop-out windows |
| 2026-10-09 | `vessel-studio` merged; the game made to compile in Amoebius; OPEN IN AMOEBIUS and PLAY IN ENGINE |
| 2026-10-09 | `Ys-bleeding-edge` (247 commits) and `bleeding-edge` (7) merged; this handoff |
| 2026-10-09 | `/vessel-studio` skill and the Sync panel (Refresh, merge then delete, shared decisions; a Claude session does the git work as jobs) |
| 2026-10-09 | `cece/magical-carson-9bdq8z` merged: Stoat studio round 15 (the field trajectory), its Unity port (`R_VesselActions/STOAT_DIPOLE.md`: field dipole on Space, pathfinder on Time) and the **Warpline** mode (`GameModes.Warpline = 65`, `Arcade/WARPLINE.md`). Their lab lessons are `L-STU-20`…`24` (renumbered from 15–19, which this branch had already used) |

Commit messages carry the detail: `git log --oneline origin/bleeding-edge..HEAD`.

## 7. Rules that bit here (read before changing things)

- **Studios are readers of the shipped numbers.** Every constant names its asset. Change the asset, then
  the page. (`VesselStudio/README.md`)
- **`StoatFlightStudio.html` is the source; `VesselStudio/stoat.html` is a copy** with a back link.
  Re-copy every round, or the hub drifts (L-STU-12).
- **Amoebius's port rule:** `Port/` changes never touch the Unity project (`Port/CLAUDE.md`). This branch
  also carries Unity work, so `check_unity_isolation.py` lists that work. Send the `Port/` commits to
  bleeding-edge as their own PR when the time comes.
- **The AI is input-only** (stick mix + triggers), in the studio and in any game port
  (`check_ai_no_state_writes.py`).
- **A vessel's element→ability mapping is a design gate.** Never fill an open slot to green an audit
  (`.claude/skills/vessel`).
