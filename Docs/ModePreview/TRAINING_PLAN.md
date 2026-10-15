# Microgame Training — design plan

**Status: PHASE 1 LANDED, NOT YET RUN IN THE EDITOR (2026-10-02)** - data layer, runner, coach view, Play gating (§10.1, §10.2). Revision 3 (2026-09-25) — revision 1's five open questions and
revision 2's four are answered and folded in (§1). Nothing here has run in the editor. The Game of the Week rotation is a
separate thread; this plan only assumes it names one `GameModes` value, whose card locks one hull.

---

## 1. Decisions (locked by the product owner, 2026-09-25)

| # | Decision |
|---|---|
| D1 | **The first-time flight tutorial LEAVES freestyle.** A brand-new player is walked, forcibly, from first login all the way into the **Game of the Week's microgame** (the Mode Preview window), and taught that ship's basic flight controls there. Once they leave the tutorial they can play the minigame |
| D2 | **Every microgame run has TWO SECTIONS.** (1) **The Lesson** — forced; teaches the ship's basic controls. **Not skippable the very first time**; after that, skippable after **3 seconds**. (2) **The Mentor** — starts when the Lesson finishes or is skipped; **always open**, even the first time: the player may stay or leave whenever they like |
| D3 | **The Mentor is CURATED, not reactive.** It offers the most effective tips, tricks and advice **in an authored order that makes sense**, like a tutor or a friend flying alongside — it does not diagnose what the player just did wrong |
| D4 | **"Racing" = the Time-genre minigames.** A mode is Time-genre because it expresses its vessel's Time controls, which are always movement. **The genre petals have landed** (`bleeding-edge` `c4934c5a`, `CosmicShore.Data.ModeGenre`), so the source of truth is `ModeGenre.TryElementsFor(mode, metric)` with `Element.Time` as either petal. It is not a hand-kept list. It adds two cards (Astro League, Scarab Scramble) to the seven originally assumed (§8) |
| D5 | **Leaderboards are shown only when RELEVANT.** Show a board only when the player is top ten in some grouping that makes sense (world, faction once factions exist, friends, …); otherwise show only their personal best (§6) |
| D7 | **"First time" is per ACCOUNT, split by flight scheme.** The account's first Lesson ever is unskippable. Separately, the first Lesson on a **two-thumb** ship is unskippable once, because two-thumb flight is unique to this game; after that, every two-thumb Lesson skips at 3 s. One-thumb flight is close to conventional flight controls, so it gets no key of its own: once the account's first Lesson is done, every one-thumb Lesson skips at 3 s (§3) |
| D8 | **Drift stays in the Lesson** on ships that have it |
| D9 | **"Near the top" = top ten, or within 10% of tenth place's time.** Likely the long-term rule |
| D10 | **Mentor pacing:** ~6 s on screen, ~8 s gap, and a tip waiting for its Moment fires anyway after **14 s** |
| D11 | **`DeveloperUnlockGate` gets out of the way, minimally** — a quest may opt in to running under the gate; its lock-applying nodes pass through. The gate's longer-term paradigm is decided separately (§5) |
| D6 | **Architecture: plans B + C combined** — a linear drill layer (B) for the Lesson and a curated tip sequence (C, re-cut from "reactive" to "curated" by D3) for the Mentor, sharing one runner, one condition set and one view |

---

## 2. What already exists, and the one thing that doesn't

- **The microgame is a well-bounded place to put this.** `ModePreviewSession` already owns the
  preview arena, the hull swap, handing the camera to `ModePreviewWindow`'s RenderTexture, the
  input-focus handoff (`ModePreviewWindow.AnyHasFocus`, with its four gates), and
  `ModePreviewRunner`, a plain MonoBehaviour that counts one `ScoringMetric` against a baseline.
  Training is a layer on top of that and rebuilds none of it.
- **The racing microgames had no race in them** (true when this plan was written). Switchback,
  Headlong, Redline and Breakwater previewed shell only, and Skein and Regatta showed their rails
  but no rings. §7 put the race in the window, and phase 2 (§10.3) closed its last gaps.

---

## 3. The flow

```
first login
  └─ Menu_Main ready
      └─ FIRST-LOGIN GUIDE (Quest Graph Phase 0, re-cut — §5)
          ├─ spotlight on the Arcade entry; everything else dimmed and dead except Settings
          ├─ the player opens the Arcade → spotlight on the Game of the Week card
          ├─ the player opens the card → spotlight on the preview window
          ├─ the player taps the window and flies in (nothing is opened FOR them)
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
          └─ the Lesson ends → the spotlight fades → the guide ends
```

The guide is first-login only. Every later preview entry, on any card, runs the same two
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
| **Lesson** — this ship's basic flight | `VesselClassType` | **Derived**: the flight-scheme block (two schemes fleet-wide, chosen by `IsSingleStickControls`) plus the hull's **Time** ability from `ElementalAbilityMapSO` (`AbilityLabel`, `AbilityDescription`, `Input` → `InputHintBindingMap` → `ControlGlyphSetSO` glyph, the chain the ability lockup already uses, so a control label can never be wrong) | **None** — a hull with a filled ability map has a Lesson on day one (its words are authored templates, §4.2.1) |
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

### 4.2.1 Every player-facing line is an authored field (project rule: CLAUDE.md § Code Style)

"Derived" in §4.1 describes **where a line's facts come from**, never where its words come from.
No line the player reads is built out of C# string literals. Every word is a serialized field a
human can edit, blank or delete without touching code:

| Text | Lives in | Human control | Where to find it (Project search) |
|---|---|---|---|
| Lesson steps (both flight schemes) | `LessonTemplateSO`: ordered `LessonStep`s, each with `Prompt` + `HintPrompt` | add / edit / reorder / delete steps | `LessonTemplate_TwoThumb` and `LessonTemplate_OneThumb` in `Assets/_SO_Assets/Drills/Lessons/` (search `t:LessonTemplateSO`) |
| The Time-ability step | a `LessonStep` in that template whose prompt is a token template, e.g. `Hold {glyph:Time} to {ability:Time}` | edit the sentence; the tokens resolve from `ElementalAbilityMapSO`, whose `AbilityLabel` / `AbilityDescription` are themselves authored | the sentence: the same `LessonTemplate_*` asset. The ability name/description: `Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`, Time entry (search the vessel name, e.g. `Dolphin`, or `t:ElementalAbilityMapSO`) |
| Per-hull Lesson changes | `DrillLibrarySO` hull entry: optional replacement steps, or a per-step **suppress** flag | override or delete a step for one ship | `Assets/Resources/DrillLibrary.asset`, *Hull Overrides* list (search `DrillLibrary`) |
| Mentor tips | `TipListSO` entries (`Prompt`) | add / edit / reorder / delete | `Tips_<MetricFamily>` (e.g. `Tips_SwitchesThreaded`), optional `Tips_<Mode>` / `Tips_<Vessel>`, in `Assets/_SO_Assets/Drills/Tips/` (search `t:TipListSO`) |
| "Did you know" ability tips | ONE authored template in `DrillLibrarySO` (`{ability:X} - {abilityDescription:X}, on {glyph:X}`) plus a per-hull, per-element suppress/replace | edit the template or silence the automatic tip for a hull | the template: `DrillLibrary.asset`, *Ability Tip Template* field. Per-hull silence: same asset, *Hull Overrides*. The facts it quotes: the vessel's `ElementalAbilityMaps/<Vessel>.asset` |
| Chrome — section titles, "Skip", the waiting/hint captions, the "PB" / board labels, empty-state lines | `DrillLibrarySO.Strings` | edit any label | `DrillLibrary.asset`, *Strings* block |

**What exists (2026-10-01):** the ability maps, `DrillLibrary.asset` (pacing, the ability tip
template, the *Strings* block with pad and flight-control labels) and the two `LessonTemplate_*`
assets, and (since phase 3, §10.4) eight `TipListSO`s in `Assets/_SO_Assets/Drills/Tips/`. The seed text was
written by `Tools/Build/author_drill_assets.py`, which authors an asset only while it does not
exist (a writer's edit is the point, not drift); its `--check` verifies the SHIPPED assets: ASCII
only, every `{token}` known to the resolver, step ids unique, scheme correct, references resolve.
The authoring window (§9, **FrogletTools > Game Modes > Drill Authoring**) will open all of them
from one place.

**One deliberate gap in the seed:** there is no one-thumb *Drift* label. Which control a one-thumb
hull drifts on has not been checked in the editor, and a missing label drops the drift step (an
honest absence), where a guessed label would teach the wrong button.

Three rules make the table hold:
- An **empty field shows nothing**. That is how a line is deleted; there is no fallback to a
  hard-coded default.
- An **unknown token renders visibly as itself** and logs once, so a typo is seen instead of being
  silently eaten.
- The **authoring window (§9)** edits all of it in place, so a writer never opens C#.

Text stays ASCII-only while the UI font covers 97 glyphs (CLAUDE.md anti-patterns, TMP font). The
authoring window flags a non-ASCII character before it can ship as tofu.

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

## 5. The first-login guide

Flight school is retired from freestyle (D1), so the Quest Graph's Phase 0 shrinks to a guide:

```
P0:  WaitMenuReady → GuideToMicrogame(source: GameOfTheWeek)   holds until the Lesson ends
     → PhaseEnd
```

**It shows the way and the player walks it** - the guided-path rule,
`Docs/HomeHub/ARCHITECTURE.md` §8. Each step spotlights the one control to press next (the Arcade
entry, this week's card, the preview window) with a call to action on it, dims everything else
and makes it dead, and never takes Settings away. Phase 4 first shipped this as a RAILROAD that
opened the Arcade, pressed the card and flew the vessel in by itself; that was the wrong shape - a
player carried somewhere cannot find the way back - and was replaced (§10.6).

Two nodes: `QuestGuideToMicrogameNode` (the spotlight, holding until the Lesson ends) and
`QuestWaitForLessonNode` (completes when a Lesson ends). The guide carries the wait itself,
because a quest resumes at its saved node. The
existing flight-school nodes (`EnterFreestyle`, `WaitForInput`, `WaitForDrift`, `WaitForSkim`,
`ExitFreestyle`) stay in the codebase for other uses but leave the Main Quest. The later phases
(the Crystal Capture funnel, the unlocks) need re-deciding against the Game of the Week, which is
the separate thread.

**The master developer unlock (D11) — SHIPPED, the least disruptive version.**
`DeveloperUnlockGate.AllUnlocked` (default ON) used to stop the whole quest graph, so in a default
checkout the first-login phase would never fire. Now:

- `QuestSO.runsUnderDeveloperUnlock` (default **off**) lets one quest opt in to running under the
  gate. Every quest that exists today keeps standing down exactly as before, so nothing changes
  until the first-login quest sets the flag.
- While the gate is on, the runner passes straight through any node whose
  `QuestNodeSO.AppliesLock` is true: `LockModes`, and the LOCKING direction of `LockNavigation`,
  `SetButtonInteractable` and `SetArcadeConstraints`. The unlock directions still run. So the gate
  still means "nothing is locked", and a guiding phase can still speak, spotlight and wait (a
  guide is not a lock - §10.6).
- `QuestArcadeConstraints.Active` already reads the gate, so a funnel persisted by an earlier
  session stays inert as before.

The gate's longer-term paradigm is still the product owner's call. Independently, the preview's
own account keys (§3) force the Lesson by themselves, so the Lesson never depended on the graph —
only the navigation to it did.

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

   **LANDED (2026-09-29).** `RaceCourseSource` (`Controller/Arcade/Racing/`) plus one sealed
   source per mode (`SwitchbackCourseSource`, `HeadlongCourseSource`, `RedlineCourseSource`,
   `BreakwaterCourseSource`, `SkeinCourseSource`, `RegattaCourseSource`, `WaystationCourseSource`
   - Waystation arrived upstream after this plan was written). A controller now supplies only
   `ModeName` and `CreateCourseSource()`, copying its serialized knobs onto the source; the
   preview calls `RaceCourseSource.For(mode)`, which carries the shipped defaults. Two proofs,
   split by what can run where:
   - `Tools/Build/race_course_source_harness/run.sh` compiles the shipped sources with Roslyn
     and compares them BIT FOR BIT against the pre-extraction controller bodies: 1,600 course
     pairs, 24,080 gates, 0 differences (negative controls: a 1-unit nudge to Switchback's first
     gate fails 320 pairs; Breakwater's inner fallback 420 -> 480 fails 160).
   - `RaceCourseSourceTests` (editor) reads the seven scene files and fails if a scene serializes
     a course knob that differs from the source default - which is what would make the preview's
     course silently stop being the match's. Measured today: every scene authors the defaults
     (Breakwater's inner fallback is 420, and its source says so).
   **Breakwater's stations LANDED in phase 2 (§10.3).** `RaceCourseSource.PoseCourseStructure` is
   the seam, and `ModePreviewDefinitionSO.CourseStructurePrefab` makes the station prefab reachable.
2. **`ModePreviewGateCourse`** stands `RaceGateRing`s in the preview arena from a local seed, tests
   crossings with the existing pure `RaceGateRing.CrossedMouth(prev, cur)`, and raises
   `GateThreaded` / `LapCompleted` for the Mentor's Moment conditions and the practice-lap time.
   Local only; the rings retire with the arena.

   **LANDED (2026-09-29).** `Controller/Arcade/Preview/ModePreviewGateCourse.cs`, owned by
   `ModePreviewSession` (`GateCourse` accessor for the drill). Raised on tap-in, tracks the local
   vessel while it holds the stick, stops counting (rings stay) on tap-out, struck with the
   arena, and re-raised on an intensity nudge that keeps the arena standing (the course is
   per-intensity even where the cell is not). Events: `OnGateThreaded(ring, total)`,
   `OnLapCompleted(lap, seconds)`, `OnRaceCompleted(seconds)`; a finished race loops. The lap
   rule (`IsLapBoundary`) is proven against the match's fold in `RaceCourseSourceTests`.
3. Gate-race preview objectives become `SwitchesThreaded` with a real target, so the launch
   panel's objective box counts gates like every other mode, and the four "OPEN-ENDED" preview
   notes retire.

   **Half landed.** The seven gate-race definitions already author `ObjectiveMetric` 9
   (`SwitchesThreaded`), so with the local count feeding the runner the objective box now counts
   gates with no asset edit. `ObjectiveTarget` stays 0 on purpose: the box never shows a target,
   and a looping race has no finish to stop counting at. The Notes now describe both phases
   (phase 2, §10.3), re-authored by `author_mode_previews.py` and `author_waystation_assets.py`.
   Regatta's Note was already accurate (the rings are not in the scale model; the rails are), so
   its generator was left alone.

---

## 8. Racing = Time genre: read it off `ModeGenre`

**The genre petals landed on `bleeding-edge` on 2026-09-26** (`4cb00bf2` … `cb04c3f3`, merged in
PR #911). The classifier is `CosmicShore.Data.ModeGenre.TryElementsFor(mode, metric, out primary,
out secondary)`:
- It is pure and Unity-free, and lives in the `CosmicShore.Data` leaf assembly.
- It is keyed on the MODE, with the scoring METRIC as the fallback.
- It is pinned by `ModeGenreTests`.

A mode is racing when **either** petal is `Element.Time`. So `DrillLibrarySO` never carries the
D4 constant at all: the transitional "constant + agreement test" step planned here is dropped. The
drill composer calls `ModeGenre` directly, with the metric resolved from the card's `ScoringRuleSO`,
exactly as `GameCard.ResolveGenrePetals` does.

**What it says, compared with the list assumed in D4:**

| Card | Why it is Time | In the D4 list? |
|---|---|---|
| Skim Race | `Crystals` | yes |
| Switchback, Headlong, Redline, Skein, Breakwater, Regatta | `SwitchesThreaded` | yes |
| **Astro League** | `Goals` | **no — new** |
| **Scarab Scramble** | `Goals` | **no — new** |
| any FUTURE card scored on `Crystals` / `OmniCrystals` / `ElementalCrystals` | metric fallback (only Skim Race and Scurry use one today) | n/a |
| Scurry (`Crystals`), Tollway (`Goals`) | explicit rows override them to **Mass** | correctly absent |

All seven assumed cards are Time, so nothing already planned moves. The additions have three
consequences:

1. **The Mentor's racing playlist must not assume GATES.** Astro League and Scarab Scramble have
   no ring course, and neither would a future crystal race. Racing tips therefore key on the metric family, which the
   Mentor already does:
   - "thread the next gate" belongs only to `SwitchesThreaded`;
   - "reach the crystal first" belongs to the crystal metrics;
   - "put it through" belongs to `Goals`.
   The practice lap (§7) stays scoped to the `GateRaceController` modes. It is a property of having
   a course, not of being Time-genre.
2. **Game of the Week eligibility widens to every Time card**, including the two ball courts. They
   are single-hull, like the rest, so the free-vessel-for-the-week rule is unchanged. Whether a
   ball court should rotate as a "racing" Game of the Week is a product call. It is recorded in §11.
3. **A two-petal Time card, if one ever ships, still counts as racing.** No shipped card is one
   today. Brood Rush is Mass + Space.

The Lesson is unaffected either way, because it teaches the ship, not the game. Non-racing
microgames get the same two sections, with their own metric family's tips.

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
| 1b **(landed)** | `DrillRunner`, `DrillCoachView`, the window's release hold and the card's Play gate (§10.2) | a forced Lesson in the window, then the Mentor |
| 1a **(landed)** | The data layer (§10.1): beats, conditions, tokens, composer, hull facts, progress store + `DRILL_PROGRESS` cloud key, the library and both Lesson templates | the Lesson composes for any hull, offline |
| 2 **(landed)** | §7 gate course in previews; start lines, Breakwater's stations, Notes (§10.3) | the race is in the window |
| 3 **(landed)** | The Mentor: TipListSOs for the gate-race family, race Moment conditions, resume (§10.4) | the curated tutor |
| 4 **(landed, re-cut)** | Quest Graph P0 first-login guide; the Game of the Week source (§10.5, §10.6) | first login → Lesson, end to end |
| 5 | Remaining races; tip lists for non-racing families; relevance-gated leaderboards (§6); practice-lap result | coverage |
| 6 | Authoring window, coverage test, analytics (genre already read from `ModeGenre`, §8) | the "every vessel ever" guarantee |
| later | `GhostDemo` cue (the preview's own autopilot flies a step once before handing over) | show, don't tell |

### 10.1 Phase 1a - what landed

All under `Assets/_Scripts/Controller/Arcade/Preview/Drill/` unless noted.

| Piece | File | Note |
|---|---|---|
| Beats | `DrillBeats.cs` | `DrillBeat` / `LessonStep` / `MentorTip`, plus `FlightScheme`, `DrillAnchor`, `DrillCue`, `DrillApplicability`, `MentorTier`. A beat about no element uses `Element.None` |
| Conditions | `DrillConditions.cs` | `[SerializeReference]` `DrillCondition` + nine kinds (steer, throttle, speed, drift, input, ability, skims, gates, timer). Per-run state lives in a `DrillConditionState` the runner owns, so an authored asset is never written at runtime. Everything they read comes through `IDrillSignals`, whose counters are monotonic: a condition baselines at `Begin` and nothing is reset between steps |
| Tokens | `DrillTokens.cs` | Two failures kept apart: a MISSING FACT (the hull has no such ability) drops the line; an UNKNOWN TOKEN (a typo) renders as itself and logs once |
| Hull facts | `DrillHullFacts.cs` | Ability words from the ability map; the control from `InputHintBindingMap`; the WORD for it from the glyph set (keyboard) or the library (pad). `FullSpeedStraightAction` is the passive sentinel, as the lockup reads it |
| Composer | `DrillComposer.cs` | Pure. Lesson: hull replacement or the scheme's template, minus suppressed ids, applicability, unresolvable lines. Mentor: hull tips, derived Charge/Mass/Space tips, metric or mode tips, advanced tips; tier, priority, authored order; seen tips go to the END; first copy of an id wins |
| Assets | `LessonTemplateSO.cs`, `TipListSO.cs`, `DrillLibrarySO.cs` | Library at `Resources/DrillLibrary` |
| Progress | `DrillProgressStore.cs`; `System/CloudData/Models/DrillProgressCloudData.cs`; `.../Repositories/DrillProgressRepository.cs` | Cloud key `DRILL_PROGRESS` plus a PlayerPrefs mirror. Every fact only grows, so a read MERGES the two (OR / union / min) - a slow cloud load cannot re-lock a Lesson finished on this machine |

Proof: `Tools/Build/drill_harness/run.sh` compiles the shipped files (plus the real ability map,
glyph set and binding map) against a UnityEngine stub and runs 52 checks; four negative controls
(the skip rule, seen-to-end ordering, missing-fact detection, the passive sentinel) each fail it.
Edit-mode: `Tests/Editor/DrillComposerTests.cs`. **Nothing has been compiled in the Unity
editor.**

### 10.2 Phase 1b - the runner, the coach, the gates

| Piece | File | Note |
|---|---|---|
| Runner | `Drill/DrillRunner.cs` | Plain MonoBehaviour the session adds beside `ModePreviewRunner`. Started in `EnterFlightAsync` right after `StartRunner` (the stick is already granted); stopped at every exit the objective runner stops at (tap-out, `Stop`, `AbortHard`). Phases Lesson -> Mentor -> Done. It is its own `IDrillSignals`: speed, the drift flag, the throttle axis (`XDiff`) and the larger eased stick come off the live vessel; presses come off `R_VesselActionHandler.OnInputEventStarted`, and an ABILITY use is a press on the input that ability's map entry names; gates come off the preview's own `ModePreviewGateCourse.Threaded`. Skip is offered after the library's delay only when the Lesson is skippable (D7, or a party guest). A completed Lesson records its scheme's keys; a skipped one records nothing. The Mentor marks a tip seen the moment it is SHOWN. A device switch re-says the current line in the new device's words (`InputController.ActiveDeviceFamily`, now exposed read-only) |
| Coach | `UI/View/DrillCoachView.cs` | Generated on first use under the window's picture rect (`ModePreviewWindow.SurfaceRect`), because a coach that must be placed in every window host is one some host will be missing. A panel along the bottom fifth: title, the line, step pips, the control chip (pad artwork from `ControlGlyphSet`, keyboard label from its row - the lockup's own chain), and Skip / Next. Font taken from the window's status label. Redraws on `DrillRunner.OnChanged`; no Update |
| Release hold | `ModePreviewWindow.HoldRelease` | While true, Escape / pad Start / a tap outside do nothing. EVERY other route out (card change, modal close, launch, scene change) still releases - the hold delays a player and never traps one |
| Play gate | `ArcadeGameConfigureModal.RefreshStartAvailability` | A third condition beside the vessel gate and the weekly lock: `ModePreviewSession.DrillHoldsPlay`, re-decided on `OnDrillGateChanged`. The caption beside the greyed button is the library's `PlayLockedCaption` |
| Names | `ModePreviewSession.SetDrillNames` | The modal passes the card's `DisplayName` and, for a single-hull card, that hull's `SO_Vessel.Name`. A multi-hull card passes no vessel name, so a line quoting `{vessel}` is dropped there |

Proof: the same harness now also drives the SHIPPED runner through scripted visits (forced
first Lesson with hint, device switch, wrong button, the skipped Skims step, drift, Mentor
pacing with a Moment and Next, closing line; a skippable later visit with Skip; a party guest;
a vessel destroyed mid-run; a missing library) - 80 checks, and five negative controls each fail
it (hold, skip gate, unsubscribe, seen-marking, party guest).

**Stated gaps, all for later phases or the editor pass:**
- **No pad Skip.** Skip and Next are buttons on the panel; a pad player skips by releasing
  focus (Start), which is allowed whenever the Lesson is skippable. A dedicated pad binding needs
  a button flight does not use, and Select is the screenshot director's.
- **`PulseAbilityRow` is not drawn.** The cue is carried and the chip shows the control, but the
  launch panel's controls block is not pulsed yet.
- **A Skims step is skipped live.** No per-vessel skim signal reaches the runner (the skim event
  is a scene-wired SOAP asset shared by every vessel), so a Skims condition would hold a forced
  Lesson forever; the runner skips it with a warning. None of the seeded steps uses it.
- ~~**No resume.**~~ Landed in phase 3 (§10.4).
- ~~**Not reached by a first login.**~~ Phase 4 guides a new player to the window (§10.6); the
  Lesson starts when they tap in, as on any preview.

### 10.3 Phase 2 - the race in the window

§7's course landed earlier (2026-09-29). Phase 2 closed the three gaps it had stated.

| Gap | Fix |
|---|---|
| The pilot spawned on the cell ring, not the race's start line | `RaceCourseSource.TryStartLine` names the point the race starts from. By default it is gate 0; Skein uses `SkeinCourse.StartPose`. `StartLineStandoff` backs the pilot off it (220 by default, Regatta 260), and the two controllers that read a standoff now initialise from those constants. `ModePreviewGateCourse.TryGetStartPose` turns it into a pose. `ModePreviewSession.EnterFlightAsync` now raises the course BEFORE parking the vessel, so the hull lands on the line, facing gate 0's axis. Modes without a course still park at `ModePreviewArena.SpawnPose` |
| Breakwater's stations (the dishes) were not stood | `RaceCourseSource.PoseCourseStructure(structure, course, cellCentre)` is the seam the controller's `OnCourseRaised` and the preview now share. Breakwater's override lives in `BreakwaterCourseSource.Structure.cs`, a partial kept apart so the course harness need not compile `SpawnableBreakwater`. `ModePreviewDefinitionSO.CourseStructurePrefab` names the prefab. `author_mode_previews.py` reads it off the scene controller's `arenaPrefab`, so the preview and the match cannot point at different stations. `ModePreviewArena.BuildCourseStructure` spawns it under the arena root. Teardown follows the track structure's path: the strike retires it with the world, and an intensity nudge strikes it with the rings |
| Notes still said OPEN-ENDED / no rings | Re-authored for Switchback, Headlong, Breakwater, Skein, Redline and Waystation to describe both phases |

Proof: `Tools/Build/race_course_source_harness` (1600 course pairs, 0 failures; it gained a
`Component` stub for the new seam), the drill harness (80), the eight standing gates, and a Roslyn
syntax pass over every changed file. Both generators' `--check` pass. **Nothing has been compiled
in the Unity editor.** Stated: a structure prefab carrying a `NetworkObject` is refused rather
than spawned; `SpawnableBreakwater` carries none.

### 10.4 Phase 3 - the Mentor's race tips, and resume

| Piece | File | Note |
|---|---|---|
| Race moments | `Drill/DrillConditions.cs` | `GateAheadCondition` (the next ring within an angle of the ship's COURSE and between two times away) and `LapsCompletedCondition`. "Away" is SECONDS at the ship's current speed, not distance: one authored number then means the same moment on a 60 u/s hull and an 840 u/s one, in a small arena and a big one. A wide angle and a short time reads as "a ring is coming up"; a narrow angle and a long time as "on a straight" |
| Their signals | `IDrillSignals.LapsCompleted` / `TryGetNextGate` | Filled by the runner from the preview's own course: `ModePreviewGateCourse.LapsCompleted` (monotonic, like `Threaded`) and `TryGetNextGate` (the lit ring's centre). Angle is measured off `IVesselStatus.Course`, which differs from the nose during a drift |
| Tips | `Assets/_SO_Assets/Drills/Tips/` | `Tips_SwitchesThreaded` (six tips for every gate race: order, lining up, best-pilot scoring, turning early, straights, learning the course) plus one short list per race mode: Switchback, Headlong, Redline, Breakwater, Skein, Waystation, Regatta. Mode lists are ADDED to the family's (`ReplacesMetricTips` off) |
| Resume | `Drill/DrillResume.cs` | Where each (mode, hull) visit stopped: the Lesson step, the next Mentor tip, or Done. Stopping records it, beginning reads it, so a tap out to read the card and straight back in carries on. Session memory only: the persistent half (finished Lessons, tips said) was already `DrillProgressStore` |

Two writing rules every seeded tip follows, and any new one should:
- **A tip must read true even when its moment never came.** A moment that does not arrive within
  the library's 14 s lets the tip go anyway (D10), so "Lap done!" would be read out with no lap.
- **A mode tip names only that mode's own hull.** Regatta seats every hull, so its tip is about the
  arena (each team's coloured rail), never about one ship's controls.

`author_drill_assets.py` seeds the tip lists only while they do not exist, and ADDS the library's
mapping rows to an existing library without touching anything else (a writer's edits stay). Its
`--check` adds: tip ids unique across every list (the seen memory is keyed on id), every moment
reference resolves, every seeded list is mapped, and the enum values it writes match the C#.
`--self-test` has 11 negative controls, plus a proof that the merge keeps a human edit and is
idempotent.

Proof: the drill harness drives the shipped runner through resume at Done, resume on a tip still
waiting for its moment (with the seen list cleared, so only the resume mark can explain it),
resume mid-Lesson, per-card isolation, and the gate signal (seconds at speed, angle off the
course, a stopped ship, no lit ring) - 97 checks. Four negative controls each fail it: resume
ignored, a waiting tip skipped, angle off the nose, a Lesson restarted. **Nothing has been run
in the editor.**

Stated: every tip line was checked against the mode docs, not against play. The Headlong and
Redline lines describe how the Rhino's ramp boost and the Manta's Soar trade speed for turn; if a
playtest finds them wrong, they are fields in their `Tips_<Mode>` asset.

### 10.5 Phase 4 - the first-login railroad (superseded by §10.6)

> **Kept as a record.** The open node, `TrySelectMode` and `ArmForcedEntry` below are DELETED; the
> phase asset is renamed `MainQuest_Phase0_FirstLogin.asset` and the generator
> `author_first_login_guide.py`. See §10.6.

| Piece | File | Note |
|---|---|---|
| Game of the Week source | `ScriptableObjects/GameOfTheWeekSO.cs`, `Resources/GameOfTheWeek.asset` | The minimal source this plan assumed: an authored, ordered rotation stepped once per UTC week (weeks start Monday, counted in whole days from a fixed Monday, so no calendar or culture is involved), with a fallback for an empty list. Seeded with the seven racing cards: Skim Race, Switchback, Headlong, Redline, Breakwater, Skein, Waystation. The real rotation is a separate thread; it replaces this asset's contents or its `Current()`, and nothing that reads it changes |
| Open the microgame | `FTUE/.../Nodes/QuestOpenMicrogameNode.cs` | Opens the Arcade, selects the card through the new `ArcadeExploreView.TrySelectMode` (which reuses `FindGameByMode` and `SelectGame`, so a progression lock on the card is bypassed exactly as the weekly challenge bypasses it), and arms `ModePreviewSession.ArmForcedEntry`, which flies the vessel in the moment that card's preview goes live, as if tapped. With `holdUntilLessonEnds` it stays on this node until the Lesson ends |
| Wait for the Lesson | `FTUE/.../Nodes/QuestWaitForLessonNode.cs` | The wait itself, also usable on its own. Ends on `DrillProgressStore.CompletedAnyLesson` (the key that forces the first Lesson) or on the new `DrillRunner.AnyLessonEnded` (a party guest's skip, which sets no key) |
| The phase | `FTUE/DataContainer/Phases/MainQuest_Phase0_Railroad.asset` | Lock nav to Arcade -> open the Game of the Week, forced, holding -> unlock nav -> phase end. `MainQuest` phase 0 now points here; the old flight-school Phase 0 asset stays on disk, unreferenced |
| Per-phase gate opt-in | `QuestPhaseGraphSO.runsUnderDeveloperUnlock`, `QuestGraphRunner` | The railroad PHASE opts in, not the quest. With the master developer unlock on (the default), Phase 0 runs (its lock nodes pass through) and the runner stands down at Phase 1, marking nothing; with the gate off, the old phases 1-5 follow as before |

**Why the open node holds instead of handing to a wait node.** A quest resumes at its saved
node. With a separate wait node, a new player who quits mid-Lesson would resume on a home screen
with nothing open and navigation locked. Holding on the open node means a resume walks them in
again.

Authored by `Tools/Build/author_first_login_railroad.py`. It writes the rotation and the phase
only while they do not exist (both are for a designer to edit), and enforces the MainQuest
re-point every run. `--check` verifies that phase 0 is the railroad, that it opts in, that its
edges and node scripts resolve, that the open node holds, and that every rotation mode has an
arcade card and a flyable preview. `--self-test` has 6 negative controls.

Proof: the drill harness gained the week arithmetic (Monday rollover, wrap, a pre-epoch date,
the empty-rotation fallback; 102 checks, a negative control on the pre-epoch floor fires). The
two nodes and the rotation type-check against the real `QuestNodeSO` with stubs for the rest.
**Nothing has been run in the editor**, and this phase cannot be judged without it: the walk-in
crosses the arcade screen, the configure modal and the preview, which no harness reaches.

To try it, in play mode: reset quest progress in the Quest Graph Editor, and run **FrogletTools >
Quest Graph > Reset Microgame Lessons (testing)** (`DrillProgressStore.ResetForTesting`, which
clears the local AND the loaded cloud copy, since a read merges the two). Then leave and re-enter
Menu_Main. Stated gaps:
- **`QuestDefaultContentBuilder` still seeds the old Phase 0.** It refuses to run while a
  MainQuest exists, so it cannot overwrite this one, but a re-seed from scratch would build the
  flight school again.
- **Phases 1-5 are unchanged** (§11, item 1). With the gate off they still follow the railroad
  and still assume a Crystal Capture funnel.

### 10.6 Phase 4, re-cut - guide, don't carry

The railroad carried the player: it opened the Arcade, pressed the card and flew the vessel in by
itself. The product rule (`Docs/HomeHub/ARCHITECTURE.md` §8, also in CLAUDE.md) is the opposite:
signal the one thing, dim and disable everything else except Settings, and let the player press
each step themselves, so they learn where the thing lives.

| Piece | File | Note |
|---|---|---|
| Guide state | `UI/Guide/MenuGuide.cs` | Static: on/off, the card it leads to, the always-available windows (SETTINGS, CREDITS) and the controls that open them - DERIVED from each Button's inspector-wired onClick, plus `MenuGuideAlwaysAvailable` for anything else |
| Spotlight | `UI/Guide/MenuSpotlight.cs`, `SpotlightDimGraphic.cs`, `SpotlightFrameGraphic.cs` | Own overlay canvas built in code (sort order 32000). The dim is an `ICanvasRaycastFilter` that refuses every press except inside a cut-out, so the real control underneath is what gets pressed. CTA frame in the palette's CTA colour, pulsing; caption from the node. Fades in/out; the cut-out glides between steps. Holds a pad selection on the path |
| The node | `FTUE/.../Nodes/QuestGuideToMicrogameNode.cs` (the old open node's file and guid) | Re-derives the step every frame: flying or Settings open → stand aside; the target card's preview open → the window; Arcade open → the card (scrolled into view, `ArcadeExploreView.TryRevealCard`); home → the Arcade hub entry, else the footer Arcade button. Fails open after `lostStepGraceSeconds`. Not `AppliesLock` |
| Menu hooks | `ScreenSwitcher`, `ModalWindowManager`, `ArcadeExploreView`, `MenuHubButton` | While a guide is on: pad Y (freestyle) and the triggers (paging) stand down; B closes only Settings; the card the guide leads to is pressable even if progression locks it (`MenuGuide.ExemptsMode`); `MenuHubButton.Target` is readable |
| Removed | `ModePreviewSession.ArmForcedEntry`, `ArcadeExploreView.TrySelectMode` | Deleted, so nothing can carry a player again by reaching for them. `ModePreviewSession.Window` is exposed instead, for the spotlight to point at |
| The phase | `MainQuest_Phase0_FirstLogin.asset` (renamed, guid kept) | Guide → phase end. The two nav-lock nodes are gone: the dim does that job, and does it for every button rather than a wired list |

`author_first_login_guide.py` replaces a phase written by its older version (one without a guide
node), and `--check` now FAILS if the first-login phase carries a `Navigate`, `EnterFreestyle` or
open-microgame node. `--self-test` has 7 negative controls.

Stated, and only the editor can settle them:
- **Nothing here has been run in the editor.** The spotlight crosses the home screen, the Arcade
  modal and the launch panel, which no harness reaches.
- **The cut-out is the target's bounding rectangle**, inflated a little, not its shape. A
  trapezoid hub button reads inside a rectangle; if that looks wrong, the frame graphic is where a
  shaped cut-out would go.
- **Settings is found from its onClick.** Menu_Main's `SettingsButton` wires
  `SettingsModal.ModalWindowIn` directly, so it is found; a Settings control that opens the window
  some other way needs `MenuGuideAlwaysAvailable`.
- **A wandering pad selection is put back** only while a gamepad is connected, so a mouse player
  never sees a selection highlight appear on the target.

---

## 11. Still open

1. **What happens to Main Quest phases 1–5** (the Crystal Capture funnel and the unlock chain) now
   that first login goes to the Game of the Week — separate thread, but they currently assume P0
   ended in freestyle.
2. **Mentor pacing** is set (D10) but wants one playtest to confirm it reads as a friend, not a
   billboard.
3. **Do the ball courts rotate as Game of the Week?** `ModeGenre` makes Astro League and Scarab Scramble Time-genre (`Goals`). The drill system handles them either way (§8). Whether the weekly *racing* rotation should include them is a product call.
