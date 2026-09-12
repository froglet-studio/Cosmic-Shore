# Prompt — get NiceVibrations' demo art and demo AUDIO out of the shipping build

Paste everything below into a fresh session.

---

Four sprites and four audio clips from a **plugin demo folder** are wired into shipped UI. Asset Store
EULAs treat demo content differently from the plugin itself, and the audio carries a licence problem
of its own that is worse than the art's.

Read `Docs/THIRD_PARTY_DECISIONS.md` §4 (row 5) and CLAUDE.md's **Audio (FMOD)** section before
starting. Measured on `claude/zealous-davinci-vjwev0`.

## Part A — the audio, and why it is the urgent half

Four clips under `Assets/NiceVibrations/HapticSamples/` are wired into shipped UI as plain
`AudioClip` serialized fields:

| Clip | Field | Wired in |
|---|---|---|
| `ApplicationUX/Beep1.wav` | `countdownBeep` | `_Prefabs/UI Elements/In Game/CountdownTimer.prefab` |
| `Objects/Cash5.wav` | `onTriggerClip` | `Menu_Main.unity` ×3, `MIgration_Prefabs/*` |
| `Objects/Coins2.wav` | `targetReachedClip` | `Menu_Main.unity` ×3, `MIgration_Prefabs/*` |
| `Objects/Keyboard3.wav` | `TypingAudio` | `Menu_Main.unity`, `Profile.prefab`, `ModalWindows.prefab` |

**The licence claim over them is non-commercial.** Their bundled notice,
`Assets/NiceVibrations/HapticSamples/HapticPackCClicense.txt`, opens:

> The haptic library packs use many sounds from freesound.org
> The audio files used in these packs have been modified from the original form.
>
> licensed under CCBYNC 3.0

**CC-BY-NC forbids commercial use.** The ~100-entry source list below that header is *almost entirely
Creative Commons 0*, with one Attribution item — so the file contradicts itself — and, decisively,
**it names freesound source titles, not the shipped filenames**, so no clip in the tree can be mapped
to a term. There is no reading of this file under which a paid Steam release is provably clear.

**Do not spend time adjudicating the licence.** The project has already decided what the fix is:
CLAUDE.md's audio convention states that `AudioClip` + `AudioSource` is the **legacy** path and *new
SFX is FMOD*. These four are legacy UI sounds. Replacing them is not a licence workaround — it is
finishing a migration the project already committed to.

**What to do:** author four FMOD events and move each call site onto an inspector-exposed
`EventReference`, per CLAUDE.md's rules — one field per sound on the component that makes it, played
through `AudioSystem.PlaySFXEvent` / `PlaySFXEventAttached` or `FMODOneShotVolumeHelper`, **never**
`RuntimeManager.PlayOneShot`.

**Ship the `EventReference` empty if the FMOD event does not exist yet.** That is the convention and
it is load-bearing here: an empty slot is a visible TODO, a borrowed "temp" event is an invisible one.
`EventReference.IsNull` makes an empty slot a clean no-op. **Do not** point these at an existing
unrelated event to keep a sound playing — silence is the correct interim state, and it is the audio
owner's call what replaces it (`Docs/AudioSystem/CHARLES_TASKS.md`).

## Part B — the sprites

Four distinct sprites, six usages:

| Sprite | Used as | In |
|---|---|---|
| `Demo/DemoAssets/RegularPresetsDemo/Sprites/RegularPresetsIcons.png` (sheet — 4 sub-sprites) | `Image.m_Sprite` | `Menu_Main.unity`, `Main Menu Screens/Arcade Screen.prefab`, `UI Elements/ModalWindows.prefab` ×2 |
| `Demo/DemoAssets/RegularPresetsDemo/Sprites/RegularPresetsIcons_8_Flipped_Vertically.asset` | `Image.m_Sprite` | `Menu_Main.unity`, `Main Menu Screens/Records Screen.prefab` |
| `Demo/DemoAssets/CarDemo/Sprites/NVCar.png` | `SO_Class_Termite.IconActive` | `_SO_Assets/Classes/SO_Class_Termite.asset` |
| `Demo/_Common/Sprites/NV7Dots.png` | `SO_Class_Termite.IconInactive` | `_SO_Assets/Classes/SO_Class_Termite.asset` |

**The two Termite icons are already placeholders.** Termite is a *planned, unimplemented* vessel
(`VesselClassType.Termite = 8`), so those slots are standing in for art that does not exist. Replace
them with a first-party neutral placeholder and note in the swap that the real art is a design task,
not a licence task.

The other four usages are live menu chrome. Re-author them as first-party sprites in the house style
(`Docs/STYLE_FOUNDATION.md`, `Docs/PALETTE.md`) at the same pixel dimensions and import settings, so
no `RectTransform` moves.

## The swap method — read this before touching a scene

**Do not edit the `.png` in place and keep the guid.** That works and it hides the change: the asset
path still says `NiceVibrations/Demo/...`, so the next audit re-reports it and a future `git clean`
or plugin re-import silently restores demo art.

Instead:

1. Create the replacement under a first-party path (`Assets/_Graphics/UI/...` for sprites; FMOD events
   for the audio).
2. Re-point each reference. A sprite-sheet sub-sprite is addressed as
   `{fileID: <subsprite>, guid: <sheet>}` — the **fileID identifies which sub-sprite**, so a one-line
   guid swap is wrong unless the new sheet reproduces the same sub-sprite fileIDs. Either author four
   separate sprites and rewrite both fields, or generate a sheet whose `.meta` declares matching
   `internalIDToNameTable` entries.
3. **Prove zero remaining references** before finishing: for each replaced asset, read its own guid
   out of its `.meta` and count occurrences project-wide, per CLAUDE.md's guid-ownership method
   (`grep -c "^guid: $g"` for unique ownership, then count holders) — **never** `grep -rl | head -1`.

`Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/` also holds references to `Cash5`, `Coins2` and
`RegularPresetsIcons_8_Flipped_Vertically`. **Do not touch that folder.** Its contents are an open
decision (`Docs/LAUNCH_BLOCKER_INDEX.md` §B3) and the folder name is a trap. Re-point the shipped
locations; leave those alone and say so.

## Do not delete the plugin here

`Assets/NiceVibrations/` also still backs `HapticController.cs`. Removing the folder waits on
[`HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md`](HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md) and then gets its own
commit with its own reference proof.

## Definition of done

1. Zero shipped references to anything under `Assets/NiceVibrations/Demo/` or
   `Assets/NiceVibrations/HapticSamples/`, proved by guid count, with `MIgration_Prefabs` explicitly
   listed as the untouched exception.
2. Four FMOD `EventReference` fields wired (or honestly empty), no `AudioClip` field left pointing at
   a NiceVibrations `.wav`.
3. `Docs/THIRD_PARTY_REGISTER.md` §0 row 5 updated, and the CC-BY-NC finding recorded in §2 rather
   than dropped once it is moot.
4. `Docs/AudioSystem/CHARLES_TASKS.md` gets the four new events as an audio-owner task if they ship
   empty.
5. Verified in the editor (`/verify-unity`) — Menu_Main, the arcade screen, the records screen, the
   profile modal and the in-game countdown all render and sound correct — or stated plainly that it
   was not.
