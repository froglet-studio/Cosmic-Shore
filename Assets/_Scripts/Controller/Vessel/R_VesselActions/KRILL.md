# The Krill — the passive swarm card vessel (vessel class 21)

The Krill is a swarm you never steer. Twenty-four krill roam the cell on their own after the objective of the moment.
A four-phase cycle moves that objective on, and **the pilot does two things only: turns the view and picks cards.**
The cards are the build: every rule that is not the bare swarm is a card, and each card's numbers are its own at the
level you hold.

Sources (the lab is the source of truth; this vessel is its clone):

- The cards: `Docs/Studios/SWARM_CARD_VESSEL.md` and `Docs/Studios/SwarmCardVessel/cards.json` (80 cards, 24 wired).
- The lab: `Docs/Studios/KRILL_LAB.md`, page `Docs/Studios/KrillLab.html` (artifact
  https://claude.ai/artifact/QsSVVTcxL4PxXsrK2MBeVY). Its `SHIPPED` block is `KrillConfigSO`'s first section, and its
  `simStep` is `KrillSwarmExecutor`, step for step.

Everything below is generated or checked by `python3 Tools/Build/author_krill_assets.py` (`--check` is the drift gate,
`--self-test` its negative controls). Change the lab or `cards.json` first, then re-run the generator.

## 1. Controls

| Input | Pad | What it does |
|---|---|---|
| Left stick | LS | **Turns the view** about its own up and right axes (`viewTurnRate`, 90°/s at full tilt). It stops when you let go. The view follows the swarm's centroid (`followRate`). |
| Pick the Mass card | A | Takes the hand's Mass card. |
| Pick the Time card | B | Takes the hand's Time card. |
| Pick the Charge card | X | Takes the hand's Charge card. |
| Pick the Space card | Y | Takes the hand's Space card. |

Mass on A and Time on B are the Sparrow's and the Angler's layout. A pick with no hand dealt does nothing. Throttle and
boost do nothing: the carrier has speed 0, so it lays no trail.

## 2. The phase cycle

Omnicrystal (30 s) → Elemental crystals (25 s, goal 5) → Opposing vessels (25 s) → Opposing mass (25 s, goal 30), then
round again. A phase ends on its timer or when its goal is met. The swarm's target in each phase is the nearest omni
crystal, elemental crystal, rival vessel or rival prism. **A hand of four cards, one per element, is dealt when the
Omnicrystal phase ends**, with a crystal or without one. The swarm keeps going while you read. A card you already hold
adds its level, up to 15. An AI Krill picks on its own 2 s after a hand arrives.

## 3. What the swarm does (the game's own things)

- **Crystals** are collected through this vessel's own skimmer and vessel impactor, so every crystal effect, score and
  fusion runs as it would for a hull flying into them (`crystalReach` 28, `omniReach` 20).
- **Rival prisms** (another domain's, `PrismSpatialIndex`) are bitten (`baseBite`), stolen or shield-stripped by the
  cards. Health prisms are never touched. New prisms are laid through `VesselPrismController.PrismSpawnChannel`.
- **Rival vessels**: every nip, sting, laser zap and EMP is a scored combat hit through `CombatHitDrain`. **PvP is
  petals only** (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`): no card stuns, silences or takes control of a vessel.
- **Members** are vessel agents like the Termite's drones, not lifeforms. Each is a pooled copy of the Krill model, and
  copies its materials and element morphs once a second.
- **The protected minimum.** A member dies only on contact with an opposing danger prism, and never at or below the
  minimum size (6; Big Family raises it). Shells and Blink Step also save it.

## 4. Stand-ins (where the lab's rule has no game counterpart)

| Lab rule | In the game | Why |
|---|---|---|
| EMP silences a rival | A scored Debuff hit (petals) | Nothing silences a vessel; no stuns. |
| Short Circuit knocks crystals loose | A Debuff hit, which ejects petals as crystals | The Debuff class already does that. |
| Rival shots kill krill | Only opposing danger prisms kill krill | No rival vessel fires at a swarm in the game. |
| A replicated swarm | Single player first: peers draw the owner's results | Picks replicate; only the deciding peer touches the world. |
| Fusion entries | Copied from the Angler's | Pending a Krill-specific design. |
| L5 upgrades | None in `ElementalAbilityMaps/Krill.asset` | Never invented; Garrett decides them. |

## 5. The HUD

The hand rides the fleet's four-icon row (`KrillHUDView`, `KrillHUDController`). Order: charge · mass · space · time.
Over each icon the offered card's name, under it the level it arrives at; the icon brightens and its gauge turns lime
while it is offered. With no hand, the icons dim and the label shows what you hold of that element. Above the row: the
phase and its seconds left, and the swarm's size. Every word is an authored field on the view (`phaseNames` and five
templates; tokens `{name}` `{level}` `{phase}` `{seconds}` `{count}` `{cap}`); the card names are the deck's.

## 6. Files

| What | Where |
|---|---|
| Class id | `VesselClassType.Krill = 21` (20 is the Orca's, in flight on `cece/happy-noether-m54hz6`) |
| Rules (pure C#, tested) | `Controller/Vessel/KrillRules.cs`; tests `Tests/Editor/KrillRulesTests.cs` (132 card goldens from `cards.json`); offline harness `Tools/Build/krill_rules_parity/run.sh` (`--self-test`) |
| The swarm | `R_VesselActions/Executors/KrillSwarmExecutor.cs` |
| Config and deck | `KrillConfigSO.cs`, `KrillCardDeckSO.cs`; assets under `Assets/_SO_Assets/VesselActions/Krill/` |
| Picks | `KrillActionSO.cs`; `KrillPick{Charge,Mass,Space,Time}.asset` |
| The view | `Controller/Vessel/KrillVesselTransformer.cs`; camera `KrillCameraSettingsSO.asset` |
| Model | `Assets/_Models/Vessel Models/Krill.fbx` from `Tools/Build/vessel_model/specs/krill.py` (four element morphs); `KrillAnimation.cs` |
| Prefab | `Assets/_Prefabs/Spacevessels/Krill.prefab` (a document clone of the Angler's, by the generator) |
| HUD | `KrillHUDVariant.prefab`; icons `Assets/_Graphics/Icons/AbilityIcons/Krill/` (`author_krill_icons.py`) |
| Class card | `SO_Class_Krill.asset`, card art by `render_cards.py` |
| Toybox | `ToyVesselRoster` (the freestyle Vessel Changer) |
