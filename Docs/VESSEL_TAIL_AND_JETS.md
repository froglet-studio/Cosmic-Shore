# Vessel Tails and Jets

> **Vessel construction, guid ownership, nested-instance parenting and what the unreferenced
> vessel models actually carry are in `Docs/VESSEL_CONSTRUCTION.md`.** Two of the three
> discrepancy classes recorded there were found by this pass; read it before swapping a
> vessel's model, because a rig swap moves every mount measured here.

**Status:** standard shipped, fleet migrated. Every vessel has a tail; 7 of 12 have jets.
Audit with **FrogletTools ▸ Vessels ▸ Audit Vessel Tails and Jets**.

## 1. Three things stream off the back of a vessel, and they are not interchangeable

The platform had one word — *trail* — for three different things, and the beacon on the Dolphin
was literally a prefab called `TrailEmpty`. That is the confusion this document ends.

| | **Trail** | **Tail** | **Jets** |
|---|---|---|---|
| what it is | conserved PRISM mass | one long streak | short plumes, one per engine |
| tuned FOR | the arena | **other players** finding you | **this vessel's own pilot** |
| what it says | "this territory is mine" | "there is a ship over there" | "you are moving, and how fast" |
| mounted on | laid into the world behind the vessel | the **vessel root**, behind the camera | the **model's engine nodes** |
| gameplay | yes — grazed, scored, ridden, stolen | none, pure photons | none, pure photons |
| persists | until an active force removes it | ~4 s | ~0.5 s |
| domain colour | yes (prism materials) | **yes** | **yes** |
| owner | `VesselPrismController` | `VesselTail` | `VesselJet` |

The tail sits **between** the trail and the jets — longer-lived and more distant than a jet, far
shorter-lived and non-physical compared to the trail.

**Nothing here touches the trail.** Conserved mass, its laws and its lifetime are
`Docs/ECOSYSTEM.md` §0 and are unaffected: a tail and a jet are photons, carry no collider and no
state, and removing one destroys nothing.

## 2. Tuned for one viewer, drawn for all of them

A **tail exists to be seen by someone else.** It hangs off the vessel ROOT (not the model) because
it wants a clean silhouette at range rather than a mechanism, and it is placed **behind that
vessel's own camera** so it never obstructs its own pilot.

A **jet is TUNED for its own pilot** — its size, its placement on the engines and its short life
are all judged against that vessel's own camera — **but it is not hidden from anyone else.** A
rival's plumes are how you read their thrust in a close fight, and hiding them would throw that
away to solve a clutter problem the short lifetime already solves. Both layers are drawn on every
machine, and there is no per-viewer switch: an earlier pass added one, and it was removed.

## 3. The components

```
Vessel root
├── VesselTailAndJets          ← ONE per vessel. Paints every tail and jet with the domain.
├── VesselTail.prefab          ← the beacon. TrailRenderer, m_Time 4.0, width 1.0 x widthScale.
│     └── VesselTail (widthScale)
└── <model>
      └── <engine node>
            └── VesselJet.prefab   ← a plume. TrailRenderer m_Time 0.5 width 0.3 + particles.
                  └── VesselJet (widthScale)
```

`VesselTail` and `VesselJet` are **markers** carrying one number each. They hold no look — that is
authored on the shared prefabs — and no colour, because a tail's colour is not the tail's to
choose. What a marker buys is that *"does this vessel have a tail, and where are its jets?"* is a
question the audit tool and this spec can ask of any prefab, instead of a name convention that the
next hull spells differently.

`VesselTailAndJets` is the sibling of `VesselCustomization`: the hull's materials take the domain,
and so do its tail and jets. It was `VesselTrailCustomization` (renamed in place, so every existing
prefab reference resolved unchanged).

### Domain colour

Every `TrailRenderer` under the vessel is repainted with the vessel's live domain, from the same
`SO_ColorSet` pair the prism trail reads (`TrailHighlightColor` → head, `TrailCoreColor` → the far
end of the gradient; those palette field names predate this vocabulary and are shared with the
domain UI colour, so they keep their names). The authored **alpha** curve is preserved per trail —
only the colour keys are rebuilt.

**Scope is the markers, not every trail under the vessel** — which is the whole reason `VesselTail`
and `VesselJet` are components rather than a naming convention. A vessel can carry streaks that are
not identity at all: the Rhino's five `RhinoSwordBladeTracer`s are a STATE readout owned by
`RhinoSwordFXController`, which drives their colour from the blade's energy and its impact flash.
An earlier pass swept every `TrailRenderer` under the vessel and repainted those with the domain,
fighting the controller for them every frame. Anything that wants the domain says so by carrying a
marker.

**Discovery is live, never cached at Awake.** A vessel's FX arrive across several frames, and a
runtime vessel swap brings a whole new set; a set captured once silently omits everything that
showed up afterwards and leaves it wearing its prefab colour forever. Re-discovery is free because
it runs on a domain change and on init, never per frame.

### Width is per vessel, and it has to be

A `TrailRenderer`'s width is a **world-space** quantity that transform scale does not affect. The
fleet's camera distances span 6.67 (Urchin) to 250 (Serpent) — a 37× range — while the shared
prefabs are tuned at the Dolphin's 20. One authored ribbon width therefore cannot serve the fleet:
unscaled, the same ribbon engulfs the small hulls and disappears on the big ones.

`VesselTail.widthScale` / `VesselJet.widthScale` multiply the prefab's authored width once in
`Awake` (`VesselFXWidth`). The fleet derives it as **`|followOffset.z| / 20`**, the Dolphin being
1. It is a transform-independent multiplier on purpose: a transform scale would do nothing, and a
per-hull width would fork the shared prefab twelve ways.

### Where it binds

`VesselController.Initialize` calls `ShipHelper.SetShipProperties`, which calls
`IVessel.SetTailAndJetColors`. `Initialize` is the one method every vessel calls on every spawn
path (single-player, multiplayer, menu autopilot, every runtime swap), which is what makes it
impossible to author a hull whose tail flies the wrong domain.

## 4. Placement: the rules, and every vessel's numbers

**Tail** — on the vessel root, on the centreline, at `z = −1.05 × |followOffset.z|`: just past the
camera, so it streams from behind the lens and never enters its own pilot's view. The Dolphin's
hand-authored −21 against a −20 camera is exactly this rule, which is where the 1.05 comes from.
The Squirrel is the one exception: it authors a laterally-offset PAIR at `(±4, 0, −12)`, in front
of its camera but out at the edges of frame, and it is left alone because it works and it is the
reference hull.

**Jets** — at whatever the model calls an engine, pointing where that engine points. Where the
model has named engine nodes the jet is parented to them, so it follows the engine when the engine
animates. Where it does not, the jet is placed at the measured rear of the hull on the vessel root.

| vessel | camera | tail z | width× | jets | mount |
|---|---|---|---|---|---|
| **Dolphin** | −20 | −21 | 1.0 | 6 | parented to the six `Engine case *`, at each pod's measured exhaust mouth |
| **Squirrel** | −17 | ±4, −12 (authored pair) | 0.85 | 4 | parented to model nodes (authored) |
| **Sparrow** | −50 | −52.5 | 2.5 | 2 | `(∓3.03, 0.40, −0.96)` at width×2 — the two nacelle cowl mouths |
| **Urchin** | −6.67 | −7.0 | 0.334 | 4 | parented to `JetTopLeft/Right`, `JetBottomLeft/Right` |
| **Rhino** | −120 | −126 | 6.0 | 10 | 2 on `engine left`/`engine right` at width×10 (the wing pods); 8 on `fusalage`, one per nozzle — table below |
| **Grizzly** | −30 † | −31.5 | 1.5 | 4 | parented to `Ship_Wedge_Jet_UL/UR/BL/BR` |
| **Scarab** | −50 | −52.5 | 2.5 | 2 | `(±1.50, 0.10, −4.40)` — the carapace rear |
| **Manta** | −30 | −31.5 | 1.5 | — | see §5 |
| **Serpent** | −250 | −262.5 | 12.5 | — | see §5 |
| **Falcon / Shrike / Termite** | −30 † | −31.5 | 1.5 | — | see §5 |

† no `CameraSettingsSO` of its own. Falcon/Shrike/Termite are flat copies of the Manta and inherit
its 30; the Grizzly has no sibling and takes 30 as the fleet's mid value. Both are *inherited*, not
measured — re-derive them when those vessels get real camera settings.

### How the mount numbers were measured

Nothing here was eyeballed. Three methods, all offline:

- **Named engine nodes** (Dolphin, Urchin, Rhino's wing pods, Grizzly): the node's own
  `BoxCollider` gives its size and centre in Unity space directly, so the jet sits at that centre
  with `z` pulled to the collider's rear face. The Dolphin instead measures its FBX vertices,
  because its engine cases carry their offset in mesh space: the case spans `z [−0.422, −0.012]`
  after the ÷100 import and the x-mirror, and its local −z maps to `(·,·,−0.917)` in vessel space,
  so `z = −0.422` is the rear. Cross-checked against the case's own collider
  `(0.1815, 0.1310, 0.4101)`.
- **Nozzles cut into the hull** (Rhino's body, Sparrow's nacelle rings): an opening cut as a lathe
  is found rather than guessed — group vertices by quantized z, cluster in `(x, y)`, keep the
  clusters that fit a circle, then merge coaxial clusters into axes. Each axis is one nozzle; its
  rearmost ring is the mouth plane and that ring's inner radius is the aperture. Thresholds and
  what it found on the Rhino are below.
- **No engine node** (Scarab): the hull's own geometry. The Scarab's is procedural, so the numbers
  come from the builder: `ZAt(t) = (t − 0.5) × length` with `length 9` puts the tail at
  `z = −4.5`, and the authored taper leaves it at ~⅔ of the `width 7.2`.

The Sparrow's tail was at `z = −4.72` — on the hull, in the pilot's face, doing a jet's job. That
is what prompted this pass.

**These two took four passes each, and what the passes cost is the useful part:**

- The **Sparrow** took four passes, and the first three all put the plume somewhere the ship has
  no engine: two jets at the measured rear lobes `|x| ∈ [1.59, 1.64]`, then one centred plume at
  the hull's rearmost point `z = −4.65`, then one centred plume pulled forward to `(0, 0.40,
  −0.09)`. The last of those was the closest and still wrong — it read the nacelle's DEPTH off the
  geometry and then held the jet on the centreline, which on this hull is the fuselage fan.
  Measured after the fact: 69 hull vertices sit within 0.5 of that mount. The jet was inside the
  ship.

  The engines are two plug nozzles, one per nacelle, on z-parallel axes at `(±3.03, +0.40)`.
  Slicing the nacelle along z shows the whole anatomy: a central plug protruding aft to `z −1.35`
  (r ≈ 0.09), the cowl mouth appearing at **`z = −0.96`** as a clean octagon of outer radius
  **0.31**, then concentric inner rings forward of it. That cowl face is the mount. `widthScale 2`
  puts a 0.60-wide plume in a 0.62-wide cowl, so the nozzle reads as full.

  Three things about this hull are worth carrying, because none of them is visible in a bounding
  box or a vertex histogram. The rearmost geometry (`z −4.65`) is fanned wing feathers, not a
  tail. The rear view shows rings at `x ≈ 2.15`, `2.60` and `3.03` that look like one nacelle
  stack and are three different structures at three different depths — only `x 3.03` is an engine
  (1,091 verts, 61 coaxial rings, spanning `z [−2.25, +1.75]`; the other two run forward to `z
  +6.8` and `z +1.9`). And each nacelle's own tail feather forms a shroud behind the mouth with an
  inner clearance of ~0.23, so the plume threads it rather than passing through solid — which a
  clearance check against the hull as a whole would have called a collision.
- The **Rhino** took four passes. The first sampled `fusalage` — the right mesh — but only the top
  of its rear face, and put four jets where there is one nozzle. The next two abandoned that mesh
  for the wrong one. Its prefab's `MeshFilter`s all carry guid
  `4a586f927b9527f469c6d95a0ac32051`, and resolving that with
  `grep -rl "guid: $g" Assets --include=*.meta | head -1` returned
  `Placeholder/Vessel_Placeholder_1.fbx.meta`, which merely sorts first. That file is a REFERENCE —
  the placeholder points its own filters at the Rhino's meshes. `Rhino_Test.fbx.meta` is the one
  whose top-level `guid:` line carries it, so the Rhino renders `Rhino_Test.fbx`'s `fusalage`.

  **Exactly one `.meta` OWNS a guid; every other hit is something referencing it.** `head -1`
  picks by filename order, which has nothing to do with ownership — the check is
  `grep -c "^guid: $g"` per candidate, and the answer is the file that returns 1. Cross-check the
  result against something the prefab itself authored: the Rhino body's `BoxCollider` reproduces
  `fusalage`'s extents
  `(5.6182, 15.3091, 16.8157)` and centre `(0, −1.1999, −1.9744)` to four decimals. Getting this
  wrong is quiet — the placeholder hull is a fifth the Rhino's height, so the jets sat in empty
  space above a ship that was never there, and the first, correct `fusalage` measurement was
  discarded as the mistake.

  With the right mesh the eight body nozzles fall out of it exactly. Each is a lathe about a
  z-parallel axis, so grouping vertices by quantized z, clustering in `(x, y)` and keeping the
  clusters that fit a circle (radial σ/r < 0.08, largest angular gap < 0.9 rad) finds them with
  nothing seeded by eye: eight axes, each a stack of coaxial rings ending at the rear in a
  64-point rolled lip band whose INNER radius is the aperture.

  | nozzle | mount (vessel space) | lip band | aperture | `widthScale` |
  |---|---|---|---|---|
  | body engine top | `(0, 3.197, −10.382)` | 0.766–0.882 | 0.766 | 3.3 |
  | body engine upper | `(0, 1.539, −9.898)` | 0.282–0.397 | 0.282 | 1.2 |
  | body engine mid L/R | `(∓1.359, −1.349, −9.067)` | 0.822–0.944 | 0.822 | 3.5 |
  | body engine low L/R | `(∓1.220, −3.445, −6.436)` | 0.582–0.704 | 0.582 | 2.5 |
  | body engine keel L/R | `(∓0.803, −5.271, −3.180)` | 0.452–0.574 | 0.452 | 1.9 |

  `widthScale` is ONE rule for all eight rather than eight judgements: `VesselJet`'s TrailRenderer
  is authored 0.3 wide, so `widthScale = 0.64 × 2r / 0.3 = 4.267 r` makes every plume the same
  fraction of its own aperture. That is what makes a ten-jet stern read as one machine — the
  apertures span 2.9× across it, and what has to be held constant is the RATIO, not the width.

  The five mouth planes step forward and down (`−10.382 → −9.898 → −9.067 → −6.436 → −3.180`)
  because the Rhino's belly rakes up toward the nose, and checking that is not optional: a plume
  tube of aperture radius swept aft from each mouth contains **zero** hull vertices, which is the
  property that says a jet will not fire through the ship. Treating the stern as one plane would
  have buried all six lower jets in several units of hull.

**The rule the Sparrow and the Rhino are both instances of: the rearmost geometry is not the
exhaust, and the way to stop guessing is to LOOK.** A wireframe of the stern plus a stack of thin
z-slabs through it takes a minute and settles what an hour of binning statistics will not — both
ships were placed correctly within one pass of rendering them, and wrongly in every pass before
that.

## 5. Why five vessels have tails but no jets

Manta, Falcon, Shrike, Termite and Serpent get the tail and the tint, and no jets, because their
models do not answer "where is the engine" and inventing an answer is the opposite of the care the
rest of the fleet got:

- The **Manta family** shares one rig whose authored scales do not reconcile: its armature carries
  a ×100 scale against a 30-unit camera, and its wing colliders are 0.004 units. Falcon, Shrike and
  Termite are flat copies of it.
- The **Serpent's** rig does have an `EngineBone` — but no serialized field in the prefab
  references it, so its fileID cannot be resolved from the asset, and the mesh bounds, the box
  collider and the 250-unit camera disagree about the hull's size by an order of magnitude.

All five are on the rig-swap list in `CLAUDE.md` § "The rigged-model swap". Their jets belong in
the same pass that gives them real art, when "where is the engine" has an answer to read.

## 6. Tuning and open items

- **Width scale is the first dial.** Every vessel's is derived, not play-tested. It is one float on
  the vessel's own tail/jet instance.
- **The Urchin is the extreme case**: its hull is ~0.4 units across with a 6.67-unit camera, so its
  0.334 scale is doing real work. Check it first.
- **Jet count on the Dolphin.** Six mouths sit ~0.3 apart with a 0.3-wide ribbon each, so they may
  merge into one sheet. Levers in order: `widthScale`, then dropping to the outer pair.
- **Count and location are look questions, not bounding-box ones.** The Sparrow proved both: two
  measured lobes but one apparent exhaust, and the exhaust nowhere near the hull's rearmost point.
  Render the model, find the structure the artist built as an engine, and mount on that.
- **A nested prefab instance is reachable TWO ways, and needs both when its parent is a plain
  Transform.** `m_TransformParent` in the instance's own modification block always, PLUS an entry
  in the parent Transform's `m_Children`. An earlier version of this list claimed the second was
  unnecessary, reasoning from the Squirrel's four jets, which carry none and work — they attach to
  a STRIPPED Transform inside the nested model prefab, and a stripped document has no children
  list to write into. That is the one case that structurally cannot carry the entry, and
  generalising from it shipped eight unreachable Rhino jets. Full record:
  `Docs/VESSEL_CONSTRUCTION.md` §3.
- **`VesselTail.prefab` carries six disabled particle systems**, dead experiments that make the
  asset ~690 KB. They are inactive, so they cost nothing at runtime; deleting them is a separate,
  purely subtractive change.
- **`DriftJet`** on the Squirrel's four jets aims them along the drift course. It is a
  Squirrel-specific extra, not part of the standard, and nothing else uses it.
- The retired branch `claude/vessel-jet-fx-audit-rdze50` attempted the whole fleet at once by
  resolving mounts from node NAMES at runtime. Its measurements are worth reading; the name
  heuristics are what made it unshippable, and hand-authoring each hull's mounts replaced it.
