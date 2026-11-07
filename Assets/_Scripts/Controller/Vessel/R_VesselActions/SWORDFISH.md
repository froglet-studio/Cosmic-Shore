# Swordfish — all sword (split off the Rhino, 2026-11-07)

Garrett, 2026-11-07: *"split the rhino into two vessels: the rhino and a swordfish. the sword fish will be all about
the sword. the rhino will be all about the plow."*

The **Swordfish** (`VesselClassType.Swordfish = 30`) is the Rhino's sword kit on the Rhino's airframe. The **Rhino**
(3) keeps the plough and the ramp, and gains a plough kit of its own (`RHINO_PLOUGH.md` § "The plough kit").

| | Swordfish (30) | Rhino (3) |
|---|---|---|
| Triggers | Swing the blade (`ShieldSwipeActionExecutor`); both held energize it | Steer the plough: a pull is a small shove that way, a hold throws every ploughed prism to that side (`RhinoPloughSteerExecutor`) |
| Charge | **Energize** / L5 **Sword Steal** | **Carry** / L5 **Avalanche** |
| Mass | **Afterimage** / L5 **Danger Afterimages** | **Blade** (a plough blade across the nose) / L5 **Berm** |
| Space | **Blade Reach** / L5 **Shield Breaker** | **Furrow** / L5 **Unstoppable** |
| Time | **Ramp Spool** / L5 **Momentum** (the same ramp asset) | **Ramp Spool** / L5 **Momentum** |
| Hull on a prism | **Rams** — destroys it (`VesselDamagePrismEffect`, the fleet ram) | **Ploughs** it aside (`RhinoPloughPrismEffect`) |
| Omni crystal | **Surge** — the 8 s ring of swords | none yet (open) |
| Modes | Cleave (only), Astro League (in the Rhino's place), Broadside (in the Rhino's place), Brood Rush | Headlong, Regatta, Brood Rush |

Everything the sword was is unchanged and still documented where it was: `RHINO_ENERGY_SWORD.md` (the blade, the
energize, the two keys), `RHINO_SHIELD_SWIPE.md` (the pose rig), `RHINO_SWORD_AFTERIMAGE.md`, `RHINO_SWORD_COMBOS.md`,
`RHINO_RAMP_BOOST.md`. Read "the Rhino" there as "the Swordfish" for anything about the sword. The code keeps its
`RhinoSword*` names: the classes are the sword's, and renaming them is a separate change.

## How it was made

`Tools/Build/author_swordfish_assets.py` (`--check`, `--self-test`). It cloned `Rhino.prefab` with the sword (a fresh
Netcode `GlobalObjectIdHash`, `vesselType: 30`, its own vessel impactor container), and then stripped the sword from the
Rhino in a document-level pass gated by `dangling_refs` (vessel skill rule 46). Both passes are spent one-shots: they run
only while the Swordfish prefab is absent and the Rhino still carries the swing executor, so `--check` validates the
shipped prefabs and never diffs against a donor.

The Swordfish's hull ram is the one deliberate kit change from the Rhino: the plough is the Rhino's, so the Swordfish's
container (`SwordfishImpactorDataContainer`) carries the fleet ram where the Rhino's carries the plough. The Swordfish
still carries no prism slow, so ramming costs it no speed. In Cleave a hull ram scores again (it destroys), as it did
before the plough (2026-10-13).

## Files

| File | Role |
|---|---|
| `Assets/_Prefabs/Spacevessels/Swordfish.prefab` | The hull: the Rhino's prefab with the sword |
| `Assets/_SO_Assets/Classes/SO_Class_Swordfish.asset` | Class 30, in `SO_Classlist_All` / `_Classes` |
| `Assets/Resources/ElementalAbilityMaps/Swordfish.asset` | The four sword cards and the Surge |
| `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/SwordfishImpactorDataContainer.asset` | Ram (not plough), danger debuff, the Surge, the crystal explosion |
| `Assets/Resources/CrystalHullFusionConfig.asset` | Four class-30 entries reusing the Rhino's bakes (it draws the Rhino's mesh) |
| `Assets/_SO_Assets/Vessel Prefab Container.asset`, `Assets/DefaultNetworkPrefabs.asset` | Spawnable, networked |
| `Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs` | In the Toybox's Vessel Changer |
| `Assets/Resources/TouchControlProfiles.asset` (`author_touch_profiles.py`) | The Rhino's old phone row (lifts swing, both off energizes) |
| `Assets/_Prefabs/CORE/NetworkManager.prefab` | ProtocolVersion 25 → 26 (a new network prefab) |

Shared with the Rhino, not copied: the HUD (`RhinoHUDVariant`), the camera (`RhinoCameraSettingsSO`), the ramp
(`RhinoRampBoostAction`), the sword's configs and effects, the model (`rhino_shapekey_with_animations.fbx`).

## In-editor verification

No `unity` binary was available in the authoring session, so none of this has been seen in play. The reference compile
(`Tools/Build/unity_refcompile`, editor config) found 0 errors in the files changed here.

1. **Toybox → Vessel Changer.** The Swordfish is offered and flies exactly as the Rhino did before the split: triggers
   swing the blade, both held energize it, afterimages, the ramp, the Surge on an omni crystal.
2. **Its hull rams.** Fly it into a trail with the blade out of the way: the prisms the hull touches are destroyed
   (the Rhino ploughs them aside).
3. **Cleave** seats the Swordfish (player and four AI); the AI still cuts and the score ticks.
4. **Astro League** seats the Swordfish in the Rhino's place; a blade strike still moves the ball.
5. **Broadside**: the Swordfish's sword hits score as the Rhino's did. **Brood Rush** offers both hulls.
6. **MPPM two clients**: a Swordfish spawns on both (the new network prefab, protocol 26).
7. **Audits**: Audit Vessel Ability Rows, Skimmers, Elemental Morphs and Construction over both hulls.

## Follow-ups

- **A swordfish model.** It draws the Rhino's rig. `/vessel-model` makes its own hull; the class card, the hangar art
  (`author_hangar_card_art.py`) and the ability icons (`/ability-icon`) follow.
- **Regatta** still seats only the Rhino: its tip says "only an energised Rhino sword opens a hole", which is now the
  Swordfish's. Regatta's balance model is being re-searched on `claude/regatta-overnight-balance` (Garrett), so the
  Swordfish joins the roster there rather than in this branch.
- **Rename the `RhinoSword*` classes** to the Swordfish's when someone next works the sword (no asset names them by type).
- **The Vessel Studio** has no Swordfish page; its Rhino page is the sword.
