# Rhino Sword Combos — trigger strings call flourishes

> Sits ON TOP of the analog swordsmanship in `RHINO_SHIELD_SWIPE.md` and changes none of it:
> holding a trigger still places the sword exactly where the finger says and keeps it there.
> Only a rapid string of **taps** calls a combo. The blade's cutting, energy and energize ritual
> are `RHINO_ENERGY_SWORD.md`; combos read the energized state and nothing else from it.

## What it is

Every two- and three-press trigger sequence has its own flourish — a unique authored path the
sword flies through before handing the pose back to the fingers — and every one has a second,
**upgraded** flourish that plays instead while the blade is ENERGIZED. 12 sequences x 2 sets =
**24 paths**.

A combo **opens toward its first press and ends on the side of its last press** (R = right,
L = left), so the fingers still read as left and right; what happens in between is the combo's
own. Both properties are asserted by the generator, not just authored.

| Seq | Base flourish | What it does | Energized flourish | What it adds |
|---|---|---|---|---|
| RR | **Twin Fang** | right cut, recoil up through centre, a deeper lower second cut | **Thunder Fang** | the second fang rolls flat and wheels into a full loop down the right side |
| LL | **Rising Crescent** | low left sweep rising into a crest overhead, leaning back | **Eclipse Crescent** | a second, wider crescent that finishes in a lunge |
| RL | **Scissor** | right, cross low through centre with a push, left | **Shear Storm** | four crossing shears, each lower, then a centre drive |
| LR | **Hook and Draw** | high over-rolled left hook, draw down, low right thrust | **Hook Lance** | the draw becomes a full-length twisting lance |
| RRR | **Cyclone** | one full clockwise turn with the blade raised and coned out | **Tempest** | two full turns, landing with a push |
| LLL | **Windmill** | one full counter-wheel beside the left of the hull | **Hurricane Wheel** | two wheels, landing with a push |
| RLR | **Figure Eight** | right, over the top, left, under, right again | **Infinity Blade** | the eight twice, the second faster |
| LRL | **Serpent Weave** | low strike left, snap high, low strike right, snap high, left jab | **Hydra Weave** | three striking heads, rear back overhead, slam down left |
| RRL | **Rising Reversal** | two stacked right cuts, overhead backhand, down on the left | **Meteor Reversal** | the backhand carries into a wheel-and-a-quarter down the left |
| LLR | **Low Sweep Uppercut** | two low left sweeps, rising right uppercut | **Skyfall Uppercut** | the uppercut goes over into a loop and drives down right |
| RLL | **Crosscut Spiral** | right cut, then one full roll spiral down the left | **Vortex Spiral** | two full roll spirals |
| LRR | **Lunge** | left feint, right cut, straight forward lunge | **Piercing Charge** | two lances, then a rising wheel off to the right |

Tip trajectories (top + side view, every path): run
`python3 Tools/Build/author_rhino_sword_combos.py --svg <file.svg>`.

## Input model — why a combo can never steal the analog control

`RhinoSwordComboDetector` (pure C#, edit-mode tested) turns trigger samples into combos:

- A **press** is a trigger reaching `pressThreshold` (0.5); it is released below
  `releaseThreshold` (0.2). Partial pulls under the press threshold are never taps.
- Consecutive presses link only within `comboWindowSeconds` (0.35).
- A press **held** longer than `tapMaxHoldSeconds` (0.3) is positioning, not a tap, and breaks
  the chain — so placing the sword and swinging it by hand is untouched.
- Two presses closer than `chordWindowSeconds` (0.08) with both triggers down are a **chord** —
  the energize stance. The chain clears and stays latched clear until BOTH triggers release, so
  feathering the stance can never leak a combo afterward.
- The two-letter combo fires **on the second press** (no waiting to see if a third comes). The
  chain stays open, and a third press within the window cancels into the finisher, blending from
  wherever the two-letter flourish had got to. A finisher then locks new presses out for
  `finisherLockoutFraction` (0.7) of its length so it plays out, and the chain restarts after.

A rolled tap (the second trigger pressed while the first is still coming off, past the chord
window) still links — that is how fast alternation is actually played.

## Pose model — one channel space for fingers and flourishes

The sword pose is four channels (`SwordPoseChannels`): yaw, roll, pitch (the existing
`Y(yaw)·R(roll)·P(pitch)` composition) and **thrust**, new, which slides the whole blade out
along itself as a fraction of its own hilt-anchor length (a lunge that reads the same at every
blade size). The analog drive is just `(diff*90, diff*90, sum*32.5, 0)` in that space, so a
flourish blends in from the current pose and back out to the **live** trigger pose with plain
lerps (`blendInSeconds` 0.06, `blendOutSeconds` 0.18). The fingers keep driving underneath the
flourish, so the blend lands where the triggers are when it ends, not where they were when it
began.

A path is sampled as a piecewise cubic Hermite through its keys (finite-difference tangents, zero
at the ends — `RhinoSwordComboPath.SamplePath`). A flourish that turns the blade a full
revolution ends at e.g. yaw 380; the end pose is **wrapped** per channel before the blend out,
which never changes what is drawn (a 360 turn about any one axis is the identity) and stops the
blend from unwinding a turn the flourish already paid for.

Swipe recovery (`swipeCooldownSeconds`) is cleared at the start and end of a flourish: the
flourish IS the swing, and it never owes the rhythm penalty a lone swipe does.

## Cutting, replication, feedback

- **Damage is untouched.** The blade cuts whatever it touches exactly as before (ordinary damage
  is ungated — locked). A flourish sweeps more space faster, and `SkimmerSwingKinematics`
  differentiates the pose it writes, so a whipped tip throws debris harder — clamped by the
  existing per-impact `debrisSpeedLimit`. No new damage path, no new collider.
- **Replication:** detection reads the replicated trigger MIRRORS (`InputStatus.LeftTriggerAnalog`
  / `RightTriggerAnalog`, Owner-write / Everyone-read) — the same values the energize stance reads
  — so the owner and every replica call the same combo to within a network tick of timing, with
  no new RPC. The energized variant is chosen from the local `IsEnergized`, which is
  local-authoritative (the known class recorded in `RHINO_ENERGY_SWORD.md`); a peer whose energy
  tally differs can play the base variant where the owner played the upgraded one. A tap shorter
  than one network tick can coalesce away on a replica (an edge is not a level across a tick).
- **Feedback:** `RhinoSwordFXController.NotifyCombo` flashes the blade (hit flash for a
  two-letter combo, pop flash for a finisher or energized combo) and strings a crackle along the
  blade (denser and hotter energized). No camera shake — combos are frequent.
- **Audio:** every path carries its own `EventReference sound` slot, shipped **empty** (silent)
  per the FMOD convention — 24 visible TODOs for the audio owner, one per flourish.

## Files

| Role | File |
|---|---|
| Detector (pure, tested) | `Executors/RhinoSwordComboDetector.cs` |
| Library SO + path + pose channels + sampler | `Data Containers/RhinoSwordComboLibrarySO.cs` |
| Playback (detect, blend in, path, blend out, thrust) | `Executors/ShieldSwipeActionExecutor.cs` |
| Wiring | `RhinoShieldSwipeConfigSO.comboLibrary` → `_SO_Assets/VesselActions/Rhino/RhinoSwordComboLibrary.asset` |
| Feedback | `Executors/RhinoSwordFXController.cs` (`NotifyCombo`) |
| Author + proof | `Tools/Build/author_rhino_sword_combos.py` (`--check`, `--self-test`, `--svg`) |
| Tests | `_Scripts/Tests/Editor/RhinoSwordComboTests.cs` |

**The library asset is generator-owned** — edit the `BASE` / `ENERGIZED` tables in the script
and re-run it, never the asset. Before writing, the script runs the shipped pose math over every
path and fails the build unless: all 12 sequences exist in both sets with well-formed keys; every
path opens toward its first press and ends on its last press's side; every pair of paths in a set
differs by at least 0.25 blade lengths of mean tip separation (time-normalized, so a retimed copy
fails) and each energized path differs from its own base by the same margin; every energized path
has at least 1.2x its base's tip travel; and no path passes the blade within 20 u of the pilot's
camera (read off `RhinoCameraSettingsSO`) at resting length. `--self-test` proves each gate fires.
The Python sampler agrees with the compiled C# `SamplePath` to 1e-4 degrees over every path.

## Tuning knobs (`RhinoSwordComboLibrary.asset`, via the generator's `DETECTION`)

| Knob | Default | Effect |
|---|---|---|
| `pressThreshold` / `releaseThreshold` | 0.5 / 0.2 | how deep a tap must go / how far it must come back |
| `comboWindowSeconds` | 0.35 | max gap between linked presses |
| `tapMaxHoldSeconds` | 0.3 | a longer press is a hold and breaks the chain |
| `chordWindowSeconds` | 0.08 | both triggers this close together = energize stance, not a combo |
| `finisherLockoutFraction` | 0.7 | share of a finisher new presses cannot interrupt |
| `blendInSeconds` / `blendOutSeconds` | 0.06 / 0.18 | hand-off into and out of a flourish |
| per path `durationSeconds` | 0.65 – 1.10 | flourish speed (energized paths run longer but carry far more travel) |

## In-editor verification

**Not yet run in the editor.** Out of editor: the new C# type-checks against a stub harness, the
edit-mode suite's logic runs 29/29 under an NUnit shim (the library-fallback test needs the real
`JsonUtility` and was not run), two negative controls prove the hold and chord tests isolate
their rules, and the generator's gates + self-test pass.

1. Freestyle, Rhino, gamepad. Hold RT half-way and sweep it slowly, hold both triggers to
   energize: the sword behaves exactly as before — no flourish ever fires from holding.
2. Tap RT twice quickly (< 0.35 s apart, each tap < 0.3 s): **Twin Fang** — two right cuts,
   blade flash, then the sword returns to rest. Repeat for LL, RL, LR — each visibly different.
3. Tap three: RRR spins the raised blade a full turn overhead; LLL wheels it down the left; RLR
   draws a figure eight. Tap RR then R: the Twin Fang is cut off into the Cyclone.
4. Energize (hold both), release, then tap combos while the blade is still energized: the
   upgraded set plays (Tempest = two turns, etc.), with the hotter crackle string.
5. Tap both triggers together repeatedly (feathering the stance): no combo.
6. Fly through a prism field running combos: prisms are cut along the path and debris flies
   harder off a whipped tip. No console errors.
7. MPPM two clients: client A runs RL, RLR; client B sees the same flourishes on A's Rhino.
8. Keyboard (Left/Right Shift = triggers): taps call combos the same way.

## Follow-ups

- **Audio**: 24 empty `sound` slots on the library asset for the audio owner.
- **HUD callout**: `RhinoSwordComboPath.DisplayName` is authored for a combo-name toast; nothing
  draws it yet (`ShieldSwipeActionExecutor.ActiveFlourish` exposes the playing one).
- **Replicated energized choice**: pick the variant from an owner-written bit rather than the
  local `IsEnergized` once the energy/energize NetworkVariable follow-up in
  `RHINO_ENERGY_SWORD.md` lands.
- **AI**: `AIPilot` never pulls a trigger, so AI Rhinos never combo. A server-side replicated
  tap pattern (Urchin spike-tap shape) would give them flourishes.
