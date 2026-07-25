# Unity In-Editor Verification Checklist

**Purpose.** Some changes land on shared branches (`bleeding-edge` and the
per-feature branches) without ever being opened in the Unity Editor —
authored and committed by a session that **cannot run the editor**, so no
compile, no play-test, no prefab/asset inspection happened on the author's
side. Those changes are correct on paper but carry editor-side risk: a prefab
import that didn't take, a Variant override that didn't serialize, a rig weight
that reads differently in-scene than in code.

This doc is where that risk gets **recorded once** instead of being re-explained
at the start of every session. When you next open the project in Unity, work the
open items below, tick what you confirm, and delete (or move to "Verified") what
holds up. When you commit code that you could not editor-verify yourself, add an
entry here rather than leaving it in a PR body or a chat message that scrolls away.

**How to use it**
- One `### ` section per unverified change set, newest first.
- Each has: what landed, the concrete **verify in editor** steps, and any
  **first-pass tuning** numbers (these are starting points, expect a balancing
  pass once the thing is observable in context — they are *not* settled).
- Status markers: 🔴 unverified · 🟡 partially confirmed · 🟢 verified in editor.

---

### 🔴 Fauna consumption v3 + shark jaw rig (fauna-consumption-behavior branch, merged)

Landed via PR #614 (`claude/fauna-consumption-behavior-*`) plus the shark-jaw
commit `438070a2`. None of it had a Unity compile or play-test from the author —
it is on the shared branch unverified. Design + mechanics reference:
`Docs/ECOSYSTEM.md` §7 / §7.3 (intentional consumption, the mouth-driven
predator, tiger-shark territoriality, centre focus).

**Verify in editor (the three things most likely to be wrong):**

1. **Jaw prefab import.** Open `Assets/_Models/Fauna/MassSharkFauna.prefab`.
   Confirm `SharkJawDriver` (`_Scripts/Controller/Environment/FloraAndFauna/SharkJawDriver.cs`)
   sits on `Shark_model` alongside the `Animator` + `RigBuilder`, that the two
   mouth `MultiAimConstraint`s and the `MawTarget` it aims at are all present and
   wired, and that weight `0` = FBX swim pose (mouth closed) / weight `1` = aimed
   at `MawTarget` (mouth open). Danger prisms are parented to the jaw bones — check
   the teeth actually gape with the mouth in a play-test (`NotifyBodyPrismsMoved`
   should keep their spatial-index positions honest as the jaw moves).

2. **Elemental Variant on the tadpole config.** Confirm the tadpole's
   `FaunaConfigurationSO` / prefab Variant carries its intended elemental setup
   (that the Variant override actually serialized and points at the creature
   prefab's `Boid`, not the dead `*Population`/manager prefab — see the §7 warning
   that the live spawn path is the cell config, not the scene-placed populations).

3. **Two feeding models coexist.** Confirm both consume paths still compile and
   run side by side without one having been collapsed into the other:
   `LightFauna` (brittlestar/shark) has **no** `_pendingMeals` grazing queue
   (intentional-feeding: approach → face → suction), while `Boid`'s **drone**
   path keeps its `_pendingMeals` burst-pacer (combat). Do not re-add the
   burst-pacer to the forager/intentional types or strip it from the drone path
   (`Docs/ECOSYSTEM.md` §7.3 explains why they differ).

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Hunt pulse (window / cycle) | **10s open / 20s interval** | `LightFaunaDataSO.huntDurationSeconds` / `huntIntervalSeconds` |
| Tiger-shark territory radius | **r = 600** | `LightFaunaDataSO.territoryRadius` (+ `territoryAnchorDistance`) |
| Jaw open / close | **0.6s open / 1.8s close** | `SharkJawDriver` (open notably faster than close) |
| Herbivore/forager centre focus | **0.35** | `FaunaConfigurationSO.CenterFocusBias` (per-deployment) |

These four are the ones the author flagged as guesses. The jaw transition is
~2.4s total per 20s hunt cycle; the driver early-outs on a single float compare
whenever the mouth is settled, so re-tuning the timings has no perf cost.
