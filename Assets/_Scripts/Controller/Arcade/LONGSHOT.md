# Longshot: the Serpent's Space game (GameModes 95)

Row 5 of `Docs/ElementalLoop/GAME_KIT_ROSTER.md`. Built 2026-11-15 for Garrett's "make all the missing games you can".
**Nothing here has run in the Unity Editor yet** (see `Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## The game

A timed shoot in a bare nucleus cell. Lances of loose neutral (Blue, no-team) prisms hang round the core: single-file
lines of 6 u cubes, 12 u apart, laid by `LongshotController` on every peer from the intensity alone (`LongshotField`).
The Serpent's Space card is the whole game:

- **Hold the scope (LT) and fire (RT).** The sniper round is one hitscan cone, 1.5 degrees wide and at least 6 u across,
  that breaks EVERY hostile prism inside it, neutral ones included, over 3,000 u (`SERPENT_SNIPER_SCOPE.md`). The rifle
  then needs 12 s to recover, down to 5.4 s at Charge 10.
- **Line up.** Fired along a lance, the round takes the whole run. Fired across it, it takes a prism or two. The
  generator measures both from the AI's line-up point on every run of every rung.
- **A rival's straight trail is a lance of their colour.** Shooting along it scores too, and the round strips the
  rival's elements if it reaches the hull.

The score is hostile prisms broken (`PrismsDestroyed`, timed, highest wins). A Serpent's own trail is its own colour,
so it never scores. The platform credits every break to the shooter, so the mode adds nothing to the score. This is
the Space twin of Shatterplane: squaring up is that game's skill, and lining up is this one's.

Nothing about the Serpent changes (the LOCKED vessel rule).

## The ladder (intensity is the map)

| Rung | Name | Lances | Runs | Out from the core | Omni crystals |
|---|---|---|---|---|---|
| I1 | Range | 8 x 48 | straight | about a third of the way to the membrane | 4 |
| I2 | Gallery | 12 x 48 | straight | about halfway | 3 |
| I3 | Dogleg | 12 x 48 | bent once: two runs of 24 | about halfway | 3 |
| I4 | Thornfield | 16 x 48 | zig-zagged: three runs of 16 | out by the membrane | 2 |

Each bent run turns 30 degrees off the lance's line, so the knee is 60 degrees. One shot clears one run.

## The AI (difficulty is the seat's AI level)

`LongshotRifleman` steers through `AIPilot.SetExternalTargetProvider` and presses only LT (the scope) and RT (the round,
while scoped). The autopilot's own ability cycler is kept off the cloak and the stop toggle, because RT unscoped is
the cloak.

1. Pick the run worth the most: standing prisms divided by the distance to its nearer end. While its domain trails,
   it goes for the nearest rival instead, whose trail behind it is a lance.
2. Line up `aiStandOff` (260 u) back along the run's line, then follow a carrot on the line to pull onto it.
3. Scope once within `aiScopeLateral` (25 u) of the line with the rifle ready. Wait the rung's settle, then fire as
   soon as the sniper's own cone would take `aiFireFraction` of the run's standing prisms. Give up after 4 s and
   line up again.

| Ladder | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| aiFireFraction | 0.25 | 0.45 | 0.65 | 0.8 |
| aiScopeSettleSeconds | 1 | 0.6 | 0.35 | 0.2 |
| aiRetargetSeconds | 14 | 11 | 9 | 7 |
| aiRivalTrailDeficitFraction | never | 0.3 | 0.15 | 0.05 |

**What the AI does not model:** leading a moving rival, a long angled shot through two runs at once, the cloak, and
the wall.

## Files

- `Longshot/LongshotController.cs`, `LongshotField.cs`, `LongshotRifleman.cs`, `LongshotSettingsSO.cs`,
  `LongshotScoringRuleSO.cs`; `TurnMonitors/LongshotTimeTurnMonitor.cs`; `Tests/Editor/LongshotTests.cs`.
- Every asset is written by `Tools/Build/author_longshot_assets.py` (`--check` is the drift gate). Re-run it rather than
  hand-editing. The card background is rendered by `render_card_backgrounds.py` (`/cardart`).

## Editor checks still owed

- A round fired down a lance breaks the whole run, and the score climbs by the run's prism count.
- The AI lines up on a run, scopes, fires and takes most of it at Hard.
- Does the autopilot hold the line well enough under the scope for the cone (6 u at the near end)?
- A rival's trail shot along its length scores, and a pilot's own trail never does.
