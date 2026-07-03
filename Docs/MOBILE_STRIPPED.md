# Android Stripped-Performance Branch

Branch: `claude/android-performance-stripped-dap5z2`

**One objective:** hold **60 fps on a mid-tier, years-old Android device** while flying the
**Squirrel** (with skimming + controls intact) through the **Wanderway conveyor toy** in freestyle.
Everything not load-bearing for that experience is stripped. This is a deliberately throwaway
performance branch — "we can do no harm."

## How to build the APK

A self-contained Editor build script ships on this branch:
`Assets/Editor/BuildAndroid.cs` (`CosmicShore.Editor.BuildAndroid`).

- **From the Editor:** `FrogletTools ▸ Build ▸ Android APK (Development)` — writes
  `Builds/Android/CosmicShore.apk`. The Development build uses Unity's debug keystore, so **no
  signing setup is required**.
- **From the command line / CI:**
  ```
  Unity -batchmode -nographics -projectPath . \
        -executeMethod CosmicShore.Editor.BuildAndroid.Build -quit
  ```
  Optional: `-outputPath <path.apk>`, `-release` (production flags; needs a real keystore).

The script forces the Android target, builds an **APK** (not AAB), and builds only the enabled
scenes (the 3 boot scenes below).

## How to reach the conveyor on device

1. App boots: **Bootstrap → Authentication → Menu_Main**. The autopilot Squirrel drifts in the
   "lava lamp."
2. **Enter freestyle** (take control of the Squirrel) — tap the crystal / freestyle affordance
   (`MenuCrystalClickHandler`). The gamepad **Start** button also toggles freestyle.
3. **Fly the Squirrel into the "Wanderway" toy** station (a world-space sphere near the play
   area). The belt of little worlds starts streaming ahead of you. Fly through it again to stop;
   again to resume. **Skim** the streamed prisms and crystals as you fly.

The conveyor is inert while on autopilot (menu) — you must be in freestyle for it to trigger.

## What was stripped / changed

Everything is gated behind one kill-switch: **`Assets/_Scripts/Utility/PerfStrip.cs`**
(`PerfStrip.Enabled = true`). Flip it to `false` to restore full behaviour — every guard becomes a
no-op. Nothing was deleted; each heavy system early-returns when the strip is on.

| # | Change | Where | Win |
|---|--------|-------|-----|
| 1 | **Vessel trail killed** — vessels lay no continuous prism trail | `VesselPrismController.StartSpawn()` early-returns on `PerfStrip.TrailsDisabled`. Single chokepoint; every re-arm caller (skimmer cooldown, drift/stationary toggles) becomes a no-op. | Biggest per-frame CPU/collider/GC win. Skimming is unaffected — it skims *other* prisms (the conveyor's), not the vessel's own trail. |
| 2 | **URP graphics cut** | `Assets/_Graphics/URP_Asset.asset`: `SupportsHDR 1→0`, `MSAA 4→1` (off), `RenderScale 1→0.8` | Kills the HDR buffer bandwidth, the MSAA resolve, and ~36% of the fragment/fill-rate cost. |
| 3 | **ARM64-only** | `ProjectSettings/ProjectSettings.asset`: `AndroidTargetArchitectures 3→2` | Drops the ARMv7 slice (any "years-old mid device" is ARM64); smaller APK, IL2CPP ARM64 is faster. |
| 4 | **Build trimmed to the boot path** | `EditorBuildSettings.asset`: only `Bootstrap`, `Authentication`, `Menu_Main` enabled (10 minigame scenes disabled) | Smaller/faster build, fewer break points. |
| 5 | **Toybox is conveyor-only** | `ToyboxController.PlaceToys()` filters to `ConveyorToyDefinitionSO` on `PerfStrip.ConveyorOnlyToybox` | Drops the other 3 toys' idle cost — notably the vessel-changer's 6 mini-ship preview models. Squirrel stays the only vessel. |
| 6 | **Conveyor mass cut ~71%** | `Assets/_SO_Assets/Toys/Toy_Conveyor.asset`: `poolSize 7→5`, `prismBudgetPerScene 100→40`, `aheadTargetScenes 5→3` | Max resident conveyor prisms **700 → 200** (each is a GameObject + BoxCollider + ~5 MonoBehaviours). This is the dominant per-frame content cost. |
| 7 | **Social networking overhead off** | `HostConnectionService.Update` (1.5s UGS presence refresh) and `FriendsInitializer.HandleSignedInEvent` early-return on `PerfStrip.DisableSocialNetworking` | Removes the recurring UGS-read + main-thread-marshal GC/hitch and the Friends init. **The Relay host that spawns the Squirrel is untouched.** |

Notes on the fundamentals (see `CLAUDE.md`): the trail kill disables prism **creation** at the
source — it never ages out or culls existing mass. "Not creating mass is allowed; aging it out is
the cheat." The conveyor's own conserved-mass stock is untouched. There is **no Cell ecosystem in
Menu_Main**, so the conveyor's flora/fauna recipes never fire there — it carries prisms + crystals
only, which is already the cheaper path.

## Tuning dials (if 60 fps isn't held, cut here first)

1. **Conveyor budget** — `Toy_Conveyor.asset`: lower `prismBudgetPerScene` (≥6) and/or `poolSize`
   (≥2). Resident prisms = `poolSize × prismBudgetPerScene`. This is the #1 dial.
2. **Render scale** — `URP_Asset.asset` `m_RenderScale`. 0.8 is aggressive; drop to 0.7 for more
   headroom, raise toward 1.0 if you have room and want crisper prism edges.
3. **Frame cap** — `Application.targetFrameRate` is set to 60 by `AppManager.ConfigurePlatform`
   (from `BootstrapConfigSO.TargetFrameRate`, default 60). Keep it **capped** (a `<=0` uncapped
   value thermally throttles a phone → *worse* sustained fps).

## Deliberately deferred (bigger wins, but real code / scene surgery — do with Unity in hand)

These were left out because they can't be verified without the Editor and a wrong move breaks the
boot/spawn path. In rough priority:

1. **Disable the 4 non-Home menu screens** (Store / Ark / Port / Hangar) in `Menu_Main.unity` —
   the UI canvas is ~1,617 MonoBehaviours under one root and is the biggest scene-load/memory cost.
   Set their roots inactive **in the scene** (serialized `m_IsActive: 0`) and verify `ScreenSwitcher`
   doesn't hard-index a disabled entry. Highest static/memory win.
2. **Local host instead of UGS Relay** — today the NetworkManager host *is* a Relay party session
   (`HostConnectionService.EnsurePartySessionAsync`), so the boot flow waits on a live Relay session
   (`AuthenticationSceneController.WaitForRelayReadyAsync`). For a solo offline build, replace that
   with a plain `NetworkManager.StartHost()` behind an offline flag. This is **new code, not a
   guard** — get it wrong and the Squirrel never spawns. Removes network startup latency + a failure
   surface (the build currently expects network at boot).
3. **Post-processing** — with HDR off the bloom pass is already cheaper (LDR); if still tight,
   lighten/disable the freestyle post Volume (costs some of the vaporwave look — conveyor spirit).
4. **Squirrel cosmetics** — `NudgeShardPoolManager` (skimmer tube-marker FX) and the Squirrel HUD /
   element bars are safe heavy cuts (both faded/idle in freestyle) but need prefab edits.
