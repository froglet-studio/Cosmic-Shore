# Prompt — replace NiceVibrations with a first-party rumble player

Paste everything below into a fresh session.

---

`Assets/NiceVibrations/` is **47 MB**, ships into the player (including its 30-script `Demo/` folder
and a `Resources/`), and **carries no product EULA** — the two files that look like a licence are a
Rust crate list for the Lofelt SDK and a Creative Commons notice covering the haptic sample audio.
The studio's decision is to **replace it rather than buy a seat**.

That is cheap, and this prompt exists because the reason it is cheap is not obvious until it is
measured.

Read `Docs/HAPTICS.md` first — it is the policy this must not change — then
`Docs/THIRD_PARTY_DECISIONS.md` §4.

## The measurement this rests on

**The entire COMPILE dependency is one file and five symbols.**

`Assets/_Scripts/Controller/IO/HapticController.cs` (345 lines) is the only first-party file that
carries `using Lofelt.NiceVibrations` — verified at ship time by
`grep -rn 'using Lofelt' Assets/_Scripts`, which returns that file's two lines and nothing else.
A plain `grep -rn 'NiceVibrations' Assets/_Scripts` returns **four** files; the other three are
**prose, not code**, and two of them are residue this deletion has to sweep (see the end of this
prompt). From the real dependency, it uses:

| Symbol | Used for |
|---|---|
| `HapticController.Load(byte[] json, GamepadRumble rumble)` | load the clip about to play |
| `HapticController.outputLevel` | the player's haptics-level setting |
| `HapticController.clipLevel` | per-pulse strength (scales iOS clip **and** motor speeds) |
| `HapticController.Play()` | play it |
| `GamepadRumble` (struct) | `durationsMs[]`, `lowFrequencyMotorSpeeds[]`, `highFrequencyMotorSpeeds[]`, `totalDurationMs` |

**Every haptic the game plays is already first-party content.** `EnsureClips()` builds all four feels
— skim, punish, alert, spray — in code, as `.haptic` JSON strings and `GamepadRumble` arrays *we
authored*, with the envelopes hand-tuned and documented inline. NiceVibrations contributes **no
clips, no patterns, no data** — only a player. There is nothing to re-author.

## The design

**On gamepad, `UnityEngine.InputSystem` replaces the player outright.** Input System **1.14.2** is
already a direct dependency (`Packages/manifest.json`), and `Gamepad.SetMotorSpeeds(low, high)` is
exactly the two-motor interface the existing `GamepadRumble` arrays are written against. Stepping
`durationsMs[i]` through `SetMotorSpeeds(low[i], high[i])` reproduces the shipped feels **exactly** —
same envelopes, same durations, same priority order.

**On PC/Steam — the launch platform — that is the whole feature.** `Docs/HAPTICS.md` already states
that every verification step needs "a device or a connected gamepad", and a bare desktop has no
motors either way. Mobile pattern haptics (iOS Core Haptics, Android `VibrationEffect`) are the only
thing the plugin was actually buying, and they are not on the launch path.

So:

1. **Keep `HapticController.cs`'s public surface byte-for-byte.** `PlaySkim`, `PlayPunish`,
   `PlayAlert`, `PlaySpray`, `PlayHaptic`, `PlayConstant`, the `HapticType` enum, the
   `GameSetting.HapticsEnabled` / `HapticsLevel` reads, and the `alert > punish > skim > spray`
   priority/rate-limit gate all stay. **No call site changes.** Grep first and prove it: there are
   call sites across vessel, arcade and UI code.
2. **Keep the four authored envelopes.** Move `GamepadRumble` to a first-party struct of the same
   shape (a plain data carrier — `durationsMs`, `lowFrequencyMotorSpeeds`, `highFrequencyMotorSpeeds`,
   `totalDurationMs`) and keep every number in `EnsureClips()` unchanged. A retune is a separate
   decision and must not ride this branch.
3. **Write the stepper.** One component or static driver that plays a rumble on the active
   `Gamepad.current`, stepping segments on a UniTask loop (not a coroutine — the project prefers
   UniTask, with a `CancellationToken`), honouring:
   * **one clip at a time** — the current gate is written against NiceVibrations' single-loaded-clip
     behaviour (`Load()` evicts whatever is playing). Reproduce that: a new load stops the previous.
   * `outputLevel` × `clipLevel` scaling both motor channels.
   * a hard stop on disable / scene exit / application pause — **a motor left running is a real bug**,
     and unlike a sound nobody can hear it stop.
4. **The `.haptic` JSON becomes dead weight on the launch path — do not delete it.** Keep
   `ClipJson()` and the four JSON blobs behind a clearly-labelled mobile branch, or move them to a
   documented block explaining they are the authored source for a future mobile backend. They are the
   only record of those envelopes in a portable form. Say plainly in `Docs/HAPTICS.md` what mobile
   does **now** — `Handheld.Vibrate()` or silence — rather than leaving it ambiguous.
5. **Do not delete `Assets/NiceVibrations/` in this branch.** Its demo sprites and four audio clips
   are still wired into shipped UI; removing the folder is
   [`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md)
   and then a final removal commit with its own reference proof.

## Traps

* **`HapticController` is the class name on both sides.** The file aliases
  `using LofeltHaptics = Lofelt.NiceVibrations.HapticController;` precisely because of that collision.
  When the alias goes, make sure no unqualified `HapticController.X` inside the file silently binds to
  the first-party class where it meant the vendor's, or vice versa. This is exactly the overload/scope
  class of error CLAUDE.md records as **editor-only** — the out-of-editor gates cannot see it.
* **`Gamepad.current` can be null and can change mid-session.** `Docs/ONE_THUMB_MOUSE_CONTROLS.md`
  §4.0 is the standing law here: *"which device is the player using" must have ONE implementation* —
  read `InputDeviceActuation` rather than adding a second detector, and do not key on
  `Gamepad.current != null` presence if the project's detector already answers the question.
* **Haptics are local-human-pilot-only.** That gating already exists upstream of these calls; do not
  move it, and do not let the new driver make an AI or a remote replica buzz somebody's pad.
* **This is a platform behaviour, not a vessel feature.** Nothing per-vessel gets a copy of the
  stepper.

## The residue a deletion leaves behind

`Lofelt` disappears when `HapticController.cs` is rewritten. The **name** does not — three
first-party files mention NiceVibrations in prose, and one of them is load-bearing:

| Site | What it is | Do |
|---|---|---|
| `CanvasUpgraderCodeScan.cs` (comment beside its first-party-roots list) | names NiceVibrations as a third-party tree to exclude from the scan | **update it** — the tree is gone, and a stale exclusion list is a claim about the repo that has stopped being true |
| `GunSpreadProfile.cs`, `GunSprayAccuracy.cs` | tooltip/doc prose explaining the spray haptic's pulse spacing as *"the interval NiceVibrations can hold"* | re-word against whatever replaces it — the CONSTRAINT is real and must survive the vendor's name |

The second row matters more than it reads: the spray texture's cadence was derived from that
limit, so a rewrite that silently changes how many clips can be queued changes a shipped feel.
Re-derive it against the new player rather than porting the number.

## Definition of done

1. `HapticController.cs` compiles with **no `Lofelt.NiceVibrations` reference** and no public-surface
   change.
2. The four feels reproduce on a connected gamepad — run `Docs/HAPTICS.md`'s own four verification
   steps and report each one's result honestly.
3. `Docs/HAPTICS.md` updated: what plays the clips now, and what mobile does.
4. `Docs/THIRD_PARTY_REGISTER.md` §2 and `Docs/THIRD_PARTY_DECISIONS.md` §3 updated.
5. Verified in the editor (`/verify-unity`) — or stated plainly that it was not.
