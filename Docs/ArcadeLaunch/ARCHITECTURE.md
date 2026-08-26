# The arcade launch panel — one card, one panel

Selecting an arcade card opens **one panel** holding everything the player configures before
the match: the game already playing in the preview window, its intensity, its briefing, the
hull's controls, the roster, and Start.

The two-screen flow it replaces — configure, then pick a vessel — existed because a card could
be flown in several hulls. **Every arcade mode locks to one now**, so the second screen had
nothing left to ask and the first had no reason to be a separate step.

---

## 1. The two panels, and why there are exactly two

| Panel | Draws | Preview | Controls block | In its place |
|---|---|---|---|---|
| `MinigameLaunchPanel` | every card except the meta-mode | the live window | the hull's four abilities | — |
| `MaelstromLaunchPanel` | `GameModes.Tournament` only | a clip | none | the pool list |

Each difference follows from one sentence — *the Maelstrom draws OTHER modes*:

- **A clip, not the live window.** A mode with no arena of its own has nothing to stand up,
  which is why `ModePreviewLibrarySO` excludes Tournament in code. `ModeVideoView` is not the
  return of the deleted video fallback (`Docs/ModePreview/ARCHITECTURE.md`): every *playable*
  mode still previews live, and Maelstrom is the one card structurally unable to.
- **No controls block.** The hull changes every round — four of the pool's seven modes are
  vessel-locked — so there is no one set of controls to teach.
- **A pool list instead**, because the question this card actually raises is "what am I going
  to end up playing?", and the intensity answers it differently.

A card finds its panel by asking (`ArcadeLaunchPanel.Handles`), not by the modal switching on a
mode enum, so a third kind of card is a new subclass and one entry in the modal's list.

## 2. The panel owns its widgets; the modal owns the decisions

A panel exposes the controls it contains — intensity row, domain tiles, Start button, the
roster — and `ArcadeGameConfigureModal` subscribes to them. Config validation, the network
commit, ready-up and launch stay in exactly one place no matter which panel is on screen.

**A panel never writes `ArcadeGameConfigSO`, never talks to `ArcadeConfigSyncManager`, and
never launches anything.** The moment a panel starts making those calls there are two
authorities on the same state, which is the failure the single-writer rule exists to prevent.

Wiring **any** panel switches the modal to the one-panel layout wholesale. There is deliberately
no half-way state where some controls come from a panel and some from the legacy Screen 1 /
Screen 2 fields: two sources for one control is how a stale widget ends up driving live config.

### 2.1 Every control resolves through ONE accessor — and a per-control fallback is the bug

`ActiveIntensityButtons`, `ActiveDomainTiles`, `ActiveStartButton`, `ActiveWaitingLabel` and
`ActivePreviewWindow` each answer from the **active panel** on the one-panel layout and from
the **legacy serialized field** otherwise — never a mix.

Falling back per-control would look harmless and be wrong twice over: the Maelstrom panel
*deliberately* has no preview window, so a fallback would arm a live satellite arena into a
leftover Screen-1 frame the player cannot see; and a panel that simply forgot to wire its Start
button would silently drive the legacy one instead of reporting the hole.

## 3. There is no Confirm button left, so the commit moves to card-open

`OnConfirmConfiguration` was the host's Screen 1 → Screen 2 step. It is also the call that
publishes the domain count, resets every human's `NetDomain` to Jade, spawns the avatar chips
and opens the same panel on every client. With one panel there is nowhere to go, so
`SetSelectedGame` commits immediately — deferring it would leave the domain tiles inert on a
panel that is already showing them.

Two consequences worth knowing:

- **It commits silently.** The Confirm sting acknowledges a *button press*, and there is no
  press; a confirmation sound for opening a card reads as having agreed to something.
  `CommitConfiguration(playSound: false)` is the auto path.
- **The party sees the card the host opens.** On the old flow the host browsed privately. This
  is inherent to there being no private step, and it is bounded: `ArcadeConfigSyncManager`'s
  own `_isCommitted` guard means one commit per open, and closing the modal re-arms it.

`OnStartGameClicked` is a latch (`_localPlayerReady`): the panel subscribes it, a prefab may
*also* carry an inspector `onClick` to it, and a player can double-click — the second call is a
no-op rather than a second sting and a second `ConfirmLocalPlayerReady`.

## 4. The controls block is DERIVED, all the way down

`VesselControlsPanel` builds one row per ability from the hull's own `ElementalAbilityMapSO`:
the map names the ability and the `InputEvents` it rides, `InputHintBindingMap.BindingFor`
turns that into a physical control, and `ControlGlyphSetSO` turns the control into artwork —
the same chain the ability lockup uses (`Docs/ABILITY_LOCKUP.md`). Nothing is authored per
vessel here, so re-binding an ability moves its chip with it and a wrong label is structurally
impossible.

Three states are drawn honestly rather than papered over:

| State | What is drawn | Why |
|---|---|---|
| a passive ability (no input) | no chip | there is no button to show |
| a pad control with no keyboard equivalent | no chip, on keyboard | a pad glyph shown to a keyboard player is misinformation |
| an unauthored map slot (`"(open design slot)"`) | no row at all | drawing it promises an ability that does not exist |

Rows run **charge → mass → space → time**, the fleet's ability-row order, so the launch panel
reads left-to-right the way the in-game HUD row does. A mode listing several hulls draws no
rows — naming one of them arbitrarily would be worse than none.

**The animation is one sweep, not N loops.** One phase advances in the panel and each row is
handed its share of it, so the block reads as a single travelling highlight and an off-screen
panel costs nothing (`Update` returns on the first line). Rows scale and fade only; nothing
writes a rect, so a row sits inside a layout group without fighting it.

Ability ICONS are matched off the vessel asset by NAME (`SO_Vessel.Abilities` ↔
`ElementalAbilityEntry.AbilityLabel`). No match is an ordinary answer: the row keeps its prefab
sprite and the element petal still says which element owns the slot.

## 5. Kicking an AI is lowering the player count

There is no AI object to remove yet — the bots are spawned by
`ServerPlayerVesselInitializerWithAI` from `GameDataSO.RequestedAIBackfillCount` once the scene
loads. A kick is therefore a request to seat one fewer, which is both what the player means by
it and **the only representation that cannot go out of step with what actually spawns**. It is
floored at the humans present: a seat a human is already in is not the host's to take from here.

The **fill-with-AI toggle** is the one-panel layout's replacement for the player-count stepper —
on seats every slot the card allows, off drops back to the humans present — and the ✕ on a seat
covers the in-between. The toggle *follows* the count rather than driving it
(`SetFillWithAISilently`), so a ✕ that drops the roster off the ceiling turns the toggle off by
itself instead of leaving it claiming a full house.

### 5.1 Ready lights are a COUNT, not an identity

`ArcadeConfigSyncManager` replicates **how many** humans have confirmed, not **which**. So seats
light in roster order as that count climbs — with the local player's own seat exact, since that
one is known locally without the wire. Ordering the local seat first would reshuffle the row as
players join, so it keeps its place and simply reads true.

Per-seat identity needs the sync manager to replicate the ready SET. Until it does this is the
honest reading, and it is right in the case players actually watch (their own).

## 6. The preview follows the intensity row

`ModePreviewDefinitionSO.PreviewCellsByIntensity` is the same shape the mode's own scene uses
(`Cell.CellTypeChoiceOptions.IntensityWise` over a `CellConfigs` list, index 0 = intensity 1),
and `ModePreviewArena.Stand` now takes the **resolved config** rather than reading one off the
definition — which arena a mode previews is a function of the chosen intensity, and the arena
has no business knowing where that number comes from.

An intensity past the end of the list **clamps to the last entry**, matching `Cell.IntensityIndex`
exactly: a mode offering four intensities against two authored arenas serves the same arena for
3 and 4 in the real scene, and a preview that disagreed would be lying about the game.

**The rebuild is gated on the arena actually differing** (`ArenaVariesByIntensity` plus a config
comparison). Standing a satellite costs a multi-second cell build and a networked hull swap, so
rebuilding an identical world would make the intensity row feel broken while changing nothing on
screen. A mode whose intensity is not an arena at all — Skim Race's track length, the Maelstrom's
pool — authors one cell and is never rebuilt.

Lists are authored by `Tools/Build/author_preview_intensities.py` (`--check`), which copies each
one **from the mode's own scene**: the scene is the authority, and any second list is a second
answer. It is read-only on scenes and idempotent.

| Mode | Arenas | Note |
|---|---|---|
| Ribcage | 5 | the cage's rind count IS the intensity |
| Dog Fight, Salvo's twin arena | 4 | the shared Boneyard configs |
| The Bends | 4 | Rampage's arena, referenced not forked |
| Wildlife Liberation | 4 | |
| **Rampage** | **1, deliberately** | its four configs hold an *identical* 9,830-prism forest and differ only in crystal count and wildlife scale — and a satellite has **no `CrystalManager`**, so the one thing that varies is the one thing a preview cannot draw. Excluded in the script with that reason recorded. |
| the other 12 | 1 | their scene Cell is not `IntensityWise` — the preview correctly does not change |

## 7. The Maelstrom's intensity ladder

`TournamentDataSO.IntensityTiers` is a **cumulative** ladder over `GameQueue`: a run at
intensity N draws from every tier up to and including N, so raising the lobby's intensity
widens the draw as well as raising each game's own intensity ceiling.

A tier lists what it **adds**, not the full pool. Authoring the full pool per rung means every
new mode has to be pasted into four lists, and the day one is missed the pool silently shrinks
at that intensity.

| Intensity | Adds | Pool |
|---|---|---|
| 1 | Joust, Skim Race, Scurry | 3 |
| 2 | Rampage, Peel the Cage | 5 |
| 3 | Scarab Scramble | 6 |
| 4 | The Bends | 7 |

> **Skim Race *is* HexRace.** `ArcadeGameHexRace.asset` carries `DisplayName: "Skim Race"` —
> they are one mode, not two. Anything that reads like a pool of "Joust, HexRace and Skim Race"
> is naming the same card twice.

**An empty ladder keeps the legacy pool** — every queued mode drawable at every intensity — so
an un-authored asset is never left unable to draw, which would be a mode that cannot start.
`GameQueue` stays the full roster: the hub's pool string, the loading splash and the launch
panel's list all still read it, and the list draws locked modes *greyed rather than hidden*,
because a list that only grows tells the player nothing about what they are missing.

`TournamentController.LoadRandomGame` draws from the filtered list. Repeat-avoidance maps
`CurrentGameIndex` (a `GameQueue` index) **into** that list first — at low intensity the two
index spaces are not the same, and treating them as one would avoid the wrong mode.

Adding a mode to the roster is unchanged and still governed by
`Docs/TournamentSystem/ARCHITECTURE.md`: domain-scored, scene in Build Settings, player/domain
range containing the Maelstrom card's. Dog Fight and Salvo are **not** in `GameQueue` yet — the
ladder cannot admit a mode the roster does not hold.

## 8. The pieces

| Piece | Location | Job |
|---|---|---|
| `ArcadeLaunchPanel` | `_Scripts/UI/View/ArcadeLaunch/` | The contract: which controls a panel exposes, what the modal may ask of it |
| `MinigameLaunchPanel` / `MaelstromLaunchPanel` | same | The two concrete panels |
| `VesselControlsPanel` / `VesselControlRow` | same | The hull's abilities and their controls, derived |
| `LobbySlotRow` / `LobbySlotView` | same | Seats, ready lights, the AI kick, the fill toggle |
| `GameBriefingView` | same | Description + rotating tips |
| `MaelstromPoolListView` / `MaelstromPoolEntry` | same | What this intensity can draw |
| `ModeVideoView` | same | The Maelstrom's clip |
| `ArcadeGameConfigureModal` | `_Scripts/UI/Modals/` | Still the one authority on config, commit, ready-up and launch |
| `TournamentDataSO.IntensityTiers` | `_Scripts/Utility/DataContainers/Tournament/` | The ladder + `GamesForIntensity` / `UnlockIntensityOf` |
| `ModePreviewDefinitionSO.PreviewCellsByIntensity` | `_Scripts/ScriptableObjects/` | Per-intensity arenas + `ResolveCell` |
| `author_preview_intensities.py` | `Tools/Build/` | Copies those lists from each mode's own scene (`--check`) |

Authored data: `SO_ArcadeGame.Tips` (per-card play tips) and `SO_ArcadeGame.PreviewVideo`
(**Maelstrom only** — every other card previews live and must never fall back to a clip).

## 9. Known limitations

- **Not verified in the Editor.** The UI these components attach to was authored in parallel
  with them; nothing here has been through play mode. Everything in §1–§7 is reasoned from the
  code it sits on, not observed.
- **Ready lights are a count** (§5.1). Per-seat identity needs the sync manager to replicate the
  ready set.
- **`SO_ArcadeGame.Tips` is empty on every card.** The briefing degrades to the description
  alone — the tip line switches off rather than showing an empty `Tip:` prefix — so the panel is
  correct but says less than it could until tips are written.
- **One glyph set serves both pad families**, inherited from the ability lockup: a PlayStation
  player sees Xbox `A`/`B` on the Sparrow's rows. Closing it is `ControlGlyphSetSO`'s follow-up,
  not this panel's (`Docs/ABILITY_LOCKUP.md`).
- **Browsing cards stands and strikes a satellite arena per selection**, plus a networked hull
  swap for a vessel-locked mode — the pre-existing cost recorded in
  `Docs/ModePreview/ARCHITECTURE.md §7`, now paid on intensity changes too for the four modes
  with per-intensity arenas.
- **The in-Maelstrom pre-game panel is not built.** The design calls for the same panel between
  rounds *without* the domain row (domain cannot change mid-tournament); that lives in the
  Maelstrom scene and is deliberately left for its own pass.
