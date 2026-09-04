# FMOD audit — leaks, crashes, silent sliders (2026-09-02)

Companion: `CHARLES_TASKS.md` (everything that must be done in FMOD Studio or in the FMOD
inspector by the audio owner). This file is the engineering record: what was found, what was
changed in code/assets, what is deliberately left alone, and how to verify it in the editor.

The ask was five things: random FMOD errors in the console, FMOD-attributed multiplayer / Unity
crashes, in-game sound "crashing", volume sliders not saving, and a general improvement sweep.
Every finding below is tagged with which of those it feeds.

---

## 1. Findings (root causes, in the order they were confirmed)

### 1.0 THE DOMINANT CAUSE: the audio sliders are FIELD-OF-VIEW sliders, and binding one SAVES full volume (sliders)

Everything in §1.1–§1.3 is real and fixed, and **none of it could have stopped the reported
symptom**, which survived them: *"every time we start the game the slider is at the top like it's at
full volume."*

Music, SFX **and** Haptics in `OptionsMenuContent.prefab` shipped as copies of the field-of-view
slider. All three were authored `m_MinValue: 60`, `m_MaxValue: 90`, `m_WholeNumbers: 1`,
`m_Value: 71` — the FOV row's own settings (`GameSettingsPanelController.fovMin/fovMax`). The real
FOV slider beside them is the only one of the four that was correct. No scene or prefab override
touched any of it, so that is what ran.

Unity's `Slider.minValue` / `maxValue` / `wholeNumbers` setters all end in
`Set(m_Value, sendCallback: true)`. So *narrowing* the window does two things: it clamps the value
the slider is carrying, and it **broadcasts the clamped result to every listener**, including the
persistent ones authored on the prefab — which code cannot conveniently detach. On these rows that
persistent listener is `AudioLevelSlider.SetVolume`, i.e. the thing that PERSISTS the setting.

Binding the panel therefore ran:

| step | effect |
|---|---|
| `minValue = 0` (from 60) | clamp 71 into [0, 90] → 71, unchanged, silent |
| `maxValue = 1` (from 90) | clamp 71 into [0, 1] → **1**, changed → **`onValueChanged(1)` fires** |
| → persistent listener | `AudioLevelSlider.SetVolume(1)` → `GameSetting.SetSFXLevel(1)` → **saved, stamped, synced to cloud** |
| `RefreshValues()` | reads the level back — now 1 — and seats the slider on it |

So opening the settings panel **destroyed the saved level and rewrote it to full volume**, then
displayed the value it had just destroyed. The player sets 0, quits, relaunches, opens settings, and
the act of opening restores full volume before they can see their own setting. Both halves of the
report — "at the top" and "not persistent" — are that one line.

This is why it outranks §1.1 and §1.2: those explain a slider that fails to *save*, and this one
explains a slider that actively *unsaves*. A correct persistence layer underneath simply persisted
the corruption faithfully, including to the cloud.

**Fix, both halves.** (1) The three audio rows are re-authored `0..1`, non-whole-number, default 1
(the FOV row keeps `60..90` and moves its authored default onto the shipped 90 — see §2). (2) No
code assigns a slider's range directly any more: `SliderRange.ApplyWithoutNotify` widens the window
to cover both the carried and the incoming value, moves the value silently, then narrows — every
assignment is a no-op clamp, so the callback cannot fire. Both `AudioLevelSlider.OnEnable` and
`GameSettingsPanelController.BindSlider` go through it, and `BindSlider` now takes the saved value
so range and value land in one silent step. `SliderRangeTests` locks it from both ends, including a
negative control that reproduces the naive assignment still firing.

**The general trap, worth carrying past audio:** *narrowing a `Slider`'s range is a WRITE, not a
display change* — and when a persistent inspector listener is what saves the setting, re-ranging a
mis-authored control silently overwrites player data. Any bound control whose prefab carries a
persistent listener has this shape.

### 1.1 The volume sliders drove a VCA that controls nothing — and were never saved (sliders)

Three prefabs carried a `Mixer` component on their slider: `OptionsMenuContent` (inside
`SettingsModal`, the live main-menu + GameCanvas pause settings) and the legacy `Music Toggle` /
`SFX Toggle` (inside `Options_Menu_Panel`, the freestyle pause menu in Menu_Main). `Mixer.SetVolume`
wrote the slider straight to `RuntimeManager.GetVCA("vca:/Music" | "vca:/SFX")`.

Two facts make that a dead end. The FMOD project (`Cosmic Shore/Metadata/VCA/*.xml`) does define
both VCAs, but **neither is assigned to any bus** (`Metadata/Group/*.xml` — the `Music` and `SFX`
group buses carry no `vcas` relationship), so the write was inaudible. And nothing persisted the
value or restored the slider on the next launch, so in the freestyle pause menu the slider was
purely cosmetic and always reset. The `SFX Toggle` prefab's slider was not wired to *anything*.

In `SettingsModal` the same slider was additionally code-bound by `GameSettingsPanelController`
to `GameSetting.SetSFXLevel`, so that UI did persist — which is why the report was intermittent
and depended on which settings screen the player used.

### 1.2 Cloud settings stomped local settings unconditionally (sliders)

`GameSetting.ApplyCloudSettings` applied the UGS `PLAYER_SETTINGS` snapshot over PlayerPrefs
whenever cloud data became ready, with no notion of which copy was newer. A slider dragged to 0
followed by a quit inside the repository's 1.5 s save debounce, a save that never reached UGS, or
a fresh account (whose "cloud data" is a `new PlayerSettingsCloudData()` — every level 1.0) all
came back on the next launch at the cloud value. That is exactly "I set it to 0 and it is reset
on restart".

### 1.3 Fresh installs booted with the levels at 0 (sliders, "sound crashes")

`GameSetting.Awake` seeded the level defaults with `PlayerPrefs.SetInt(...)` and read them with
`PlayerPrefs.GetFloat(...)`. A type mismatch reads as the default (0), so every fresh install
started with `MusicLevel = SFXLevel = 0` — silent — until the cloud stomp of 1.2 "rescued" it
with 1.0. Offline (no cloud) a fresh install stayed silent.

### 1.4 The music was a leaked, un-mutable one-shot (errors, improvement)

Bootstrap's `AudioSystem` prefab instance carried a scene-added `AudioManager` component that
called `RuntimeManager.PlayOneShot(event:/Music/Music)` in `Start`. `Music` has a loop region.
`PlayOneShot` is `create → start → release`; a released looping instance is never freed and can
never be reached again, so the music ignored the Music slider and the Music toggle for the whole
session, and every re-entry of the component would have stacked another. (The Unity `Jukebox`
on the same prefab is already removed at scene level — `m_RemovedComponents` in Bootstrap — so
FMOD music was the only music path; this was not double music, just an unowned one.)

### 1.5 Unguarded FMOD calls that throw, and teardown that resurrects FMOD (errors, crashes)

`RuntimeManager.CreateInstance` throws `EventNotFoundException` for a GUID no loaded bank knows
and `SystemNotInitializedException` when FMOD failed to start (no audio device). Four owned-
instance controllers (`ShipAudioController`, `DriftAudioController`,
`ProximityBoostAudioController`, `FloraAmbientAudioController`) called it bare; the engine
controller retries creation every frame until it succeeds, so one stale reference would have been
one exception **per frame** for the life of the vessel.

On the way down, the same controllers called `RuntimeManager.DetachInstanceFromGameObject` from
`OnDestroy`. That goes through `RuntimeManager.Instance`, whose getter **creates a new
RuntimeManager and re-initialises FMOD** whenever the old one is gone — which is the state during
application quit if the manager's `OnDestroy` ran first. FMOD's own `StudioEventEmitter` guards
this with an `isQuitting` flag; ours did not. Re-initialising a native audio system mid-quit is
the classic FMOD-on-exit hang/crash shape.

Also confirmed: the engine event and its layers were `start()`ed before being attached, so a 3D
instance played its first frame from the world origin and tripped FMOD's editor warning
("Instance of Event … has not had EventInstance.set3DAttributes() called").

### 1.6 Console noise that is authoring, not code (errors → Charles)

- `boostActivateEvent` is wired to `event:/SFX/Oneshots/Gameplay sfx/Boost Activate`, which
  **loops**. `FMODOneShotVolumeHelper` refuses it (by design, `PERFORMANCE_OPTIMIZATION.md §0.4`)
  and logs one `LogError` per session. The boost is silent until the loop region is removed.
- `driftStartEvent`, `driftEndEvent`, `creatureBlockHitEvent` on `AudioSystem` are unwired → one
  warning each, first time the category fires. Drift is covered by `DriftAudioController`;
  `CreatureBlockHit` is genuinely missing.
- `ProximityBoostAudioController` on the Squirrel has `boostLoopEvent` = the same one-shot as
  `boostTickEvent` (`…/Skim`): a one-shot used as a loop — no leak, but every skim plays twice.
- `CrystalTime.prefab` carries two `StudioEventEmitter`s for `Creature colide`, one on
  `TriggerEnter` (fires on every collider that enters) and one on `ObjectDestroy`.
- With no vessel spawned yet (boot, menu before the spawn chain) there is **no**
  `StudioListener` in the world, so FMOD logs "Please add an 'FMOD Studio Listener'" once per
  session. Cosmetic; listeners live on vessels by design (`ShipStudioListenerGate`).

### 1.7 Voice pressure (in-game "sound crashes", multiplayer)

Every fauna carries a looping 3D `StudioEventEmitter` (`Mass shark`, `Mass Tadpole`, …). Wildlife
Liberation seeds ~520 creatures and caps at ~1,200; the playInEditor platform allows 1,024 virtual
/ 256 real channels and the **default (build) platform allows FMOD's default 32 real channels**.
Every loop was instantiated regardless of distance (`StopEventsOutsideMaxDistance` was off), so a
full arena exceeded the virtual channel budget and FMOD started stealing/refusing voices —
audible as dropouts and "the sound broke". Editor and build also mixed differently (256 vs 32 real
channels).

### 1.8 What was checked and is fine

- Every `EventReference` GUID in prefabs/scenes/SO assets resolves to an event in the FMOD project
  (`Tools`: the audit script in this branch's history; 15 asset refs + 47 Bootstrap overrides, one
  zero-GUID = the unwired `driftStartEvent`). No stale references.
- All 12 vessel prefabs ship their `StudioListener` disabled; `ShipStudioListenerGate` enables
  exactly the local pilot's. No multi-listener mix pollution.
- Fauna are `Destroy`ed, not pooled, so their `ObjectDestroy`-stopped emitters do not leak.
- No first-party code raises FMOD from a UGS/Netcode `Task` continuation.

---

## 2. What changed (code + assets)

| Area | Change |
|---|---|
| `GameSetting` | Level defaults seeded as **floats**; one-time repair of the legacy int-typed key (an unstamped install whose level reads 0 is reset to 1 — no legacy UI could persist a 0). **Last-writer-wins** between PlayerPrefs and cloud via a UTC stamp (`PlayerPrefKeys.SettingsModifiedUtc` ↔ `PlayerSettingsCloudData.ModifiedUtcTicks`, pure rule `GameSetting.ShouldApplyCloud`); when local is newer it pushes to cloud instead. Setters are idempotent (a repeated slider value costs nothing). `PlayerPrefs.Save` is coalesced to once per frame and flushed on pause/quit, and the settings repository is flushed on pause/quit (its local snapshot is written synchronously before the network call). `[Inject]` null-guarded. |
| `PlayerSettingsCloudData` | `+ long ModifiedUtcTicks` (0 on legacy payloads — Newtonsoft default). |
| `CloudDataRepository<T>` | `+ HasPersistedData` — true only when the data came from the cloud, the local snapshot, or a save; false for the `new T()` a missing key falls back to. A default nobody wrote can no longer overwrite a chosen value. |
| `OptionsMenuContent.prefab` | Music / SFX / Haptics sliders re-authored from the field-of-view range (`60..90`, whole numbers, value 71) to `0..1`, non-whole, default 1. The FOV row keeps its range and takes the shipped 90 as its authored default. |
| `SliderRange` (new) | `ApplyWithoutNotify` — sets a slider's range + value with a guaranteed-silent widen → seat → narrow. Used by `AudioLevelSlider.OnEnable` and `GameSettingsPanelController.BindSlider` (which now takes the saved value). |
| `DisplayGraphicsSettings` | `SettingsVersion` 1 → 2 with a v2 migration putting **every** player on 90° FOV — v1's "a saved value is a deliberate pick" rule did not hold once the slider itself was found to be writing 71. |
| `Mixer` → `AudioLevelSlider` | Same file GUID, so all three prefabs keep the component with no re-wiring. Reads the saved level on enable, writes through `GameSetting.SetMusicLevel/SetSFXLevel`, follows external changes. The legacy `VCA` string ("Music"/"SFX") is the channel selector. `m_TargetAssemblyTypeName` / `m_EditorClassIdentifier` updated in the three prefabs so the persistent `SetVolume` listener still resolves. |
| `AudioSystem` | Owns the **music**: `musicEvent` (wired to `event:/Music/Music` as a Bootstrap scene override, moved off the deleted `AudioManager`), created once, volume follows Music slider + toggle, stopped/released on destroy. **One volume mapping** for every FMOD instance the code creates: `AudioVolumeMath` (pure) + `AudioSystem.ResolveSfxInstanceVolume / ResolveMusicInstanceVolume`. **Opt-in VCA mode** (`driveFmodVcas`, default OFF): when on, the sliders are written to `vca:/SFX` / `vca:/Music` and every per-instance resolver collapses to its trim so the slider is never applied twice; a VCA that fails to resolve falls back per-channel and reports once. Per-slider-tick `CSDebug.Log`s removed. |
| `FmodSafe` (new) | `TryCreateInstance` (no throw; reported once per event, then silent), `Attach` / `Detach` (no-ops once the runtime is tearing down — never resurrects `RuntimeManager`), `StopAndRelease` (detach, stop if started, release, clear handle). |
| Four owned-instance controllers | Route creation, attach/detach and teardown through `FmodSafe`; a failed create stops retrying. `ShipAudioController` positions the engine instance and each layer **before** `start()`. All four resolve volume through the shared resolver (removed four copies of the same arithmetic). |
| `FMODOneShotVolumeHelper` | Creation through `FmodSafe` (and therefore nothing is created while quitting). |
| `AudioManager.cs` | **Deleted**, and its added component removed from `Bootstrap.unity`. |
| `FMODStudioSettings.asset` | `StopEventsOutsideMaxDistance: 1` — a 3D `StudioEventEmitter` only holds an instance while a listener is inside its max distance; a distant creature's loop costs no voice. Menu (no listener yet) → emitters wait until the vessel spawns. |
| Tests | `AudioSettingsPersistenceTests` (edit-mode): the precedence rule and the volume mapping incl. the "never squared" VCA case. |

Not changed on purpose: the per-instance volume model stays the shipped path until the FMOD
project routes buses through the VCAs (Charles task C1). `StudioEventEmitter`-driven sounds
(fauna loops, crystal `Goal` loops) still ignore the SFX slider — that is what C1 + flipping
`driveFmodVcas` fixes in one move, and it is the reason the VCA mode exists rather than
sprinkling a slider component onto seven prefabs.

---

## 3. Verification status

**Not verified in the editor** (no Unity in this session — `/verify-unity` could not run). What
to check, in order:

1. **Compile.** Open the project; zero errors expected. `check_conditional_compilation.py` is clean.
2. **Edit-mode tests.** `AudioSettingsPersistenceTests` green.
3. **Sliders — the actual report.** Main menu → Settings → drag SFX to 0, drag Music to 0.3 → quit
   → relaunch → **reopen Settings** and both sliders still show those values. Reopening is the step
   that used to destroy them, so a test that only checks the audio without reopening the panel
   passes against the bug. Music is at 0.3 and no SFX plays. Repeat from the freestyle pause menu
   (`Pause_Menu_Panel` → options) — the legacy sliders now show the saved value on open and save.
   Sign out / clear cloud → the local values win on relaunch (no reset to 1).
4. **Fresh install.** Delete PlayerPrefs (Edit ▸ Clear All PlayerPrefs) → launch → music and SFX
   audible at level 1.
5. **Music.** Plays from Bootstrap on; Music toggle OFF silences it; slider scales it. The
   `AudioSystem` inspector shows `musicEvent = event:/Music/Music` (scene override). FrogletTools ▸
   Performance ▸ FMOD Live Diagnostics shows **exactly one** `event:/Music/Music` instance for the
   whole session.
6. **Quit.** Play → Stop in the editor and Alt+F4 in a build: no FMOD errors after the first
   "Cleaning up" line, no hang.
7. **Voices.** Wildlife Liberation intensity 4: FMOD Live Diagnostics — total channels should now
   track creatures near the player rather than the whole population.
8. **FOV.** Settings → Display: the field-of-view slider reads **90** on an existing profile as
   well as a fresh one (the v2 migration runs once at startup and is saved immediately). Changing it
   afterwards still sticks across a restart.
9. **Console.** Expected remaining noise until Charles's tasks land: one `Boost Activate` looping-
   event error, three "No FMOD EventReference wired" warnings, one "Please add an FMOD Studio
   Listener" warning at boot.

Known migration cost: a legacy install whose level keys were int-typed reads its levels as 1.0 once
(that is the repair; before this change they read as 0).
