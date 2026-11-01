## 3. Technique: prefab/scene YAML surgery

Unity YAML = documents headed `--- !u!<class> &<fileID>`. A MonoBehaviour is
class 114 with `m_Script: {fileID: 11500000, guid: <script guid>, type: 3}`;
its GameObject lists it in `m_Component`.

- **Excise a component** (the right way — never leave "Missing (Mono Script)"
  rows): (1) sweep the WHOLE repo for the component's fileID — any external
  reference means stop and think; (2) remove the `- component: {fileID: X}`
  line from the owning GameObject; (3) remove the whole `--- !u!114 &X`
  document; (4) assert the fileID no longer appears anywhere in the file.
- **Delete a script safely**: get its GUID from the `.meta`, sweep ALL of
  Assets (`.unity`/`.prefab`/`.asset`/`.cs`) for the GUID and the class name
  BEFORE deleting; excise components first, delete `.cs` + `.meta` together.
- **ADD a component to an existing GameObject** (the inverse of excise; this is
  how a new MonoBehaviour gets onto N vessel/prop prefabs without the editor):
  (1) write the script's `.meta` yourself with a fresh `uuid4().hex` guid and
  **assert that guid appears in exactly one `.meta` repo-wide**; (2) pick a
  fileID, assert the literal does not already occur in the target file, and
  **assert it is ≤ `9223372036854775807` — a fileID is a SIGNED int64 and a
  random 19-digit decimal overflows it about 16% of the time.** Unity does not
  recover: `SerializedFile::IndexTextFile` fails the *whole file* with
  `Could not extract 'FileID' … This number overflows internal type`, every
  reference inside it turns into `Broken text PPtr`, and nothing is reported
  until someone loads that prefab — two vessel prefabs in this repo carried one
  for weeks (`9678703874602163012`, `9900976137657699045`) and only surfaced
  when a new tool tried to open them. Keep a readable family (e.g. `…778`,
  `…779`) so a human reading the diff can see they are yours. Sweep for the
  whole class with one regex over `&(\d+)` / `fileID: (\d+)` filtered to
  `> INT64_MAX`; fixing one is a whole-word rewrite of the anchor plus every
  reference, then re-assert the file's dangling-reference set is unchanged
  (that last check is what proves you renumbered the references too, not just
  the anchor); (3) insert
  `  - component: {fileID: X}` after the LAST existing entry of the owning
  GameObject's `m_Component` list — anchor the regex on that document
  (`^--- !u!1 &<goID>\nGameObject:\n.*?\n  m_Layer:`, DOTALL) rather than on a
  line number; (4) append the `--- !u!114 &X` document with `m_GameObject`
  pointing back, `m_Script` naming your guid, and one key per serialized field
  you want non-default. Insert it **before the trailing `--- !u!1001`
  PrefabInstance blocks**, not at EOF — a `!u!114` after them still loads, but
  every human diffing the file reads it as misplaced. Serialize enum fields as
  their INTEGER value (`condition: 1`), and get the integer from the C# —
  an enum with explicit values is not its declaration order.
  **And insert the component ENTRY inside the matched GameObject's OWN body — never by searching
  forward from where the match ended.** `re.search(r"^--- !u!1 &<go>$(.*?)(?=^--- !u!)", ...)` gives
  you a group whose `.end()` is the END of that GameObject, so a `text[m.end(1):].replace("  m_Layer:",
  …, 1)` lands the entry on whatever object is serialized NEXT. Unity accepts that in silence: the
  component's own `!u!114` doc still names the right `m_GameObject`, so nothing dangles, nothing
  errors, and the component is simply attached to the wrong object. Splice inside `m.group(1)` and
  write it back over `[m.start(1):m.end(1)]`, then **assert afterwards** that the fileID appears in
  the intended GameObject's component list AND that the component doc points back at that same
  GameObject — the two-way check is what names the failure, because either one alone passes.
- **Authoring a whole new asset FOLDER: emit its `.meta` too, or Unity re-mints it.** A directory
  under `Assets/` is itself an asset and needs `fileFormatVersion: 2` / `guid:` /
  `folderAsset: yes` / `DefaultImporter:`. Without it Unity generates one on next import — fine
  locally, and a fresh guid on every other machine, so the folder shows as an untracked change
  forever. Same uniqueness assert as a script meta.
- **Mint asset guids DETERMINISTICALLY when a script emits a whole asset set.** `uuid4()` is right
  for a one-off, wrong for a generator with `--check`: a re-run mints new guids, every
  cross-reference inside the set changes, and the diff is total. `md5(f"<project>/<set>/{name}")`
  is stable across runs and machines, so `--check` compares content instead of identity — and the
  uniqueness assert still applies (sweep every `.meta` repo-wide and confirm each new guid appears
  exactly once).
- **CHANGE a component's TYPE in place, by rewriting its class id and KEEPING its
  fileID.** Swapping `SphereCollider` → `CapsuleCollider` reads like an excise+add,
  and doing it that way is strictly worse: a new fileID means editing the owning
  `m_Component` list and re-sweeping every external reference. Instead rewrite the
  document header and body only — `--- !u!135 &<id>` / `SphereCollider:` becomes
  `--- !u!136 &<id>` / `CapsuleCollider:` at the SAME `&<id>`, so the component-list
  entry, and any `{fileID: <id>}` pointing at it, resolve unchanged. Get the class
  id and the exact field set from a REAL instance of the target type already in the
  repo (`grep -rn -A20 "^CapsuleCollider:" --include=*.prefab`), and copy its
  `serializedVersion:` line verbatim — that key is per-class and per-Unity-version,
  and it is the one thing you cannot infer from the type you are replacing.
  Afterwards assert the fileID still appears in exactly one `m_Component` list and
  the document count is unchanged. **This is an import check the human must run**
  (open the prefab, confirm the new component type renders and is not "Missing") —
  a rejected class id shows up nowhere else.
- **Self-check the surgery by resolving every local fileID.** After any add or
  excise, parse the file: collect `^--- !u!\d+ &(\d+)` as definitions and
  `fileID: (\d+)\}` as references, then report references with no definition —
  filtering the ones followed by `guid:` (those are cross-asset and legitimate).
  A survivor is either a dangling ref you just created or, just as usefully, a
  **pre-existing** one you must not be blamed for: run the same check against
  `git show HEAD:<file>` to tell the two apart before reporting anything.
- **Deleting a source object means purging FIVE record shapes, not one.** When you remove a
  GameObject from a prefab, every consumer that instantiates that prefab may hold a record
  naming it, and they do not all look alike. Sweep for all five or the leftovers are dangling
  references: (1) `m_AddedGameObjects` entries whose `addedObject` is the doomed transform;
  (2) `m_RemovedGameObjects` entries `- {fileID: N, guid: G, type: 3}` (a *removal* record for
  an object that no longer exists — harmless to Unity, pure noise to a reader); (3)
  `m_Modifications` whose **`target`** is the doomed object (transform overrides, `m_IsActive`);
  (4) `m_Modifications` whose **`objectReference`** POINTS AT it — this is the one that gets
  missed, because the entry's `target` is a completely different component and only the
  reference is doomed; and (5) `stripped` documents whose `m_CorrespondingSourceObject` names
  it. A validate-before-write assertion (`no doomed id survives as a whole word`) finds each
  missed shape in one run instead of one Unity import per shape.
- **A stale serialized KEY inside a component body is a sixth shape, and it needs the
  two-part test.** A retired field (`silhouetteContainer:`) survives in YAML until something
  re-saves the prefab, so it can still name an object you are deleting. Do NOT strip such a
  line on sight: resolve the doc's `m_Script` guid → its `.cs` → its serialized-field set, and
  drop the key only if the class no longer declares it. Note the degenerate case — a guid that
  resolves to NO script is an existing *Missing (Mono Script)* component, so the key cannot be
  live and dropping it is safe. Say which case each hit was.
- **Excise a whole PREFAB INSTANCE from a scene** (a hand-placed object that
  should not be there). It is more than one document, so drive it by PARSING,
  never by the line numbers a report handed you — the first deletion invalidates
  every later one. Split the file on `^--- !u!`, then: (1) find the `!u!1001`
  `PrefabInstance` whose `m_SourcePrefab` carries the guid; (2) collect every
  `stripped` document whose `m_PrefabInstance` points back at it — those are how
  other objects reference its children; (3) drop those documents; (4) drop every
  `  - {fileID: N}` line naming a dropped id (`m_Children`, `SceneRoots.m_Roots`);
  (5) **assert** no dropped id survives as a whole-word token and the prefab guid
  is gone. A word-boundary regex matters — fileIDs are substrings of each other.
  One parser then runs over N scenes identically and self-checks each.
- **A variant's component fileIDs are DERIVED, so a literal id will not match
  them**: `variantFileID = baseFileID XOR prefabInstanceFileID` (unsigned 64-bit,
  the instance being the variant's own `!u!1001` anchor). This is why a sweep for
  "modifications targeting component X" silently misses every prefab variant.
  **The XOR is a fallback, not a law: read the stripped stub first.** Measured 2026-10-26 over every
  `stripped` document under `Assets/` (`--- !u!N &ID stripped` with its `m_CorrespondingSourceObject`
  and `m_PrefabInstance`): 1,176 of 1,726 satisfy `(instance ^ source) & INT64_MAX`, and **550 do not**
  (GameCanvas, ArcadeGameConfigureModal, KitPopup, ButterflyHUDVariant among them). So where a stub
  exists, its own `&ID` is the variant-side fileID; compute the XOR only when no stub was written
  (`Tools/Build/check_cytoplasm_shards.py` `Prefabs._resolve` does it in that order).
  Either compute the XOR, or — better — let Unity resolve it:
  `AssetDatabase.LoadAllAssetsAtPath` + `TryGetGUIDAndLocalFileIdentifier` gives a
  fileID→object map per prefab that covers base and variant alike, so you can ask
  `obj is MyComponent` instead of matching numbers.
- **Prefer a FLAT COPY over a variant when the repo already does.** "A variant,
  never a copy" is the rule for *shared behaviour*; for "the same prop at another
  size" check the folder first. Sibling flat copies (identical internal fileIDs,
  differing only in name + scale) mean the referencing SO field stays byte-identical
  and the whole malformed-variant-YAML risk class disappears. Authoring a variant by
  hand requires deriving three XOR'd fileIDs correctly; a flat copy requires none.
- **Sweep DEAD prefab-instance modifications.** Unity never prunes an override whose
  `propertyPath` names a field the script no longer has — it survives every reserialize,
  often pointing at a guid no asset carries, and reads as real wiring to the next
  person. Find them with a TWO-part test, or you will delete live data: the
  modification's `target` must resolve to the component type you mean (previous
  bullet), AND its `propertyPath` root (split on `.`, so `Foo.Array.data[0]` → `Foo`)
  must not be a serialized field on that type. Skip `m_*` (Unity built-ins). Get the
  valid names from the C# by reflection, not by hand — a hand list is how
  `CrystalSkimAudioClip` nearly gets eaten by a filter meant for `Crystal`.
- **Re-author SO numbers**: regex with `re.subn(..., count=1)` + assert n==1
  per field; print the before/after table so the human can eyeball the math.
- **Author a whole asset FAMILY from a generator, not by hand.** When a change
  needs N sibling assets (per-element configs, per-species prefabs), write an
  in-repo Python generator: guid = `md5("<project>/<stable name>")` so re-runs
  are idempotent and reviewable, and retuning is ONE edit + re-run instead of N
  hand edits that drift. Keep the generator committed — it is the source, the
  assets are the build.
- **Validate hand-authored MonoBehaviour YAML against the C#.** Extract the
  class's `[SerializeField]`/public field names by regex, extract the prefab
  document's `^  (\w+):` key set, and diff BOTH ways. A field you forgot to
  emit silently takes its initializer; a key you misspelled is silently
  dropped — and neither shows up until someone plays the scene.
- **Author a NEWLY-serialized field into an existing prefab/SO**: Unity's YAML
  is name-KEYED, not positional or exhaustive — a key the file lacks simply
  deserializes to the C# initializer. So adding `[SerializeField] float Foo`
  needs NO mass re-save: insert `Foo: <value>` into the component's `!u!114`
  block for the instances whose value differs from the default, leave the rest
  alone, and every untouched prefab keeps working. Match the C# identifier
  EXACTLY (case-sensitive, no `m_` prefix on your own fields); a typo'd key is
  silently ignored and the field reads its default forever — assert the key
  count after writing, and grep the C# declaration to confirm spelling.
  **The corollary is a free retune, and a trap**: because the key is absent,
  changing the C# INITIALIZER retroactively changes every instance that never
  serialized it — re-tuning `MaxJawAngle` on the Dolphin needed no asset edit at
  all, because neither `Dolphin.prefab` nor `DolphinHUDVariant.prefab` had ever
  written the key. So **grep the prefabs for the key before deciding where to make
  the change**: absent everywhere → edit the C# default and you are done; present
  on some instances → the default reaches only the others, and editing it alone
  produces a silent split. The grep is the decision, not a formality.
- **Read authored MESH geometry without opening Unity** (which end is the apex?
  where's the pivot? is +Y up?): a `.asset` mesh carries `m_LocalAABB` for
  extents and the vertex buffer as hex in `m_VertexData`/`_typelessData`.
  Derive the stride from `m_Channels` (offsets + dimensions; pos is offset 0,
  3 floats), then `struct.unpack_from('<fff', bytes.fromhex(h), i*stride)` per
  vertex. Bucketing the positions by one axis answers orientation questions
  outright — a cone's apex is the axis end with ONE unique (x,z), the base is
  the end with a ring of them. This is how a claim like "the apex sits at the
  container origin" gets PROVEN instead of assumed from a comment.
- **Read an authored FBX directly — a binary FBX is ~60 lines of Python.** The `.asset`
  recipe above only reaches meshes Unity has already serialized; the SOURCE model
  answers questions no imported copy can (how many polygons, what SHAPE are they, are
  the faces planar, is it one solid or many, what did the artist actually build). Format:
  a 27-byte header (`Kaydara FBX Binary`, version at offset 23), then nested node records
  — `end, nprops, proplen` (u32 triple below version 7500, u64 above), a length-prefixed
  name, then properties typed by a single char: `CBYIFDL` are scalars, `fdlib` are arrays
  with an `(len, encoding, bytes)` header and `encoding == 1` meaning **zlib-deflated**,
  `SR` are length-prefixed blobs. Read `Objects/Geometry` → `Vertices` (flat xyz) and
  `PolygonVertexIndex` (a **negative index terminates a polygon**, and its real value is
  `-i - 1`); `LayerElementMaterial/Materials` gives per-polygon submesh assignment,
  `LayerElementNormal` its mapping mode (`ByPolygon` + one normal per face = hard-edged).
  Connected components over shared indices tell you it is 60 disjoint 10-vertex prisms
  rather than one shell — which is the fact that decides whether a shader should be
  adding a "spread" at all.
- **Do not assume an authored polygon is PLANAR — measure it, and never decide "same
  face" from a tight angle threshold.** 120 of 300 quads on one crystal were twisted by
  5.21°, so a 1° coplanarity test misread 120 triangulation diagonals as real edges and
  drew a wireframe effect straight across face interiors. Decide face identity
  STRUCTURALLY: two triangles cut from one imported face reference the very same vertex
  INDICES, while across a face boundary they cannot (the importer split them by normal).
  Keep an angle test only as a backstop, and size it by measuring BOTH populations first
  — intra-face deviation (5.21°) against the shallowest genuine dihedral (57.5°) leaves a
  50° window, and picking from inside a measured gap is not the same act as guessing 1°.

- **Exactly one `.meta` OWNS a guid — resolve with `grep -c "^guid: $g"` per candidate, NEVER
  `grep -rl … | head -1`.** `head -1` picks by filename order, which has nothing to do with
  ownership, and a model file's `.meta` is a completely ordinary place for another model's
  guid to appear: an FBX `.meta` can carry an `externalObjects` MATERIAL REMAP into a
  different FBX. That produced a placeholder hull as the confident answer for "which mesh does
  this vessel render", and two rounds of geometry were measured against a ship a fifth the
  real one's height. Then cross-check the winner against something the PREFAB itself authored
  — a BoxCollider reproducing the mesh's extents to four decimals turns a resolution into a
  fact.
- **Unity's FBX sub-asset fileIDs are NOT derivable offline — reference model sub-objects by
  NAME at runtime instead.** When a model's `.meta` has `internalIDToNameTable: []` (the
  normal case), the ids Unity assigns to bones/meshes inside the FBX exist nowhere you can
  read, and they are not a plain hash of the name (MD4 over name/class, both byte orders and
  offsets, tested against seven known meshes: zero hits). So you cannot hand-author a prefab
  reference to a bone inside a nested model prefab. Do not burn the session
  reverse-engineering it: add a serialized NAME and resolve it in `Awake`, which also survives
  a re-export — the ids do not. This is the same choice the project's own `ResolvePart` makes. **Nor can you BORROW one** by renaming a node to match a name whose id another FBX already records (two manta FBXes sharing `manta` → one id made it look safe): tried on the omni crystal's triangle shells (2026-10), Unity did not reproduce the id and the shells rendered nothing. When a hand-authored prefab must draw an FBX mesh, BAKE it into a native `Mesh` `.asset` from a generator (donor-clone `Assets/_Models/Testing/Prism.asset`'s layout; `Tools/Build/author_omni_crystal_triangles.py` is the worked example) — its reference is `{fileID: 4300000, guid: <own meta>, type: 2}`, deterministic by construction.
- **A nested prefab instance is reachable TWO ways, and needs both when its parent is PLAIN.**
  `m_TransformParent` in the instance's modification block always, PLUS an entry in the parent
  Transform's `m_Children` **iff that parent is a plain (non-stripped) Transform**. The
  exception that looks like a counter-example: an instance parented to a STRIPPED Transform
  cannot carry the entry, because a stripped document is a reference stub with no children
  list. Generalising "no entry needed" from that one case shipped eight prefab instances that
  were in the file and not in the hierarchy. Audit it as a predicate over the whole family,
  not per-instance.
- **To learn what a bone actually drives, read the SKIN WEIGHTS — not the bone's position, and
  not its name.** A `Deformer` of subtype `Cluster` carries `Indexes` + `Weights` and links to
  one bone `Model`; the indices name exactly the vertices it moves. Ranking every bone by the
  centroid of the geometry it skins describes a whole model with no interpretation, and it is
  decisive where names and geometry disagree — four passes of guessing which structure was an
  engine were settled in one query that showed those vertices belonged to `b_ShipGun1`.
  Corollary: an artist's bone NAME is a hypothesis (bones named `b_Tail*` drove the blades at
  the model's opposite end).
- **Validate a model→world axis mapping against an AUTHORED anchor before you measure anything
  with it.** Unity's Z-up→Y-up conversion is `(−x, z, −y)`, but a mis-signed axis still
  produces a plausible-looking hull, and every measurement after it is confidently wrong. The
  anchor is something a human placed BY HAND in the prefab against the same geometry — a
  muzzle transform landing on the gun bone's barrel end on all three axes, a collider
  reproducing a mesh's extents. Prefer two independent anchors; one coincidence is not a
  proof.
- **Render the geometry and LOOK at it — a wireframe costs a minute and settles what an hour
  of binning statistics will not.** Vertex histograms, bounding boxes and "rearmost point"
  queries described the wrong structure on two different models in one session; a rear view
  plus a stack of thin slices along the view axis showed the real openings immediately. Both
  models were placed correctly within one pass of rendering them, and wrongly in every pass
  before it.

### Technique: REMOVING a component (or a whole GameObject) from a prefab

Deleting the `MonoBehaviour` document is the part everyone remembers and the smallest
part of the job. A component removal is **five** edits, and missing any one leaves Unity
importing a prefab with a hole in it:

1. the `--- !u!114 &<id>` document itself;
2. its entry in the owning GameObject's `m_Component:` list;
3. if the GameObject existed *only* to host it (check `m_Component` has nothing left but
   the `Transform`), its `--- !u!1 &<go>` and `--- !u!4 &<xform>` documents too;
4. that Transform's link in the **parent's** `m_Children:` list — grep the parent by the
   child's `m_Father:` id;
5. **every serialized LIST that referenced the component by fileID.** This is the one that
   bites: `ActionExecutorRegistry._executors`, effect-container arrays, HUD wiring. A
   component can be referenced from anywhere in the file, so sweep for the bare id rather
   than reasoning about who "should" hold it.

Then prove it, and **prove it against the BASE revision, not against zero**:

```python
# anchors = {a for '--- !u!N &a'};  refs = {n for '{fileID: n}' with no guid on the ref}
# report: len(anchors), duplicate anchors, sorted(refs - anchors), and
#         GameObjects whose m_Component names an id not in anchors
```

Run it on `git show <base>:<path>` as well as on the working file and **diff the two
reports**. Unity prefabs carry pre-existing dangling references — `Dolphin.prefab` has had
a `view: {fileID: 257326519381942953}` pointing at nothing since before this session — and
a checker run only on your output reports that as damage you caused. The signal you want is
"document count fell by exactly the N I removed, and the dangling set is **unchanged**".

### Technique: REVERTING a nested instance's component swap (added + removed components)

A swap made on a nested prefab instance (here: four lifeforms removed `CrystalMass`'s shell
MeshRenderers and added SkinnedMeshRenderer + `SpaceCrystalAnimator` per shell) lives in FOUR places,
and all four must go together: the `m_AddedComponents` entries (3 lines each; the target line may
wrap), the added components' own documents, the `m_RemovedComponents` entries for the source's
components (empty list → `m_RemovedComponents: []`), and every `m_Modifications` entry that pointed
a source field at an added component (`crystalModels.Array.data[i].spaceCrystalAnimator`). Then
drop the `--- !u!1 &id stripped` GameObject docs that existed only so the added components had an
owner — but only once nothing references them (loop until no orphan remains). Assert no surviving
`{fileID: id}` names a dropped document. `Tools/Build/author_mass_crystal_look.py` is the worked
example and keeps the result held under `--check`. **Splitting on `"\n--- "` loses the file's final
newline when the LAST document is one you drop** — the newline belonged to it. Restore it
explicitly (`if text.endswith("\n") and not out.endswith("\n")`) and diff the output's tail; a
missing final newline is a one-line diff noise on every future edit and the tell that a splitter
was wrong.

### Technique: ADDING a nested prefab instance (and referencing a component inside it)

The read side of nested instances is covered above (§3's two-ways rule, §4.9). Writing one is
**three documents plus one list edit**, and the third is the one nobody expects:

1. `--- !u!1001 &<newId>` **`PrefabInstance`** — `m_Modification.m_TransformParent` pointing at
   the host Transform that will own it, an `m_Modifications` list (each entry a `target:
   {fileID: <SOURCE object id>, guid: <source prefab guid>, type: 3}` + `propertyPath` + `value`
   + `objectReference`), the four empty `m_Removed*`/`m_Added*` lists, and
   `m_SourcePrefab: {fileID: 100100000, guid: <source prefab guid>, type: 3}`.
   Always author `m_Name` plus the full local TRS (`m_LocalPosition.x/y/z`,
   `m_LocalRotation.x/y/z/w`, `m_LocalEulerAnglesHint.x/y/z`) — Unity writes all of them and a
   partial set reads as an instance that only half-overrides its pose.
2. `--- !u!4 &<newId2> stripped` **Transform** stub —
   `m_CorrespondingSourceObject: {fileID: <source transform id>, guid: <G>, type: 3}` +
   `m_PrefabInstance: {fileID: <newId>}` + `m_PrefabAsset: {fileID: 0}`.
3. **One `stripped` stub PER COMPONENT you need to reference.** This is the step that is easy to
   miss, because nothing fails until a serialized field silently reads `None`. A
   `[SerializeField] TrailRenderer tail;` on the HOST cannot point into the nested instance
   without a `--- !u!96 &<newId3> stripped` / `TrailRenderer:` document of exactly the same
   shape as (2). The class id must match the component (`!u!4` Transform, `!u!96` TrailRenderer,
   `!u!114` MonoBehaviour, `!u!137` SkinnedMeshRenderer, …).
4. The host Transform's `m_Children:` gains an entry for (2) — see §3's two-ways rule.

Mint the new fileIDs **deterministically and collision-checked** against the ids already in the
file (hash a stable key, reject on collision, re-salt) so re-running your script is idempotent and
a second nested instance in the same file cannot land on the first one's id.

Prefer this over hand-authoring a copy of the source prefab's contents: a copy severs
propagation, which is the mistake `Docs/GAMECANVAS.md` exists to record.

**Exactly ONE stub per (class, source object, instance) - a twin binds to NOTHING.** Two
`stripped` documents with the same `m_CorrespondingSourceObject` and `m_PrefabInstance` are not a
harmless duplicate: Unity binds one of them to the live object and leaves the other unresolved, so
every reference through the twin reads as **destroyed** at runtime (`if (field)` is false; DOTween
reports "has been destroyed"), while the prefab looks perfectly wired in YAML and in any offline
check that only resolves fileIDs inside the file. It is easy to write: an instance emitter writes the
root's stub, then a field-wiring step mints its own stub for the same root under a different derived
id. It cost the style-B end screen in every mode for two days (`GameCanvas.prefab`'s
`Scoreboard.scoreboardPanel`, 2026-10-17), and the same twin sat in Menu_Main's purchase window.
Reuse the stub that exists (look it up by its identity, not its id) and gate it:
`author_menu_topnav.duplicate_stubs(text)` returns every twin; `author_ingame_hud.py` and
`author_menu_purchase.py` fail on one and plant one in `--self-test`.

### Technique: deleting objects from a SHARED prefab — prove it project-wide first

Stripping dead objects out of a prefab that a dozen other prefabs instance is safe **iff no
instance anywhere references one of the objects you are removing** — an instance can carry a
`m_Modifications` entry, an `m_RemovedComponents`/`m_RemovedGameObjects` entry, or a plain
serialized reference targeting any source object by id.

The query is not a name grep. Sweep every `.prefab`/`.unity` for references INTO the source
prefab by guid and check each target id against the set you are keeping:

```python
# NOTE the \s*\n?\s* — Unity wraps the guid onto the NEXT line at ~80 columns, so a
# single-line grep for 'fileID: N, guid: G' misses roughly half the real references.
re.finditer(r'fileID: (-?\d+),\s*\n?\s*guid: ' + SOURCE_GUID, text)
```

A clean result is stronger than "I grepped the names": it enumerates every object the project
actually depends on, so the survivors are proven rather than assumed. On the run this was written
for, 13 files referenced exactly FOUR of the source prefab's 28 objects (plus the
`100100000` prefab-asset id, which is not an object) — the other 24 were provably unused and
went.

**And re-open the "it costs nothing" claim.** Dead-but-inactive objects in a shared prefab are
genuinely free while their only consumers are a handful of scene-level instances, and that is
usually how the "leave them, deleting is a separate change" note got written. Adding a **POOLED**
consumer voids it: `VesselTail.prefab`'s six disabled `ParticleSystem`s were free across 12
vessels and would have been ~480 live components across a 20-deep projectile pool per Sparrow.
A cost claim about an asset is scoped to its current consumers; pooling a new one re-opens it.

### Technique: measure TEXT FIT and GLYPH COVERAGE from the TMP font asset — no editor

Two questions come up the moment you wire or author a `TMP_Text`, and both are answerable offline
because a `TMP_FontAsset` serializes everything you need:

- **Will this string fit?** `m_PointSize` is the atlas size the metrics were baked at, the glyph
  table carries `m_HorizontalAdvance` per `m_Index`, and the character table maps
  `m_Unicode → m_GlyphIndex`. Advance-sum × `fontSize / m_PointSize` is the rendered width in the
  same units as the label's `RectTransform`. Compare against the rect the label actually gets — a
  stretch-anchored TMP takes its PARENT's `m_SizeDelta`, which is usually the button, not the text
  object's own zeroes.
- **Do these characters EXIST?** The same `m_Unicode` list IS the font's coverage. `ChakraPetch-Regular
  SDF` in this repo carries **97** — printable ASCII plus NBSP and U+2026 — so a `✓`, an arrow, a
  bullet or a box-drawing character renders as nothing. Check the fallbacks too before concluding
  a missing glyph is fatal: `m_FallbackFontAssetTable` on the font, `m_fallbackFontAssets` in
  `TMP Settings.asset`, and `m_missingGlyphCharacter` (0 = draw nothing). In this repo all three are
  empty/zero, and the default `LiberationSans SDF` (250 chars) does not carry U+2713 either.

Two rules fall out. **Wiring a label is what EXPOSES the strings it was hiding** — an unwired
`TMP_Text` reference means every `.text =` in the class has been a no-op, so the day you wire it you
are shipping code paths nobody has ever seen render; read every assignment for glyphs the font lacks
before you call the wiring done. And **check wrapping before deciding an overflow is cosmetic**:
`m_TextWrappingMode: 1` with `m_overflowMode: 0` turns a too-long caption into a second LINE, which in
a single-line button reads as two captions rather than as clipped text.

### Trap: an authored LAYOUT container is not safe to switch on as authored

A container an artist made through **UI ▸ ...** carries the `Image` Unity attaches with it: no
sprite, full white, `m_RaycastTarget: 1`. That is invisible while the object ships `m_IsActive: 0`,
and the moment your code activates it the object paints a solid rectangle the size of its rect AND
starts eating raycasts — which, for a strip that lives INSIDE a button, means it swallows the very
press the strip exists to encourage. Neutralise before showing: disable a graphic with no sprite (it
is Unity's default component, not somebody's art), leave one WITH a sprite drawing, and clear
`raycastTarget` either way. The authored `m_IsActive: 0` is usually correct and worth preserving as
the empty state — show the container only while it has children to show.

### Trap: an idiom copied from a sibling component inherits that sibling's authored data

Cloning a working component's approach is the right instinct and it silently imports the
assumptions it makes about ITS OWN prefab. `ConnectingPlayerRoster` borrows its chip template's
sprite as the halo shape — correct there, because that template is a frame — and the same two lines
in a new roster whose template is a PROFILE ICON draw a tinted, scaled-up copy of a stranger's face
behind every avatar. Likewise its "fall back to `transform` as the container" is safe on a panel and
catastrophic on a BUTTON, whose first child is the button's own label: the fallback makes that label
the chip template, hides it, and clones it once per row entry. When you copy an idiom, list what it
reads out of the prefab and re-check each one against the prefab YOU are pointing it at.

### Technique: harvest a BUILT-IN component's guid from shipped prefabs — and disambiguate by MEASURING

Unity's own UI components (`Image`, `TextMeshProUGUI`, `VerticalLayoutGroup`, `LayoutElement`,
`ContentSizeFitter`) are package scripts, so their `.cs.meta` is not under `Assets/` and there is
nothing to `find`. Recall is not an option either — a wrong guid authors a component that imports
as *Missing (Mono Script)*. Harvest instead: walk every `!u!114` document in the shipped prefabs,
key by `m_Script` guid, and identify each by the SERIALIZED KEYS only that component has
(`m_FillMethod` → Image, `m_text` + `m_fontAsset` → TextMeshProUGUI, `m_PreferredHeight` +
`m_MinHeight` → LayoutElement). Rank by frequency; the real one wins by orders of magnitude.

**Where it breaks, and the rule that saves you: `HorizontalLayoutGroup` and `VerticalLayoutGroup`
serialize an IDENTICAL key set** (both derive from `HorizontalOrVerticalLayoutGroup`), so the
signature returns two candidates and nothing separates them. Do not reach for names — objects named
`…Row` in this repo use *both* guids, and the one object named `ColumnTitles` uses the horizontal
one, so a naming heuristic lands exactly backwards. **Measure what the component DID to its
children**: a layout group overwrites its children's `anchoredPosition`, and the authored values in
the file ARE its last output, so gather each instance's children and ask whether x or y varies.

```
30649d3a…  samples=64  children vary in X: 35   in Y:  0  -> HORIZONTAL
59f81469…  samples=51  children vary in X:  2   in Y: 37  -> VERTICAL
```

Two decisive populations, no judgement. Generalises to any pair of components you cannot tell apart
from their fields: find the observable the component IMPOSES on something else, and count it.

### Technique: change ONE consumer of a SHARED prefab by overriding a field the source never serializes

A component's serialized block in a prefab holds only the fields somebody has *touched* —
everything else falls back to its C# field initializer. So `Skimmer.prefab`'s
`ForcefieldCrackleController` serializes exactly one line (`overlayRenderer`) while eleven
tuning fields, including the one that was drawing a permanently visible bubble on eight
vessels, exist only as initializers.

You can still override such a field on ONE nesting instance. Unity resolves a
`m_Modifications` entry by SerializedProperty path against the instantiated object, not against
the source's serialized text, so an entry naming a path the source omits applies normally:

```
    - target: {fileID: <componentFileIDInTheSourcePrefab>, guid: <sourcePrefabGuid>,
        type: 3}
      propertyPath: fresnelRimIntensity
      value: 0
      objectReference: {fileID: 0}
```

Append it inside that instance's `m_Modifications` (assert the list's last entry ends
`objectReference: {fileID: 0}` before appending, and that your `propertyPath` appears exactly
once in the file afterwards).

**Prefer this to disabling the renderer/component, and the reason generalises.** Both make the
thing invisible today; the override is the one that survives the feature being wired up later.
A disabled renderer silently swallows the effect the day someone adds the driver — which is the
vessel skill's rule 22 (a shared impact effect is per-vessel wiring) reached from the other
side. Disable only when the object must also stop *existing* for something (a collider, a
raycast target).

Two checks that make it safe: prove what the component actually outputs when nothing drives it
(read the shader — `Alpha = fresnel` at `_ImpactCount <= 0` is a very different claim from "it
looks off"), and re-run the file's dangling-fileID set against `git show HEAD:<file>` — an
appended modification must leave it byte-identical.

### Trap: a PREFAB ASSET does not tell you what its INSTANCE wires

Reading a prefab asset to answer "what does this thing contain / reference / bind?" is fast,
feels authoritative, and is the wrong source whenever something *instances* it. A prefab instance
can do two things the asset cannot show you:

- **Add GameObjects the asset has never heard of** — they live in the INSTANCING file, parented to
  a *stripped* transform whose `m_CorrespondingSourceObject` points back at the asset.
- **Populate serialized fields the asset leaves empty**, as `m_Modifications` entries.
- **Override serialized fields the asset DOES author** — the same `m_Modifications` mechanism, but
  this one is worse, because the asset shows a plausible value that is simply never used.

So a component that looks unwired in the asset can be fully wired in every real use of it. This
shipped a wrong claim twice on one branch: a HUD asset whose view wired one field was documented
as having three dead root branches, while the *vessel* prefab instancing it added three more
branches and populated six live readout fields. Resolve an instance's added children by walking
`m_Father` until you hit a stripped transform, then map that transform's
`m_CorrespondingSourceObject` back into the asset to learn its name:

```python
# in the INSTANCING file: parent chain ends at a stripped rect
src = re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+), guid: (\w+)', stripped_body)
# then look up src.group(1) in the ASSET to get the real parent's name
```

**The override direction has its own tell: the edit you make has NO effect and NOTHING errors.**
A vessel prefab overrode `m_Sprite` on one HUD icon; editing that sprite in the HUD variant changed
nothing on screen, and there is no warning anywhere because both values are valid. Two habits close
it: when an asset edit does not show up in play, dump the INSTANCING file's `m_Modifications`
filtered to the component's fileID *before* re-examining the asset — and note that a lone override
among otherwise-unoverridden siblings (one of four icons) is the signature of a stray edit made on
the instance, so **delete it** rather than repointing it, or you keep two authorities for one value.

**Parse `m_Modifications` as RAW LINES, never with a one-line regex.** The entry wraps:

```yaml
    - target: {fileID: 8778855275387912087, guid: c1572db06ad4244469ad3f25d86940b8,
        type: 3}
      propertyPath: m_Sprite
      value: 
      objectReference: {fileID: 21300000, guid: 0cc6e2a2018ce2d43a02078b739adf9b,
        type: 3}
```

`- target: {...}` and `objectReference: {...}` each break across two lines, so
`r'- target: \{fileID: (\d+), guid: (\w+), type: \d\}\s*\n\s*propertyPath: ...'` matches **zero**
entries and a 166-override instance reads as clean. Walk the lines instead — index every
`propertyPath:` line, then scan backwards to the nearest `- target:` and forwards for the value and
`objectReference` — and sanity-check the parse by asserting your count equals
`body.count('propertyPath:')` before you trust a negative result. *A zero-match result from a
hand-written Unity-YAML regex is a claim about your regex, not about the file.*

Same family as the `m_Name`-override trap below and the "field initializer is not the shipped
value" rule: **an assertion about what an asset contains has to be read from whoever USES it.**

### Technique: when a doc states a RELATIONSHIP between assets, check it as set algebra

A prose line like *"`ArcadeGames` holds the master minus the arena cards"* is not commentary —
it is a **testable invariant over three asset files**, and encoding it turns a vague report
("two of the new games aren't showing up") into an exact answer in one pass. Parse each list's
entry guids, then assert the relation:

```python
master, arena, arcade = entries("OrganicRematchGames"), entries("ArenaGames"), entries("ArcadeGames")
assert master - arena == arcade, sorted(name(g) for g in (master - arena) ^ arcade)
```

Two things make this worth reaching for before reading any code. It finds the omission **and**
its exact membership — the symmetric difference names the cards — and it hands you a gate for
free, because the same three lines run in a `Tools/Build/` checker afterwards. This session found
Breakwater and Skein that way in a single comparison, after the modes had shipped complete,
launchable and drawn by no screen with nothing complaining.

**Then decide report-vs-fail deliberately.** A set relation that a human may legitimately
violate (withholding a finished mode from a grid while keeping it launchable) must REPORT and
name the card, not fail the build — a hard gate that is wrong about a legitimate state gets
disabled, and then it guards nothing. And per §2.5's rule: **negative-control it** — pull one
entry back out and watch it name that entry — because a set check that happens to be vacuous
(an empty master, a path typo resolving to no file) passes exactly as quietly as a clean one.

### Trap: an asset list's LENGTH is not what the screen draws

The list is the roster; the VIEW between it and the screen is free to filter and reorder, so
grid arithmetic read off the asset is wrong by however much the view does. `ArcadeExploreView`
drops one card (the meta-mode, which has its own window) and sorts the rest ALPHABETICALLY by
display name, so a 17-card roster draws 16 cards and a card's slot has nothing to do with its
position in the file — a mode added last can land in row 1 slot 1 (`Breakwater` does). Reading
the roster and reporting "17 cards, 5 rows" was wrong twice over: wrong count, wrong rows.

Read the populate method, not the asset, for anything positional — and where the layout is
grown at runtime (a cloned overflow row), get the authored capacity by walking the scene's own
children rather than assuming the roster fits. Same family as the trap above: **an assertion
about what an asset produces has to be read from whoever CONSUMES it.**

### Trap: a multi-document regex silently spans documents and returns a plausible wrong answer

Unity YAML is a stream of `--- !u!<type> &<id>` documents. A regex like

```python
re.search(r'--- !u!224 &\d+\n(.*?m_Father: \{fileID: 0\}.*?)', text, re.S)   # WRONG
```

does not "find the root transform" — with `re.S` the `.*?` crosses document boundaries, so it
happily pairs the FIRST `224` header with some LATER document's `m_Father: 0`. It does not throw;
it returns a well-formed match with the wrong body, and every number you read out of it is wrong.
On the run this was written for it reported a HUD root as centre-anchored 100×100 when it is
actually a full-canvas stretch — and the wrong number nearly went into a doc as a correction to a
claim that had been right all along.

**Always split into documents first, then query WITHIN one:**

```python
docs = {m.group(2): (m.group(1), m.group(3)) for m in
        re.finditer(r'--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)', text, re.S)}
roots = [k for k,(t,b) in docs.items() if t=='224' and re.search(r'^  m_Father: \{fileID: 0\}', b, re.M)]
```

Note the `^  ` and `re.M` on the inner query too — without them a nested `m_Father` inside a
modification block matches. **The tell that you have this bug is disagreement between two of your
own measurements**; when that happens, do not pick the one you like, rebuild the parse.

### Trap: a regex over a YAML LIST assumes field order, and steals the next entry's fields

The document-spanning trap above has a sibling one level down: the same `.*?` crossing an ENTRY
boundary inside a single list. It is harder to see, because every field you read back is a real
field from a real entry — just not the one you asked about.

Unity writes a persistent `UnityEvent` call as

```yaml
      - m_Target: {fileID: 8758355337821682526}
        m_TargetAssemblyTypeName: CosmicShore.UI.ToyConfigureModal, Assembly-CSharp
        m_MethodName: ModalWindowOut
```

— type BEFORE method. A scanner that asked for them in the other order

```python
r"- m_Target: \{...\}"  r".*?m_MethodName: (\S*)"  r".*?m_TargetAssemblyTypeName: ..."   # WRONG
```

ran past its own entry into the NEXT one, so every call was labelled with its **successor's** type
— and because `finditer` resumes after the match, the successor's own `- m_Target` had already been
consumed, so **alternate entries were never matched at all**. Measured on the shipped tree: 279 of
992 calls read, 71.4% invisible, and 236 of the 279 mislabelled. It had been like that since the
tool was written, and its output was cited in three documents.

**Split the list into entries first, then read each entry's OWN slice** — bounded by the next
entry start or by the key that terminates one, whichever comes first:

```python
starts = list(re.finditer(r"^[ \t]*- m_Target: \{fileID: (-?\d+)", text, re.M))
for i, m in enumerate(starts):
    stop  = starts[i+1].start() if i+1 < len(starts) else len(text)
    end   = re.compile(r"^\s*m_CallState:", re.M).search(text, m.end(), stop)
    slice_ = text[m.end(): end.start() if end else stop]
    method = re.search(r"^\s*m_MethodName: (\S*)$", slice_, re.M)
```

Do not anchor the split on a specific INDENT: a list inside a nested-prefab override sits a level
deeper (96 of that project's 992 calls did), and an indent-specific split drops them silently.

**The check that makes this class of bug impossible to sit on: count what you parsed against
something independent.** `- m_Target: {` and `m_MethodName:` each occur exactly once per call, so
either number exposes a narrowing parser immediately — and a parser that narrows is invisible to
every finding written in terms of that parser. Assert the agreement in the tool itself, not in a
one-off script, and negative-control it by making the parser drop entries on purpose.

**Its downstream partner: a fallback that GUESSES turns a parse bug into a plausible fact.** When
the stolen type name failed an "is this a type declared in that file?" test, the tool fell back to
the alphabetically first declaration — so a nested `readonly struct Layer` became the reported class
name, and two such guesses were written into the tool's own frozen baseline as fact. If a resolver
must guess, COUNT and REPORT the guesses separately; and prefer a structural answer where one exists
(Unity refuses to serialise a `MonoBehaviour` whose class name does not match its file, so the
FILENAME outranks any serialized type name).

### Trap: a `TrailRenderer` on a POOLED object draws a streak across the arena

Two failures that do not exist on a scene-level object and both look like a rendering bug:

- **Reuse.** A `TrailRenderer` records points in WORLD space, so a pooled object reissued
  somewhere else draws one straight ribbon from wherever it died to wherever it respawned.
  `Clear()` at the point the object is positioned for its new life — not in `OnEnable`, which
  can run before the spawner has placed it.
- **Retirement.** Returning the object to the pool deactivates it and the whole live ribbon
  vanishes in one frame. A `TrailRenderer`'s points age out on their own, so the fix is not an
  animation: detach it to world space, set `emitting = false`, and reclaim it either when its
  own `time` elapses or on the next launch, whichever comes first — a pool cycles faster than a
  multi-second ribbon, so both paths are real.

Third, cheaper trap: **`TrailRenderer` IS a `Renderer`**, so any `GetComponentsInChildren<Renderer>()`
used as a semantic test ("does this transform draw a model?") silently changes answer the day
somebody adds a trail. Exclude it explicitly, the same way such sweeps already exclude a
shell/overlay child.


### Technique: "is this folder referenced?" is not "does this SHIP" — walk reachability from the real build roots

A per-folder reference check answers a much smaller question than the one usually being asked, and
answers it confidently. Measured on this project: the folder-by-folder pass found ~17 MB of
deletion candidates; walking guid references **transitively from Unity's real inclusion roots**
found **662 MB unreached out of 7,526 assets** — roughly forty to one. Reach for the second
whenever the question is build size, "what can we cut", or "is this pack used".

The roots are Unity's rules, not a convention:

1. every **enabled** scene in `ProjectSettings/EditorBuildSettings.asset`;
2. every asset under a folder named `Resources/` that is **not** under an `Editor/` folder;
3. `preloadedAssets` in `ProjectSettings/ProjectSettings.asset`.

Then follow guids transitively out of every parseable asset **and its `.meta`** (an importer's
`externalObjects` remap is a real edge — it is how `RhinoModel.fbx` has 0 asset references and 2
inbound remaps). Rooting the whole of `Resources/` is what **closes** the `Resources.Load`-by-name
blind spot rather than dodging it: a name-loaded asset is a root, so it can never be missed.
Reference implementation: `Tools/Build/measure_build_reachability.py`.

**Three limits belong to every number it produces** — state them or do not quote the number:

- **Code ships regardless of references.** A `.cs` with no guid referrer still compiles into
  `Assembly-CSharp`. An unreached script is a dead-code question, not a build-size one.
- **Native plugins ship by platform importer settings, not by guid.** Nothing references FMOD's
  per-platform binaries and they ship anyway.
- **A RETIRED serialized key still greps as a live edge.** Unity never prunes an unresolvable
  serialized key, so a field the script no longer declares still names a guid in the YAML and a
  text sweep still follows it — this OVER-reports. Measured: 40 `SO_ArcadeGame` assets carry a
  `PreviewClip:` the type no longer declares, which pulled 110 MB of video into "ships". It is
  invisible to the compiler AND to the inspector, so a text sweep is the only thing that sees it
  at all — which is simultaneously why the sweep is worth running and why its output needs this
  caveat attached.

The retired-key case generalises past reachability: **when checking whether a serialized field is
dead, resolve the asset's `m_Script` guid to its OWNING TYPE — never grep the field name.** A name
grep on `PreviewClip` also returns 24 `SO_VesselAbility` assets where the field is live, so "is
this identifier used anywhere" answers a question nobody asked.

Two smaller measurement traps from the same pass:

- **`du` deduplicates across arguments within one invocation.** `du -sh Assets/_Graphics
  Assets/_Audio Assets` reports `Assets` at the size of what the earlier arguments did *not*
  already cover, which reads as a plausible total. Invoke it once per path.
- **Cite a gate only after you have watched it fail.** The reachability tool's `--self-test`
  asserts four assets that must be reachable; it is worth nothing until negative-controlled.
  Dropping the `Resources/` root class (196 roots → 31) makes exactly the `TMP Settings.asset`
  probe fail — the one probe that exists to prove the load-by-name blind spot is closed. Build
  the negative control at the same time as the gate.

### Trap: a FOLDER-SCOPED reference check counts siblings as external referrers

Sweeping "which assets outside this folder point into it" and then reporting the unreferenced
share as an orphan rate is wrong whenever the folder is internally cohesive: a video folder whose
clips are each referenced by a `*Preview_Prefab.prefab` **in the same folder** measures as ~99%
orphaned, because every real referrer was excluded by the scope. The number is not slightly wrong,
it is inverted — the folder's cohesion is what produces it.

Either include siblings and report the folder as a UNIT (does anything outside reach *any* of it?),
or use transitive reachability from build roots, which has no scope to get wrong. The tell that you
are about to make this mistake: your exclusion filter is a path prefix that also matches the thing
you are measuring.

### Trap: an unquoted path with a space yields an EMPTY guid, and the grep then matches everything

The idiom that reads a guid out of a `.meta` —

```sh
g=$(grep -m1 '^guid:' $meta | awk '{print $2}')
grep -rl "$g" Assets --include='*.prefab' | wc -l
```

— fails catastrophically and silently when `$meta` is unquoted and the path contains a space
(`Assets/_Models/Vessel Models/…`). `grep` takes the two halves as two filenames, reports
`No such file or directory` on stderr, `$g` becomes **empty**, and `grep -rl ""` matches **every
file it is given**. Measured live: a model with 0 asset references reported **1,944**.

It is worse than an error because the output is a plausible number in the right units, arriving
in the middle of a batch where the stderr line scrolls past. Two habits close it:

- quote every path expansion (`"$meta"`), and
- **anchor the guid pattern and assert it matched**: `grep -m1 -E '^guid: [0-9a-f]{32}$'`, then
  refuse to search on an empty capture. An empty needle should abort, never search.

The same shape appears anywhere a computed needle can come back empty — a `sed` capture, a YAML
key lookup, a `jq` path that misses. **A search whose needle is empty is not a search that found
everything; it is a search that was never asked a question.**

### Trap: a reference check says who points AT an asset, never what the asset needs to FUNCTION

This is the one that breaks shipped content while every measurement says the removal is safe.

A guid sweep over `Assets/Unity Assests/TextMesh Pro/Examples & Extras` correctly found exactly
two assets first-party content referenced — two TMP font assets, one on `Manta.prefab`, one on a
quest UI prefab — and the removal plan written from it said "move those two font assets, then
delete the rest". Both fonts are `m_AtlasPopulationMode: 1` (**dynamic**) with `m_GlyphTable: []`
and `m_CharacterTable: []`: they carry no baked glyphs and build their atlas at runtime from
`m_SourceFontFile`, and **both source TTFs were inside the folder being deleted**. Executing the
plan as written would have left a shipped vessel prefab and the quest UI rendering **no glyphs at
all**, with both font references still resolving perfectly and nothing dangling.

The sweep was not wrong. It answered the question it was asked — *who needs this?* — and the
question that decides a removal is *and what does THAT need?* The failure shape is specific and
recurring: an asset that is a **recipe rather than a payload** (a dynamic font, a material with a
texture, a prefab variant, an animator with clips, a mesh with an `externalObjects` remap) carries
its dependency as an ordinary reference that no inbound check looks at.

So for every asset you plan to SALVAGE out of a folder you are deleting, dump its **outgoing**
guids and resolve each to an owner:

```sh
grep -o 'guid: [0-9a-f]\{32\}' "$asset" | cut -d' ' -f2 | sort -u | while read g; do
  printf '  %s -> %s\n' "$g" "$(grep -rl "^guid: $g\$" Assets --include='*.meta' | head -1)"
done
```

Anything resolving INSIDE the doomed folder has to come with it. And read the asset's own mode
flags before deciding a dependency is editor-only — a *static* TMP font genuinely does not need
its TTF at runtime, which is exactly why the dynamic case is easy to wave away.

### Trap: a measurement that scans a bounded PREFIX of a file has measured the prefix

Classifying assets by "does this file contain guid X" is cheap and correct until you cap the read
for speed. `open(a,'rb').read(4000)` looking for `TMP_FontAsset`'s script guid missed **5 of 21**
font assets, because in a TMP font asset `m_Script` sits around line 121 — past 4 KB once the
embedded material sub-asset is above it. The undercount then propagated into three separate claims
in three artifacts (a census of 16/14/2 that was really 21/18/3, and a referenced-by-shipped-content
count of 10 that was really 13 — the missing ones including the project's single most-referenced
font at 64 referrers).

All three were **self-consistent with each other**, because they shared one upstream measurement.
Self-consistency across artifacts is not corroboration when they have a common source — the same
rule `/ship` §2 states for a value read off a field initializer, reached from the measurement side.
Read whole files unless you can prove the marker is in the prefix, and when a claim is load-bearing,
re-derive it from a second, differently-shaped query.

### Technique: prove a removal dangled nothing — key edges by the referrer's GUID, not its path

`Tools/Build/measure_dangling_guid_references.py` is the committed reader for this (READER; it
writes only a snapshot JSON you name, and `--self-test` is negative-controlled). Snapshot the merge
base, make the change, snapshot again, `--diff` them with `--removed-under <the path you removed>`.

Three properties are what make the answer trustworthy, and each was a wrong first cut:

1. **The absolute count is not a defect count.** Package code has no `.meta` in a clone
   (`Library/PackageCache` is not checked in), so every `m_Script` into a package reads as unowned
   — ~440 distinct guids on this project before any change. Only the DELTA is the signal.
2. **A falling count is not a proof.** A new dangle hides behind a larger number of removals.
   Difference the SETS both ways, and split every lost edge by whether its referrer was inside the
   path you removed. The proof is two zeros: no new unowned guids, and no lost edge whose referrer
   was outside the change.
3. **A MOVED referrer is not a lost reference.** Key an edge by the referrer's own guid, never its
   path, or a `git mv` reports as one loss plus one gain. A path-keyed first cut produced 24
   phantom "lost" references for three demo scenes that had moved one folder deeper — on a branch
   that both moves and removes, that is the difference between a gate and noise.

### Trap: "reachable MB" and "folder MB" are different numbers and both are in scope

`measure_build_reachability.py` reports how many bytes a folder's assets are *reached by a build*;
`du -sh` reports what is on disk. They diverge by whatever is unreferenced, which for a vendored
package is most of it — and the reachability number is the smaller, more impressive one, so it is
the one that gets quoted by mistake. Two claims in one session said "12 MB" and "4.2 MB" for
folders that measure 12 MB (coincidence) and **4.4 MB** on disk.

State which you mean every time: *"reaches a build"* vs *"on disk"*. A removal's headline is
usually the first and its licence/repo-hygiene argument is the second.

### Trap: a runtime `new Material(shader)` draws a variant the BUILD stripped

A player build keeps only the shader variants some shipped material (or a variant collection) asks for, so
a material made in code at runtime can ask for a keyword nobody shipped. The Editor has every variant,
so it works there and fails only in the build: `INSTANCING_ON` is the usual casualty, and the draw comes out
non-instanced or invisible. When code needs a material with a keyword or instancing, author a material
ASSET with it set (GPU instancing ticked), keep it reachable (`Resources/`, or a reference from a shipped
SO), and load it rather than constructing one. `SwarmMemberFallbackDraw` loads
`Resources/SwarmMemberInstancedFallback.mat` for exactly this (claude/webgl-toybox-kg5qx7, 2026-10-14).

### Technique: check a `.jslib` against a vendor's JS internals by grepping its static library

A WebGL plugin's browser half is often JavaScript embedded as plain strings inside an Emscripten static
library (`Assets/Plugins/<vendor>/platforms/html5/lib/<ver>/*.a`). A `.jslib` that calls into it
(`Module.mInputRegistered`, `Module["OutputWebAudio_resumeAudio"]`) can be checked without a build:
`grep -c -a '<name>' <lib>.a` for each name, against the library version Unity will link (FMOD picks
`3.1.39` for Unity 6), and `grep -a -o '.{200}<name>.{300}'` to read the code around it. Names that are
not there mean the `.jslib` does nothing, silently (claude/webgl-toybox-kg5qx7, 2026-10-14).

