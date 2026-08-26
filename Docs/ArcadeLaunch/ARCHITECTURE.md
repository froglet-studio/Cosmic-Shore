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

**The Maelstrom is not one of the arcade grid's cards.** It draws the OTHER modes, so listing it
beside them invites "play this one" when it means "play several of these"; `ArcadeExploreView`
excludes it from the grid and it is opened by its own control through
`ArcadeGameConfigureModal.OpenMaelstrom()`. It stays in `SO_GameList` — that list is also the
roster the tournament pool and the client-side mode lookup read.

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

## 4. The controls block: the mode's abilities — and the icon animates like the game

`VesselControlsPanel` draws two kinds of row.

**Authored rows come from ONE asset, and the default is none.** `ModeControlsLibrarySO`
(`Resources/ModeControlsLibrary`) says, per MODE, what the section shows besides the abilities —
a list of `FlightControl` rows (headline, description, icon, optional control), plus a per-mode
switch for the derived rows. It ships with an empty default: **a card's designated abilities and
their controls ARE the section.** The stick primer ("Left stick — steer") used to be authored on
the panel itself, which put it on every card whether or not it earned the space and left nowhere
to say "this mode's section opens with THIS" — editing what a mode shows is now editing that one
asset, never a panel, a scene, or a prefab. The panel's own serialized list survives only as the
fallback for a project state with no library asset.

**Ability rows are DERIVED, all the way down.** The hull's own `ElementalAbilityMapSO` names the
ability and the `InputEvents` it rides, `InputHintBindingMap.BindingFor` turns that into a
physical control, and `ControlGlyphSetSO` turns the control into artwork *and* into the name used
in the sentence — the same chain the ability lockup uses (`Docs/ABILITY_LOCKUP.md`). Nothing is
authored per vessel here, so re-binding an ability moves its whole row with it and a wrong label
is structurally impossible.

The headline is a sentence, not a noun: **"Press RT to activate Drift"**, with the
`abilityHeadlineFormat` / `passiveHeadlineFormat` strings on the panel as the only copy.

Three states are drawn honestly rather than papered over:

| State | What is drawn | Why |
|---|---|---|
| a passive ability (no input) | `"<name> (passive)"`, no chip, no recharge | there is no button to show and nothing to recharge |
| a pad control with no keyboard equivalent | no chip and no control name, on keyboard | a pad glyph shown to a keyboard player is misinformation |
| an unauthored map slot (`"(open design slot)"`) | no row at all | drawing it promises an ability that does not exist |

Rows run **charge → mass → space → time**, the fleet's ability-row order, so the block reads
left-to-right the way the in-game HUD row does. A mode listing several hulls (`VesselClassType.Any`)
still draws the mode's **authored** rows — whatever the library says is true whatever you end up
flying — and no ability rows, because naming one of several hulls arbitrarily would be worse than
none.

### 4.1 The icon animates the way it animates in the game

Not a decorative pulse. The row whose turn it is replays the **ability lockup's own three beats**:

1. **press flash** — `AbilityLockupStyleSO.pressFlashColor`, decayed rather than switched off;
2. **the recharge veil** — a *clockwise* radial sweeping off the icon, `cooldownVeilColor`;
3. **the ready flash** — `cooldownReadyFlashColor`, the beat the player is actually waiting for
   and the loudest thing the card ever does.

Every colour comes out of `Resources/AbilityLockupStyle`, the same asset the HUD reads, so the
preview **cannot drift from the game**: retune the recharge veil once and both follow.

Two details carried over verbatim from `AbilityLockupView.BuildCooldownOverlay`, because getting
either wrong looks like a bug rather than a difference:

- **`fillClockwise = false` is what reads as clockwise.** The veil *depletes*, so the flag names
  the direction the wedge is drawn and the edge the player watches is its far end travelling the
  other way. True would draw the wedge clockwise and therefore retreat anticlockwise.
- **The veil and the flash are siblings drawn after the icon, never children of it.** They have to
  darken and light the icon — and a child would inherit the icon's scale, which this row animates,
  and so draw at the wrong size on every pulse.

Both overlays are built **lazily**, on the first row that needs them, and sized from the icon's own
rect. A row that never demonstrates a recharge — a flight axis, a passive — builds neither and
draws neither.

### 4.2 One sweep, not N loops

One phase advances in the panel: the row whose turn it is plays the three beats, every other row
is dimmed, and the highlight wraps off the last row onto the first without a seam. So the block
teaches one control at a time in the game's own visual language, and an off-screen panel costs
nothing (`Update` returns on its first line). Rows scale and fade only; nothing writes a rect, so
a row sits inside a layout group without fighting it.

### 4.3 The row reads correctly on the simplest prefab

A row needs exactly **one text field**. Given both a headline and a description field it uses
them; given only a description it writes the headline first and the detail on the next line. The
block has to be right on the plainest prefab anyone would author — an icon and a line of text —
because that is what a controls row actually is.

Ability ICONS are matched off the vessel asset by NAME (`SO_Vessel.Abilities` ↔
`ElementalAbilityEntry.AbilityLabel`). No match is an ordinary answer: the row keeps its prefab
sprite and the element petal still says which element owns the slot.

### 4.2.1 An ability's icon comes from that vessel's own HUD

The HUD is the authority on what an ability looks like, and it is keyed by **element** — which is
exactly the key a row already has (`VesselHUDView.TryGetAbilityIcon`). The vessel's HUD prefab is
**read, never instantiated**: a prefab asset's components are inspectable as they are, so this
costs one `GetComponentInChildren` per card.

Matching the vessel's ability *assets* by name was the first attempt and it silently found
nothing — `SO_VesselAbility.Name` and `ElementalAbilityEntry.AbilityLabel` are authored
independently and do not agree — so every row fell back to the prefab's placeholder and the
Sparrow's card showed four identical marks. **A name match between two lists nobody keeps in step
is a lookup that reports success by showing the wrong thing.** It survives as the fallback for a
vessel whose HUD binds no icon for that slot (three of the fleet bind none at all).

`VesselPrefabContainer` **must be wired** — unlike the glyph set and the bars config it does not
live in `Resources`, so there is no load to fall back on. The panel warns once rather than quietly
drawing placeholders.

### 4.2.2 The panel owns its row container, all of it

`HideForeignRows` switches off anything in the container the panel did not build. The thing that
is reliably *not* its own is the hand-authored row the wirer cloned into a prefab: it kept
rendering its placeholder copy ("Press RT to active drift") above every real row, on every card,
whatever hull was selected — so a Sparrow card advertised a Dolphin ability. A panel that owns a
container has to own all of it; leaving one child to the scene is how that happens.

### 4.2.3 Every row goes down before any goes up

`Show` deactivates the whole row list first, then builds. Deactivating only the *tail* — rows the
new card did not need — leaves a row visible whenever the new hull has at least as many as the old
one, which is how a card kept showing the previous vessel's ability: the row was not stale data,
it was a row nobody rebuilt because the count happened to match.

### 4.3.1 An authored ability description is a DESIGN NOTE, not player copy

`ElementalAbilityMapSO.AbilityDescription` runs to several hundred characters of mechanism — the
Dolphin's four are essays about multipliers and cooldown curves. Quoting one whole buries the row
it belongs to and every row under it, which is exactly what the first build did.

Rows show the **first sentence**, hard-capped (`descriptionCharacterCap`, 120): that sentence is
reliably the summary and the rest is rationale. `DescriptionStyle` can select `None` (headline
only) or `Full` for a vessel whose descriptions are genuinely player copy. The cap breaks on a
word — an ellipsis mid-word reads as a bug rather than as a trim.

**Rows go under the scroll view's `content`, never the block root.** A row parented to the root
sits outside the viewport's mask and draws over everything below it, which is the overlapping wall
of text a scroll view gets added to fix. `ArcadeLaunchPanelWirer.RowHost` resolves this.

### 4.4 The briefing is ONE text field, cycling

The description and the tips are the same voice answering the same question at different depths,
so they take turns in one place rather than stacking into a wall of copy beside a preview window.
A permanently-held second line would also mean authoring for the worst case: a card with four tips
needs four lines of space that a card with none leaves empty.

The order is **description first** — a player who has just opened the card is asking "what is
this?" before "how do I play it?" — then each tip, then back. The description holds longer than a
tip (`descriptionExtraDwell`), because it is the card's own answer. A card with no tips never
rotates.

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

### 5.2 Every domain is unlocked, and the fill toggle fills to four

**Every domain is always pickable.** Tiles used to be dimmed and made non-interactable outside
`ActiveDomains[0..DomainCount-1]`, which reads as "Gold is locked" — a progression claim the game
makes nowhere else. The domain count is a property of how the MATCH is scored, not a gate on which
colour a player may fly.

**The fill toggle fills to four, not to the card's ceiling.** Several cards allow six or twelve, and
a party game filled to twelve bots is not what the switch means; `MaxFilledPlayers` is the house
match size. Off, the roster drops back to the humans present.

**The toggle may live on the PANEL or inside a `LobbySlotRow`.** The one-panel layout puts it beside
the domain tiles — where the AI it seats actually appear — so `ArcadeLaunchPanel` takes an optional
`fillWithAIToggle` of its own. Whichever is wired raises the same event and both may be wired at
once.

**AI seats are previewed under the domain each bot will really join.** The bots do not exist yet —
`ServerPlayerVesselInitializerWithAI` spawns them in the game scene from
`GameDataSO.RequestedAIBackfillCount` — so these chips preview a roster rather than showing one.
That is exactly why the placement runs the spawner's own `BuildActiveDomains` /
`BuildHumanCounts` / `GetBalancedDomain` over the same counts it will use: a preview that
distributed them its own way would be a promise the match then breaks. The avatar is random per
chip because a bot has no profile to read one from, and four seats showing icon 0 read as one
player repeated.

One ordering rule: `ClearStaleChipsFromAllStrips` removes **every** chip under a strip, AI included
— it cannot tell one from a hand-placed leftover and should not have to — so `SpawnChipsForAllPlayers`
rebuilds the AI chips itself rather than leaving it to whichever call happened to run last.


#### 5.2.1 Chip placement bugs, both structural

- **Every AI chip landed under one domain** because placement read
  `gameData.RequestedDomainCount` — written only at COMMIT, defaulting to 1 — so the active set
  was `[Jade]` alone and "balanced" meant "all on Jade". Placement now reads the LIVE
  `config.DomainCount`, and with every tile unlocked the default domain count is **3**, so both
  the chips and the real match's backfill spread across the triad. Chips also rebuild only when
  what they SAY changes (count / domain count / human placement) — reshuffling avatars on every
  ready-count tick read as the roster changing when it had not.
- **A player's second domain pick vanished their avatar** whenever the chip had been destroyed
  (modal close, panel switch) while the NetDomain subscription survived — the move handler hit a
  missing chip and silently returned. It now SELF-HEALS: the event that exposes a missing chip is
  the one moment we know it is missing, so it respawns under the player's current tile. And since
  each panel carries its own tile strips, a panel switch re-homes every chip onto the new panel's
  strips instead of leaving them stranded under hidden ones.

### 5.3 The controls block: three marks per row, in two sections

**An ability row is a GLYPH, an ICON and a NAME.** No sentence. "Press RT to activate Boost Ring"
spends a line restating the glyph beside it, and the authored `AbilityDescription` is a *design
note* — several hundred characters of mechanism, written for engineers — which buried the row it
belonged to and dragged in prose about other modes entirely. That prose is where "Skim Race talks
about jousting" came from: the Squirrel's four abilities are Skimming / Trail Volume / Skimmer Reach
/ Boost Ring, none of which mentions a joust.

**The block is sectioned**, the way the Froglet Master Tool groups its own categories: a heading,
then the things under it. `VesselControlRow.BindSection` draws a heading using the SAME prefab with
its control parts switched off — a separate header prefab is one more asset to author and keep in
visual step, for a row that is a piece of text. A heading whose section turned out empty is taken
back, so a vessel with no authored rows never shows a bare "CONTROLS" label.

**Every row animates, headings never do.** The sweep runs over an index list of control rows
(`_sweepRows`), not over every row: leaving headings in it would spend a beat of each cycle
highlighting a word and make the travel visibly stall twice a pass. A flight row now flashes on its
turn like an ability does — a row that only dims and brightens reads as *disabled* next to one that
flashes — and only the RECHARGE stays conditional, because only an ability has one.

**A hull is shared across modes, so a mode may narrow what its card says.**
`ModeControlsLibrarySO.ModeEntry` gained two fields:

- `Abilities` — show only these elements. Empty means all four, which is the right default: they
  are the vessel's abilities and the vessel is what you fly. It exists because Skim Race and Joust
  are *both* the Squirrel, so without it the two cards say exactly the same thing about two very
  different games.
- `Vessel` — describe this hull whatever the card lists. For a card listing several (Scurry 3,
  Brood Rush 6, Freestyle 6) the panel previously required *exactly one* and drew no ability rows at
  all, so those cards showed an empty block. It now falls back to the card's FIRST vessel, and this
  field names a better one. "Nothing" is not more honest than "one of the hulls you may fly", it is
  just less useful.

### 5.4 The chip is the row's reason to exist — and its wiring is checked, not assumed

The first wirer pass bound only `icon` and `descriptionText`, so every card drew ability icons with
**no button beside them** — the exact thing the row template authored its `GlyphIcon` child for —
and section headings drew nothing at all, because `BindSection` wrote only through `nameText`,
which was also unwired. Two symptoms, one wiring function.

- `WireControlRow` now binds `chipGlyph` by NAME (`GlyphIcon`/`Glyph`/`ControlChip`/`Chip`, never
  "the first Image" — that is the row background), and CREATES a `GlyphLabel` TMP under the chip
  when the template authored none, styled off the description text. Without the label a keyboard
  player sees nothing where a pad player sees a button — not the honest blank (that is for a
  control with no keyboard equivalent) but a missing widget.
- `BindSection` draws through whichever text the row HAS (`nameText`, else `descriptionText`) —
  a heading that only knows the nicety field does not exist on the one prefab everyone authors.
- `Bind` restores the prefab's own icon sprite when a row gets none: rows are reused across
  cards, so "leave the sprite alone" meant "keep whichever ability's icon was here last".

The glyph chain itself needed nothing: `InputDeviceIconSetSwitcher.Current` defaults to a pad set,
the Squirrel's `OnlyLeft/RightStickAction` map to the triggers, and `ControlGlyphSet` carries
sprites plus LSHIFT/RSHIFT labels.

### 5.5 Every mode opens its CONTROLS section with its objective

`Tools/Build/author_mode_controls_library.py` writes one `ModeControlsLibrary` entry per
previewable mode whose first row is the mode's own `ObjectiveText` (from its `ModePreview_*.asset`)
— so a card says what you are supposed to DO before it lists the buttons, and a mode with no locked
vessel (the duel cards) still has a section to show. The script is idempotent, re-runnable after
any ObjectiveText edit, and replaces only the one row it owns — hand-authored rows, `Abilities`
filters and `Vessel` picks pass through untouched.

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

## 7.5 The Maelstrom lives in its OWN window — but not its own authority

Its layout shares almost nothing with a minigame card's (a clip instead of the live preview, a
pool list instead of the controls block), so it is a separate modal GameObject —
`MaelstromGameConfigurationModal`, `ModalWindows.MAELSTROM_GAME_CONFIGURE`.

**It carries a plain `ModalWindowManager`, never a second `ArcadeGameConfigureModal`.** Closed
modals in this project stay ACTIVE — `ModalWindowManager` hides through the CanvasGroup precisely
so `OnEnable`/`OnDisable` keep firing for children — so a second configure-modal instance would sit
subscribed to `ArcadeConfigSyncManager` alongside the first, and a client's commit would open both.
One window, one authority: `ArcadeLaunchPanel.hostModal` is how a panel says "I live in that
window", and the one modal still owns every decision.

Three consequences:

- **`ArcadeGameConfigureModal.OpenFor` is the entry point a card tile calls**, replacing
  `ModalWindowIn()` + `SetSelectedGame()`. Which window opens has to be decided *before* anything
  is shown; opening the arcade window first and closing it a frame later would flash the wrong
  window every time a player picks the Maelstrom.
- **`Hide()` on a panel with a host modal closes the WINDOW and leaves the content alone.** The
  window animates out over half a second and deactivating the content under it would cut that off
  mid-frame.
- **A close from the window's own controls is routed back through the modal.** Its X and gamepad B
  call `ModalWindowOut` directly, which is a window animating out — not the session ending, with
  clients still holding the modal open and a satellite arena still standing. `OnHostModalClosed`
  turns it into a real `CloseAndNotifyClients`, behind a reentrancy guard because that close then
  closes the window that reported it.

**`launchPanels` is necessarily a scene-instance override.** The arcade modal is a prefab and the
Maelstrom panel is a scene object, and a prefab cannot reference a scene object. It is legitimate
here rather than the drift `Docs/GAMECANVAS.md` warns about, because Menu_Main is the only scene
these modals appear in — but it is the reason the wirer writes the whole setup into the scene
instead of splitting it across two files.

## 8. The pieces

| Piece | Location | Job |
|---|---|---|
| `ArcadeLaunchPanel` | `_Scripts/UI/View/ArcadeLaunch/` | The contract: which controls a panel exposes, what the modal may ask of it |
| `MinigameLaunchPanel` / `MaelstromLaunchPanel` | same | The two concrete panels |
| `VesselControlsPanel` / `VesselControlRow` | same | The hull's abilities and their controls, derived |
| `ModeControlsLibrarySO` | `_Scripts/UI/View/ArcadeLaunch/` | Per-mode authored rows for the controls block; `Resources/ModeControlsLibrary`, default empty |
| `LobbySlotRow` / `LobbySlotView` | same | Seats, ready lights, the AI kick, the fill toggle |
| `GameBriefingView` | same | Description + rotating tips |
| `MaelstromPoolListView` / `MaelstromPoolEntry` | same | What this intensity can draw |
| `ModeVideoView` | same | The Maelstrom's clip |
| `ArcadeGameConfigureModal` | `_Scripts/UI/Modals/` | Still the one authority on config, commit, ready-up and launch |
| `TournamentDataSO.IntensityTiers` | `_Scripts/Utility/DataContainers/Tournament/` | The ladder + `GamesForIntensity` / `UnlockIntensityOf` |
| `ModePreviewDefinitionSO.PreviewCellsByIntensity` | `_Scripts/ScriptableObjects/` | Per-intensity arenas + `ResolveCell` |
| `author_preview_intensities.py` | `Tools/Build/` | Copies those lists from each mode's own scene (`--check`) |
| `ArcadeLaunchPanelWirer` | `_Scripts/Editor/` | **One-off.** Builds the two row prefabs from the authored rows, adds and wires every component, registers the Maelstrom window. Scan reports what it cannot do; retire it through the ship panel once its output is pushed |

Authored data: `SO_ArcadeGame.Tips` (per-card play tips) and `SO_ArcadeGame.PreviewVideo`
(**Maelstrom only** — every other card previews live and must never fall back to a clip).

## 9. Known limitations

- **Not verified in the Editor.** The UI these components attach to was authored in parallel
  with them; nothing here has been through play mode. Everything in §1–§7 is reasoned from the
  code it sits on, not observed.
- **Ready lights are a count** (§5.1). Per-seat identity needs the sync manager to replicate the
  ready set.
- **`SO_ArcadeGame.Tips` is empty on every card.** The briefing then shows the description and
  never rotates — the correct resting state, not a degraded one — so the panel is right but says
  less than it could until tips are written.
- **One glyph set serves both pad families**, inherited from the ability lockup: a PlayStation
  player sees Xbox `A`/`B` on the Sparrow's rows — and now reads them in the sentence too
  ("Press RT to…"), since `ControlDisplayName` uses the same one-family vocabulary. Closing it is
  one field on `ControlGlyphSetSO` and would fix both surfaces at once (`Docs/ABILITY_LOCKUP.md`).
- **The controls library ships EMPTY** (`Resources/ModeControlsLibrary.asset`: no default rows,
  no mode entries), so every card shows abilities alone until somebody authors a mode's rows.
  That is the intended default, not a stub — but per-mode teaching copy (a Joust card explaining
  the joust, a Scramble card explaining the juke steal) is authored content waiting on a pass.
- **Browsing cards stands and strikes a satellite arena per selection**, plus a networked hull
  swap for a vessel-locked mode — the pre-existing cost recorded in
  `Docs/ModePreview/ARCHITECTURE.md §7`, now paid on intensity changes too for the four modes
  with per-intensity arenas.
- **The in-Maelstrom pre-game panel is not built.** The design calls for the same panel between
  rounds *without* the domain row (domain cannot change mid-tournament); that lives in the
  Maelstrom scene and is deliberately left for its own pass.
