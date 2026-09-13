# Prompt — re-point the player settings at PC

Paste everything below into a fresh session.

---

Three Player Settings values were authored for a mobile-first project and never re-pointed at the
Windows target. They are individually trivial and collectively the first impression a Playtest
tester gets: the game opens in a small, non-resizable window under a bundle identifier naming a
product that no longer exists.

This is the code half of checklist item **B2** (PC platform sanity pass, 3.0 person-days). The
manual half — pads, alt-tab, focus, quit path, no mobile-only prompts — is a human pass and is
tracked separately as R1/R3 on `Docs/STEAM_RELEASE_TASKS.md`.

Read `Docs/BUILD_AND_DELIVERY.md` §2 and §6 first.

## Measured 10 Sep 2026 from `ProjectSettings/ProjectSettings.asset` — re-verify before acting

| Key | Current | Should be | Why |
|---|---|---|---|
| `defaultScreenWidth` | **1024** | 1920 | Mobile-era default. The game opens in a 1024×768 window on a 1080p monitor. |
| `defaultScreenHeight` | **768** | 1080 | Also the wrong **aspect** — 4:3 on a 16:9 project whose canvases are authored at 1920×1080. |
| `resizableWindow` | **0** | 1 | A PC player expects to resize. Steam's own window management assumes it. |
| `Standalone` bundle id | **`com.Froglet-Games.Tail-Glider`** | a Cosmic Shore identifier | Names the project's previous title. Visible in crash reports, save paths and Cloud Diagnostics grouping. |

**Already correct — do not "fix" these:**

- `fullscreenMode: 1` (fullscreen window) — correct for PC, and what `CosmicShoreBuildPipeline`
  sets deliberately.
- `runInBackground: 1`, `visibleInBackground: 1`, `allowFullscreenSwitch: 1` — correct.
- Graphics APIs for `WindowsStandaloneSupport` are `[Direct3D12, Direct3D11]` with
  `m_Automatic: 0`. **This is load-bearing and must not change.** It is the fix for the Intel Iris
  Xe crash recorded in `Docs/PRISM_ECS_MIGRATION.md` §8: a D3D12-only list made Unity's device
  filter refuse the integrated GPU and fall back to D3D11 with no D3D11 shaders present, so every
  Entities Graphics compute kernel failed to load and the first frame died natively. D3D11 must
  stay in the list, below D3D12.
- `scriptingBackend: Standalone: 1` (IL2CPP) — correct, and what CI drives.

## The one that needs care

**The bundle identifier change is not free.** On Windows it participates in the persistent data
path (`%USERPROFILE%/AppData/LocalLow/<company>/<product>`), which is where `LocalCloudDataCache`
writes its offline snapshot and where the benchmark tool writes its reports. Changing the company
or product name moves that folder.

For this project the risk is low — offline cache is a *last-known-good* snapshot that rebuilds
from Cloud Save on the next sign-in, and benchmark history is local scratch. But check
`LocalCloudDataCache` and `Assets/_Scripts/Utility/PerformanceBenchmark/` for anything that treats
a missing folder as an error rather than as a cold start, and say in the PR what moves.

`companyName` is already `Froglet Games` and `productName` is already `Cosmic Shore`; it is only
the Standalone *identifier* that is stale.

## Constraints

- **Do not raise `managedStrippingLevel`.** There is no `Standalone` entry today, which is
  deliberate and documented: Reflex DI, Netcode for GameObjects and Newtonsoft all resolve types by
  reflection, and raising it silently removes types they need — a failure that only appears at
  runtime in the player. It needs a `link.xml` first, and that is not this task.
- Edit `ProjectSettings.asset` directly; these keys have no editor-only wiring. Keep the diff to
  the four values.
- `CosmicShoreBuildPipeline.cs` also sets desktop window behaviour at build time. Check whether it
  already overrides any of these — if it does, the settings asset and the pipeline must agree, and
  the pipeline is the one CI runs.

## Definition of done

1. The four values above are corrected, the diff touches nothing else, and the graphics API list
   is byte-identical to before.
2. `python3 Tools/Build/check_conditional_compilation.py` passes (no C# changed, but it is cheap
   and this branch touches build configuration).
3. The PR body records what the persistent-data-path move affects, or states that nothing reads it
   in a way that breaks.
4. `Docs/STEAM_RELEASE_TASKS.md` R5 is ticked.
5. The *Verification status* section says whether a player build was produced — if it was not, say
   so, because R1 is the item that proves this landed.
