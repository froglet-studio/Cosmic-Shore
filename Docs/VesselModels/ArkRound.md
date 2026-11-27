# The round Ark: model dossier

`ArkRound.fbx` is built from `Tools/Build/vessel_model/specs/ark_round.py`. Since 2026-11-25 it is the Ark every
voyage draws (`Wander_WithArk.asset` -> `ArkRoundModel.asset`). It replaces [ArkReverse](ArkReverse.md), whose files
and definition stay unchanged and unused.

## The brief (Shombith, 2026-11-24 / 25)

The design was settled on the concept board (https://claude.ai/artifact/DhVb8uuZekpv6yj1KRcuNM), where Shombith picked
concept D, round 13. Its features:

- glass at the front;
- a solid back wall in the mouth, with the omni crystal centred in the open gap;
- the pods moved out onto cables;
- no turret shapes;
- a junction hall at the stern, with three straight hollow exits running out of it;
- the real omni crystal, neutral (the lime call to action, never a team colour);
- the in-game look.

The engines use Trail 3, "Eight streams": every engine leaves its own straight stream in the domain colour.

Standing rules:

- The body is black, and the prisms carry the game's tiers.
- Nothing glows except the trails.
- Every piece is the game's block prism.
- Prisms never animate on their own. They ride turning parts that alternate direction.

## How it is built

The geometry is the artifact's own code, not a copy of it. `specs/ark_round_geometry.js` is the concept page's
geometry, verbatim, plus one number: `PC`, the prism cell scale. At `PC=1` it reproduces the page exactly. Run under
node, it exports polygons, prism records, motions and the omni crystal's place as JSON, and `ark_round.py` builds the
FBX from that.

**Frame.** Artifact (x is the nose axis, y is up) maps to Unity as (az, ay, ax - CentreX) * Scale:

- Scale = 1.154979, chosen so the nose-to-jets length is 196.
- CentreX = -19.45.

The rig table stores both numbers (`artifactScale`, `artifactOrigin`). `ArkRoundModelTests` holds the C# solid to them.

**Size in the game.** `ArkRoundModel.asset` draws the model at `scale: 2.3`, so the Ark is about 451 long. That keeps
the artifact's proportion against its Squirrel for scale (2.42 long there, 6.451 in the game): about 70 Squirrels. At
scale 1 it was 30 Squirrels, and Shombith's playtest (2026-11-26) found it too small and too short against the artifact.
`Definition_DrawsTheArkAtTheArtifactsSize_AgainstASquirrel` holds it there. Everything the model carries scales with it:
sockets, prisms, solid, streams, crystal and launch tubes. The tube's open radius is 17 world units (inside its turning rings), the hall's 39 and
each exit's 16 (round 15 narrowed both, below). The voyage reads the Ark's half length from the model (`Ark.HalfLengthOr`), not from the spindle's
`arkHullLength`, for the intro's framing, the squad's slots and the crystals a raid sheds.

**Prism budget.** `PRISM_CELL = 2.2` gives **637 hull prisms**:

| Tier | Count |
|---|---|
| Plain | 353 |
| SuperShielded | 129 |
| Shielded | 115 |
| Danger | 40 |

The artifact draws 1,985. Changing that one number changes the count: 2.0 gives 667 and 2.4 gives 556. Danger prisms
sit only at the mouth's front lip and the stern.

**Turning parts** are in the rig table under `spins`. `ArkModel` turns them; their prisms ride the bones.

| Bone | Speed | Direction |
|---|---|---|
| `deck.0`..`deck.4` | 5.73°/s | each turns against its neighbour |
| `gate.0`, `gate.1` | 8.02°/s | opposed to each other |
| `pods` | 6.88°/s | turn about the nose axis |

**Engines.**

- `engine.0` is the big bell. `engine.1`..`7` are the ring of seven.
- Each bell is puppeted by the turn takes: pitch, yaw, and roll (roll as a differential).
- Each bell carries a stream under `trails`, drawn by `ArkEngineTrails`. A stream's colours are the domain's
  `TrailCoreColor` -> `TrailHighlightColor`, and it fades out over 7 s.

**Omni crystal.** It is the game's own mesh and material (`OmniCrystalExport`, `CrystalMaterial`), on the `omni` bone
inside the turning `omni.ring` collar. A property block gives it the colour set's environment CTA (bright and dark),
the same neutral as every other omni crystal.

**Launch tubes.** `hangar.slot.0..2` (pads) and `hangar.exit.0..2` (mouths) sit in the three stern exits:

- 0 is Port.
- 1 is Starboard.
- 2 is Top.

`ArkFormation.Hangar` launches each seat out of its own exit.

**The turning rings** (concept page round 15, 2026-11-26; Shombith: "I want the inside to be cooler and the radius can
be a bit smaller", then "Let's do B"). The tube's wall radius went from 9.2 to 8 artifact units (24 to 21 in game) and the
exits' from 8.2 to 7.13 in proportion. Inside, the tube is a stack of sixteen short black drums (4.2 long, 11 in game) on
a dark lining, each lined with eight prisms raked the way it turns; neighbouring drums turn opposite ways at 9.2 degrees
a second, the one at the lip the way deck.0 does (`tubeLining` in `ark_round_geometry.js`). The even drums ride
`ring.0` and the odd ones `ring.1`, so the rig has ten spins. `ArkRoundSolid.TubeLining` (1.5) keeps the drums and
their prisms solid, so a vessel flies the tube's middle. The page's other two insides (rifled, a gallery of windows into
the deck rooms) were not built.

**Solid.** `ArkRoundSolid` is a signed distance over the hull. It carves out the passage: the tube from the lip, the
junction hall, and the three exits. The gap between the mouth and the hull is open water. The pods' solid turns with
them. `ArkHullSolid` dispatches to it when the definition's `hullShape` is `Round`.

Since 2026-11-26 (Shombith: "I can literally go inside the ark's model while boosting"):

- The slots between the turning decks are closed. They were open down to the tube's wall, so a vessel could sit inside
  the model.
- A vessel's centre is held 3 world units off the surface (`RoundVesselMargin`; ArkReverse keeps 1.2), so its wings and
  the chase camera stay out of the plating.
- Every move is swept. `ArkHullSolid` traces each vessel's path since the last frame (`FirstContact`) and meets the
  hull where the path first does, so a boost cannot step over a wall between two frames. A jump longer than 25 world
  units (a launch, a reset) is held out only where it lands. A vessel the launch holds (`IsStationary`) is not swept.
- A swept vessel is never stopped dead (Shombith's playtest, 2026-11-26: "once the vessel sticks to the ark ... we get
  stuck"). Only the part of its move that points into the hull is taken away; the rest slides along the surface
  (`Slide`, traced again so it cannot cross a corner). The nose turns along the hull only on a real contact or push,
  no longer every frame it flies near the plating.

The see-through hull (option C on the concept page, PR #2410) was built and taken out the same day: Shombith, "C is
not good". The tube stays opaque; the bigger Ark (B) is what lets the camera follow a vessel inside.

## Numbers

- 175,280 triangles and 92,945 vertices, FBX 3.8 MB.
- Polygons per slot: Domain 22,784, Body 64,058, Window 186, Engine 612.
- The slots, and the `.meta`'s slot remap, are the same as ArkReverse's.

## Rebuild

```
<bpy python> Tools/Build/vessel_model/build.py Tools/Build/vessel_model/specs/ark_round.py            # writes FBX + rig.json
<bpy python> Tools/Build/vessel_model/build.py Tools/Build/vessel_model/specs/ark_round.py --check    # drift gate
```

## Not yet seen in Unity

All of it, so it is listed in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("The round Ark replaces ArkReverse in the
voyages"). The offline renders beside the artifact are in `/mnt/project-files/ark-round/`.
