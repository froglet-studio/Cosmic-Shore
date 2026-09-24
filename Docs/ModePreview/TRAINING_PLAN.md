# Microgame Training — design plan

**Status: PLAN, not built.** Written 2026-09-24 to choose an architecture for teaching players
*inside* the Mode Preview ("microgame") window. Nothing here has run in the editor. The
"Game of the Week" rotation itself is a separate thread; this plan only assumes it will name a
`GameModes` value (and that the week's hull is that card's locked `Vessel`).

---

## 0. The ask, restated as requirements

1. A first-time player is railroaded to the **Game of the Week's microgame** and put through a
   **forced (or semi-forced) training moment** there.
2. Every **racing** microgame gets a real FTUE, for **every vessel we have or will ever build**.
3. Every **other** microgame gets a lighter set of instructions + tips.
4. Light on performance and code, easy to author, and the next vessel/mode must not need a
   programmer to be covered.

Two facts about the codebase shape every option below:

- **The microgame is already a well-bounded venue.** `ModePreviewSession` owns a satellite arena,
  a hull swap, a camera loan into `ModePreviewWindow`'s RenderTexture, an input-focus handoff
  (`ModePreviewWindow.AnyHasFocus`, four gates), and `ModePreviewRunner` — a plain MonoBehaviour
  that already counts ONE `ScoringMetric` against a baseline and raises
  `OnObjectiveProgress`. A training layer rides on top of that; it builds none of it.
- **Racing microgames currently have no race in them.** `Docs/ModePreview/ARCHITECTURE.md §6`:
  Switchback, Headlong, Redline, Breakwater preview **shell-only** — their rings are solved by the
  `GateRaceController` at match start, and the satellite has no controller. Skein and Regatta show
  their rails but no rings. Skim Race is the only race with its track and crystals in the window.
  **A racing FTUE needs rings in the window first** — that is prerequisite §5, independent of
  which training paradigm wins.

---

## 1. The one decomposition that makes "every vessel ever" tractable

Whatever runs the training, the *content* must not be authored per (mode × vessel) — that is
~7 races × 11 hulls today and grows multiplicatively. It splits into two independent kinds of
knowledge, and each is keyed on something the platform already treats as canonical:

| Knowledge | Keyed on | Where it comes from | New vessel/mode cost |
|---|---|---|---|
| **How to fly THIS HULL** (its 4 abilities, their controls) | `VesselClassType` | **Derived** from `ElementalAbilityMapSO` — `AbilityLabel`, `AbilityDescription`, `Input` → `InputHintBindingMap` → `ControlGlyphSetSO` glyph. The same chain the ability lockup and the launch panel's controls block already use, so a wrong control label is structurally impossible | **Zero** — a hull with a filled map is taught on day one. An optional authored module adds verbs a map cannot express (the Dolphin's "skim, then fly into a crystal"; the Rhino's ramp) |
| **How to win THIS MODE** (thread gates in order, lap, boost the straight; or destroy / collect / hit) | `ScoringMetric` family, overridable per `GameModes` | Authored **templates per metric** — the same key `ObjectiveIconSetSO` and the goal stack already use ("never on the game mode") | **Zero** for a mode that reuses a metric (every gate race is `SwitchesThreaded`); one template for a genuinely new metric |

Persist them **separately**. A player who learned the Sparrow in Dog Fight is not re-taught the
Sparrow in Breakwater — only "thread the stations". A player who learned gate racing in
Switchback is not re-taught gate racing in Redline — only the Manta. That is what keeps the
forced moment short enough to stay forced.

This decomposition is the load-bearing part of the plan and is shared by all three options.

---

## 2. Three roughly-equal options

### Option A — Extend the Quest Graph into the microgame

Add a `Microgame` `QuestVenue`, new gate nodes (`WaitForGateThreaded`, `WaitForAbility`,
`WaitForMetricDelta`, `WaitForLapTime`), a `CoachMark` presentation node anchored to the preview
window, and a **second runner instance** hosted by `ModePreviewSession` rather than Menu_Main.

- **For:** the visual editor, node validation, enable toggles, checkpoint view and Force-Advance
  already exist; designers already know it; branching (fail → retry port) is native.
- **Against:**
  - The runner is a Menu_Main singleton with ~20 scene references and ONE persisted cursor per
    quest; `QuestRuntimeContext` is menu-shaped (nav buttons, game cards, freestyle events). A
    microgame runner needs a second context type and a different persistence model
    (node-by-node UGS resume is wrong for a 60-second drill — you restart a drill, you don't resume
    it at node 7).
  - A graph is an authored artifact, so it answers requirement 2 badly: either one graph per
    (mode × vessel) — the multiplication §1 exists to avoid — or graph "includes" and
    parameterised nodes, which is a macro system bolted onto a tool that has none.
  - `DeveloperUnlockGate.AllUnlocked` (default ON) **stands the whole quest graph down**, so in a
    default checkout nothing would run. Solvable, but it couples drills to a switch whose job is
    entitlement.
  - Coroutine-per-node; fine at this scale but it is the heaviest runtime of the three.

### Option B — A linear DRILL layer owned by the preview (new, small)

A **Drill** is an ordered list of **beats**; each beat is a prompt + a completion condition. A
`DrillComposer` builds the list at arm time from the §1 sources (mode template ⊕ derived vessel
beats ⊕ optional authored vessel module), minus beats the player has already learned. A plain C#
`DrillRunner` (sibling of `ModePreviewRunner`, same lifetime) ticks the one active beat.

- **For:** covers every vessel by construction; smallest runtime (one active condition, event-driven
  where a SOAP channel exists); lives entirely inside the preview's existing lifecycle so focus,
  teardown, party and satellite rules are inherited, not re-derived; no Menu_Main coupling.
- **Against:** linear only (a beat can repeat or offer a hint, not branch); needs its own authoring
  surface (a coverage matrix window, §6) instead of a canvas; the editor Quest Graph's checkpoint /
  force-advance niceties must be re-provided (cheaply — a drill is a list).

### Option C — Reactive COACHING rules (no sequence at all)

No lesson order: a set of `(trigger condition → tip)` rules evaluated while the player flies —
"missed gate 2 by >40u → *ease the stick; your turn radius grows with speed*", "8s without
boosting → *hold RT on the straights*", "hit a danger prism → …". Each tip shows once per
(player, tip) and a cooldown spaces them.

- **For:** cheapest content per insight, never blocks, excellent for requirement 3 (non-racing tips)
  and for players who already know the basics; scales by metric/vessel the same way.
- **Against:** cannot *force* anything — there is no "you must do X before Y", so it fails
  requirement 1 on its own; tips fire on failure, which is a worse first impression than a guided
  success.

### Evaluation

| | A — Quest Graph | B — Drill layer | C — Reactive rules |
|---|---|---|---|
| Forced first-time moment | ✅ | ✅ | ❌ |
| Every vessel, zero per-vessel authoring | ⚠ needs a macro/include system | ✅ derived | ✅ derived |
| Runtime cost | coroutine per node, second runner | one condition object per frame, mostly event-driven | N conditions polled (cap it) |
| New code | medium — context split, venue, 5–6 nodes, second persistence model | small — runner, composer, ~10 conditions, 1 view | small — rule evaluator, ~10 triggers, 1 view |
| Authoring ease | best canvas; worst for coverage | list + coverage matrix | list of rules |
| Coupling risk | Menu_Main runner, `DeveloperUnlockGate`, UGS cursor | preview only | preview only |
| Branching / remediation | ✅ native | ⚠ retry + hint only | ✅ is all it does |

---

## 3. Recommendation: B, with C folded in as a beat kind, bridged to the Quest Graph

**Build the Drill layer (B)** and give its beat model a second kind — **tips** — which are C's
trigger-driven rules. One system, two kinds of beat, one condition vocabulary, one view.
**Keep the Quest Graph for what it is good at — the macro journey across the app shell** — and
connect the two with two nodes. That split mirrors the graph's own venue concept: the graph owns
"where is the player standing", the drill owns "what is the player doing in the arena".

```
Quest Graph (app shell)                          Drill layer (inside the preview window)
───────────────────────                          ─────────────────────────────────────
... → OpenMicrogame(GameOfTheWeek) ──arms──►  ModePreviewSession.SetDefinition(...)
                                                  └─ DrillComposer.Compose(mode, hull, progress)
                                                       → [ mode beats ⊕ vessel beats ⊕ tips ]
                                                  └─ DrillRunner ticks the active beat
      WaitForDrill(key) ◄──── completes ────────  DrillProgressStore.MarkLearned(...)
... → (launch the real game / leaderboard CTA)
```

Why not A: the per-(mode × vessel) content problem is the hard requirement, and the graph is the
wrong shape for it; everything the graph would add inside the arena (branching) is better served
by B's retry/hint plus C's tips. Why not C alone: it cannot force.

---

## 4. Architecture (Option B + tips)

### 4.1 Data

```
DrillBeat  [Serializable, polymorphic via [SerializeReference]]
  Kind            Step | Tip
  Prompt          string with tokens: {glyph:Charge} {ability:Space} {vessel} {target} {metric}
  Anchor          Window | AbilityRow(Element) | ObjectiveArrow | NextGate
  Condition       IDrillCondition  (completion for a Step, trigger for a Tip)
  Cue             None | PulseAbilityRow | HighlightNextGate | GhostDemo (later)
  MinShowSeconds, HintAfterSeconds + HintPrompt, SkipAfterSeconds
  LearnKey        e.g. "vessel/Manta/Space", "mode/SwitchesThreaded/order" — what completing it teaches

DrillTemplateSO           one per ScoringMetric family (+ optional per-GameModes override)
                          ordered Step beats + Tip beats for "how to win this mode"
VesselDrillModuleSO       OPTIONAL, one per VesselClassType: extra verb beats + tips the map
                          cannot express, and per-element overrides of the derived beat
DrillLibrarySO            Resources/DrillLibrary — metric→template, mode→override, hull→module,
                          the forcing policy table (§4.4)
```

`[SerializeReference]` rather than the Quest Graph's SO sub-assets: a beat is small, owned by one
list, and never referenced from elsewhere, so a polymorphic field is lighter than an asset per
beat. (If designers later want beats reusable across templates, promote them to SOs then.)

**Derived vessel beats** (no asset): for each `ElementalAbilityEntry` with `Input != 0`, in
`VesselHUDView.AbilityDisplayOrder`:
`Step{ Prompt = "{glyph} — {AbilityLabel}: {AbilityDescription}", Condition = InputPressed(Input),
Cue = PulseAbilityRow(element), LearnKey = "vessel/{hull}/{element}" }`. Passive abilities
(`Input 0`) become a Tip with a timer trigger instead of a Step — a player cannot "press" a passive.
Flight basics (throttle, turn, drift) are ONE shared derived block keyed on
`IsSingleStickControls` (two schemes, not eleven), learned once per player.

### 4.2 Conditions — one small vocabulary, reused by Steps and Tips

Each is a tiny class implementing `Begin(DrillContext) / bool Evaluate() / End()`. Event-driven
ones subscribe to channels that already exist; polled ones read one field.

| Condition | Source it reads (existing) |
|---|---|
| `Timer(s)` | unscaled time |
| `InputPressed(InputEvents)` / `InputHeld(e, s)` | the `ScriptableEventInputEvents` the quest runner already uses, filtered to the local vessel |
| `SpeedAtLeast(u/s)` / `SpeedFraction(of top)` | `VesselStatus.Speed` |
| `DriftHeld(s)` | `VesselStatus.IsDrifting` (as `QuestWaitForDriftNode`) |
| `Skims(n)` | `ScriptableEventBoostChanged` (as `QuestWaitForSkimNode`) |
| `MetricDelta(metric, n)` | `ScoringMetrics.Read` against a baseline — lifted from `ModePreviewRunner` |
| `GateThreaded(n)` / `LapCompleted` / `GateMissed(by u)` | the preview gate course, §5 |
| `AbilityActivated(Element)` | the vessel's action handler start event (resolved through the map) |
| `NoInputFor(s)` / `NoBoostFor(s)` | tip triggers — the negative of the above |

Only the ACTIVE step's condition plus a capped set of armed tips (say ≤ 6, evaluated round-robin,
one per frame) are live. No per-frame allocation. Cost is noise next to the satellite arena the
preview is already running.

### 4.3 Runtime

- `DrillRunner` (plain MonoBehaviour beside `ModePreviewRunner`, created by the session) — starts
  on the tap-in arrival, stops on every existing exit route (release, card change, launch, strike).
  It never writes `GameDataSO` and never replicates: the preview is local by design and so is the
  drill. A party guest gets tips, never a forced drill (§4.4).
- `DrillComposer` — pure function `(mode, hull, DrillProgress, policy) → List<DrillBeat>`; edit-mode
  testable, and the same function drives the authoring matrix (§6), so what the tool shows is what
  runs.
- `DrillCoachView` — one panel overlaid on the preview window rect in the arcade modal: prompt
  line, the control glyph chip (drawn from `ControlGlyphSetSO` exactly as the lockup draws it),
  step pips, a Skip affordance. Anchors reuse surfaces that exist: the launch panel's controls-block
  rows already light when a control is held — `PulseAbilityRow` drives that same row; the
  objective arrow already points at the next gate.
- `DrillProgressStore` — a set of learned `LearnKey`s + seen tip ids + best practice-lap time per
  (mode, hull). PlayerPrefs mirror + one Cloud Save key through the existing repository pattern
  (`LocalCloudDataCache` gives offline for free). Deliberately NOT the quest cursor.

### 4.4 The forcing ladder

| Level | When | Behaviour |
|---|---|---|
| **Forced** | First ever preview entry (solo, not in a party), or when the Quest Graph's `OpenMicrogame` node asks | Window auto-focuses on arrival (no tap needed); outside-tap / Escape / Start release routes are suppressed; the card's Play button is disabled until the drill's Steps complete; **Skip appears after N seconds** (accessibility + the "I already know this" player), and a skip records the keys as *seen*, not *learned* |
| **Guided** | New mode or new hull for this player | Steps run with prompts; every release route works; leaving pauses the drill and re-entering resumes at the current step |
| **Tips** | Everything already learned, and every non-racing mode by default | No steps; tips fire from their triggers, once each |

Forced suppression is one flag read by the existing release path in `ModePreviewWindow`
(`WantsRelease`) — it adds no new gate, it holds an existing one shut. The ordering rule the
session already records (a teardown that runs while the app is LEAVING must forfeit its restore)
applies unchanged.

### 4.5 What a racing drill looks like (worked example: Manta in Redline)

Composed for a brand-new player, ~60–90 s:

1. *(flight basics, shared, one-stick block)* "Push {glyph:stick} to turn." → `InputHeld(stick, 1s)`
2. "Thread the first gate — follow the arrow." → `GateThreaded(1)`, cue `HighlightNextGate`
3. *(derived, Manta Time)* "{glyph:LT}+{glyph:RT} — Soar: …" → `AbilityActivated(Time)`
4. *(Redline template)* "Ease one trigger to carve the corner." → `GateThreaded(3)`, hint after 12 s
5. *(gate-race template)* "Finish the lap." → `LapCompleted`
6. Result card: "Practice lap 0:47 · this week's best 0:39 — beat it on the leaderboard" → Play.

For a player who already knows the Manta, the same card composes to 2, 4, 5, 6.
For a player who knows gate racing but not the Manta: 1 (if never learned), 3, 5, 6.

Step 6 is the bridge to the Game of the Week: the practice-lap time from the drill is a real
number to beat, which is the most direct activation available.

---

## 5. Prerequisite — put the race in the racing microgames

Independent of the paradigm, and the largest single piece of work:

1. **Lift course construction out of the controller.** `GateRaceController.BuildCourse(seed,
   gateCount, inner, outer)` is `protected abstract` on a NetworkBehaviour. The course generators
   behind it are already pure (`SwitchbackCourse`, `HeadlongCircuit`/`RedlineCourse`,
   `SkeinCourse`, `RegattaCourse`, Breakwater's builder — all have edit-mode tests). Introduce a
   per-mode `IRaceCourseSource` (the controller and the preview both call it) so there is ONE
   course definition per mode.
2. **`ModePreviewGateCourse`** — stands `RaceGateRing`s in the satellite from a local seed, tests
   crossings with the existing pure `RaceGateRing.CrossedMouth(prev, cur)`, and raises
   `GateThreaded`/`LapCompleted`/`GateMissed`. Local only; rings retire with the strike.
3. The preview objective for gate races becomes `SwitchesThreaded` with a real target, so the
   launch panel's objective box counts gates like every other mode.
4. `ModePreviewDefinitionSO` gains nothing new beyond a flag; the course is derived from the mode.

This also retires the "OPEN-ENDED" note on four preview definitions and makes the racing cards
honest previews even for players who skip training.

---

## 6. Authoring tool

**FrogletTools ▸ Game Modes ▸ Microgame Drills** — master/detail, following `Docs/TOOLING.md`:

- **Coverage matrix**: modes × hulls. Each cell is `DrillComposer.Compose(...)` for a brand-new
  player, coloured by status (✅ full, ⚠ derived-only, ❌ empty or unresolvable). This is the answer
  to "every vessel we will ever build": a new hull appears as a new column and is ✅/⚠ the moment
  its ability map exists.
- **Detail**: the composed beat list with each token resolved (actual glyph, actual ability name),
  per-source colour (template / derived / module), and inline editing of the template or module
  the beat came from.
- **Simulate as**: choose a learned-key set ("knows gate racing", "knows the Sparrow") and see the
  composed drill — the dedupe logic made visible.
- **Validate**: every token resolves; every `InputPressed` has a glyph for both pad and keyboard;
  every condition's signal exists in a preview (e.g. `MetricDelta(Crystals)` on a card whose
  preview mints no crystals is an error); every Forced drill has ≥1 Step and a Skip time.
- It is a READER + SO editor; it writes only the drill SOs, recorded through
  `FrogletToolChangeLedger` per the tool contract.

**Gates (CI-style, no editor):** an edit-mode `DrillCoverageTests` asserting every playable hull ×
every racing mode composes ≥ N Steps with all tokens resolved — the fleet-wide guarantee, so a new
vessel with an empty ability map fails a test rather than shipping a silent microgame.

---

## 7. Quest Graph bridge (two nodes)

- `QuestOpenMicrogameNode` — Navigate to Arcade → select the card (`Fixed mode` or
  `GameOfTheWeek` source) → arm the preview with `forced: true`. Venue: Gameplay.
- `QuestWaitForDrillNode` — completes when the drill for (mode, hull) reports done or skipped;
  ports `Completed` / `Skipped` so the graph can branch its follow-up dialogue.

Note the conflict to resolve: `DeveloperUnlockGate.AllUnlocked` (default ON) stands the quest
graph down. The railroad needs either that default flipped or the drill's forced entry triggered
without the graph (the `DrillProgressStore`'s "first ever preview" rule in §4.4 does exactly this,
so the forced moment works even with the graph off).

---

## 8. Analytics

Per beat: `drill_beat` with `{mode, hull, beat_id, outcome: completed|skipped|hinted, seconds}`,
plus `drill_complete` with the practice-lap time. Fold into existing parameters where possible —
UGS schema rows are permanent and capped (`Docs/Analytics/DATA_ARCHITECTURE.md`). This is what
answers "where do new players drop out of the Game of the Week funnel".

---

## 9. Phasing

| Phase | Scope | Proves |
|---|---|---|
| 1 | §5 gate course in previews for Switchback + Redline (one open chain, one circuit) | the race exists in the window |
| 2 | `DrillRunner`, `DrillComposer`, 6 conditions, `DrillCoachView`, gate-race template, derived vessel beats, `DrillProgressStore`; Guided level only | composition + dedupe on two modes × all hulls |
| 3 | Forced level + Skip; Quest Graph bridge nodes; practice-lap result card | the railroad end to end |
| 4 | Remaining races (Headlong, Breakwater, Skein, Regatta, Skim Race); tips kind + metric templates for non-racing modes | coverage |
| 5 | Authoring window + coverage test + analytics | the "every vessel ever" guarantee |
| later | `GhostDemo` cue (AI flies the beat once before handing over — reuses the preview's own autopilot) | show-don't-tell |

---

## 10. Open questions for the team

1. **Does Forced disable the card's Play button**, or only hold the window focus? (Disabling Play is
   the stronger railroad; it also blocks a party host from launching over a guest's drill — hence
   forced only when solo.)
2. **Skip time** — a fixed 10 s, or after the first Step?
3. **Are flight basics taught in the microgame or kept in freestyle** (the old Quest Graph P0
   flight school)? The plan assumes the microgame, since that is where players now land first.
4. **Which modes count as "racing"** for the Game of the Week? Assumed: Skim Race, Switchback,
   Headlong, Redline, Skein, Breakwater, Regatta (all time-scored).
5. **Practice-lap time vs the real leaderboard** — show the week's best from the weekly
   leaderboard service, or only the player's own best?
