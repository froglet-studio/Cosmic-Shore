# The Starlings — the passive swarm card vessel (vessel class 21)

**Renamed from the Krill on 2026-10-20** (Garrett: "the krill will be a swarm of vessels lets change the to the starlings"); the model became a starling, the rules and numbers did not change. The Starlings is a swarm you never steer. Twenty-four starlings roam the cell on their own after the objective of the moment.
A four-phase cycle moves that objective on, and **the pilot does two things only: turns the view and picks cards.**
The cards are the build: every rule that is not the bare swarm is a card, and each card's numbers are its own at the
level you hold.

Sources (the lab is the source of truth; this vessel is its clone):

- The cards: `Docs/Studios/SWARM_CARD_VESSEL.md` and `Docs/Studios/SwarmCardVessel/cards.json` (80 cards, 24 wired).
- The lab: `Docs/Studios/STARLINGS_LAB.md`, page `Docs/Studios/StarlingsLab.html` (artifact
  https://claude.ai/artifact/QsSVVTcxL4PxXsrK2MBeVY). Its `SHIPPED` block is `StarlingConfigSO`'s first section, and its
  `simStep` is `StarlingSwarmExecutor`, step for step.

Everything below is generated or checked by `python3 Tools/Build/author_starling_assets.py` (`--check` is the drift gate,
`--self-test` its negative controls). Change the lab or `cards.json` first, then re-run the generator.

## 1. Controls

Round 2 (Garrett, 2026-10-21): zoom and roll on the stick diffs, pinch and spin on a phone, and card details on a held
trigger or a long press. Round 3 (Garrett, 2026-10-21): the camera follows flight's sense (the sums keep turning the view
with two thumbs down; the roll was flipped), the details list what every held card does and can be browsed across the
four elements, and the swarm is a murmuration: 5x the members (start 120, cap 200, protected minimum 30, the lab's
SHIPPED too), with the flock's separation on a grid so its cost stays linear in the members. The class is named
**Starlings** on its card.

| Input | Pad | Phone | What it does |
|---|---|---|---|
| Both sticks together (the sums) | LS + RS | one thumb | **Turns the view** about its own up and right axes (`viewTurnRate`, 90°/s at full tilt). It stops when you let go. The view follows the swarm's centroid (`followRate`). |
| x-diff (sticks spread apart / squeezed) | LS-x vs RS-x | **pinch** | **Zooms**: spread = in, squeezed = out, at `zoomRate`; a pinch zooms 1:1 with the fingers. Held between `zoomNearest` (0.35) and `zoomFarthest` (2.5) times the camera's authored distance. |
| y-diff (one stick up, the other down) | LS-y vs RS-y | **two-finger spin** | **Rolls the camera** about the view's axis at `rollRate` (90°/s); a spin rolls 1:1 with the fingers. The camera rolls AGAINST y-diff and the spin, so the world turns the way a hull rolls in flight and with the fingers (Garrett, 2026-10-21: the first cut turned it the flipped way). The sums keep turning the view while two thumbs are down, as in flight. |
| Pick the Mass card | A | tap the Mass card | Takes the hand's Mass card, on RELEASE. |
| Pick the Time card | B | tap the Time card | Takes the hand's Time card. |
| Pick the Charge card | X | tap the Charge card | Takes the hand's Charge card. |
| Pick the Space card | Y | tap the Space card | Takes the hand's Space card. |
| Card details | a trigger held + A/B/X/Y, or hold A/B/X/Y | **long press** on a card | Shows that card's details (§5) for as long as it is held, and picks nothing. A press held past `detailsHoldTime` (0.45 s) or made with a trigger past `detailsTrigger` opens them. |
| Browse the details | while they are open, press another of A/B/X/Y | touch another card | Turns the open details to that element and picks nothing (Garrett, 2026-10-21: "peruse all your abilities"). They stay open while any pick button is down and close with the last one. |

Mass on A and Time on B are the Sparrow's and the Angler's layout. A pick with no hand dealt does nothing. Boost does
nothing: the carrier has speed 0, so it lays no trail. x-diff and y-diff inside `diffDeadZone` do nothing, so a resting
pad never drifts. On a phone the diffs are read only from the pinch and spin (the touch mix holds x-diff after a thumb
lifts, which as a rate would keep zooming). The camera is written only by the local pilot while this hull is the
camera's target, and handed back to its authored distance only if nobody else moved it since.

## 2. The phase cycle

Omnicrystal (30 s) → Elemental crystals (25 s, goal 5) → Opposing vessels (25 s) → Opposing mass (25 s, goal 30), then
round again. A phase ends on its timer or when its goal is met. The swarm's target in each phase is the nearest omni
crystal, elemental crystal, rival vessel or rival prism. **A hand of four cards, one per element, is dealt when the
Omnicrystal phase ends**, with a crystal or without one. The swarm keeps going while you read. A card you already hold
adds its level, up to 15. An AI Starling picks on its own 2 s after a hand arrives.

## 3. What the swarm does (the game's own things)

- **Crystals** are collected through this vessel's own skimmer and vessel impactor, so every crystal effect, score and
  fusion runs as it would for a hull flying into them (`crystalReach` 28, `omniReach` 20).
- **Rival prisms** (another domain's, `PrismSpatialIndex`) are bitten (`baseBite`), stolen or shield-stripped by the
  cards. Health prisms are never touched. New prisms are laid through `VesselPrismController.PrismSpawnChannel`.
- **Rival vessels**: every nip, sting, laser zap and EMP is a scored combat hit through `CombatHitDrain`. **PvP is
  petals only** (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`): no card stuns, silences or takes control of a vessel.
- **Members** are vessel agents like the Termite's drones, not lifeforms. Each is a pooled copy of the Starlings model, and
  copies its materials and element morphs once a second.
- **The protected minimum.** A member dies only on contact with an opposing danger prism, and never at or below the
  minimum size (6; Big Family raises it). Shells and Blink Step also save it.

## 4. Stand-ins (where the lab's rule has no game counterpart)

| Lab rule | In the game | Why |
|---|---|---|
| EMP silences a rival | A scored Debuff hit (petals) | Nothing silences a vessel; no stuns. |
| Short Circuit knocks crystals loose | A Debuff hit, which ejects petals as crystals | The Debuff class already does that. |
| Rival shots kill starlings | Only opposing danger prisms kill starlings | No rival vessel fires at a swarm in the game. |
| A replicated swarm | Single player first: peers draw the owner's results | Picks replicate; only the deciding peer touches the world. |
| Fusion entries | Copied from the Angler's | Pending a Starling-specific design. |
| L5 upgrades | None in `ElementalAbilityMaps/Starling.asset` | Never invented; Garrett decides them. |

## 5. The HUD

The hand rides the fleet's four-icon row (`StarlingHUDView`, `StarlingHUDController`). Order: charge · mass · space · time.
Over each icon the offered card's name, under it the level it arrives at; the icon brightens and its gauge turns lime
while it is offered. With no hand, the icons dim and the label shows what you hold of that element. Above the row: the
phase and its seconds left, and the swarm's size. Every word is an authored field on the view (`phaseNames` and five
templates; tokens `{name}` `{level}` `{phase}` `{seconds}` `{count}` `{cap}`); the card names are the deck's.

**The objective card** (2026-10-21) sits directly left of the omnicrystal's card: a core card,
`CoreAbility.Objective`, with no control chip. Its glyph is the phase's target (`objectiveIcons`): the game's own omnicrystal icon,
the four element petals as one flower (Elemental crystals), a crosshair on a hull (Opposing vessels), and prism blocks
cut into triangles by an X of negative space (Opposing mass; a prism is made of triangles). The last three are drawn by
`author_starling_icons.py` (Garrett's notes, 2026-10-21), and it **drains
from the top** as the phase timer runs out, over a faint copy of itself (`objectiveSpentColor`). The lockup packs the
core cards it draws against the elemental row, so a hull with the objective and the omnicrystal card shows no gap, and
the Squirrel's drift and omni cards sit where they always did.

**The card details** (a trigger held while picking, or a long press) show a **large card in the middle of the screen**
(on the root canvas, so no HUD layout can push it off screen; round 2 placed a panel above the header and Garrett did not
see it): the element's glyph in its colour, the card's name large, the level it arrives at, then its text, each number
at that level, and its next jolt above the level you hold. With no hand dealt it lists every card you hold of that
element with what it does: its heading and level, its text, each number at the held level and the jolts it has reached
(Garrett, 2026-10-21: "list what each ability does not just the ability name and level"). Pressing another element's
button while the card is open turns it to that element, so every ability can be perused without letting go. Every line is an authored template on the view (`detailsTitleTemplate`, `detailsLevelTemplate`,
`detailsStatTemplate`, `detailsMilestoneTemplate`, `detailsHeldTemplate`, `detailsReachedTemplate`, `detailsNothingHeld`,
`detailsBrowseHint`); the card words are the
deck's.

**Level scaling** (Garrett, 2026-10-24: "the cards and current level display need to show what the next level will do
to give them a sense of level scaling"). Picking a card ADDS its level to the one you hold (`StarlingRules.Pick`), so:
- **The row**: a hand card you already hold reads `Lv 3 → 7` (`cardLevelUpTemplate`, tokens `{from}` `{to}`); a new
  card still reads `Lv {level}`.
- **The details of a hand card** show the level held and the level the pick reaches (`detailsLevelUpTemplate`), each
  number at both (`detailsStatUpTemplate`: `Range: 150u → 165u`), every jolt the pick passes (`detailsUnlockTemplate`,
  so a new card arriving at Lv 6 lists its Lv 5 jolt), then the next jolt beyond. A new card shows its numbers at the
  level it arrives at.
- **The details with no hand** head each held card `Lv 3 → 4` (`detailsHeldUpTemplate`), set each number beside the next
  level's, and add the card's next jolt under the ones it has reached.
- A number that does not change between the two levels (a count that rounds to the same value, a step not yet reached)
  reads once through `detailsStatTemplate`; a card at 15 shows `detailsMaxLevel`. The formatting is
  `StarlingHUDController.StatLine` / `PickedLevel`, held by `StarlingHUDLevelScalingTests`.

## 6. Files

| What | Where |
|---|---|
| Class id | `VesselClassType.Starling = 21` (20 is the Orca's, in flight on `cece/happy-noether-m54hz6`) |
| Rules (pure C#, tested) | `Controller/Vessel/StarlingRules.cs` (also `StarlingPilotControls`, `StarlingPickPress`); tests `Tests/Editor/StarlingRulesTests.cs` (132 card goldens from `cards.json`); offline harness `Tools/Build/starling_rules_parity/run.sh` (`--self-test`) |
| The swarm | `R_VesselActions/Executors/StarlingSwarmExecutor.cs` |
| Config and deck | `StarlingConfigSO.cs`, `StarlingCardDeckSO.cs`; assets under `Assets/_SO_Assets/VesselActions/Starling/` |
| Picks | `StarlingActionSO.cs`; `StarlingPick{Charge,Mass,Space,Time}.asset` |
| The view | `Controller/Vessel/StarlingVesselTransformer.cs`; camera `StarlingCameraSettingsSO.asset` |
| Model | `Assets/_Models/Vessel Models/Starling.fbx` from `Tools/Build/vessel_model/specs/starling.py` (four element morphs); `StarlingAnimation.cs` |
| Prefab | `Assets/_Prefabs/Spacevessels/Starling.prefab` (a document clone of the Angler's, by the generator) |
| HUD | `StarlingHUDVariant.prefab`; icons `Assets/_Graphics/Icons/AbilityIcons/Starling/` (`author_starling_icons.py`) |
| Class card | `SO_Class_Starling.asset`, card art by `render_cards.py` |
| Toybox | `ToyVesselRoster` (the freestyle Vessel Changer) |
