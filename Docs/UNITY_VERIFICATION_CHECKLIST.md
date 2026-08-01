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

### 🔴 Presence sync — Commit 1 (observability): benign-skip counters, shared rate-limit classifier, NetDiag on every presence catch

Commits `44587a2f`, `11559a93`, `92ec00f7` on
`claude/multiplayer-presence-lobby-sync-6j924k`. Plan:
`Docs/PresenceSystem/PRESENCE_SYNC_PLAN.md` § 5 Commit 1.

**What landed.** Counters + one throttled `CSDebug.Log` per 10 s on the two
previously-empty benign catch branches in `HostConnectionService`; a new
`UgsErrorClassifier.IsRateLimit` (chain-walking) replacing three divergent
private copies; the `[rate-limit]` branch moved above `[benign]` at both catch
sites; `LogNetDiag(operation, e)` on all seven `PresenceLobbyService` catches.

**Verify in editor**

1. **It compiles.** `UgsErrorClassifier.cs` is a new file authored without an
   editor — confirm Unity imported it, generated a `.meta` matching the
   hand-authored GUID `517b66f60f7b4b1dab3cb151fecd2c5f` (it should adopt the
   committed one, not mint a new one), and that
   `Assets/_Scripts/Controller/Party/Services/` compiles clean. It references
   `SessionException` / `SessionError` via `Unity.Services.Multiplayer` and
   `RequestFailedException` via `Unity.Services.Core`; both are already used by
   `HostConnectionService`, so no asmdef change should be needed.
2. **The benign counter actually ticks.** Run solo Menu_Main for ~60 s.
   `Docs/PresenceSystem/BUGS.md` B1 reports this SDK fault firing every ~3 s, so
   `[HostConnectionService] Benign SDK fault on the presence read` should appear
   at most once per 10 s with a climbing `skips: presence=N`. **If N stays 0 for
   a full minute, that is the interesting result** — it means B1 is no longer
   firing on this path and the "stale list" symptom has a different cause than
   RC-2. Record either outcome in `BUGS.md` B1/B6.
3. **No new console spam.** The whole point of the original silence was B1's
   ~3 s spam. Confirm the throttle holds — one line per 10 s maximum, and none
   at all when the SDK is behaving.
4. **Rate-limit reorder didn't steal benign traffic.** If step 2 shows skips
   climbing, confirm they are still classified benign and NOT appearing as
   `Rate limited during refresh - backing off`. A benign stale-index fault
   carries neither a 429 nor "Too Many Requests", so the two should stay
   disjoint; if they don't, `UgsErrorClassifier.IsRateLimit` is over-matching.
5. **Party smoke still green.** 3-VP MPPM with uniquely tagged virtual players:
   accept / decline / leave / second accept after leave
   (`Docs/PartySystem/TESTS.md` S-series). The classifier now fires in
   `catch ... when` filters on `PresenceLobbyService` query/join/create and
   `PartySessionService` create/join, so a retry that previously did NOT engage
   for a wrapped 429 now will — that is the intended fix, but it is the one
   behavior change in this commit set and deserves the smoke.

**Not verifiable in editor:** whether a real wrapped 429 is actually shaped the
way `UgsErrorClassifier` expects. Confirm from a live NetDiag log line showing
`class=RateLimit` alongside the new backoff warning before treating RC-3 as closed.

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
