# Vessel model dossiers

One file per vessel, measured from the repo (prefab, FBX, `.meta`, driver code, controller) on
2026-10-17, before the seven artist files were rebuilt in place by the `/vessel-model` skill
(`Tools/Build/vessel_model/`). Each dossier is the record of what the ARTIST file contained and
the numbered constraints (section 8) a replacement meets; the specs under
`Tools/Build/vessel_model/specs/` were written against them, and every name, bone rest pose,
slot order and frame they keep is explained here.

| vessel | dossier | live file the prefab draws | rebuilt file (2026-10-17) | spec |
|---|---|---|---|---|
| Squirrel | [Squirrel.md](Squirrel.md) | `SquirrelVessel_CosmicShoresTest1.fbx` | the same, in place | `specs/squirrel.py` |
| Dolphin | [Dolphin.md](Dolphin.md) | `dolphin_shapekey_with_animations.fbx` | the same, in place | `specs/dolphin.py` |
| Sparrow | [Sparrow.md](Sparrow.md) | `SparrowModel1.fbx` | the same, in place | `specs/sparrow.py` |
| Serpent | [Serpent.md](Serpent.md) | `SerpentExport.fbx` | the same, in place | `specs/serpent.py` |
| Manta | [Manta.md](Manta.md) | `mantis_shapekey_with_animations.fbx` + `Manta_shapekey_rigged.fbx` (two coincident hulls, VESSEL_CONSTRUCTION.md 3.4) | `Manta_shapekey_rigged.fbx`, in place | `specs/manta.py` |
| Urchin | [Urchin.md](Urchin.md) | `Urchan_Test.fbx` (part-per-mesh placeholder) | `urchan_shapekey_with_animations.fbx`, the rig-swap target, with four REAL morphs where the artist rig had none | `specs/urchin.py` |
| Rhino | [Rhino.md](Rhino.md) | `Rhino_Test.fbx` (part-per-mesh placeholder) | `rhino_shapekey_with_animations.fbx`, the rig-swap target, with four REAL morphs | `specs/rhino.py` |

**Before / after, for anyone judging the look without Unity:** `sheets/<vessel>_before.jpg` is the artist file's
Cycles contact sheet (four views, the four morph extremes, four poses) and `sheets/<vessel>_after.jpg` the rebuilt
file's, rendered from the FBX re-imported with studio clay per slot (tan = Body, lime = Engine, cyan = Window, the
domain band in each vessel's colour). The Urchin and Rhino have a third sheet, `*_live_placeholder_before.jpg`: the
part-per-mesh hull their prefabs still draw. The chase tile (top left) is the one that matters: it is the hull at
the size the game shows it.

Regenerate any number: `python3 Tools/Build/vessel_model/fbx_probe.py "<fbx>" --takes`,
`python3 Tools/Build/vessel_model/part_map.py "<fbx>"`,
`python3 Tools/Build/measure_vessel_models.py --no-refs --only <stem>`,
`python3 Tools/Build/element_ability_table.py <Vessel>`.

Where a dossier and `Docs/VESSEL_CONSTRUCTION.md` or a vessel's `R_*.md` disagree, the dossier
names the asset it read and the asset is the record (each dossier's header says so). The dossiers
were written by independent readers in one session; the skill's `references/CONTRACT.md` is the
cross-vessel digest and is what a modelling session reads first.
