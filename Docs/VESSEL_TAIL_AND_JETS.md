# Vessel Tails and Jets

**Status:** standard shipped; Dolphin migrated. Fleet migration open — audit with
**FrogletTools ▸ Vessels ▸ Audit Vessel Tails and Jets**.

## 1. Three things stream off the back of a vessel, and they are not interchangeable

The platform had one word — *trail* — for three different things, and a component literally named
`TrailEmpty` doing the job of a beacon. That is the confusion this document ends.

| | **Trail** | **Tail** | **Jets** |
|---|---|---|---|
| what it is | conserved PRISM mass | one long streak | short plumes, one per engine |
| who it is FOR | everyone — it is the arena | **other players** | **this vessel's own pilot** |
| what it says | "this territory is mine" | "there is a ship over there" | "you are moving, and how fast" |
| mounted on | laid into the world behind the vessel | the **vessel root** | the **model's engine nodes** |
| gameplay | yes — grazed, scored, ridden, stolen | none, pure photons | none, pure photons |
| persists | until an active force removes it | ~2 s | ~0.5 s |
| domain colour | yes (prism materials) | **yes** | **yes** |
| owner | `VesselPrismController` | `VesselTail` | `VesselJet` |

The tail sits **between** the trail and the jets — longer-lived and more distant than a jet, far
shorter-lived and non-physical compared to the trail.

**Nothing in this document touches the trail.** Conserved mass, its laws and its lifetime are
`Docs/ECOSYSTEM.md` §0 and are unaffected: a tail and a jet are photons, they carry no collider and
no state, and removing one destroys nothing.

## 2. Why the split is by VIEWER, not by taste

A **tail exists to be seen by someone else.** That is its whole function, so it hangs off the
vessel ROOT (not the model) — it wants a clean silhouette at range, not a mechanism — and it is
authored to sit clear of its own pilot's view, measured against that vessel's own camera distance.
It is drawn on every machine.

A **jet exists to be seen by its own pilot.** It comes out of wherever the model says thrust comes
out of, because at close range a plume that is not attached to an engine reads as a decal stuck
behind the ship. It is drawn on exactly one screen: every pilot sees their own jets and nobody
else's.

That last rule is the one worth stating as a rule rather than a default. Four pilots in a cell,
each with six engines, is twenty-four plumes of somebody else's exhaust in your view — and the one
signal a jet exists to carry (*your* thrust) is the first thing lost in it. The tail already covers
"where is everyone", at a scale that survives distance.

**The exception is deliberate and per-jet:** `VesselJet.visibleToOtherPilots`. A hull whose jets
ARE a signal to the rest of the field — the Serpent — turns it on. It is per-jet rather than
per-vessel so a hull can telegraph with one plume and keep three private.

## 3. The components

```
Vessel root
├── VesselTailAndJets          ← ONE per vessel. Domain tint + viewer visibility.
├── VesselTail.prefab          ← the beacon. TrailRenderer, m_Time 2.0, width 1.0.
│     └── VesselTail
└── <model>
      └── <engine node>
            └── VesselJet.prefab     ← a plume. TrailRenderer m_Time 0.5 width 0.3 + particles.
                  └── VesselJet
```

`VesselTail` and `VesselJet` are **markers**. They carry no look — that is authored on the shared
prefabs — and no colour, because a tail's colour is not the tail's to choose. What a marker buys is
that *"does this vessel have a tail?"* becomes a question the audit tool and this spec can ask of
any prefab, instead of a name convention that the next hull spells differently.

`VesselTailAndJets` is the sibling of `VesselCustomization`: the hull's materials take the domain,
and so do its tail and jets. It was `VesselTrailCustomization` (renamed in place, so every existing
prefab reference resolved unchanged).

### Domain colour

Every `TrailRenderer` under the vessel is repainted with the vessel's live domain, from the same
`SO_ColorSet` pair the prism trail reads (`TrailHighlightColor` → head, `TrailCoreColor` → tail of
the gradient; those two palette field names predate this vocabulary and are shared with the domain
UI colour, so they keep their names). The authored **alpha** curve is preserved per trail — only
the colour keys are rebuilt.

The pass deliberately does **not** know which layer a renderer belongs to. Anything under the
vessel that draws a streak IS the vessel's colour. That is what makes a new FX layer inherit the
tint for free, and what makes it impossible to author one into the wrong domain.

**Discovery is live, never cached at Awake.** A vessel's FX arrive across several frames, and a
runtime vessel swap brings a whole new set; a set captured once silently omits everything that
showed up afterwards and leaves it wearing its prefab colour forever. Re-discovery is free here
because it runs on a domain change and on init, never per frame.

### Where it binds, and why there

`VesselController.Initialize` — beside the occlusion corridor and the speed tunnel, for the same
reason those are there: `Initialize` is the one method every vessel calls on every spawn path
(single-player, multiplayer, menu autopilot, every runtime swap). Binding there is what makes it
impossible to author a hull, or write a mode, in which a pilot ends up looking at somebody else's
engine plumes.

Two details are load-bearing:

- **The call is NOT gated on `IsLocalPilot`** — the flag is the *argument*. A remote replica must
  be told to HIDE its jets just as deliberately as the local vessel is told to show them; a gated
  call would simply never run on the machines where hiding matters.
- **`ChangePlayer` re-runs it.** That method hands a LIVE vessel to a different player (the
  Cellular Duel round-boundary swap) and never reaches `Initialize`. Without the second call the
  vessel a pilot just gave up keeps drawing its jets on their screen, and the one they just took up
  never starts.

Hiding is `SetActive(false)` on the jet, which is safe precisely because a jet is pure photons. The
tint pass still finds a hidden jet (it discovers with `includeInactive: true`), so a jet revealed
later is already the right colour instead of flashing its prefab colour for a frame.

## 4. The Dolphin

The first hull on the standard.

**Tail** — one `VesselTail` on the vessel root at `(0, 0, -21)`. Its camera sits at
`followOffset z = -20` (`DolphinCameraSettingsSO`), so the beacon streams from behind the lens and
never enters its own pilot's view. Previously untinted: the Dolphin carried the beacon prefab but
not the tint component, so it flew its domain and streaked the prefab's red-orange on every screen.

**Jets** — six `VesselJet`s, one per engine case, at that pod's exhaust mouth. Every number is
measured off `Dolphin_Test.fbx` rather than eyeballed:

| quantity | value | how it was derived |
|---|---|---|
| case mesh, file units | x `[37.73, 55.88]`, y `[-8.01, 5.09]`, z `[-42.25, -1.23]` | FBX `Geometry/Vertices` per `Engine case *` |
| same, imported | x `[-0.559, -0.377]`, y `[-0.080, 0.051]`, z `[-0.422, -0.012]` | ÷100 (`UnitScaleFactor 1`, `useFileUnits`), x mirrored (FBX right- to left-handed). Cross-checked against the case's own `BoxCollider` size `(0.1815, 0.1310, 0.4101)` |
| which end is the exhaust | local `z = -0.422` | the case's local −z maps to `(·, ·, −0.917)` in vessel space — i.e. backward |
| **jet local position** | `(-0.4681, -0.0146, -0.4225)` | mesh centre in x/y, rear face in z |
| **jet local rotation** | identity | `VesselJet.prefab` already flips its VFX 180° about Y, so a jet at identity fires along its mount's −z. Same convention the Squirrel uses |
| **jet local scale** | `(0.6, 0.6, 0.13)` | the Squirrel's small jets, the only shipped reference. See tuning below |

The six exhaust mouths land at vessel-space `x ∈ [-0.589, 0.584]`, `y ∈ [-0.095, 0.580]`,
`z = -2.247` — a fan around the tail, which is what makes six separate plumes read as six engines.

## 5. Fleet status

Run the auditor rather than trusting this table; it is a snapshot.

| vessel | tail | jets | tinted | note |
|---|---|---|---|---|
| **Dolphin** | 1 | 6 | ✅ | on the standard |
| **Squirrel** | 2 | 4 | ✅ | the reference hull. Its jets also carry `DriftJet`, which aims them along the drift course — a Squirrel-specific extra, not part of the standard |
| Sparrow | 1 | 0 | ❌ | has a beacon that never takes its domain |
| Scarab | 1 | 0 | ❌ | same |
| Manta, Rhino, Serpent, Urchin, Grizzly, Termite, Falcon, Shrike | 0 | 0 | — | no FX at all |

The Serpent is the named exception for `visibleToOtherPilots` when it is migrated.

## 6. Tuning and open items

- **Jet scale is the first thing to tune by eye.** `TrailRenderer` width is a WORLD-space quantity
  and is not affected by transform scale, so the authored `(0.6, 0.6, 0.13)` sizes the particle
  systems only — every jet's ribbon is 0.3 wide on every hull. On the Dolphin the six mouths sit
  ~0.3 apart, so at that width the ribbons may merge into one sheet; the levers, in order, are the
  prefab's `widthMultiplier`, then dropping to the outer pair.
- **The Dolphin's tail is a single centreline ribbon**; the Squirrel authors a laterally-offset
  PAIR at `(±4, 0, -12)`. Both work — the Dolphin's sits behind its camera rather than beside it —
  and the pair is worth trying if one ribbon reads thin at range.
- **`VesselTail.prefab` carries six disabled particle systems**, dead experiments that make the
  asset ~690 KB. They are inactive, so they cost nothing at runtime; deleting them is a separate,
  purely subtractive change.
- **Sparrow and Scarab need only `VesselTailAndJets`** on their roots to have correctly-coloured
  tails. That is a two-line prefab edit each and is the cheapest next step.
- The retired branch `claude/vessel-jet-fx-audit-rdze50` attempted the whole fleet at once by
  resolving mounts from node NAMES at runtime. It is worth reading for its measurements — camera
  distances run 17 (Squirrel) to 250 (Serpent), which is why a hull-relative tail offset tuned on
  one vessel cannot transfer — but the name heuristics are what made it unshippable, and hand
  authoring each hull's mounts is the approach that replaced it.
