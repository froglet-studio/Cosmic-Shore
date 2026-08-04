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

### 🟡 Presence sync — CONSOLIDATED STATUS (read this before the four entries below)

Branch `claude/multiplayer-presence-lobby-sync-6j924k`, 27 commits. **Partially
verified 2026-08-04.** The four per-commit entries below are kept for their
detail, but this block is the current truth; work
`Docs/PresenceSystem/PRESENCE_SYNC_VERIFICATION.md` § Progress for the live table.

**Verified in editor ✅**

- **B11** — presence reaches `Present`; no instance stalls at `Announced`.
- **B14** — a `presenceState` change repaints the roster row; peers promote
  CONNECTING… → ONLINE. *(This pair was the branch's headline symptom.)*
- **B13** — 4-instance boot, no permanent loading splash. Wider runs pending;
  the Relay 500 is upstream and intermittent, so one clean run is weak evidence.
- **Benign-skip counters** — firing, stackless, and now measured twice
  (`PresenceSystem/BUGS.md` § MEASURED runs 1 and 2).

**Partially verified 🟡**

- **B12 (explicit leave)** — the *hard-kill* case is confirmed at ~30–50 s,
  which is the correct and unimprovable answer. The **graceful path has never
  run**: deactivating an MPPM virtual player kills the process, so no leave is
  emitted, and stopping play mode stops every VP at once leaving no observer.
  Needs a standalone build (guide Step 5a) or `PresenceSystem/TODOS.md`
  § TODO-P10. The verification guide's old departure table was wrong about this
  and has been corrected.

**Still unverified 🔴** — Steps 3 (`ArcadeLobbyList` / RC-9, **the branch's
stated goal**), 4 (party smoke), 5 (quit / background / in-match), 6 (icons),
7 (rate-limit budget), and 1b/1d/2a/2e.

**The two editor actions below are still outstanding** and Step 6 cannot start
without the first one.

---

### 🔴 Presence sync — Commits 5-8 (state machine, tombstones, UI binding, icons)

Commits `641ec251`, `c9c6db17`, `24a9b420`, `3b9a30fa` on
`claude/multiplayer-presence-lobby-sync-6j924k`. **All 8 commits of the branch
are now landed and none are editor-verified.**

**Work `Docs/PresenceSystem/PRESENCE_SYNC_VERIFICATION.md`** — one guide covering
all 8 in a single pass, rather than these per-commit entries.

**TWO EDITOR ACTIONS REQUIRED before anything will look right:**

1. **Assign `Unknown Icon`** on `_SO_Assets/SO_DefaultProfileIcons.asset` (new
   field under "Fallback"). A sprite could not be authored headlessly. Use
   something clearly not one of the 18 real avatars. Unassigned, unresolved
   avatars render as nothing.
2. **Confirm `On App Quit Requested`** on `ApplicationLifecycleEvents.asset`
   points at `EventOnAppQuitRequested`, not `None` — it was wired by raw-YAML
   text edit, and SOAP fields fail loud (NRE on quit).

**New hand-authored files** (hand-written `.meta` GUIDs; Unity should adopt them,
not mint new): `PresenceState.cs`, `PresenceStateMachine.cs`, `IModalPanel.cs`
(plus `UgsErrorClassifier.cs` from Commit 1).

**Highest-risk behaviour to confirm:**

- **`PresenceStateMachine` reaches `Present`.** Solo Menu_Main should log
  `Offline → Joining → Announced → Present`. Stalling at `Announced` means the
  vessel-spawn signal is not arriving and every peer will show you as
  `CONNECTING…` permanently.
- **`ArcadeLobbyList` re-reads on open** (the RC-9 fix). Its `OnEnable` fired once
  per scene load before; `IModalPanel` dispatch replaces it.
- **`ModalWindowManager` panel array is lazily resolved, not `Awake`-based** —
  `ArcadeGameConfigureModal` declares a non-virtual `Awake` that would have
  hidden a base hook on the very modal this fixes.

**Deliberate gaps** (not oversights, documented in the guide): tombstone
*rendering* is not implemented (only the eviction delay); `PresenceState` has no
SOAP channel (C# event only, matching `PartyStateMachine`); granular roster SOAP
events were not added.

---

### 🔴 Presence sync — Commits 3 + 4 (push channel, explicit leave)

Commits `b0adfa72`, `8a146795`, `2452a392`, `52b8f5f6` on
`claude/multiplayer-presence-lobby-sync-6j924k`.

**Step-by-step verification lives in
`Docs/PresenceSystem/PRESENCE_SYNC_VERIFICATION.md`** — work that guide rather
than this entry; it covers commits 1–4 in one pass. The highest-risk items:

- **Hand-authored asset.** `_SO_Assets/Event Channels/Lifecycle/EventOnAppQuitRequested.asset`
  (+ `.meta`, GUID `65f957fb…`) was written as raw YAML and wired into
  `ApplicationLifecycleEvents.asset` by text edit. Confirm the container's
  **On App Quit Requested** field is not `None`. SOAP fields fail loud by policy,
  so a bad wire = NullReferenceException on quit.
- **`ISession` event names are doc-verified, not compile-verified.** Checked
  against Unity's API reference for `com.unity.services.multiplayer@1.1`
  (package not vendored here). If push never fires, fall back to subscribing
  `Changed` alone — see the guide, Step 2c.
- **The safety poll is still 1.5 s on purpose.** Raise it to 10 only after push
  is confirmed working. Prefab-only change.

---

### 🔴 Presence sync — Commit 2 (poll cadence): honest interval field, wall-clock accumulator, jitter

Commits `09381def`, `084dce0b`, `6a3a37a5` on
`claude/multiplayer-presence-lobby-sync-6j924k`. Plan:
`Docs/PresenceSystem/PRESENCE_SYNC_PLAN.md` § 5 Commit 2.

**What landed.** `refreshIntervalSeconds` now actually drives the poll (it did
not — `AppManager`'s factory hardcoded 1.5f and the prefab said 3, so 1.5 won);
the backoff and session-settle timings that were incidentally riding that field
moved to their own constants at unchanged values; the scheduler's accumulator
now measures wall time instead of gate-eligible time; ±10% jitter added;
`BOOSTED_INTERVAL_SECONDS` 0.75 → 1.1.

**Verify in editor**

1. **Effective cadence is still ~1.5 s.** `PartyServices.prefab` moved
   `refreshIntervalSeconds: 3 → 1.5` specifically so this commit changes no
   timing. Confirm the prefab imported the value (it is a plain YAML scalar
   edit made without the editor) and that presence refresh logs are ~1.5 s
   apart in solo Menu_Main, not ~3 s.
2. **The field is live now.** Set `refreshIntervalSeconds` to something obvious
   (e.g. 6) in the inspector, enter play, confirm the refresh cadence actually
   follows it, then set it back to **1.5**. This is the single check that the
   `DefaultInterval` wiring works; it was untestable before because the field
   was inert.
3. **No double-fire and no burst.** The accumulator now runs unconditionally,
   including before the presence lobby exists. Confirm exactly ONE refresh
   fires immediately on lobby join (intended — first online-list population no
   longer waits an interval) and that a long mutex-held write is followed by a
   single catch-up refresh, not a rapid series of them.
4. **Party join still settles.** `ResetDeferred(POST_SESSION_SETTLE_SECONDS)`
   now counts down in wall time rather than eligible time, so the post-join
   window is genuinely ~3 s + interval instead of "3 s of whenever we happened
   to be eligible". Run the 3-VP accept flow and confirm no stale-session 404
   burst right after a join.
5. **Jitter is not visible as stutter.** ±10% on a 1.5 s poll is ±150 ms; the
   refresh is a fire-and-forget network call, so it should be invisible.
   Confirm no periodic hitch appeared.

**First-pass tuning (expect a balancing pass):**

| Knob | Value | Where |
|---|---|---|
| Poll cadence | **1.5 s** | `PartyServices.prefab` → `refreshIntervalSeconds` (becomes the 10 s safety poll after Commit 4) |
| Boosted cadence | **1.1 s** | `LobbyRefreshScheduler.BOOSTED_INTERVAL_SECONDS` |
| Interval jitter | **±10%** | `LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION` |
| Rate-limit backoff | **6 s** | `HostConnectionService.RATE_LIMIT_BACKOFF_SECONDS` |
| Post-session settle | **3 s** | `HostConnectionService.POST_SESSION_SETTLE_SECONDS` |

The boosted cadence is the one to watch: it was raised from 0.75 s to get under
the ~1/s UGS read cap, at the cost of ~0.35 s of invite latency. If invites feel
sluggish in the 3-VP smoke, the fix is the push channel (Commit 4), **not**
lowering this back under 1 s.

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

### 🔴 Dolphin elemental pass — skim feedback, drift boost, cone blast (`claude/dolphin-energy-crystal-cooldown-zpvc07`)

Authored without a Unity compile or play-test. Garrett play-tested the HUD/boost
rounds mid-branch, but **the final skim-feedback fix is unconfirmed** — the last
report was still "no skimming indication", after which the branch found (a) the
crackle needs three pieces the Dolphin had none of, and (b) all three skim signals
are individually invisible on desktop. Nobody has yet seen a Dolphin skim work.
Mechanics + full knob list: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

**Verify in editor (highest risk first):**

1. **Run `FrogletTools > Vessels > Audit Vessel Skimmers`.** Expect
   `Dolphin  NearFieldSkimmer: 'EnergySkimmer' OK`. This is the branch's headline fix —
   `VesselStatus._nearFieldSkimmer` pointed at a DISABLED legacy skimmer, so
   `Skimmer.Initialize` never reached the object whose trigger fires and
   `SkimmerImpactor` dropped every contact silently. (Serpent is expected to FAIL —
   known, untouched.)
2. **Skim in Menu_Main freestyle.** Fly the Dolphin through cell mass: crackle arcs
   should sweep the skimmer sphere per prism, the HUD jaw icon should punch per skim,
   and the gape (icon + the model's own jaws) should widen toward 18.4° per side as
   energy fills. Watch the console — an unauthored `Prism.ParticleEffect` now logs one
   named warning per prefab instead of throwing per contact.
3. **The boost loop.** Hold drift → the ring steps up; release → speed rises and decays
   as it drains. Flying straight must NOT fill the ring (the passive `resourceGainRate`
   is gone). Drift → release → drift again must return to normal speed (the interrupted
   discharge used to leave `BoostMultiplier` stuck).
4. **Crystal impact.** The cone fires, energy empties, the jaws snap shut, and the Space
   icon flashes with a prism count. At Space L5 the cone must stop damaging your own
   domain's prisms.
5. **Charge L5.** A second crystal pip appears and two team crystals can be planted back
   to back. The deploy preview must be tinted your domain, and bloom/wither rather than
   pop (continuity of existence).
6. **MPPM two-client:** the L5 upgrade effects are gated on the replicated
   `IsUpgradeActive`, so confirm both peers agree on Clean Blast and Twin Seed.

**Hand-authored assets that have never had an editor import round-trip:** the Dolphin
HUD variant's four-icon row, the Dolphin prefab's crackle overlay + controller, and
`DolphinSkimmerChangeResourceByPrismEffect.asset`. Their YAML keys were machine-checked
against the scripts' serialized field sets, but Unity has not re-serialized them.

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
