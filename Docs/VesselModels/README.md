# Vessel model dossiers

> These files measure WORK-IN-PROGRESS artist models and prefabs. They record what each prefab binds
> inside its FBX (the reason an in-place replacement keeps a name or a bone), not what a vessel
> should be. The intention is `.claude/skills/vessel-model/references/FLEET_CONVENTION.md`; a quirk
> recorded here is a legacy binding with a migration step there, never a rule to copy (Garrett,
> 2026-10-17: "building a framework that meets our intentions not mimic halfbaked examples").

One file per vessel, measured from the repo (prefab, FBX, `.meta`, driver code, controller) on
2026-10-17, before the seven artist files were rebuilt in place by the `/vessel-model` skill
(`Tools/Build/vessel_model/`). Each dossier is the record of what the ARTIST file contained and
the numbered constraints (section 8) a replacement meets; the specs under
`Tools/Build/vessel_model/specs/` were written against them, and every name, bone rest pose,
slot order and frame they keep is explained here.

How every vessel is coloured (the colour language, the shared materials' scheme, the card livery): [COLOR.md](COLOR.md).

| vessel | dossier | live file the prefab draws | rebuilt file (2026-10-17) | spec |
|---|---|---|---|---|
| Squirrel | [Squirrel.md](Squirrel.md) | `SquirrelVessel_CosmicShoresTest1.fbx` | the same, in place | `specs/squirrel.py` |
| Dolphin | [Dolphin.md](Dolphin.md) | `dolphin_shapekey_with_animations.fbx` | the same, in place | `specs/dolphin.py` |
| Sparrow | [Sparrow.md](Sparrow.md) | `SparrowModel1.fbx` | the same, in place | `specs/sparrow.py` |
| Serpent | [Serpent.md](Serpent.md) | `SerpentExport.fbx` | the same, in place | `specs/serpent.py` |
| Manta | [Manta.md](Manta.md) | `mantis_shapekey_with_animations.fbx` + `Manta_shapekey_rigged.fbx` (two coincident hulls, VESSEL_CONSTRUCTION.md 3.4) | `Manta_shapekey_rigged.fbx`, in place | `specs/manta.py` |
| Urchin | [Urchin.md](Urchin.md) | `Urchan_Test.fbx` (part-per-mesh placeholder) | `urchan_shapekey_with_animations.fbx`, the rig-swap target, with four REAL morphs where the artist rig had none | `specs/urchin.py` |
| Butterfly | (none: the vessel had no model; `BUTTERFLY.md` section 2 is its brief) | `Butterfly.fbx` (new, 2026-10-18), replacing the procedural `ButterflyHullBuilder` | the same | `specs/butterfly.py` |
| Thresher | (none; `THRESHER.md` section 5b) | `Thresher.fbx` (new, 2026-10-18), replacing `ThresherHullBuilder` | the same | `specs/thresher.py` |
| Scarab | (none; `SCARAB.md` section 3.0) | `Scarab.fbx` (new), replacing `ScarabHullBuilder` and its hidden Sparrow | the same | `specs/scarab.py` |
| Stoat | (none; `STOAT.md` section 2.1) | `Stoat.fbx` (new), replacing `StoatHullBuilder` | the same | `specs/stoat.py` |
| Angler | (none; `ANGLER.md` section 9) | `Angler.fbx` (new), replacing the Stoat stand-in | the same | `specs/angler.py` |
| Brittle Star | (none; `BRITTLE_STAR.md` section 2) | `BrittleStar.fbx` (new), replacing `BrittleStarHullBuilder` | the same | `specs/brittlestar.py` |
| Orca | [Orca.md](Orca.md) | `Orca.fbx` (new, 2026-10-21; rebuilt the same day as the first HOVERING ASSEMBLY to SKILL 0.5: 17 / 17 parts float 1.55-2.26 % clear, `assembly_report.py --floating` OK), the 4-seat crew hull with three code-aimed turrets, nested in `Orca.prefab` (`author_orca_assets.py`) and posed by `OrcaHullRig` | the same | `specs/orca.py` |
| Shrike | (none; `SHRIKE.md` section 2) | `Shrike.fbx` (new), replacing `ShrikeHullBuilder` | the same | `specs/shrike.py` |
| Starling | (none; `STARLING.md` section 6) | `Starling.fbx` (new, 2026-10-20); each swarm member is a copy of it | the same | `specs/starling.py` |

| Grouper | [Grouper.md](Grouper.md) | `Grouper.fbx` (new, 2026-10-20), the Wrasse's carrier hull, drawn by `Wrasse.prefab` in place of the Sparrow's model | the same | `specs/grouper.py` |
| Wrasse Kite | [WrasseKite.md](WrasseKite.md) | `WrasseKite.fbx` (new, 2026-10-20), the navigator's kite, drawn where the kite flies (`WrasseCrewBodies`) | the same | `specs/wrassekite.py` |
| Marlin | [Marlin.md](Marlin.md) | `Marlin.fbx` (new, 2026-10-21), Marlin and Jacks' hull, drawn by `Marlin.prefab` in place of the Rhino's rig | the same | `specs/marlin.py` |
| Jack | [Jack.md](Jack.md) | `Jack.fbx` (new, 2026-10-21), the two herders, drawn where the jacks run (`MarlinCrewBodies`) | the same | `specs/jack.py` |
| Rhino | [Rhino.md](Rhino.md) | `Rhino_Test.fbx` (part-per-mesh placeholder) | `rhino_shapekey_with_animations.fbx`, the rig-swap target, with four REAL morphs | `specs/rhino.py` |

**2026-10-18, withdrawn after the owner's Editor test:** the rebuilt Sparrow flew backwards, the Urchin did not
change in the game (its prefab draws the placeholder file), and the Squirrel, Dolphin and Sparrow read as cheap
remakes. The seven artist files are restored. The from-scratch specs moved to `specs/experimental/`; the path now
is the ARTIST'S hull plus the framework, one vessel per round, proven by the owner flying it (`LESSONS.md` 38-40).
The first: the **Squirrel** (`specs/squirrel.py`), the artist's mesh and the artist's own takes (retargeted, proven
part by part by `take_parity.py`) with a tail spine and a stronger Mass morph; `sheets/squirrel_after.jpg` is that file.
**It passed the owner's Editor test on 2026-10-18** ("I can't tell it isn't the original", with its elements
upgraded and its domain changed); `.claude/skills/vessel-model/SKILL.md` section 4 is the loop to repeat.
The **Dolphin** is the loop's second vessel (`specs/dolphin.py`): reproduced exactly, nothing visible added, for the
owner's Editor test. The **Butterfly** is the first hull with no artist file at all (`specs/butterfly.py`,
`sheets/butterfly_after.jpg`): a new `Butterfly.fbx` that replaces the procedural placeholder, built to the fleet
convention and the fleet's look, for the owner's Editor test (`LESSONS.md` 44).
**The fleet pass (2026-10-18, Garrett: "please continue with the rest of the fleet, and get the card images etc.
updated as well"):** the **Sparrow**, **Serpent** and **Manta** are reproduced exactly through the loop
(`specs/sparrow.py`, `serpent.py`, `manta.py`; every take, morph, material and node proven against the artist
file; sheets `sheets/<vessel>_after.jpg`). The **Urchin** and **Rhino** got the rig swap: since 2026-10-18 their prefabs draw the artist's
RIG (`urchan_shapekey_with_animations.fbx`, `rhino_shapekey_with_animations.fbx`) with four real morphs instead of the
part-per-mesh placeholders (`sheets/urchin_rig_swap.jpg`, `rhino_rig_swap.jpg`). The procedural hulls (**Thresher**,
**Scarab**, **Stoat**, **Angler**, **Brittle Star**, **Shrike**) got new models like the Butterfly's
(`sheets/<vessel>_after.jpg`). Card images for every hull whose look changed are rendered from its model by
`Tools/Build/vessel_model/render_cards.py`.

**Round 3, 2026-10-17 (after a review of the gates; files since withdrawn):** the artist-reference gate had been dead (a swallowed
`NameError`), the drawn-size reader had read each artist file with all four morphs at 100%, and no check told an
inverted pose from a correct one. With those fixed (`LESSONS.md` 32-36) the files were rebuilt: the artist's slot
colours are in every file; the Dolphin's wings are back at the dossier's +-2.64; the Squirrel sits at the heights its
parked pose draws (1.23 tall); the Sparrow's tail vanes span the artist's -0.82..+0.96 and its poses are written in
the file's own frame; yaw is a negative rotation about up on every hull (it was inverted on four) and roll drops the
right side (inverted on five); the Urchin's appendages are modelled 1.75x out, as Unity draws the artist rig. Every
spec now carries `pose_expect`, so the re-import proves each take's direction.

**Before / after, for anyone judging the look without Unity:** `sheets/<vessel>_before.jpg` is the artist file's
Cycles contact sheet (four views, the four morph extremes, four poses) and `sheets/<vessel>_after.jpg` the rebuilt
file's, rendered from the FBX re-imported with studio clay per slot (tan = Body, lime = Engine, cyan = Window, the
domain band in each vessel's colour). The Urchin and Rhino have a third sheet, `*_live_placeholder_before.jpg`: the
part-per-mesh hull their prefabs still draw. The chase tile (top left) is the one that matters: it is the hull at
the size the game shows it.

Two cross-vessel rule sets, measured the same day: [RULES_unity_import.md](RULES_unity_import.md) (what Unity's importer,
the metas and the prefab stubs bind, per file) and [RULES_puppetry.md](RULES_puppetry.md) (every driver's parameter signs,
blend trees and measured take deflections). Both describe today's bindings; the intention is the fleet convention.

Regenerate any number: `python3 Tools/Build/vessel_model/fbx_probe.py "<fbx>" --takes`,
`python3 Tools/Build/vessel_model/part_map.py "<fbx>"`,
`python3 Tools/Build/measure_vessel_models.py --no-refs --only <stem>`,
`python3 Tools/Build/element_ability_table.py <Vessel>`.

Where a dossier and `Docs/VESSEL_CONSTRUCTION.md` or a vessel's `R_*.md` disagree, the dossier
names the asset it read and the asset is the record (each dossier's header says so). The dossiers
were written by independent readers in one session; the skill's `references/CONTRACT.md` is the
cross-vessel digest and is what a modelling session reads first.

## Follow-ups from the ship review (2026-10-18, `/ship-deep` of `ccr-d93a6dfb-ugbxng`)

Three independent re-reads of the branch's C#. The blocker (the Brittle Star's thrown ring tumbled about the hull's
UP, because a Z-up FBX's mesh node keeps the file frame) and the should-fixes (the flares cloned materials on
every skinned hull; the Manta family painted the domain through the cloning path; the Thresher hitch lost its
morph tracking on a re-init) were fixed on the branch. These were left, each with the evidence that names it.
None blocks building on the branch. Team task T-20261018-2c7e.

| # | what | evidence | kind |
|---|---|---|---|
| 1 | Confirm `SkinnedMeshRenderer.BakeMesh(mesh, false)` bakes WITHOUT the transform's scale. `FoldActionExecutor` (the Fold ghost) then sets `localScale = source.lossyScale`, and `BrittleStarHullModel.DressThrownRing` re-applies the scale through `TransformPoint`. If `false` keeps the scale, both come out at s^2. | Latent: every hull's `lossyScale` is 1 today, except during the Butterfly's arrival bloom (`ApplyVisualScale` < 1 while a Fold starts). One Editor look at a Fold started mid-bloom settles it. | check |
| 2 | The Shrike's lance leaves `gun.mouth`, a bone the Space morph does not move. At high Space the beam starts inside the lengthened barrel. | `ShrikeExecutor.cs` beam origin; `specs/shrike.py` (Space lengthens the barrel). The Brittle Star solved the same thing with mouth travel read from the blend-shape deltas (`BrittleStarHullModel.ReadMouthTravel`). | visual |
| 3 | The Thresher's pectoral comment says the inside fin's leading edge goes UP. The code puts it DOWN on a right turn (`attack = side*yaw*twist`, then `Euler(+X)`). | The `ThresherAnimation.cs` pectoral loop; the attack line predates this branch. The owner decides which is meant. | sign |
| 4 | `RhinoAnimation` makes its bones follow the parts in `Update`, while the Urchin's do it in `LateUpdate`. A part moved later in the frame draws one frame behind its collider. On a prefab without the rig, the follow lookups also report `wing1.l` etc. as unresolved, though the parts animate. | `RhinoAnimation.cs` FollowParts / ResolveParts; `UrchinAnimation.cs` LateUpdate. Latent: only `Rhino.prefab` uses it, and it has the rig. | nit |
| 5 | `VesselCustomization.ResolveModelGeometries` REPLACES a renderer-less geometry with its child SkinnedMeshRenderer, so a collider-bearing parent (the Rhino's `Rhino_Test (1)`) leaves `ShipGeometries`. | Only `GhostAction` and `UrchinSlipActionExecutor` read colliders from `ShipGeometries`, both Urchin-only, so nothing breaks today. Appending the renderer, or replacing only collider-less geometry, is safer. | latent |
| 6 | `GhostAction` has no caller. Its `Initialize` skips `base.Initialize`, so `Vessel` would be null. | `grep -rn GhostAction Assets --include=*.cs` shows only its own file; the Urchin prefab holds only the component. Deletion PROPOSAL: a guid sweep does not see a `Resources.Load` or a type reference from Amoebius (`Port/` compiles `Assets/_Scripts`). | dead? |
| 7 | The six procedural hull builders (`Scarab/Shrike/Stoat/Thresher/Butterfly/BrittleStarHullBuilder`) are referenced by NO prefab, scene or asset. Only the fallback branches of their animation and executor scripts name them. | Script-guid sweep over `Assets/**/*.{prefab,unity,asset}`: 0 each. Deletion PROPOSAL, with their fallback branches. The `*HullForm` classes stay (the harnesses, tests and `BrittleStarHullModel` read `Defaults`). Check Amoebius first. | dead? |
| 8 | `BrittleStarHullModel` reads the whole mesh (bone weights twice, bindposes, four blend-shape frames, every submesh's triangles) on each spawn. | Once per spawn, not per frame, but an AI backfill spawns several. Cache statically per `sharedMesh`. | perf |
| 9 | `updateWhenOffscreen = true` on the seven skinned hulls recomputes their bounds every frame, off screen too, AI included. | Deliberate (the morphs and the Rhino's fold reach past the bind bounds). Padding `localBounds` once would cost nothing per frame. | perf |
