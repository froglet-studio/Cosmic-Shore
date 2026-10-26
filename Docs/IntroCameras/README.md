# Per-mode intro cameras

**Status: prototype, waiting on the team's verdicts. Nothing in Unity has changed.**

Artifact: https://claude.ai/artifact/Py9YqiPqdUZQsPfmNU7N1f (source: `intro-cam-lab.html` here).

Today every mode plays the same `PreGameCinematicController` path
(`Assets/_Scripts/UI/PreGameCinematic/PreGameCinematicController.cs`): four straight chords of a
150-unit circle, 60 up, looking at `MiniGameHUD.cinematicLookAtCenter`, a 0.3 s pause at each corner,
then 1.5 s to behind the pilot (0.3 s on Skip). The artifact gives each of the 35 modes on the Arcade and
Arena lists its own intro and can play today's orbit beside it ("Today's orbit", or `C`).

## What the artifact holds per mode

- a concept and pitch, and a shot list. Each shot has a move kind (`move` dolly/crane, `orbit`, `path`
  flying a curve, `vertigo` dolly zoom, `cuts`, `hold`), a duration, an easing, a field of view, roll,
  shake, and captions (title / sub / stat / lower third).
- the same hand-off to the pilot the game uses today.
- the arenas are stand-ins built in the page. Coordinates in it are NOT Unity coordinates.

Team verdicts (keep / tweak / redo plus a note, one document per person) live in the artifact's
`verdicts` collection. Read them before porting a mode.

## Porting plan (when verdicts are in)

1. One sequence asset per `GameModes` value, holding the shot list. `PreGameCinematicController` plays it
   when one is assigned and falls back to today's orbit when not, so modes move over one at a time.
2. Anchors are named scene objects (the crystal, gate 0, a hulk, the nucleus), resolved at play time. They
   are never coordinates copied from the page.
3. Every caption is an authored text field on the asset; an empty field shows nothing (CLAUDE.md, the
   player-facing text rule). World highlights such as Salvo's target frames are 3D, in the world.
4. Skip, the gamepad South button and the 1.5 s / 0.3 s hand-off stay as they are.

Affects: `MiniGameHUD.cs` has in-flight edits on `cece/vibrant-archimedes-n3xn3x` and
`cece/nice-brahmagupta-ojclij`; check with Garrett, Yash and Shombith before the Unity port touches it.
