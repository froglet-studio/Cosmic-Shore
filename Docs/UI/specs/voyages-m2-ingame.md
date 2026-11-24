# Spec voyages-m2-ingame: the in-game voyage screens of Milestone 2 (Rev 134 and Rev 137)

Board: R134Pause, R134Vote, R134VoteSolo, R134VoteTie, R134Awards, R134Lost, R134Crossing, R134Event, R134Detour, R134Rival, R134Hostile, R134Joins, R134Dock, R137ArkMeet, R137ArkOpen, R137ArkBreak, R137Runaway, R137Rout, R137Chart, R137Fall (.dc.html) on Frogma (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2), tabs Rev 134 and Rev 137, read live 2026-11-24
Status: approved (Shombith, 2026-11-24: "Pause A / Vote B / Awards A", Rev 134 run screens and Rev 137 rivals, relayed in the project chat); in PR into voyages
Allowed overrides: 0
Generators: Tools/Build/author_voyage_assets.py

## Target

The voyage run's own screens, built at run time in code by `VoyageKit` on overlay canvases that
`VoyageHarbour.Create` adds beside the existing voyage HUD (no scene, no prefab, no GameCanvas override). One layout on
the 1920 x 1080 frame for every device (Shombith, 2026-11-20), inside the safe area (`SafeAreaFitter`), text sizes as
the current voyage HUD uses them.

- Pause A (`VoyagePauseChart`, order 24000): replaces the shared PauseMenu's window while a voyage runs, through the new
  `PauseMenuStage` hook; the menu's own Resume, Settings and Leave are what its buttons call.
- Vote B (`VoyageVotePanel`, order 20002): the detour vote, low left.
- Rival plate (`VoyageRivalPlate`, order 20002): Rev 137 ArkMeet to Rout, top right.
- Awards A and Lost (`VoyageAwardsBoard`, inside `VoyageScoreboardView`): the end board of a NAMED voyage. Crystal Run
  (unnamed) keeps its board exactly as it was.
- Run toasts (`VoyageScreenToasts`): Crossing, Event, Detour, Rival, Hostile, Joins, ArkMeet to Rout, through the existing
  toast feed under the chip (Rev 120 B).

## Regions

- Pause: left column (kicker, voyage name, crystals and goal, where line, Resume / game action / Settings / Leave, the
  party note); the chart to its right (the lane, its cells, the Ark live, events next door, rivals); the legend along
  the bottom.
- Vote: one band with the asker's portrait and quote, the cost line, Detour and Press on with arrow keycaps, the clock,
  one ballot per seat in a party, the rule or result line.
- Rival plate: threat rule, mark and name, faction and temper, hold (or haul) and hull, a row per escort, how to beat
  them.
- Awards: kicker, voyage name, the run line through its places, up to three award cards (hull, award, who flew it, the
  line), the pay row, then the board's existing buttons. Lost: kicker, "ends at" title, the stats line, the squad.

## Components

| Component | Role | Token(s) | Type role | Spacing | Sliver | Unity |
|---|---|---|---|---|---|---|
| Pause Resume (keycap B), game action, Settings, Leave | the column of choices, keycap first | kit Quiet (Light, Outline) | button | 360 x 56, 12 gap | kit | `VoyageKit.Quiet`, left aligned |
| Chart cells | the lane's cells | kit Light / Sub, faded | data-small captions | from the layout | none | `VoyageRing` |
| Chart lines | sailed solid, ahead dashed | kit Light faded | - | - | none | `VoyageChartLine` |
| Events next door | ring + Marked box | kit Light, Second | body-small | ring offset 245 | none | `VoyageRing`, `VoyageOutline` |
| Rivals on the chart | threat | VoyageHudColors.Threat | body-small | offset 330 | none | `VoyageChartLine`, `VoyageGlyph` |
| Vote choices | Detour / Press on, arrow keycaps | kit Popup .8 fill, Sub .6 edge; your press edged Light | button | 144 x 44, 12 gap | none | `VoyageOutline`, `VoyageGlyph` Left / Right |
| Vote ballots | one per seat | kit Light, Sub | body-small | 26 faces | none | `VoyageKit.Picture` |
| Rival plate | threat | VoyageHudColors.Threat, kit Popup .78 | label 16, body 13-14 | 416 wide, 40 rows | none | `VoyageKit.Block`, `VoyageGlyph` |
| Award card | one per award | kit Glass, Light, Sub | h3 award, body | 500 x 400, 40 gap | kit | `VoyageKit.Glass` |

## States

| State | Looks like | Token / rule |
|---|---|---|
| rest | as the components table | kit tokens |
| focus | Resume selected on open, Settings again after its window closes; pad B resumes | the kit's focus on `Quiet` |
| hover | the kit's hover | `VoyageKit` buttons |
| pressed | the kit's press | `VoyageKit` buttons |
| disabled | Leave in a party says what it does (party note) | authored `PauseParty*` lines |
| vote picked | the choice you pressed is edged in Light | kit Light |
| vote closed | result line, lingers 1.6 s, fades .25 s | `VoteEnd*` lines |
| escort silenced | row dims to Sub .7 | kit Sub |
| rival broken / routed | toast turns good (green) | `ArkwayVoyageHud.Mood.Good` |

## Text

Every word is a `VoyageLines` field (134 new ones: Pause*, Chart*, Sea*, Side*, Verb*, Vote*, Rival*, Temper*,
Toast*, Awards*, Award*), authored in `Docs/Voyages/voyage_slice.json` `hud` and written by
`author_voyage_assets.py` into `VoyageContent.asset`. Tokens are filled with `TemplateText.Fill`. Printable ASCII only
(the spec's rule): the board's middle dot is written " - " like the voyage's other lines. Voyage and place names come
from the Codex through the job and `VoyageObjectiveText.StopName`; a voyage type is never a title.

## Icons

`VoyageGlyph` shapes: Ark, Gem and the new Flag (3), Warn (4), Flare (5), Pack (6), Left (7), Right (8) (the vote's
arrow keycaps). Award cards show the seat's hull sprite from the intro's vessel list (`VoyageSeats.HullOf`). Pilot
portraits from `PilotArtifactCatalogSO.ResolvePilot`.

## Motion

Pause chart refreshes at 2 Hz; the sailed line fades in from the start. Vote and rival plate fade in and out over
0.25 s; the vote lingers 1.6 s after it closes. Toasts use the existing feed's motion.

## Devices

- PC: the 1920 x 1080 frame; mouse clicks every button and vote choice.
- Phone: the same layout fitted into the safe area (one HUD layout for all devices); touch presses the choices.
- iPad: the same layout, letterboxed by `VoyageKit.Fit`.
- Gamepad: pause opens on Resume, B resumes; the vote takes the D-pad (left Detour, right Press on), the keyboard arrows
  too.

## Literals

| Literal | Count | Token |
|---|---|---|
| #E6E9FF | 457 | TextLight (web text); kit Light |
| #9FE8FF | 336 | Q1 |
| #99FF80 | 130 | Cta (web cta, cta-fill); kit CtaFill |
| #747BAD | 106 | NeutralLightest (web text-soft); kit Sub |
| #FF4B3A | 86 | Danger (web danger); VoyageHudColors.Breach |
| #B8C0F0 | 83 | Q2 |
| #00041F | 82 | SurfaceVeryDark (web ground, on-fill); kit CtaText |
| #00010A | 72 | SurfaceBlack (web ground-deep); kit Popup |
| #00D4FF | 60 | ControlActive (web control, selection); kit Cyan |
| #FB8E7E | 54 | Q3 |
| #FF8A7C | 44 | Q3 |
| #434C89 | 43 | SurfaceLight (web surface-raised, line); kit Outline |
| #9FB4E6 | 27 | Q1 |
| #A600FF | 20 | Tooltip (web tooltip, commit, commit-fill) |
| #FFFFFF | 20 | UITheme.OnCommit (web on-commit) |
| #E6F7FF | 14 | Q1 |

Translucent colours on the boards are these colours at an alpha; the build writes them as `VoyageKit.Fade(token, a)`.
Every colour goes through `ColorTrace.Retrace` (the kit and `VoyageHudColors`).

## Questions

Q1. The boards' cyan text and marks (#9FE8FF, #E6F7FF, #9FB4E6) belong to the chip and the plate over the Ark that
Rev 120 B already built; they have no token. Board: drawn around the new screens as context. Meanwhile: those parts
are the existing HUD, untouched; the new screens use kit Light and Sub.

Q2. The secondary text (#B8C0F0) has no theme token. Board: captions, quotes, cost lines. Meanwhile:
`VoyageHudColors.Second`, the authored colour Rev 113 introduced, traced through the colour set.

Q3. The threat red (#FF8A7C and #FB8E7E, two near shades) has no theme token. Board: only a rival wears it. Meanwhile:
one colour, `VoyageHudColors.Threat` (#FF8A7C), traced.

Q4. The board's awards cards carry a mood or rank line per pilot. Board: a short mood under the name. Meanwhile: none;
the game has no mood or rank system yet.

Q5. The crossing toast names the zone ("The Crossing"). Board: the zone name on the second line. Meanwhile: "Cell n of
m - sea"; voyages have no zone data yet.

## Build map

| Change | File | Prefab / scene / code | Shared? |
|---|---|---|---|
| Screen data, vote and awards rules, chart layout | `Assets/_Scripts/Data/Voyages/VoyageScreens.cs` | code | no |
| What the run tells the screens | `Controller/Voyages/VoyageScreenFeed.cs`, `VoyageRun.Screens.cs` | code | no |
| Hooks in the run | `VoyageRun.cs`, `VoyageRun.Raid.cs` (HookScreens, TellScreens, crossing toast) | code | gameplay thread's files |
| Pause stage hook | `UI/PauseMenu.cs`, `UI/PauseMenuAction.cs` | code (GameCanvas's PauseMenu script, no prefab change) | yes: every mode runs PauseMenu; with no stage it behaves as before |
| Pause chart | `UI/Views/VoyagePauseChart.cs`, `VoyageChartLine.cs` | code | no |
| Vote | `UI/Views/VoyageVotePanel.cs` | code | no |
| Rival plate | `UI/Views/VoyageRivalPlate.cs` | code | no |
| Awards and Lost | `UI/Views/VoyageAwardsBoard.cs`, `VoyageHud.cs` (VoyageScoreboardView) | code | no |
| Glyphs | `UI/Views/VoyageGlyph.cs` | code | voyage UI only |
| Toasts | `Controller/Voyages/VoyageScreenToasts.cs` | code | no |
| Words | `VoyageContentSO.cs`, `voyage_slice.json`, `author_voyage_assets.py`, `VoyageContent.asset` | asset via generator | no |
| Tests | `Tests/Editor/VoyageScreensTests.cs` (and the headless harness) | code | no |

## Differs from the board

- The events next door, the rivals and the vote show only once the gameplay fills them (`VoyageRun._neighbours`,
  `_rivals`, `_vote`; the "Build the four Milestone 2 voyages" thread). Until then those screens stay hidden and the
  pause chart shows the lane, its cells and the Ark.
- No mood or rank line on award cards (Q4); no squad-fit or wreck breakdown on the pay row (the pay is the board's
  existing total).
- No zone in the crossing toast (Q5).
- Faction names come from `VoyageCrewRules.FactionName`, which names the group ("the Squirrels"), where the board
  writes the faction singular.
- The in-world red labels over rival hulls (Rev 137) are not built: the world look rule says a world fact is shown in
  3D, which is the gameplay thread's model work.
- The intro card's Rev 134 restyle (kicker with giver and type, squad chips) is not in this change; the intro is as it
  was.
- Sea rung words (Calm, Choppy, Rough, Abyssal) are this build's reading of the board's one example.
- One threat red for the board's two shades (Q3).
