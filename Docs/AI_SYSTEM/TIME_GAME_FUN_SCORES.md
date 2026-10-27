# Time minigame fun scores (estimate, 2026-10-27)

Garrett, 2026-10-27: make every vessel's Time game fun, because it is what makes the vessel fun to fly between
places on a voyage. Score each one, then raise the floors.

**Scoring:** six dimensions, each 0-10. Fun is their mean; Floor is the lowest one.
- **Verb:** is the hull's own speed verb the way to win?
- **Flow:** is the next target always clear, with no dead air?
- **Ladder:** does each intensity add one demand, with no cliff?
- **Feedback:** is there a moment-to-moment reward?
- **Rivals:** do the AI rivals race and finish?
- **Fairness:** is the game free of soft-locks, and do people finish it?

These are estimates read from the mode docs, the code and `VESSEL_MODE_CHART.md` §3 (the recorded flights). Nobody has
played them in the editor for this report. Where there are no flights, Rivals and Fairness are scored low: unknown counts
against a mode, not for it.

| Vessel | Time game | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|---|---|
| Dolphin | Switchback | 7 | 6 | 8 | 4 | 5 | 6 | **6.0** | 4 Feedback |
| Stoat | Warpline | 9 | 7 | 5 | 4 | 3 | 7 | **5.8** | 3 Rivals |
| Sparrow | Breakwater | 6 | 6 | 7 | 4 | 4 | 7 | **5.7** | 4 Rivals/Feedback |
| Manta | Redline | 9 | 7 | 8 | 4 | 2 | 3 | **5.5** | 2 Rivals |
| Butterfly | Waystation | 9 | 6 | 7 | 4 | 3 | 4 | **5.5** | 3 Rivals |
| Thresher | Thresher Time | 6 | 7 | 7 | 5 | 4 | 4 | **5.5** | 4 Verb/Rivals |
| Rhino | Headlong | 9 | 7 | 7 | 3 | 1 | 2 | **4.8** | 1 Rivals |
| Grizzly | Grizzly Time | 8 | 7 | 7 | 3 | 2 | 2 | **4.8** | 2 Rivals |
| Urchin | Skein | 8 | 5 | 5 | 4 | 3 | 3 | **4.7** | 3 Ladder cliff |
| Wrasse | Wrasse Run | 7 | 6 | 6 | 5 | 2 | 2 | **4.7** | 2 Rivals |
| Squirrel | Skim Race | 5 | 7 | 6 | 5 | 2 | 2 | **4.5** | 2 Fairness |
| Scarab | Scarab Scramble | 2 | 6 | 5 | 7 | 3 | 3 | **4.3** | 2 Verb |
| Serpent | none | - | - | - | - | - | - | **0** | no game |

## What the floors say

1. **Design is mostly good. Proof is missing.** Verb scores 7-9 for nine of the twelve games. Most floors sit on Rivals and
   Fairness, because almost nobody has raced these modes. Flying each one at I1 and I4 with AI rivals is the cheapest way
   to raise them. The races are recorded automatically and the hourly AI learning sweep learns from them
   (`AUTO_LEARNING.md`).
2. **Feedback is a 3-5 everywhere.** No gate race had a sound for threading a ring.
   - **Done (this change):** `GateRaceController` now has two FMOD slots, `gateThreadedEvent` and `courseFinishedEvent`,
     played for the local pilot only.
   - They come from the same live progress that lights the next ring, so they stay quiet on a rollback.
   - They ship EMPTY (the FMOD convention), so audio must author the two events and wire them on each mode scene.
   - Every ring race inherits them: Switchback, Warpline, Slingshot, Headlong, Redline, Skein, Breakwater, Waystation,
     Thresher Time, Grizzly Time, Wrasse Run and Regatta.
3. **Three design floors need an owner's call** (not changed here):
   - **Scarab Scramble:** the Scarab's throttle never decides a goal. Two options:
     - make a Scarab gate race its Time card;
     - make a full-throttle strike carry the ball further. That changes the ball shared with Astro League.
   - **Skein:** I3 and I4 run 127 s and 190 s against the ~90 s target, and the next ring is almost never on screen.
     - Cut the laps from 4/6 to 3/4. Laps and ring spacing are tied together (`SKEIN.md`), so this is not a one-number
       edit.
     - Pin every third ring at I1, so the rail is taught rather than optional.
   - **Skim Race:** I3 and I4 change the lap length and halve the mouths at once, and nobody has finished a race (0 of 7).
     - Proposed: `intensityFourMouthFraction` 0.5 → 0.75.
     - Proposed: laps [3,3,2,2] → [2,2,1,1], through the End Game Conditions window only.
4. **AI numbers belong to the learning sweep** (Yash). Proposals, for him to confirm:
   - **Headlong:** turn on `AICommitFromApproachPoint`, the fix that made the Switchback AI race. Headlong is the plan's
     negative control today.
   - **Butterfly:** raise `aiFoldAimDegrees` from 12 to 18.
   - **Wrasse:** raise `aiBoostRingDistance` from 180 to 250.
   - **Thresher:** lower `plantMinTurnDegrees` from 70 to 55.
5. **Serpent has no Time game.** The cheapest one is a clone of Redline or Thresher Time on `HeadlongCircuit`, with the
   course cut for pellet-boosted speed. Check enum ids on `bleeding-edge` and the open branches first: `SerpentCharge`
   holds 67 on an in-flight branch.

Re-score a game after people have raced it. The chart's §3 rows are what move Rivals and Fairness.
