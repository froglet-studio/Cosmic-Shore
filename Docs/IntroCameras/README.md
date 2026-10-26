# Per-mode intro cameras

**Status: built and compiled against real Unity references; not yet played in the editor.**

Artifact: https://claude.ai/artifact/Py9YqiPqdUZQsPfmNU7N1f (source: `intro-cam-lab.html` here). Shombith
approved all 35 intros on 2026-10-26 and asked for them to be shuffled within families.

## What happens before a match

`MiniGameHUD` calls `PreGameCinematicController.Play(center, pilot, mode, vessels, accent)` when the
connecting screen closes, as before. The controller:

1. finds the mode's **family** in `Resources/IntroCameraLibrary.asset` (`IntroCameraLibrarySO`);
2. picks one of the family's intros at random, never the one that family played last;
3. gathers the arena once (`IntroCameraScene.Gather`): the nearest cell's centre and membrane radius, every
   vessel and its domain, every `Crystal`, every `RaceGateRing` in course order, and the eight biggest
   mesh renderers (wrecks, cacti, posts; vessels, the membrane and anything tiny are skipped);
4. flies the intro's shots (`IntroCameraMath.Evaluate`) under letterbox bars, with the MODE's title,
   tagline, objective and lower third (`IntroModeWords`), whichever family intro is playing;
5. hands off to the pilot exactly as before: 1.5 s, 0.3 s on Skip, the gamepad South button skips.

A mode in no family (Tandava, anything new) plays the old orbit unchanged.

## The six families

| Family | Modes | Intros |
|---|---|---|
| Crystal fields | Skim Race, Joust, Scurry, Maelstrom | 4 |
| Gate races | Headlong, Redline, Switchback, Breakwater, Skein, Waystation, Grizzly Time, Slingshot, Warpline, Thresher Time, Wrasse Run, Regatta | 12 |
| Boneyard | Dog Fight, Salvo, Dustup, Broadside | 4 |
| Forests and demolition | Rampage, The Bends, Sirocco, Wrecking Ball, Grizzly Charge, Cleave | 6 |
| Cages | Wildlife Liberation, Undertow | 2 |
| Courts and cells | Scarab Scramble, Tollway, Hijack, Bloomrush, Tapestry, Astro League, Brood Rush | 7 |

## Changing an intro or a line of text

Everything is authored by `Tools/Build/author_intro_cameras.py`; edit it there and run it, never the asset
by hand. `--check` is the drift gate (it also fails when a mode on the Arcade or Arena list has no family or
no words), `--self-test` proves the gate fires. The words are player-facing text: the asset holds them, code
never builds them, and an empty line shows nothing.

Shots name **anchors**, not coordinates (`IntroAnchor`: Center, Player, Rival, Teammate, Pilots, Crystal,
Gate, Landmark). An anchor a scene lacks falls back to the centre; a gate or crystal path with fewer than
three points becomes a ring round the centre. Arena-space distances are the artifact's units (a 240-unit
cell) times the real cell's radius over `referenceRadius`; raise `scaleMultiplier` on the asset if every
intro flies too close.

## What the artifact has that the game does not (yet)

- Speed lines and the warp streaks: the game conveys speed with field of view only.
- Mode-specific set pieces: Salvo's red target frames, Grizzly Charge's prism blast, Tapestry's painters,
  Bloomrush's fuses. These were stand-in world effects; the game shows the real arena instead.
- Broadside's per-hull weapon captions (the words are per mode, not per cut).

## Files

`Assets/_Scripts/UI/PreGameCinematic/`: `IntroCameraData.cs` (the serialized types), `IntroCameraLibrarySO.cs`
(families, words, the no-repeat pick), `IntroCameraScene.cs` (anchors and paths), `IntroCameraMath.cs` (shot
evaluation), `IntroCameraOverlay.cs` (bars, flash, captions), `PreGameCinematicController.cs` (plays it).
Tests: `Assets/_Scripts/Tests/Editor/IntroCameraTests.cs`.

Affects: `MiniGameHUD.cs` (one call site) has in-flight edits on `cece/vibrant-archimedes-n3xn3x` and
`cece/nice-brahmagupta-ojclij` (Garrett, Yash, Shombith); the change there is the `Play` call only.
