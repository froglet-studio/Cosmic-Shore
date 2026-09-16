---
name: fauna
description: Use for ANY work on a CREATURE — adding a fauna species, reviving or repairing one, changing how a creature moves/looks/eats/dies, wiring a FaunaConfigurationSO or LightFaunaDataSO, seating a heart, placing body prisms, or answering "why does this creature look stiff / do nothing / not die". Loads the fauna anatomy contract, the five things a creature needs to be ALIVE, the four motion tiers, the heart-seat rule and its gate, and the traps that cost real time. Trigger when editing Assets/_Scripts/Controller/Environment/FloraAndFauna/** (Fauna, LightFauna, Boid, WormFauna, Spindle, HealthPrism, LifeFormCrystal), any Assets/_Prefabs/FloraAndFauna/*.prefab or Assets/_Models/Fauna/**, any `* Fauna *` config asset, or Docs/ECOSYSTEM.md §§23-26, 40, 44-45.
---

# Fauna: the per-creature contract

`/ecology` owns the **system** — populations, diets, phase ladders, the locked invariants,
the collider budget. **This skill owns the CREATURE**: what one animal has to be made of,
what makes it move, where its heart goes, and the specific ways a species has shipped
looking finished while being dead.

**Run `/ecology` first for anything that changes the SYSTEM** (spawn counts, caps, diet
rules, reproduction, phase thresholds). The invariants it loads are LOCKED and this skill
does not restate them; it assumes them.

---

## 1. The roster, measured

Six fauna prefabs are in use. They are **not built the same way**, and "where do I put a
fin / a heart / a prism" has a different answer per species.

| species | prefab | class | body construction | motion | configs |
|---|---|---|---|---|---|
| Shark | `_Models/Fauna/MassSharkFauna.prefab` | `LightFauna` | HealthPrism children + 3 Spindles + an FBX ARMATURE | Animator + Animation Rigging, plus `SharkJawDriver` | 10 |
| Brittlestar | `_Models/Fauna/MassBrittlestarFauna.prefab` | `LightFauna` | HealthPrism children + 11 Spindles + an ARMATURE | Animator + `DampedTransform` chains (the dangling arms) | 12 |
| Tadpole | `_Prefabs/FloraAndFauna/TadPoleFauna.prefab` | `Boid` | one HealthPrism + nested crystal | flocking | 12 |
| QuadFish | `_Prefabs/FloraAndFauna/QuadFish.prefab` | `LightFauna` | a **Spindle** wrapping one mediumfish submesh + 4 HealthPrism fins | GPU sway + `QuadFishSwimDriver` (fin stroke + body bank) | 12 |
| Clawfish | `_Prefabs/FloraAndFauna/Clawfish.prefab` | `LightFauna` | a nested **FBX model instance** (`ClawfishTest.fbx`) — **no prisms, no Spindle** | steering only (see §7) | 4 |
| Worm colony | `_Prefabs/FloraAndFauna/WormColony.prefab` | `WormFauna` | **its MEMBERS** — head/body/tail are each their own fauna | follow-the-leader slither | 9 |

The authoritative set is *"every distinct `FaunaPrefab` guid across `_SO_Assets/**/*.asset`"*
— derive it, don't trust this table after a species lands.

---

## 2. The five things a creature needs to be ALIVE

The Clawfish shipped for ~2 years with a prefab, four element configs, a Codex page, a
baked portrait and a station on the Lifeform Matrix bench, and **did nothing at all**
(`Docs/ECOSYSTEM.md §45`). Every item below was individually invisible. Check all five,
by measurement, before you conclude a creature is fine.

1. **A behaviour class that actually has behaviour.** `Fauna` is `abstract` and its base
   has **no movement** — an empty subclass compiles, serializes, shows up enabled in the
   inspector and sits there. Today the live classes are `LightFauna`, `Boid` and
   `WormFauna`. *A component named after a fish is not a component that swims.*
2. **A `cellData` that resolves.** Every living flora/fauna prefab points at
   `_SO_Assets/Cell Data/Runtime Cell Data.asset` (`8d4e8398…`). A dangling guid
   deserializes to `None` and renders as an empty slot, i.e. exactly like a slot nobody
   filled in. Check `grep -rl "guid: <g>" Assets --include=*.meta` returns **one** owner.
3. **Field parity with its own class.** Run the parity check (§8). Orphan keys are residue
   Unity never prunes and they read as real wiring; missing keys silently take the C#
   initializer, which may or may not be what the species wants.
4. **A heart, seated clear of the body.** §5.
5. **Body prisms.** `Fauna._bodyPrisms` is `GetComponentsInChildren<HealthPrism>(true)`.
   With **zero**, `OnBodyPrismExploded` can never fire, so the creature **cannot be killed
   by shooting it**, carries no conserved mass in its body, and leaves no §26 skeleton.
   (The Clawfish is in this state today — `Docs/ECOSYSTEM.md §45.4`.)

---

## 3. Motion: there are FOUR tiers and they are not interchangeable

Reach for the cheapest one that can express the movement.

| tier | what it is | cost | when |
|---|---|---|---|
| **flocking** | `LightFauna` / `Boid` steering — the whole creature translates and turns | free (already running) | shoaling, murmuration. It is **not** limb motion. |
| **GPU sway** | `SpindleSway.hlsl` spliced into the spindle graph, off `_PrismClock` + the `_Phase` Spindle stamps | **zero** per-frame CPU, stays SRP-batchable | a limb that BENDS — branches, a fish body, a worm segment. §4. |
| **CPU driver** | a `[DefaultExecutionOrder(-1)]` MonoBehaviour writing `localRotation` on parts (`SharkJawDriver`, `QuadFishSwimDriver`) | one component per creature | a part a vertex shader cannot touch, above all a **`HealthPrism`** — conserved mass with its own collider. |
| **armature rig** | FBX armature + Animation Rigging (`DampedTransform` chains = dangling arms, `MultiParentConstraint` binds prism clusters to bones) | an Animator per creature | only where the ART already has a rig. Shark and Brittlestar do; nothing else does. |

**A CPU driver must write `localRotation` and never `localPosition`** on a `HealthPrism`.
A rotation costs zero collider work and zero index work; a position write makes
`PrismSpatialIndex` re-file the prism every frame.

**Before assuming a creature has an animation, check whether it has ever RUN.** Both fish
carried a legacy `Animation` component with `m_PlayAutomatically: 1` pointing at a clip
with `m_Legacy: 0` — the component refuses a non-legacy clip, so it had never played on
any build, and it looks *identical* to one that works (`Docs/ECOSYSTEM.md §44.9`).

---

## 4. Sway: what it takes, and what silently denies it

`Docs/ECOSYSTEM.md §44`; `CLAUDE.md ▸ "A SPINDLE IS A LIMB, NOT A ROD"`.

To sway, a piece of geometry needs **all three**:

1. a **`Spindle`** component whose `RenderedObject` is that renderer — this is what stamps
   a bucketed `_Phase`, so neighbouring creatures desync;
2. a **material whose graph carries the `SpindleSway` custom function** into
   `VertexDescription.Position`;
3. that material authoring a non-zero **`_SwayAmplitude`**. It defaults to **0**, which is
   what makes the graph splice a provable no-op for everything that has not opted in.

Consequences to hold on to:

- **`Amplitude` is a dimensionless SLOPE**, because the bend is a first-order SHEAR
  (`offset.x = Amplitude * PositionOS.z * sin(...)`). One number therefore means the same
  bend on meshes that disagree about scale by three orders of magnitude, and the bend is
  exactly zero at the root — a spindle can never tear off its parent.
- **Amplitude transfers across meshes; FREQUENCY does not.** A fern (0.08 / 1.4 rad/s) and
  a fish (0.13 / 5.2) get **different materials**, not a compromise that makes the plant
  buzz. A shared material is a claim that everything wearing it moves alike.
- **No `Spindle` ⇒ one `_Phase` for the whole species**, i.e. every live individual
  undulates in lockstep. That is the failure mode to check for before adding sway to a
  species whose body is a raw model instance.
- `Tools/Shaders/verify_spindle_sway.py` proves the shipped HLSL (compiles it with clang,
  8 properties, negative-controlled). `Tools/Shaders/wire_spindle_sway.py` does the splice.

---

## 5. The heart: WHERE it goes, and HOW BIG it is

Two separate rules, two separate authorities. Do not conflate them.

**WHERE — `Docs/ECOSYSTEM.md §23.9`:** *a heart is seated at the FRONT of its member's own
prisms with the body trailing (the tadpole arrangement — that prefab puts its crystal at
the origin and its body at z −5.81), never buried inside them.*

- **Forward is +z.** `LightFauna` steers with `LookRotation`, so the creature travels along
  +z and the leading end is the max-z end of the body, whichever way the artist modelled it.
- **A heart has TWO sizes and a seat must clear BOTH**: the prefab's authored `localScale`
  (what the prefab view shows) and the runtime size, which `LifeFormCrystal.ApplyHeartSize`
  forces from the config's `HeartWorldScale`. They are usually close and never equal, so a
  seat proven against one is proven in exactly one of the two places anyone looks.
- The crystal's visible half-extent is `meshHalfExtent × the crystal prefab's child scale ×
  the seat scale` — the crystal ROOT carries no mesh, only its model child does, and the
  root's `SphereCollider` is **smaller** than what is drawn. Size against what is DRAWN.
- **Gate: `python3 Tools/Build/verify_fauna_heart_seat.py`.** It measures from the shipped
  FBXs and prefab YAML, carries a negative control, and names what it does not cover.

**A seat change can move the SIZE, and that is a bug when it happens.** The heart-size
tool measures a creature's body by walking its transform tree; anything the walk cannot
see makes some *other* part of the creature the binding term. On the Clawfish that was its
own crystal, so `heart = K · body^0.5` made the heart's size a function of the heart's seat
(`Docs/ECOSYSTEM.md §45.3a`). **After any placement change, re-run the authoring gate — and
if it moves a number it should not, look for a loop rather than assuming you caused it.**

**HOW BIG — `Docs/ECOSYSTEM.md §40`:** authored per element in that species' own
`FaunaVariantTuning.HeartWorldScale`, never a curve and never a per-prefab accident.
**Never hand-edit it** — `python3 Tools/Build/author_lifeform_heart_sizes.py [--check]`
owns the whole band (`K · bodyDiameter^0.5`, solved so the largest lifeform lands on
`HEART_MAX` 4.6) and **fails the build** on an overshoot, on a non-monotone measurement,
and on any drift. A bigger kill pays more, because the collect reward reads the crystal's
world scale.

**Every lifeform drops exactly one elemental crystal** — enforced by `LifeFormCrystal`, and
it must not be possible to author one that does not. A connected COLONY is a population, so
**every member carries its own heart**; only the colony ROOT is heartless.

---

## 6. Units: the cliff that makes two measurements incomparable

**A raw FBX extent is meaningless until it is normalised by that file's own
`UnitScaleFactor`, and the conversion is a cliff, not a ramp**: a file declaring **100**
lands 1:1 in Unity; a file declaring **1** lands at **1/100** of its raw numbers.

Measured across `Assets/_Models/Fauna/`: Brittlestar and Shark declare 100; Clawfish,
mediumfish, bonita, worm, wormbody, wormhead declare **1**; SwordFish_A declares
0.9999999776. So a naive comparison of two fauna meshes is wrong by 100× most of the time.

Use `Tools/Build/fbx_binary.py` to read vertices, and copy the normalisation from
`Tools/Build/author_lifeform_heart_sizes.py::_fbx_mesh_extent` rather than re-deriving it.

**Reading a body's shape without Unity:** dump the vertex cloud and plot z-vs-y and z-vs-x
as ASCII. It is crude and it answers the only question that matters — which end is the
head. (On the Clawfish the +z end came out as a dense spiked mass and the −z end tapered
away; that agreed with the `LookRotation` argument, which is how the heart seat was
settled.)

---

## 7. Prefab surgery on a creature — the shapes that bite

Load **`/asset-surgery`** for the general rules. These are the fauna-specific ones.

- **Swap the SCRIPT guid, keep the `fileID`.** A `FaunaConfigurationSO.FaunaPrefab` field
  addresses the prefab through the **MonoBehaviour's** fileID and the Codex addresses it
  through the **GameObject's**. Re-pointing `m_Script` in place keeps every downstream
  reference valid; deleting and re-adding the component does not.
- **A nested prefab instance's pose is not a `!u!4` document.** It lives as
  `propertyPath: m_LocalPosition.*` / `m_LocalScale.*` rows inside the instance's own
  `m_Modifications`, and those rows **WRAP** — a one-line regex over the block matches
  nothing and reads as "no overrides". Walk lines.
- **You cannot derive an FBX sub-object's fileID headlessly.** Anything that needs one —
  binding `Spindle.RenderedObject` to a renderer inside a model instance, overriding a
  nested model's material by fileID — is an **editor** step. Say so rather than guessing.
  (An FBX's **material remap** in its `.meta` `externalObjects` is the one exception: it is
  keyed by material NAME, so it can be re-pointed headlessly.)
- **Placing body prisms is an editor step, not a measurement.** `Docs/VESSEL_CONSTRUCTION.md`
  records two passes of Rhino jets landing on a placeholder hull a fifth of the ship's
  height. Prisms are gameplay colliders and conserved mass; put them where a human can see
  them land.
- **Every guid a creature prefab references must have exactly one `.meta` owner.** Sweep it:
  `for g in $(grep -o "guid: [0-9a-f]\{32\}" <prefab> | sort -u | cut -d' ' -f2); do ...`

---

## 8. Before you hand back

Run these. They need no Unity.

```bash
# 1. serialized-field parity for every MonoBehaviour on the prefab
#    (.claude/skills/asset-surgery/field_parity.py — walk the INHERITANCE chain,
#     or every base-class field reads as an orphan)
# 2. the heart clears the body
python3 Tools/Build/verify_fauna_heart_seat.py
# 3. the heart is the size the band says
python3 Tools/Build/author_lifeform_heart_sizes.py --check
# 4. the sway HLSL still holds its 8 properties
python3 Tools/Shaders/verify_spindle_sway.py
# 5. the standing C# gates (syntax-only; see CLAUDE.md for what they CANNOT see)
python3 Tools/Build/check_conditional_compilation.py
python3 Tools/Build/check_enum_member_references.py
python3 Tools/Build/check_switch_label_collisions.py
python3 Tools/Build/check_using_directives.py
python3 Tools/Build/check_self_referential_locals.py
```

Then state, in the hand-back:

- **the collider-budget delta** — colliders per live creature × `MaxLivePopulation`, summed
  over the configs, against a cell the game already ships. This is a HARD gate.
- **which invariants the change touched** and that it violates none (`/ecology` §2).
- **what you could not verify** — anything needing the editor, by name. A creature that
  "should" move is not a creature that moves.

---

## 9. Two traps that are about the PROCESS, not the code

- **`git log` in a shallow clone reports the GRAFT BOUNDARY, not the author.** This clone
  has 391 commits and **45** boundaries; asking who wrote a fauna asset returns a commit
  about a menu button. Check `.git/shallow`, then go to the remote
  (`mcp__github__list_commits` with a `path`). Say "provenance is not recoverable here" if
  it is not, rather than naming the boundary commit.
- **A generated document describing a creature is not evidence the creature works.** The
  Codex's Behaviour line for the Clawfish was real output from a real harvester whose input
  was a hand-written prose row keyed on a type name — the only true part of the sentence.
  If you change what a creature IS, `CodexHarvester.BehaviourModel` and the shipped
  `Assets/Resources/Codex.asset` row both have to follow.
