# Microgame Training — design plan

**Status: PLAN, not built.** Revision 3 (2026-09-25) — revision 1's five open questions and
revision 2's four are answered and folded in (§1). Nothing here has run in the editor. The Game of the Week rotation is a
separate thread; this plan only assumes it names one `GameModes` value, whose card locks one hull.

---

## 1. Decisions (locked by the product owner, 2026-09-25)

| # | Decision |
|---|---|
| D1 | **The first-time flight tutorial LEAVES freestyle.** A brand-new player is walked, forcibly, from first login all the way into the **Game of the Week's microgame** (the Mode Preview window), and taught that ship's basic flight controls there. Once they leave the tutorial they can play the minigame |
| D2 | **Every microgame run has TWO SECTIONS.** (1) **The Lesson** — forced; teaches the ship's basic controls. **Not skippable the very first time**; after that, skippable after **3 seconds**. (2) **The Mentor** — starts when the Lesson finishes or is skipped; **always open**, even the first time: the player may stay or leave whenever they like |
| D3 | **The Mentor is CURATED, not reactive.** It offers the most effective tips, tricks and advice **in an authored order that makes sense**, like a tutor or a friend flying alongside — it does not diagnose what the player just did wrong |
| D4 | **"Racing" = the Time-genre minigames.** A mode is Time-genre because it expresses its vessel's Time controls, which are always movement. The list for now: Skim Race, Switchback, Headlong, Redline, Skein, Breakwater, Regatta. **Source of truth later: the "genre petals"** a parallel branch is adding (categories Mass / Charge / Space / Time) — not on `bleeding-edge` as of `3d9f7660`; switch to it when it lands (§8) |
| D5 | **Leaderboards are shown only when RELEVANT.** Show a board only when the player is top ten in some grouping that makes sense (world, faction once factions exist, friends, …); otherwise show only their personal best (§6) |
| D7 | **"First time" is per ACCOUNT, split by flight scheme.** The account's first Lesson ever is unskippable. Separately, the first Lesson on a **two-thumb** ship is unskippable once, because two-thumb flight is unique to this game; after that, every two-thumb Lesson skips at 3 s. One-thumb flight is close to conventional flight controls, so it gets no key of its own: once the account's first Lesson is done, every one-thumb Lesson skips at 3 s (§3) |
| D8 | **Drift stays in the Lesson** on ships that have it |
| D9 | **"Near the top" = top ten, or within 10% of tenth place's time.** Likely the long-term rule |
| D10 | **Mentor pacing:** ~6 s on screen, ~8 s gap, and a tip waiting for its Moment fires anyway after **14 s** |
| D11 | **`DeveloperUnlockGate` is out of scope** — the product owner handles it manually and will decide its new paradigm separately (§5) |
| D6 | **Architecture: plans B + C combined** — a linear drill layer (B) for the Lesson and a curated tip sequence (C, re-cut from "reactive" to "curated" by D3) for the Mentor, sharing one runner, one condition set and one view |

---

## 2. What already exists, and the one thing that doesn't

- **The microgame is a well-bounded place to put this.** `ModePreviewSession` already owns the
  preview arena, the hull swap, handing the camera to `ModePreviewWindow`'s RenderTexture, the
  input-focus handoff (`ModePreviewWindow.AnyHasFocus`, with its four gates), and
  `ModePreviewRunner`, a plain MonoBehaviour that counts one `ScoringMetric` against a baseline.
  Training is a layer on top of that and rebuilds none of it.
- **The racing microgames have no race in them yet.** Switchback, Headlong, Redline and Breakwater
  preview **shell only**: their rings are built by `GateRaceController` at match start, and the
  preview has no controller. Skein and Regatta show their rails but no rings. Skim Race is the only
  race whose track is in the window. The Lesson can teach flight without rings, but the Mentor's
  racing tips ("take the gate on the inside", "boost the straight") need them. **§7 is a
  prerequisite for phase 2.**

---

## 3. The flow

```
first login
  └─ Menu_Main ready
      └─ FIRST-RUN RAILROAD (Quest Graph Phase 0, re-cut — §5)
          ├─ navigation locked to Arcade
          ├─ Navigate → Arcade → Game of the Week card (auto-selected)
          ├─ preview armed FORCED: window takes focus by itself on arrival
          │
          │   ┌──────────── inside the microgame ─────────────────────────────┐
          │   │ SECTION 1 — THE LESSON                                         │
          │   │   first time for this hull: no Skip, focus held, Play disabled │
          │   │   every later run: Skip appears after 3 s                      │
          │   │   beats: the hull's flight scheme + its Time (movement) ability│
          │   │                     ↓ finished or skipped                      │
          │   │ SECTION 2 — THE MENTOR                                         │
          │   │   always open; every release route works; Play enabled        │
          │   │   curated tips, one at a time, in authored order               │
          │   │   ends at the last tip, or whenever the player leaves          │
          │   └────────────────────────────────────────────────────────────────┘
          │
          └─ WaitForLesson(complete) → navigation unlocked → railroad ends
```

The railroad is first-login only. Every later preview entry, on any card, runs the same two
sections through the same runner, just not forced into by navigation.

**"First time" is per ACCOUNT, with one extra key for two-thumb flight (D7).** Every ship is
one of two flight schemes, named by how many thumbsticks it flies with:

| Scheme | Ships (today) | Why it gets its own key |
|---|---|---|
| **Two-thumb** | Dolphin, Squirrel, Rhino, Manta, Urchin, … | Unique to this game. Learned once, it transfers to every other two-thumb ship |
| **One-thumb** | Sparrow, Serpent, Scarab, Grizzly, Termite, Falcon, Shrike | Close to conventional flight controls; no separate key |

Two account-level keys decide whether a Lesson may be skipped:

```
lessonSkippable(hull) =
      account.completedAnyLesson                                   // the account's first Lesson, whatever the ship
  AND (scheme(hull) == OneThumb  OR  account.completedTwoThumbLesson)
```

So a new player whose first Game of the Week is a one-thumb race sits through that one Lesson; their
first two-thumb ship later is unskippable once more; everything after that skips at 3 s. A player
whose first Lesson is two-thumb sets both keys at once. **Completing** a Lesson sets a key; skipping
never does (by construction, skipping is only offered after the key is already set).

The scheme is read from the flag the platform already computes, `IVesselStatus.IsSingleStickControls`
(and the roster `OneThumbVesselCoverageTests` pins), so no new vessel labelling is needed for this.
If the ships are later labelled more formally, the resolver changes in one place. The scheme is
also what picks which of the two shared flight blocks the Lesson teaches (§4.1), so "what the
Lesson teaches" and "what counts as having learned it" come from the same source.

---

## 4. Architecture — one runner, two sections

### 4.1 The decomposition that covers every vessel ever built

The content is never authored per (mode × vessel). It splits by what it teaches, and each half
is keyed on something the platform already treats as canonical:

| Content | Keyed on | Source | New vessel / mode cost |
|---|---|---|---|
| **Lesson** — this ship's basic flight | `VesselClassType` | **Derived**: the flight-scheme block (two schemes fleet-wide, chosen by `IsSingleStickControls`) plus the hull's **Time** ability from `ElementalAbilityMapSO` (`AbilityLabel`, `AbilityDescription`, `Input` → `InputHintBindingMap` → `ControlGlyphSetSO` glyph, the chain the ability lockup already uses, so a control label can never be wrong) | **None** — a hull with a filled ability map has a Lesson on day one |
| **Mentor** — tips for this ship in this mode | hull tips ⊕ mode tips (per `ScoringMetric` family, overridable per `GameModes`) | **Curated**: authored tip lists. The hull's other three abilities appear as derived "did you know" tips until someone writes better ones | **Near zero** — a new hull gets its derived ability tips; a new mode that reuses a metric gets that family's tips |

Why the Lesson is "flight + Time ability": D1 says the Lesson teaches *basic flight controls*, and
D4 says the Time ability is the ship's movement ability — so flight plus Time is exactly "how this
ship moves". Charge, Mass and Space abilities go to the Mentor, where the player can pick them up
at their own pace.

### 4.2 Data

```
DrillBeat   [Serializable, polymorphic via [SerializeReference]]
  Prompt        text with tokens: {glyph:Time} {ability:Time} {vessel} {mode}
  Anchor        Window | AbilityRow(Element) | ObjectiveArrow | NextGate
  Cue           None | PulseAbilityRow | HighlightNextGate | GhostDemo (later)
  LessonStep:   Condition (completion), HintAfterSeconds + HintPrompt
  MentorTip:    DwellSeconds (how long it stays up), optional Moment condition (§4.4),
                Priority (orders tips inside the playlist), TipId

LessonTemplateSO   the shared flight-scheme blocks (single-stick / dual-stick), authored once
TipListSO          an ordered tip list; one per metric family, optional per mode, optional per hull
DrillLibrarySO     Resources/DrillLibrary: metric→TipListSO, mode→override, hull→TipListSO,
                   Mentor ordering policy, the Lesson's skip delay (3 s), per-beat pacing defaults
```

`[SerializeReference]` rather than the Quest Graph's per-node sub-assets: a beat is small, owned
by one list, and never referenced from anywhere else.

### 4.3 Section 1 — the Lesson (plan B)

A strictly linear list of steps, each one a prompt plus a completion **condition**:

| Condition | Reads (already exists) |
|---|---|
| `InputHeld(stick/trigger, s)` / `InputPressed(InputEvents)` | the `ScriptableEventInputEvents` channel the quest runner already uses, filtered to the local vessel |
| `SpeedAtLeast` / `SpeedFraction` | `VesselStatus.Speed` |
| `DriftHeld(s)` | `VesselStatus.IsDrifting` (as `QuestWaitForDriftNode`) |
| `AbilityActivated(Element)` | the vessel's action-handler start event, resolved through the ability map |
| `Skims(n)` | `ScriptableEventBoostChanged` (as `QuestWaitForSkimNode`) |
| `Timer(s)` | unscaled time |

A composed Lesson (single-stick hull, e.g. Sparrow):
1. "Move the mouse / {glyph:stick} to steer." → `InputHeld(stick, 1.0)`
2. "Throttle up." → `SpeedFraction(0.8)`
3. "{glyph:Time} — {ability:Time}: {description}" → `AbilityActivated(Time)`, pulsing the
   ability row
4. *(if the hull drifts)* "Hold both triggers to drift." → `DriftHeld(0.75)`

Four or five steps, about 30 s for a player who does as asked. Only the active step's condition
is live.

### 4.4 Section 2 — the Mentor (plan C, curated)

**A playlist, not a rule engine.** The Mentor composes one ordered list:

```
[ hull tips (curated, then derived ability tips) ]
    ⊕ [ mode tips for this card's metric family ]
    ⊕ [ advanced tips: element upgrades, the Game of the Week board ]
    − tips this player has already seen (moved to the END, not dropped)
```

and shows them one at a time. "An order that makes sense" is authored as **tiers** — *basics of
this ship → how to win this mode → how to win it faster* — with `Priority` ordering inside a
tier. Seen tips go to the back rather than disappearing, so a returning player hears something new
first and the Mentor never goes silent.

**Pacing is the whole feel of the Mentor**, because it is what makes it a friend and not a
billboard:
- A tip stays up for its `DwellSeconds` (default ~6 s) and the next one arrives after a quiet gap
  (default ~8 s). The player never has to dismiss anything; a "next" affordance exists for readers.
- **An optional `Moment` condition** lets a tip *wait for a good time* to say its piece — "boost
  on the straights" waits until the ship is actually at speed, and "take the gate on the inside"
  waits until a gate is ahead. This changes **when** a tip is said, never **which** tip is said,
  so the Mentor stays curated rather than reactive (D3). A Moment that doesn't come within **14 s**
  lets the tip go anyway (D10).
- The Mentor ends when the list is exhausted (a closing line pointing at Play) or when the player
  leaves; re-entering resumes where it left off.

This reuses plan C's machinery (conditions + one view) with its trigger model replaced by a
playlist, which is how B and C combine without becoming two systems.

### 4.5 Runtime pieces

| Piece | Job |
|---|---|
| `DrillComposer` | Pure function `(mode, hull, progress) → (lessonSteps, mentorTips)`. Edit-mode testable; the authoring window (§9) calls the same function, so what the tool shows is what runs |
| `DrillRunner` | Plain MonoBehaviour beside `ModePreviewRunner`, same lifetime; created by the session, starts at tap-in, stops on every existing exit route. Runs the Lesson, then the Mentor. Never writes `GameDataSO`, never replicates (the preview is local by design) |
| `DrillCoachView` | One panel over the preview window rect: prompt line, the control glyph chip (drawn from `ControlGlyphSetSO` exactly as the lockup draws it), Lesson step pips, a Skip button that appears only when allowed, the Mentor's "next". Anchors reuse surfaces that already exist: the launch panel's controls block already lights a row when its control is held, and `PulseAbilityRow` drives that same row |
| `DrillProgressStore` | The two account keys — `completedAnyLesson`, `completedTwoThumbLesson` (§3) —, tip ids **seen**, best practice lap per (mode, hull). PlayerPrefs mirror plus one Cloud Save key through the existing repository pattern (`LocalCloudDataCache` makes it work offline). Deliberately NOT the quest cursor |

**Forcing uses gates that already exist.** While the Lesson is unskippable, `ModePreviewWindow`'s
own release test (`WantsRelease`) is held shut by one flag, and the card's Play button is
disabled. The moment the Mentor starts, both open (D1: "once the player leaves the tutorial they
should be able to play the minigame"). **In a party, the Lesson is never forced**: a guest cannot
be held in a window while the host launches, so a guest gets the Lesson skippable from the start.

**Performance:** one live Lesson condition, or one pending Mentor moment, at any time;
event-driven wherever a SOAP channel exists; no per-frame allocation. That is negligible next to
the preview arena the window is already running.

---

## 5. The first-login railroad

Flight school is retired from freestyle (D1), so the Quest Graph's Phase 0 shrinks to a router:

```
P0 (new):  WaitMenuReady → LockNavigation(Arcade only)
           → OpenMicrogame(source: GameOfTheWeek, forced: true)
           → WaitForLesson(hull of that card)
           → LockNavigation(unlock) → PhaseEnd
```

Two new nodes: `QuestOpenMicrogameNode` (navigate → select the card → arm the preview forced) and
`QuestWaitForLessonNode` (completes when `DrillProgressStore` marks that hull's Lesson done). The
existing flight-school nodes (`EnterFreestyle`, `WaitForInput`, `WaitForDrift`, `WaitForSkim`,
`ExitFreestyle`) stay in the codebase for other uses but leave the Main Quest. The later phases
(the Crystal Capture funnel, the unlocks) need re-deciding against the Game of the Week, which is
the separate thread.

**Known, and deliberately out of scope (D11):** `DeveloperUnlockGate.AllUnlocked` (default ON)
stops the whole quest graph, so in a default checkout the railroad does not fire. The product owner
is handling that switch manually and will choose its new paradigm separately. Either way, the
preview's own account keys (§3) force the Lesson by themselves, so the Lesson does not depend on
the graph running — only the navigation to it does.

---

## 6. Relevance-gated leaderboards (D5)

The Mentor's closing line and the post-lap result card use one resolver:

```
LeaderboardRelevance.Resolve(player, board) →
    for each grouping in [World, Faction*, Friends, …]:
        if player's rank in grouping ≤ 10           → show that grouping's top ten, player highlighted
    best-ranked qualifying grouping wins; ties → the narrowest grouping (friends before world)
    none qualifies                                   → Personal Best only
```

(*Faction once factions exist.*) One query per grouping for the player's own rank (UGS returns
rank with the player's score), only when a result screen actually needs it — never per frame, and
cached for the session. It generalises past this feature (any result screen, the weekly
challenge panel), so it belongs in its own small service beside
`WeeklyChallengeLeaderboardService` rather than inside the drill code. "Near" the top (D5 says "on
or near") needs a number; proposed **top 10, or within 10% of tenth place's time**, tunable in
config.

---

## 7. Prerequisite — put the race in the racing microgames

1. **Move course construction out of the controller.** `GateRaceController.BuildCourse(seed,
   gateCount, inner, outer)` is `protected abstract` on a NetworkBehaviour, but the generators
   behind it are already standalone code with edit-mode tests (`SwitchbackCourse`,
   `HeadlongCircuit`/`RedlineCourse`, `SkeinCourse`, `RegattaCourse`, Breakwater's builder). A
   per-mode `IRaceCourseSource` that both the controller and the preview call gives ONE course
   definition per mode. Its inputs are exactly what the controller feeds `BuildCourse` today — a
   seed, the gate count (`AuthoredGateTarget()`), the shell radii (`ResolveShell`, read off the
   cell) and the intensity — and every one of those is available in the preview from its
   definition and its arena. Lead-in gates and laps go with it (`LeadInGates`, `LapsPerRace`,
   and the static `RaceLengthFor`/`RingIndexFor` already exist), so the preview laps a circuit
   exactly as the match does. Done as a pure move (same seeds give the same courses), the existing
   course tests prove it changed nothing; one new test per mode asserts the controller and the
   preview get identical gates for the same seed.
2. **`ModePreviewGateCourse`** stands `RaceGateRing`s in the preview arena from a local seed, tests
   crossings with the existing pure `RaceGateRing.CrossedMouth(prev, cur)`, and raises
   `GateThreaded` / `LapCompleted` for the Mentor's Moment conditions and the practice-lap time.
   Local only; the rings retire with the arena.
3. Gate-race preview objectives become `SwitchesThreaded` with a real target, so the launch
   panel's objective box counts gates like every other mode, and the four "OPEN-ENDED" preview
   notes retire.

---

## 8. Racing = Time genre: until the genre petals land

Until the genre-petal branch merges, the Time-genre list is the constant in D4, held in
`DrillLibrarySO` (not hard-coded). When the petals land, `DrillLibrarySO` switches to reading each
card's genre, and a test asserts the two agree for one release before the constant is deleted.
Being Time-genre changes only the **Mentor's** content (racing tips, the practice lap, the Game of
the Week board); the Lesson is the same for every mode, because it teaches the ship, not the game.
Non-racing microgames get exactly the same two sections, with their metric family's tips.

---

## 9. Authoring tool and gates

**FrogletTools ▸ Game Modes ▸ Microgame Drills** (following `Docs/TOOLING.md`):
- **Lesson tab**: one row per hull — the composed Lesson with every token resolved (the actual
  glyph and ability name). A hull whose Time slot is an open design slot, or whose Time ability
  has no input, is flagged: it cannot have a Lesson step 3.
- **Mentor tab**: modes × hulls; each cell is the composed playlist, coloured by source (hull /
  mode / advanced / derived), with inline editing of the TipListSO a tip came from, and a
  **"simulate as"** control (tips already seen) that shows the reordering.
- **Validate**: every token resolves; every control has both a pad and a keyboard glyph; every
  Moment condition's signal exists in that card's preview (a gate Moment on a card with no rings
  is an error until §7 lands).
- It writes only the drill assets, each recorded through `FrogletToolChangeLedger` per the tool
  contract.

**Test:** `DrillCoverageTests` (edit mode) asserts every playable hull composes a Lesson of at
least three steps with every token resolved, and every playable card composes a Mentor list of at
least N tips. A new vessel with an empty ability map fails a test instead of shipping a silent
microgame.

**Analytics:** `drill_lesson` `{hull, outcome: completed|skipped, seconds, first_time}` and
`drill_mentor` `{mode, hull, tips_shown, left_at_tip}`. The second answers "how long do people
stay with the Mentor", which is the tuning question for its pacing. Keep parameters few: UGS
schema rows are permanent and capped (`Docs/Analytics/DATA_ARCHITECTURE.md`).

---

## 10. Phasing

| Phase | Scope | Proves |
|---|---|---|
| 1 | `DrillRunner`, `DrillComposer`, `DrillCoachView`, `DrillProgressStore`; the Lesson with derived beats; the two account keys + 3 s skip; Play gating | every hull has a Lesson |
| 2 | §7 gate course in previews (Switchback + Redline first) | the race is in the window |
| 3 | The Mentor: TipListSOs for the gate-race family, pacing, Moment conditions, resume | the curated tutor |
| 4 | Quest Graph P0 railroad; the Game of the Week source | first login → Lesson, end to end |
| 5 | Remaining races; tip lists for non-racing families; relevance-gated leaderboards (§6); practice-lap result | coverage |
| 6 | Authoring window, coverage test, analytics; switch D4's list to genre petals | the "every vessel ever" guarantee |
| later | `GhostDemo` cue (the preview's own autopilot flies a step once before handing over) | show, don't tell |

---

## 11. Still open

1. **What happens to Main Quest phases 1–5** (the Crystal Capture funnel and the unlock chain) now
   that first login goes to the Game of the Week — separate thread, but they currently assume P0
   ended in freestyle.
2. **Mentor pacing** is set (D10) but wants one playtest to confirm it reads as a friend, not a
   billboard.
