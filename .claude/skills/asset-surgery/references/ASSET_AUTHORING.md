## 4.6 Technique: hand-authoring a new asset trio

Adding a new SO-configured, prefab-backed thing (here: a cell) means four
hand-authored files plus a scene array entry. The recipe:

1. `uuid4().hex` for every new GUID; **sweep `Assets/**/*.meta` and assert each
   appears exactly once** before proceeding.
2. **Donor-clone the prefab** from a working sibling (§1.3) and diff the
   serialized field NAME LIST against the donor — an identical set proves you
   did not miss an inherited `[SerializeField]`. A missing one silently
   defaults (a null `prism` = a cell that builds zero prisms and says nothing).
3. The SO's `m_Script` GUID comes from the SO class's own `.cs.meta`, never
   from memory. Component references (`EnvironmentPrefab`) point at the
   **MonoBehaviour's fileID inside the prefab**, not the GameObject's.
4. Registering into a serialized array on a **prefab instance in a scene** is
   two coordinated edits: bump `<Field>.Array.size` AND append a
   `'<Field>.Array.data[n]'` override with the same `target` fileID/guid as its
   siblings. Validate by parsing back: size == count of `data[]` entries ==
   contiguous 0..n-1, and every `objectReference` GUID resolves to a real asset.
5. Order can be load-bearing — appending is safe, inserting may not be. Here
   index 0 must stay the environment-free config (`CellTypeChoiceOptions.
   EnvironmentFree` boots on the first config with no environment).
6. **Machine-check every hand-authored asset against its script before ship.**
   Parse the `[SerializeField]`/public field names out of the `m_Script` GUID's
   `.cs` **and its base classes**, then diff against the asset's top-level YAML
   keys. An unknown key is a typo or a stale rename that will silently default;
   this catches in one second what an editor import round-trip catches in ten
   minutes, and it works on files you never opened. Run it over the whole set
   the branch touched, not just the ones you remember editing.

## 4.7 Technique: recovering authored values from DELETED assets

Origin: the worm-colony rebuild (2026-08). The session deleted a decade-old
prefab family as "dead content" and rebuilt the system cleanly — then the
prompter pushed back twice: *"the old prefab had the proper placement… take the
good from the old and leave the bad"*, and *"spaced too far. again use the model
for reference."* Both times the session had **invented geometry it could have
read**. The lesson generalizes:

- **Deleting the CODE does not mean discarding the AUTHORING.** Structure, wiring
  and broken behavior are what you delete; hand-placed transforms, proportions and
  arrangements are somebody's craft and are still in git. Before authoring any
  replacement geometry, mine the deleted asset:
  `git show <sha>^:path/to/Old.prefab` and parse it.
- **Extract verbatim, don't eyeball.** Each nested prefab instance's transform
  lives in its `m_Modification.m_Modifications` list as `m_LocalPosition.{x,y,z}` /
  `m_LocalRotation.{x,y,z,w}` / `m_LocalScale.{x,y,z}` rows. Pull them into
  literal tables and emit them unchanged — quaternions especially, which are
  impossible to re-derive by eye.
- **Ratios transfer, absolutes don't.** When the replacement renders at a
  different root scale, the invariant is the RATIO (e.g. `gap ÷ model scale`).
  The session guessed a segment spacing of 14 and was 1.67× too wide; the
  authored chain stated it exactly (gaps of 8.05/8.39/8.63/8.71 at model
  scale 1 → the value is 8.4). Derive the ratio from the old data, then
  re-express it in the new scale — and put the derivation in the config
  tooltip so the next person doesn't re-guess it.
- **Audit what you recover.** The old asset also encodes its BUGS: this one had
  a whole spindle tier authored at scale ZERO (invisible) and prisms named
  "Shielded…" whose `IsShielded` flag was actually `0`. Recover the geometry,
  fix the defects, and say which was which in the commit.

## 4.8 Technique: headless FBX interrogation (takes, bones, curves, scale, mesh bounds)

An FBX is a parseable binary, not a black box you must open Unity to ask about. The format
(`Kaydara FBX Binary`, version at byte 23; ≥7500 uses u64 record headers, below u32) is a tree
of named records with typed properties; arrays (`f d i l b`) carry an `(alen, encoding, clen)`
header and are zlib-per-array when `encoding=1`. A ~60-line recursive parser answers questions
that would otherwise cost a round-trip to a human at the editor:

- **Which bones does each animation take actually move?** Walk `Objects` for typed ids
  (`AnimationStack`/`AnimationLayer`/`AnimationCurveNode`/`AnimationCurve`/`Model`), walk
  `Connections` (`C` records: OO/OP src→dst), then per stack: layer → curve nodes → `OP` edges
  to `Model` names. Decompress each curve's `KeyValueFloat`/`KeyTime` and report ranges —
  constant curves are baked filler; the moving bone is the animation. This is how "Missile
  Launch 1 = RIGHT bay (`b_Missile.R`), departs 0.4s, peaks 0.64s of 0.88s" was established
  as fact instead of assumption. `KeyTime` is in KTime ticks: divide by 46,186,158,000/s.
- **Will a donor clip retarget onto another model's rig?** Provable statically: (1) bone NAME
  sets equal (`strings -n 3 file.fbx | grep '^b_'` is the quick first pass; the parser for
  rigor); (2) same armature/root object name, since Unity binds clip curves by transform PATH
  relative to the animator root; (3) the **numeric scale product matches** — read
  `GlobalSettings.UnitScaleFactor` from the FBX AND `globalScale`/`useFileScale` from the
  `.meta`, then compare products (SparrowModel1: FBX-unit 100 × meta 1; SparrowModel4:
  FBX-unit 1 × meta 100 — equal, so translation curves land 1:1). Curves targeting nodes the
  target model lacks simply never bind — harmless.
- **Rest poses / pivot positions** without a scene: `Model` records' `Properties70 → P` entries
  for `Lcl Translation/Rotation/Scaling` give every bone's authored rest TRS (how the bay-bone
  positions and the 0.2034 armature scale were read).
- **Mesh size and orientation**: decompress `Geometry → Vertices`, take bounds; identify a
  mesh's "nose" by comparing cross-section extents near each end of its long axis (the
  radially-symmetric end is the nose, the asymmetric one is the fins).
- **"When does this block start moving?" is NOT "the first key that differs".** An eased curve
  often carries a key at t=0 and the next one much later with a sub-degree difference (the Time
  crystal's `89`: −270.00 at 0 s, −269.46 at 0.64 s), so a `diff(values) > ε` scan reports motion
  from t=0 when nothing visible happens for 0.6 s. Sample the curve on a fine grid and threshold
  the VISIBLE quantity (1° of rotation, a few mm of translation) against the value at the instant
  you care about. The first reading said "no still window at the loop seam" and would have killed
  a correct design.
- **A snap to a symmetry of an IMPORTED model needs the frame Unity actually produced, and a
  Z-up FBX has no anchor in this repo.** Unity's conversion permutes and signs axes, and for a
  shape whose symmetry has two coordinate-aligned orientations (the icosahedron's
  (0,±1,±φ) vs (0,±φ,±1) families) the SIGN decides which one you get — so a hard-coded group is
  wrong for half the possible conversions and nothing offline tells you which half. Resolve it at
  RUNTIME from something the import cannot reinterpret: named bones (sub-asset fileIDs are not
  derivable, names are), whose rest positions in the model root's local space name a symmetry axis
  — the retired `TimeCrystalVertexHop` snapped the first-ring bones' centroid onto the nearest of the 24
  candidate five-fold axes, which fixes axis AND orientation in one step. Its successor,
  `FlipWaveRig`, needs every plate's geometry anyway, so it turned the model **Read/Write enabled**
  and resolves the frame from the plates themselves (each plate's outward direction is a two-fold
  axis, and only one orientation puts all 30 at 31.72° from two vertices). Either is fine; what is not
  fine is reading `Mesh.vertices` off an `isReadable: 0` mesh, which has none in a player. Then PROVE the resolver against
  every conversion it could face: export the FBX's bone heads and mesh, apply all **48** signed
  axis permutations in a Roslyn harness, run the shipped resolver on each, and assert it recovers
  the true axis and that every group element leaves the converted mesh congruent (48/48, 6e-7
  here). Negative-control with a tilted frame and the wrong bone set.
- **To animate PART of a static `MeshFilter` model without re-exporting it, skin a runtime TWIN,
  and give every reader of the old `MeshFilter` a way back to the source.** The omni crystal's
  rhombi flip the same way the Time crystal's do, but its body is one unskinned mesh.
  `RhombusSkinBaker` welds it, finds the 30 rhombus plates, and instantiates a twin with rigid
  bone weights: bone 0 is the still body, and plate *i* binds to `Translate(-centroid_i)`. The
  prefab's `MeshRenderer` becomes a `SkinnedMeshRenderer` in place, keeping the same fileID.
  That swap silently blinds every system that reads `MeshFilter.sharedMesh` (hull fusion and
  vessel crystal morphs did). Sweep for them and route each through
  `RhombusSkinBaker.SourceOf(skin.sharedMesh)`, the same idea as
  `CrystalEdgeArcMeshBaker.TryGetSource`, so they still see the authored asset and not the twin.
  Then prove the twin at rest draws the source exactly (8.9e-16 here), and that the body's
  vertices never move.

## 4.8b Technique: prove a runtime VISUAL claim offline, by walking to the authored value

"Why is this the wrong colour?" is answerable without Unity, because every step of what a
renderer shows is a serialized reference. Walk the chain and print it:

1. **Who owns the visual** — for a nested prefab (a lifeform's crystal, a vessel's part), the
   host prefab holds a `PrefabInstance` whose `m_SourcePrefab` guid names the real source
   asset. The host's own YAML shows only `stripped` stubs, so a naive grep for the component's
   script guid finds a block with no fields and reads as "unwired" when it is simply inherited.
2. **Which material the RENDERER actually uses** — read `m_Materials` off the `MeshRenderer`
   (and any `m_Modifications` entry whose `propertyPath` is `m_Materials.Array.data[N]`, which
   overrides it). This is frequently NOT the material named in the component's own fields: a
   `Crystal` lists `defaultMaterial`/`inactiveMaterial` for its *transitions* while the renderer
   is authored with one of them, and only the renderer's is on screen at rest.
3. **The authored values** — `.mat` files carry the properties under `m_SavedProperties`; resolve
   the guid via the `.meta` sweep. Compare those numbers against the live SO the code says it
   reads (`ThemeManagerDataContainer.asset` → `ColorSet`).

That chain proved, with no editor, that `ChargeCrystalMaterial._BrightCrystalColor` is *exactly*
`EnvironmentColors.BrightCTA` — i.e. the fallback colour and the intended colour were the same
value, which is why one of the four elements looked correct while the mechanism producing it was
entirely dead.

- **A symptom that is asymmetric across variants is the tell, and it hides the bug.** The same
  broken mechanism produced a correct-looking result on two of four crystal prefabs (Mass and
  Space author the *Blue* material on their renderer; Charge and Time author the lime one), so
  half the ecosystem looked right by accident. Before concluding "it works for X so the system
  works", check whether X's authored fallback happens to equal the intended output. Count the
  affected content too — 21 species assets per element turned "two crystals look odd" into "half
  the ecosystem", which is what set the priority.

## 4.8c Technique: binary FBX SURGERY — edit an artist's model without a DCC

§4.8 reads an FBX. The same parser, given a WRITER, edits one: replace the geometry arrays,
delete a subtree, and write the file back. That turns "we need the artist to re-export" into a
tool run — and, crucially, it lets an edit keep the file's IDENTITY, which is what makes the
change reference-free.

**Why in place beats a new file.** Unity's `fileIdsGeneration: 2` derives a sub-asset's fileID
from its TYPE and NAME, not its ordinal. Keep the geometry's name and you keep its fileID; keep
the file and you keep its guid. Then the prefab that references the mesh, the material array, the
submesh order and the import settings all keep working with **no edit at all** — the whole change
is one binary. (A subdivision that went out as `… Subdivided.fbx` would have needed a prefab
re-point and a bet on the fileID hash. Reference stability beats tooling elegance.)

**The codec** (`Tools/Build/fbx_binary.py` is the worked example):

- Keep property values as `(type_char, value)` pairs. A parser that returns bare Python objects
  loses the distinction between `D`/`F` and `L`/`I`, and a writer that guesses wrong produces a
  file that reads fine in YOUR parser and is rejected downstream.
- Node record (< 7500): `endOffset u32, numProperties u32, propertyListLen u32, nameLen u8, name,
  properties…, [children…, 13-byte NULL record]`. `endOffset` is ABSOLUTE, so serialization is a
  recursion that needs the node's own file offset passed in.
- Write arrays with `encoding=1` (zlib) to match what exports do; `encoding=0` is equally legal
  and reads back identically, but it inflated one 58 KB file to 646 KB.
- Copy the FOOTER verbatim (everything after the top-level NULL record). Nothing in the Unity or
  assimp path validates its scrambled id, and reproducing it is pure risk.
- **Prove the round trip before you use the writer for anything.** Read → write → read, and
  compare full property VALUES (not just node names and lengths). Then run an independent reader
  over both — see below. A round trip that survives both is a writer you can trust.

**Validate with `assimp`, the model-format analogue of §4.5c's clang.**

```sh
apt-get install -y assimp-utils            # available in this container
assimp info "Assets/_Models/Thing.fbx"     # meshes, submesh ORDER, verts, faces, materials,
                                           # animations, bounds — from a real FBX reader
```

Diff the report for the original against the report for your output. It catches what your own
parser structurally cannot: your parser agrees with itself by construction. It confirmed a
subdivided missile kept its 2 submeshes IN THE SAME ORDER (so the prefab's material array still
lined up), its 2 materials, its animation and its bounds — and it caught the trap below, which no
amount of re-reading the tree would have.

**Editing geometry**: rewrite `Vertices`, `PolygonVertexIndex` (last index of each polygon is
`~i`), `Edges`, and the `LayerElement*` children. Mind the domains: `LayerElementMaterial` is
usually `ByPolygon`, normals/UVs `ByPolygonVertex` + `IndexToDirect`. **Read UVs in the CORNER
domain, never collapsed to per-vertex** — a UV seam IS one control point carrying different UVs
in different faces, and flattening welds it shut.

**Deleting a subtree** (blend shapes, a deformer, a camera): remove the `Objects` children, then
remove every `Connections` record whose src OR dst is a doomed id, then fix the `Definitions`
`ObjectType → Count` rows. Assert afterwards that no connection references a missing object —
that one check is worth more than re-reading the diff.

**Repairing geometry the IMPORTER discards, and the one technique that makes it safe.**
Unity's `"A polygon of Mesh 'X' ... is self-intersecting and has been discarded"` is not a
quality warning — it names an action the importer TOOK, so it is a report of missing faces in
the shipped mesh. **Measure the action a warning names, not the ratio**: "one face out of
11,113, 0.009%, pre-existing art" is entirely true and answers the wrong question, and it is
how twelve missing hull faces survived a first pass. (The mesh name in the message is the
**Model** node, not the Geometry node — they differ, and searching the wrong one finds nothing.)

The usual offender is a **zero-length edge**: two adjacent corners indexing different vertices
at the identical position. Repair by dropping the redundant CORNER, not by moving or merging a
vertex — and check first whether the two corners carry the same UV (if so the edit is lossless;
if not you are about to weld a seam). Where they differ only in normal, pick which to keep by
MEASURING — the corner whose normal sits farther from the polygon's own Newell normal is the
one to drop. Scope the repair to exactly what the importer discards: a zero-area triangle is
degenerate too and Unity KEEPS it, so repairing one both exceeds the report and would leave a
2-gon.

Removing one corner touches five parallel arrays with three different domains —
`PolygonVertexIndex` (corner), normals (ByPolygonVertex), UV indices (ByPolygonVertex +
IndexToDirect), materials (**ByPolygon — unchanged**, the polygon count does not move), and
`Edges` + smoothing (ByEdge). Getting the domain wrong is silent.

**The generalizable technique: PROVE A REBUILD RULE AGAINST THE SHIPPED ARTIFACT BEFORE YOU
RELY ON IT.** `Edges` stores one corner position per unique undirected edge, so removing a
corner shifts every later entry and patching it by hand is guesswork. Instead, guess the rule
that BUILT it — emit the first occurrence of each undirected vertex pair in polygon-corner
order — and compare against what is already in the file. When that reproduces the shipped array
exactly (same length, same order, same values), "I think this is how it was built" becomes a
fact and rebuilding it is safe; when it does not, you have learned that cheaply and can refuse
to write. This applies to any derived array you must regenerate rather than patch. Sanity-check
the *direction* of the result too: here the edge count RISES by one per repaired polygon, and a
model that predicted a fall would have been wrong about the topology.

Then verify in layers, cheapest first: round-trip the writer on the untouched file (node count
and every property value, plus an independent reader); diff the node tree and assert that ONLY
the arrays you meant to touch differ; assert the invariants that must hold (vertex positions
byte-identical, blend shapes byte-identical, polygon count unchanged, each layer's length
matching its domain); then `assimp` the before and after and diff the reports.

**A path with a space silently truncates a shell sweep.** `for f in $(git diff --name-only …)`
and `… | xargs grep` both word-split, so `Assets/_Models/Vessel Models/Thing.fbx.meta` becomes
two nonexistent paths — and the loop does not fail, it just processes the files that happen to
have no spaces. A deleted-asset guid sweep reported "1 file, 0 references" for a change that
deleted 8. Use `-z`/`-0` (`git diff --name-only -z … | xargs -0`, or
`while IFS= read -r -d ''`), and sanity-check the COUNT against the diffstat before believing a
clean result.

## 4.8d Technique: authoring blend-shape NORMALS into an FBX (the space crystal, 2026-10)

Exporters (Blender writes all-zero shape normals) and Unity's *Calculate* mode both derive each
key's normal delta against the BASE mesh, and Unity sums active keys. That is right for one key
and wrong for keys meant to be STACKED (key B ramped while key A is held at 100): rotations do
not add, and the space crystal's stacked half-spins were off by 20°/42° and popped on reset.
`Tools/Build/author_space_crystal_mesh.py` is the worked fix. Four rules it paid for:

- **Shape normals are per CONTROL POINT.** A hard-edged mesh needs one control point per polygon
  corner (unweld it) before a shape normal can be per-face.
- **The importer normalizes each frame's TARGET normal (`base + delta`) before re-deriving the
  delta.** assimp does it (`NormalizeSafe` in its FBX converter) and Unity behaved the same in
  play. So a delta authored for a stacked key as `t − a` (true normal minus the stacked-on pose)
  gets bent wherever `|n0 + t − a| ≠ 1`, worst on faces that turn ACROSS the spin axis. In playtest
  that showed up as "the big faces are perfect, the small side faces still snap". Author every
  target UNIT length: `delta = λ·t − a` with `λ` the positive root of `|λ·t + (n0 − a)| = 1`; the
  stacked sum is then `λ·t`, along the true normal, under either importer model.
- **Validate against BOTH importer models** (raw deltas and normalized targets). A validator that
  replays only the raw sum passed the broken file with 0.0005°.
- **Reference = per-quad Newell normal, never one triangle's.** Linear shape keys twist quads
  (14° here); the shading uses one normal per quad, so a triangle reference reports phantom
  6–25° errors. Use assimp (`libassimp-dev` + a 20-line C++ dumper of `aiAnimMesh` normals) as the
  independent reader; it normalizes targets, so it reproduces the Unity failure. Negative-control
  it on the broken file.

## 4.9 Technique: answering "does every X actually carry Y?" THROUGH prefab nesting

Origin: the crystal-capture rework (2026-08). The branch's whole payoff was routed through
`Crystal.Explode`, which does nothing useful unless the crystal carries a `SpentCrystalPrefab`
and a non-null `explodingMaterial`. The doc asserted it did. Checking that claim by grepping
one prefab proves nothing, because **the crystal the lifeform actually drops is a NESTED
PREFAB INSTANCE** — the value lives in the *source* prefab and can be overridden, or not, at
each nesting site. This is the general shape of the ship protocol's "find the PRODUCER" gate
whenever the producer is a serialized reference, and it is three greps, not a judgment call:

1. **Find every direct owner** — grep for the component's script GUID (from its `.cs.meta`),
   then walk `--- !u!114` MonoBehaviour blocks and read the field out of the block whose
   `m_Script` matches. Do *not* regex the field name across the whole file: several components
   can carry a same-named key, and you will attribute the wrong one.
2. **Resolve the nesting** — a prefab whose component block is *absent* holds the thing as a
   `--- !u!1001 PrefabInstance`; its `m_SourcePrefab: {fileID: …, guid: G}` names the source.
   Map `G` back to a path via `grep -rl "guid: G" Assets --include=*.meta`, and you have
   reduced "16 lifeforms" to "4 crystal prefabs I can check exhaustively".
3. **Check for a nesting site that STRIPS it** — `grep -rn "propertyPath: <Field>" Assets`.
   An override to `{fileID: 0}` at one site is precisely the case that makes a
   verified-at-the-source claim false in the field, and it is invisible from the source prefab.

Report the resulting table (owner → source → field state) in the ship report. An exhaustive
"all 16 resolve to 4 prefabs, all 4 SET, no site overrides it" is evidence; "I checked one" is not.

**Counting the same way answers "how many Y per X?", with one trap that silently inflates the
count: a `stripped` MonoBehaviour document MATCHES your script-guid grep.** §4.8b records the
other face of this (a stripped stub reads as *unwired*); for a census it reads as *an extra
instance*. A stripped doc is a reference stub — it exists only so something else in the file can
point at a nested instance's component — so the count is inflated by exactly the number of
cross-references into that instance, which is usually 0 or 1 and therefore looks plausible.
Filter on the DOC HEADER (`--- !u!114 &<id> stripped`), not on the body, and recurse
`m_SourcePrefab` for the real instances. Measured on Cosmic Shore's fauna: naive counting gave
QuadFish 5 / shark 11; filtered, 4 / 10 — and the shipped budget table asserting "11, measured
from the prefabs" had made the same mistake. **Validate the counter against a prefab whose
number you already trust** (the brittlestar's 10) before believing it about the one you don't.

## 4.9b Technique: stripping a dead serialized key from many prefabs

> **Not the same case as the dead-`m_Modification` trap in §5** (which says record it, don't
> hand-edit). That one is about **override entries inside a `PrefabInstance` block**, where
> deadness is a three-part question and the entries are a coupled list. This one is about a
> plain serialized key in a **directly-serialized component block**, whose field you deleted
> from the C# in the same commit — deadness is not in question, and the edit is a line removal
> inside one known component. If you are unsure which you are looking at, you are looking at
> the §5 case: check first.

Deleting a `[SerializeField]` in C# leaves its key in every prefab that authored it. Unity
never prunes an unresolvable modification, so the inspector keeps showing a value nothing reads
— worse than no field at all. Removing them mechanically is safe under three conditions:

- **Scope by the enclosing `m_Script`.** Track the last `  m_Script:` line as you stream the
  file and only drop the key while that GUID is the component you retired. A bare
  `sed '/moveToVesselDuration/d'` will happily strip a same-named key from another component.
- **Assert the scoping found everything.** Collect the rejects (key matched, wrong component)
  and print them; an empty reject list is the proof the pass was total.
- **Round-trip the bytes.** `'\n'.join(text.split('\n'))` preserves a trailing newline; verify
  against `git show <base>:<path>` that `endswith(b'\n')` is unchanged for every file, and
  confirm `git diff` contains *only* the removed key lines and no `\ No newline` marker. A
  whitespace-only byte change on 15 prefabs is indistinguishable from a real edit in review.

## 4.9c Technique: proving a SCENE's UI wiring without opening the editor

The sibling of §4.9. There the producer was a nested prefab; here it is a **scene** field
pointing at a **prefab instance**, and the behaviour you need to confirm lives in neither
document on its own.

Origin: adding four modes to the Maelstrom pool (2026-08). The tournament only advances when
the host presses Continue on the per-mode `Scoreboard`, and that button is a
`[SerializeField] GameObject continueButton` whose own tooltip says *"leave unassigned in
non-tournament scenes"*. So four scenes could each satisfy every other admission criterion and
still **stall the tournament after their round** — a null there is a silent early-out, not an
error. The whole go/no-go rested on it, and it is four greps:

1. **Find the component's block in the scene**, by script GUID from its `.cs.meta`, and read
   the field out of that block (§4.9 step 1 — never regex the field name file-wide).
2. **A `{fileID: 0}` is the failure.** A non-zero id means *something* is assigned; it does
   not yet mean the right thing.
3. **Follow the id.** `grep -n -A6 '^--- !u!1 &<id>'`. If the header ends in ` stripped`, the
   object belongs to a prefab instance: its `m_CorrespondingSourceObject` carries the source
   `guid`, and `grep -rl "guid: G" Assets --include=*.meta` names the prefab. **Do not look
   for the button's `onClick` in the scene** — it is not there, and its absence reads as
   "unwired" when the wiring is one hop away.
4. **Confirm the handler in the SOURCE prefab**, by method name *and* target type:
   `grep -B8 'm_MethodName: OnContinueButtonPressed'` should show
   `m_TargetAssemblyTypeName: <Namespace>.<Class>, Assembly-CSharp`. Matching the method name
   alone will happily accept a handler on some other component.

The payoff is comparative, so run it across the **known-good** scenes too: finding the four new
scenes carry the same `continueButton` fileID and the same source prefab as the modes already
shipping through that flow turns "it looks wired" into "it is wired identically to the proven
case". A shared fileID across several scenes is not a coincidence to explain away — it is the
signature of one prefab instanced in all of them, and it is the evidence.

