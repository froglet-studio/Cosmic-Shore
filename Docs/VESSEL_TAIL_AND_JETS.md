# Vessel Tails and Jets

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

The pass deliberately does **not** know which layer a renderer belongs to. Anything under the
vessel that draws a streak IS the vessel's colour. That is what makes a new FX layer inherit the
tint for free, and what makes it impossible to author one into the wrong domain.

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
| **Sparrow** | −50 | −52.5 | 2.5 | 2 | `(±1.60, −0.10, −4.65)` — the two rear lobes, measured |
| **Urchin** | −6.67 | −7.0 | 0.334 | 4 | parented to `JetTopLeft/Right`, `JetBottomLeft/Right` |
| **Rhino** | −120 | −126 | 6.0 | 2 | parented to `engine left` / `engine right` |
| **Grizzly** | −30 † | −31.5 | 1.5 | 4 | parented to `Ship_Wedge_Jet_UL/UR/BL/BR` |
| **Scarab** | −50 | −52.5 | 2.5 | 2 | `(±1.50, 0.10, −4.40)` — the carapace rear |
| **Manta** | −30 | −31.5 | 1.5 | — | see §5 |
| **Serpent** | −250 | −262.5 | 12.5 | — | see §5 |
| **Falcon / Shrike / Termite** | −30 † | −31.5 | 1.5 | — | see §5 |

† no `CameraSettingsSO` of its own. Falcon/Shrike/Termite are flat copies of the Manta and inherit
its 30; the Grizzly has no sibling and takes 30 as the fleet's mid value. Both are *inherited*, not
measured — re-derive them when those vessels get real camera settings.

### How the mount numbers were measured

Nothing here was eyeballed. Two methods, both offline:

- **Named engine nodes** (Dolphin, Urchin, Rhino, Grizzly): the node's own `BoxCollider` gives its
  size and centre in Unity space directly, so the jet sits at that centre with `z` pulled to the
  collider's rear face. The Dolphin instead measures its FBX vertices, because its engine cases
  carry their offset in mesh space: the case spans `z [−0.422, −0.012]` after the ÷100 import and
  the x-mirror, and its local −z maps to `(·,·,−0.917)` in vessel space, so `z = −0.422` is the
  rear. Cross-checked against the case's own collider `(0.1815, 0.1310, 0.4101)`.
- **No engine node** (Sparrow, Scarab): the hull's own geometry. For the Sparrow, binning the mesh
  by the length axis shows the rear three slices collapsing to `|x| ∈ [1.59, 1.64]` — two clean
  lobes, which is where the jets go. For the Scarab the hull is procedural, so the numbers come
  from the builder: `ZAt(t) = (t − 0.5) × length` with `length 9` puts the tail at `z = −4.5`, and
  the authored taper leaves it at ~⅔ of the `width 7.2`.

The Sparrow's tail was at `z = −4.72` — on the hull, in the pilot's face, doing a jet's job. That
is what prompted this pass.

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
- **`VesselTail.prefab` carries six disabled particle systems**, dead experiments that make the
  asset ~690 KB. They are inactive, so they cost nothing at runtime; deleting them is a separate,
  purely subtractive change.
- **`DriftJet`** on the Squirrel's four jets aims them along the drift course. It is a
  Squirrel-specific extra, not part of the standard, and nothing else uses it.
- The retired branch `claude/vessel-jet-fx-audit-rdze50` attempted the whole fleet at once by
  resolving mounts from node NAMES at runtime. Its measurements are worth reading; the name
  heuristics are what made it unshippable, and hand-authoring each hull's mounts replaced it.
