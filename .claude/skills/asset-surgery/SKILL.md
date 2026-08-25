---
name: asset-surgery
description: Do "editor-only" Unity work programmatically instead of handing the human an in-editor checklist - ShaderGraph node wiring via JSON synthesis, prefab/scene YAML component surgery, SO asset re-authoring, binary FBX read AND write (edit a model in place, keeping its guid and fileID, validated with assimp - see 4.8/4.8c), and REAL compilation of the C#/HLSL you are about to commit (dotnet-sdk/Roslyn + clang are installable here - see 4 and 4.5c; do not settle for inspection). Use whenever the plan is drifting toward "I'll prepare instructions and you do it in the editor", whenever a task touches .shadergraph/.prefab/.unity/.asset/.fbx files directly, or when the human says some flavor of "you can do this" / "write a tool for it". The human's editor time is for PLAY TESTING and things that genuinely need the running editor - not for mechanical asset edits you can machine-validate.
---

# Asset Surgery — do it programmatically, prove it before writing

Origin: the clock-material migration (2026-08). The session initially handed the
human a multi-phase in-editor ShaderGraph wiring checklist, rationalized as
"node splicing is semantic, belongs to eyes-on editing." The human pushed back
twice — "I think you can do these things or write tools to do these things" —
and was right: all four graph phases, the prefab component excisions, and a
1,600-line deletion pass shipped out-of-editor, first-try clean, because every
edit was **machine-validated before writing**. This skill captures that method
so the confidence doesn't have to be re-learned.

## 0. The doctrine

- **Punting is the last resort, not the default.** Before writing any
  "steps for you to do in the editor," ask: what EXACTLY needs the running
  editor? Almost always only: play testing, device profiling, play-mode
  measurement tools, and final import verification. Everything mechanical —
  edits to serialized assets, wiring, deletions, re-authoring numbers — you can
  do, because the formats are text and correctness is checkable by machine.
- **Risk is managed by validation, not by human hands.** A human clicking
  through the Shader Graph UI is not safer than a script that parses every
  block, resolves every reference, asserts every invariant, and only then
  writes. It is slower and less repeatable.
- **The human is the SEMANTIC gate, not the mechanical one.** Structure your
  work so their one playtest validates meaning ("does it look right?") while
  your validation already guaranteed structure ("does it parse, resolve,
  compile?").
- **Incremental trust loop.** On a new technique: do ONE representative
  instance → human playtests → then batch the rest without asking. ("Do it for
  the block grow graph. I will test. Then you will be able to confidently do
  the rest.")

## 1. The safety pattern (applies to every technique below)

1. **Read + parse the WHOLE file first.** Build an object model (every JSON
   block / YAML document), not a regex-only view.
2. **Idempotency guard first.** If the edit already landed, print "already
   done" and exit 0. Scripts get re-run.
3. **Donor-clone for schema exactness.** Never hand-author a serialized object
   from documentation. Clone a same-file donor of the same type and rewrite
   only identity fields (IDs, names, values). The file's own serializer version
   is then correct by construction.
4. **Validate EVERYTHING before writing anything.** Rebuild the output in
   memory; assert: every block/document parses; IDs unique; every node, slot,
   edge, property, child-list reference resolves; the specific edit landed
   (feeder counts, renamed fields, removed IDs truly gone). A failed assert
   costs nothing because the file was never touched.
5. **Write once, verify once more** (re-read and re-run the checks if the edit
   was risky). Git is the rollback — one file per concern, commit early.
6. **Leave a safety net in the repo:** an idempotent in-editor repair tool
   (auto-wire / re-add), a validator menu item that reports ✅/❌ per
   requirement, and fail-loud runtime diagnostics (once-per-offender errors
   naming exactly what's unwired). These catch reverts and future drift — and
   they're what makes strict no-fallback modes safe to ship.
7. **If the tool WRITES assets, its output is the deliverable — wire it to
   ship.** You cannot see that output: the tool runs in the human's editor,
   minutes or days later, and the result lands in THEIR working tree while your
   branch carries only the tool. That is how a migration merges half-landed —
   code that expects a scene nobody pushed, broken everywhere, with nothing in
   the diff to explain it. So: `FrogletToolChangeLedger.Record(ToolName, path)`
   in the same block that writes each asset, and
   `FrogletToolShipPanel.Draw(Ship, this)` at the bottom of `OnGUI` — that gives
   the human **Validate & Push** (stages only this tool's paths) and **Retire
   Tool** (deletes the one-off once its output is safely pushed). Contract and
   rules: `Docs/TOOLING.md` § "Tool output is a deliverable"; the end-of-branch
   gate is `/ship-tools` (and `/ship` §2.5, which no ship mode may skip). A
   READER tool that only logs needs none of it — say so in its doc comment so
   nobody hunts for output that was never meant to exist.

## 2. Technique: ShaderGraph JSON synthesis

`.shadergraph` = multiple JSON blocks separated by blank lines; block 1 is
`GraphData` (holds `m_Properties`, `m_Nodes`, `m_Edges`, category data);
every other block is one object keyed by 32-hex `m_ObjectId`.

- **Add a property**: clone a same-type donor property block; rewrite
  `m_ObjectId` (new 32-hex), `m_GuidSerialized` (new GUID), `m_Name`,
  `m_RefNameGeneratedByDisplayName`, `m_DefaultReferenceName`, clear
  `m_OverrideReferenceName`, set `m_Value`. Per-instance stamps: exposed
  (`m_GeneratePropertyBlock: true`) + Hybrid Per Instance
  (`overrideHLSLDeclaration: true`, `hlslDeclarationOverride: 3`). Global
  uniforms (drivable by `Shader.SetGlobalFloat`): UNEXPOSED
  (`m_GeneratePropertyBlock: false`), no HLSL override. Register the new ID in
  BOTH `GraphData.m_Properties` AND a blackboard `CategoryData`'s
  `m_ChildObjectList` (pick the category with the most children).
- **Add a node**: nodes reference slot blocks by object-id in `m_Slots`; each
  slot block carries the INTEGER `m_Id` used by edges. Custom Function node:
  `m_SourceType: 0`, `m_FunctionName` (no `_float` suffix),
  `m_FunctionSource: <hlsl asset GUID>` — pin that GUID by committing the
  `.meta` yourself. Slot integer ids must match the HLSL parameter order.
  PropertyNodes: clone an existing one, point `m_Property.m_Id` at the target.
  Register every node in `GraphData.m_Nodes`.
- **When a splice needs "the node's OTHER input", DERIVE it from the node's slot set —
  never from the edge that led you to the node.** Finding the grow `Multiply` by walking
  `PrismGrowScale.Scale`'s outgoing edge hands you the slot Scale feeds (`B`); the slot you
  actually want to intercept is the one it does NOT feed (`A`). Returning the wrong one
  retargets the *feature's own* feeder into your new node and quietly cuts it out of the
  chain. Take the node's input-slot ids, subtract the one you arrived on, and `assert` exactly
  one remains. This is cheap to get wrong and free to catch — validate-before-write flagged it
  with `PrismGrowScale.Scale no longer feeds a Multiply` and nothing was written.
- **Splice an edge**: edges are
  `{m_OutputSlot:{m_Node:{m_Id}, m_SlotId}, m_InputSlot:{...}}`. To intercept
  a feed, RETARGET the existing edge's end (don't add a duplicate feeder into
  the same input slot — assert exactly one feeder per input).
- **Add an INPUT to a SUBGRAPH**: the consuming `SubGraphNode`'s input slot integer id is
  **`Guid.GetHashCode()` of the subgraph property's guid** — in .NET that is the XOR of the
  guid's four little-endian 32-bit words (`struct.unpack("<4I", uuid.UUID(g).bytes_le)`), NOT
  the older `_a ^ (_b<<16|_c) ^ (_f<<24|_k)` formula and NOT any string hash. The node also
  serializes the mapping outright in **`m_PropertyGuids` / `m_PropertyIds`**, index-aligned, so
  you can verify your derivation against every id the file already carries before writing one —
  do that, it is a five-line check and it is the difference between wiring an input and adding
  a slot Unity will silently drop on import. Two consequences: **PIN the new property's guid** as
  a constant in the wirer (minting it per run re-mints the slot id, so a re-run produces a
  different graph and any edge you wrote to the old id dies), and append to BOTH arrays. A stale
  entry in those arrays for a property that no longer exists is normal and harmless — the shipped
  `RotateFacesAlongAxis` node carries one — so do not "clean it up" and do not treat array length
  as the slot count. Output slots need none of this: they mirror the `SubGraphOutputNode`'s own
  small integer ids (1, 2, …).
- **A pivot/anchor constant buried in a subgraph is a MESH MEASUREMENT in disguise.** Before
  reusing a vertex-animation subgraph on a second mesh, look for bare numeric literals in its
  position math: `RotateFacesAlongAxis` carried a `0.5` that was really "half a cube face's
  half-width", correct for the one mesh it was written against and wrong (pivot outside the
  polygon) for every other. Porting the new mesh's attributes in cannot reach a constant like
  that — it has to become an input.
- **Remove**: drop the block, drop its entry from every registry
  (`m_Nodes`/`m_Properties`/category child list), assert no edge references it.
- **READ a graph before you touch it — dump the edge list, don't eyeball JSON.**
  To answer "what does property X actually do?", stream-parse the file with
  `json.JSONDecoder().raw_decode` in a loop (plain `json.load` throws
  `Extra data` — it is CONCATENATED objects, not one document), index every
  block by `m_ObjectId`, then print each `m_Edges` entry as
  `srcNode.srcSlot -> dstNode.dstSlot` with nodes labeled by type + resolved
  property name (`PropertyNode.m_Property.m_Id` → that property's `m_Name`)
  and slots resolved through the owner's `m_Slots`. The semantics fall out in
  one screen. Follow `SubGraphNode.m_SerializedSubGraph.guid` into the
  `.shadersubgraph` (find it via `grep -rl <guid> Assets --include=*.meta`)
  and repeat — the meaning usually lives one level down. Also dump unconnected
  input-slot `m_Value`s: an input with no edge is a hardcoded constant.
- **Which properties are tunable per material**: an exposed property that is
  NOT Hybrid Per Instance and is never written by a `Stamp*`/`SetFloat` call
  is a plain material constant — tune it in the `.mat` (`m_Floats`), and the
  instanced/entity draw path picks it up with no code change. Confirm the
  entity path reads the SAME asset (e.g. `PrismDebris` copies
  `PrismExplosion.prefab`'s `sharedMaterial`) or you will tune a material
  nothing draws with.
- Unity reimports on pull; the in-editor validator + a shader-error check
  (`ShaderUtil.ShaderHasError`) confirm; magenta = `git checkout` the graph.

### 2a. Reading a graph to learn what a property MEANS

Before tuning any value a shader consumes, find out what the shader does with it —
the field name lies often enough to be worth 5 minutes. (`OutsideBlockColor` /
`InsideBlockColor` are actually the prism's base face and its fresnel rim; nothing
about them is "outside" or "inside".)

- **Parse robustly**: `.shadergraph`/`.shadersubgraph` are CONCATENATED JSON
  documents, so `json.loads(whole_file)` dies with
  `JSONDecodeError: Extra data: line N`. Don't split on blank lines (CRLF, §5);
  loop `JSONDecoder().raw_decode(s, i)`, skipping whitespace between documents.
  Robust against any separator.
- **Build the model**: index every doc by `m_ObjectId`; the one doc whose
  `m_Type` contains `GraphData` holds `m_Edges`. Each edge is
  `{m_OutputSlot:{m_Node:{m_Id},m_SlotId}, m_InputSlot:{...}}` — resolve
  `m_Node.m_Id` through the index to get the node, and for a `PropertyNode`
  follow `m_Property.m_Id` to the property doc for its `m_Name`.
- **Follow it down**: `SubGraphNode`s carry `m_SerializedSubGraph` with the
  subgraph's **guid** — resolve it by grepping `.meta` files, then repeat. A
  property's real meaning is usually two subgraphs deep.
- Print the edge list as `label(out) --> label(in)`; the semantics fall out of
  reading ~20 lines. This is how you replace "I think this is the rim colour"
  with "I traced it."

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

## 4. Technique: C# verification — get a real compiler first

**Reach for ROSLYN, not `mcs`.** `mcs` is a C# 7.x compiler and this codebase is
C# 9: it rejects ordinary, already-shipped gameplay code (`effects is { Length: > 0 }`,
`readonly Dictionary<int,float> x = new();`, `effect is not FooSO`) and dies on the
last one with `error CS0589: Internal compiler error during parsing … type pattern
matching`, which names nothing useful. Every line of the desugaring table below exists
only to work around that. Skip it:

```sh
apt-get update && apt-get install -y dotnet-sdk-8.0    # the update is REQUIRED — a stale
                                                       # index 404s on the .deb (seen 2026-08)
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
dotnet "$CSC" -langversion:9.0 -target:library -out:/tmp/x.dll Stubs.cs <files>
```

**When `apt-get` isn't available (remote/rootless containers), install it per-user** — same
Roslyn, no root, ~40s, and it lands in the scratchpad so it never pollutes the repo:

```sh
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 8.0 --install-dir "$PWD/dotnet" --no-path
CSC=$(ls "$PWD"/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
"$PWD"/dotnet/dotnet "$CSC" -langversion:9.0 -target:library -out:/dev/null <files>
```

Do NOT conclude "no compiler here" from a missing `dotnet` on `PATH` — that was the state of
a 2026-08 remote session that then nearly shipped on inspection alone. There are also no Unity
managed DLLs in such a container (no `Library/`, no `UnityEngine.dll` anywhere on disk), so a
**whole-assembly** type check is impossible and the no-stubs filter below is the fallback.

**But a REAL type check of the files you actually wrote is still available, and it is worth the
20 minutes** on new code (as opposed to a small edit inside a large existing file). Build a stub
harness — the mcs recipe below, minus the desugaring — and hand Roslyn the .NET **reference pack**
so `System.Object` exists:

```sh
REFDIR=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)   # or $PWD/dotnet/packs/...
REFS=$(ls $REFDIR/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
dotnet "$CSC" -langversion:9.0 -nostdlib -noconfig $REFS \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
  -target:library -out:/tmp/x.dll Stubs.cs <your files>
```

Without `-nostdlib -noconfig $REFS` this dies in a wall of `CS0518 Predefined type 'System.Object'
is not defined` and reads as a broken harness. Two rules make the stubs honest: **transcribe every
signature from the real declaration** (grep it, don't remember it — the whole value is that a wrong
member name or arity fails HERE), and **declare each stub in the type's real namespace** so a
missing `using` still fails. `UniTaskVoid` needs an `[AsyncMethodBuilder(...)]` struct with the six
builder methods — ~25 lines, and it is what lets an `async UniTaskVoid` method be checked in place
rather than desugared. A 2026-08 Urchin session type-checked two new ability files this way against
~200 lines of stubs and shipped them clean; the errors it *did* surface were both stub gaps
(`Object.name`, `Behaviour.isActiveAndEnabled`), which is what a working harness looks like.

Roslyn parses the real files, so **the throwaway desugared copy disappears entirely** —
and with the same `Stubs.cs` harness you still get the full type check. Cost is one
install (~1 min) against a desugaring pass that has to be redone per file and can itself
introduce errors.

**The 30-second version, when a stub harness isn't worth building** (a small edit to a
file that touches a huge API surface): compile the real files with **no stubs at all**
and read only the error classes that stubs cannot cause —

```sh
# Pass the file list through a RESPONSE FILE — see the CS2001 note below.
git diff --name-only origin/bleeding-edge...HEAD | grep '\.cs$' | sed 's/^/"/;s/$/"/' > files.rsp
dotnet "$CSC" -langversion:9.0 -target:library -out:/tmp/x.dll "@files.rsp" 2>&1 \
  | grep -E "error CS(1[0-9]{3}|8[0-9]{3}|0102|0106|0128|0136)"
```

**`CS2001: Source file '…' could not be found` on a path you can `cat` is a QUOTING bug, not a
missing file.** This repo is full of directories with spaces (`Skimmer Crystal Effects`,
`Effect Containers`, `Data Containers`, `Cell Configs`), and a shell-expanded `<files>` splits
every one of them into fragments — a single such path produced three CS2001 lines naming
`.../EffectsSO/Skimmer`, `Crystal`, and `Effects/….cs`. The tell is that the fragments
concatenate back into a real path. A response file with one quoted path per line fixes it for
good, and is worth using unconditionally so the failure can never appear.

The flood of `CS0246`/`CS0234` (missing Unity types) is expected noise; a hit in that
filter is real. **Bucket the diagnostics before reading any of them** —
`| grep -oE "error CS[0-9]+" | sort | uniq -c | sort -rn` turns 4,000 lines into six rows, and
the shape of that histogram tells you instantly whether anything real is in there. If you add
`-nostdlib -noconfig` (useful when the SDK's own ref assemblies muddy the output) then
`CS0518`, `CS8179` and `CS8137` join the expected-noise list, since they are all "predefined
type not defined" in disguise — filter those three too or they read as findings.

**Know exactly what this pass is blind to, because it is blinder than it looks.** Once a type's
BASE CLASS is unresolved — which is every `MonoBehaviour`/`NetworkBehaviour` in this repo when no
Unity assemblies are present — Roslyn stops binding that type's METHOD BODIES, and every
body-level diagnostic silently vanishes. Measured on `AstroLeagueBall` (base `NetworkBehaviour`)
by injecting one defect at a time and counting the hits:

| injected defect | caught? |
|---|---|
| syntax error (`CS1xxx`) | **yes** (3 hits) |
| duplicate member declaration (`CS0102`) | **yes** (1 hit) |
| undeclared identifier inside a method body (`CS0103`) | **NO** (0) |
| shadowed local in a nested scope (`CS0136`) | **NO** (0) |

The line is **declaration level vs body level**: declarations still bind, bodies do not. So this
pass proves syntax and the shape of the type, and proves *nothing whatsoever* about the code you
wrote inside a method — an earlier version of this skill claimed it caught `CS0128`/`CS0136`
"inside a `switch` section or a nested loop", and that is precisely the half it misses. A session
shipped a dropped field declaration straight through a green full-file pass; only the stub harness
caught it. **So treat the stub harness as mandatory for any new code in a method body**, not as
the escalation for "when a wrong member name matters" — and prove your own gate the way this table
was produced: inject the defect you care about, confirm the gate fires, restore, `cmp` the file.
A gate you have not seen fail is not a gate.

### Fallback: `mcs` (only when dotnet can't be installed)

`apt-get install mono-mcs` gives you `mcs`, and a Unity gameplay file usually touches a
small, stubbable surface. Recipe (validated 2026-08 on two ~400-line generators, both
of which compiled clean and shipped):

1. Write `Stubs.cs` covering ONLY the API the target files touch — the
   `UnityEngine` types (`Vector3` with its operators, `Quaternion`, `Mathf`,
   `Debug`), the project enums, and the base class with the exact protected
   members used. Bodies can return anything; you are checking names, types,
   arity, and control flow, not behaviour.
   **Declare every stub in the type's REAL namespace, and verify that namespace
   from the repo rather than assuming it.** A harness that parks everything in
   one convenient namespace cannot see a missing `using`, which is the single
   most likely error in a new file — it compiles clean and Unity then rejects it.
   Cosmic Shore has bitten here once: `GameDataSO` lives in `CosmicShore.Utility`,
   not `CosmicShore.Gameplay` with the controllers that consume it, so a new
   `ScoringRuleSO` subclass shipped without `using CosmicShore.Utility;` and
   surfaced as `CS0246` plus a cascade of `CS0534 does not implement inherited
   abstract member` (every override whose signature mentions the unresolved type
   stops matching its base). Harvest the namespaces mechanically — walk the
   `.cs` files building a `type → namespace` map — and, once the harness is
   fixed, prove it by deleting the `using` again and watching it fail.
2. **Desugar what mcs 6.8 (C# 7.x) cannot parse but Unity (C# 9) can** — in a
   THROWAWAY COPY, never the real file. The full list this project has needed
   (2026-08, ecology death-path branch — every one of them showed up in ordinary
   gameplay files, so budget for all of them up front):

   | C# 8/9 form | Desugar to | Note |
   |---|---|---|
   | `T x = new(...)` | `T x = new T(...)` | regex on the *declaration* line |
   | `f.field = new() { … }` | `f.field = new T { … }` | no declared type on the line — the declaration regex MISSES it, so grep for surviving bare `new(` separately |
   | `x ??= expr;` | `if ((object)x == null) x = expr;` | anchor the regex to statement form; a **trailing `// comment`** or a **multi-line lambda RHS** defeats a naive `(.+);$` and both occur in this repo |
   | `x is { A: true }` | `(x != null)` | property pattern |
   | `if (x is not T y) return;` | `var y = x as T; if (y == null) return;` | a blanket `is not` → `!=` replace **corrupts** this into a syntax error — handle the declaration form FIRST |
   | `v = k switch { A => a, _ => b };` | `if`/`else if` chain | switch *expression*; grep `` switch$ `` to find them |
   | `async UniTaskVoid M()` | `async void M()` | mcs lacks the AsyncMethodBuilder plumbing |
   | `switch (x) { case T y: … }` | `as`-cast `if`/`else` chain | type-pattern switch STATEMENT — mcs never implemented it and reports `error CS0589: Internal compiler error during parsing … type pattern matching`, which names nothing useful |

   Assert zero bare `new(` remain, and re-grep after desugaring — the parse dies at
   the first one and every later error is cascade noise that will waste your time.
   **Cascade discipline generally**: one unparsed construct produces a dozen
   "Unexpected symbol `?`" and "cannot be used before it is declared" errors far
   from the real cause. Fix the FIRST error and recompile; never triage the list.
3. `mcs -target:library -langversion:latest -out:/dev/null Stubs.cs <files>`.
4. Ignore a `CS0436` warning about a type you stubbed that Mono's BCL also has
   (e.g. `System.HashCode`) — harness artifact, not a finding.

Cost is minutes and it catches the whole class of errors inspection misses
(wrong member name, wrong arity, a `ref readonly` misuse, an unreachable
branch). Do NOT commit the harness — stubs rot against the real API. Rebuild it
per task; it is cheap.

### Fallback: verification without a compiler

- **Brace balance**: a naive count fails on interpolated strings. Use the
  tokenizer that tracks modes (`//`, `/* */`, `"str"`, `@"verbatim"`, `'c'`,
  `$"interp"` with `{...}` holes — compare hole-close depth BEFORE
  decrementing) — the session's checker caught its own bug that way.
- **NESTED interpolated strings** (`$"{string.Join(",", xs.Select(x => $"{x}"))}"`):
  when a `"` closes a string in interp mode, return to **code** mode
  unconditionally — a string literal is an expression inside the hole's code.
  The outer interp string's BODY is only re-entered via its hole-closing `}`
  (matched by recorded brace depth), never by a quote. Modeling it as a
  string-mode stack falsely flags valid files (this bit the checker on
  `Debug.Log($"... {string.Join(", ", xs.Select(g => $"{g.Key}"))}")`).
- **Check the brace balance DIFFERENTIALLY, not absolutely.** Run the checker
  on the file at the BASE revision (`git show <base>:<path>`) and on your
  edited copy, then compare (depth, mode) pairs. Equal = your edit is
  balance-neutral, which is the actual question; a non-zero absolute depth is
  usually the checker tripping over interpolated-string handling, not a real
  imbalance, and chasing it wastes the pass. This session's two "BAD" files
  flagged identically before and after the edit.
- **Blast radius**: before deleting/renaming any member, grep for every caller
  (`\.Member\b` patterns); after editing, sweep again — the deleted surface
  must appear ZERO times outside historical docs.
- **Comment hygiene**: after deleting a system, grep its NAME across code AND
  docs; rewrite comments that describe the dead architecture as live (they
  become false doctrine — this session found its own outdated rationalization
  quoted in a header comment).
- **Comment hygiene, harder case: the system SURVIVED but its ROLE changed.**
  Deleting a system is the easy sweep, because the name goes to zero. When a
  strict/no-fallback mode lands, the retired tier usually still *compiles and
  runs* — it just no longer does what its comments claim. Prism debris: the
  pooled `PrismExplosion` path was described as "the fallback for a disabled
  render service" in three places including a doc, and under strict clock mode
  it actually renders NOTHING (the renderer is disabled unconditionally before
  the entity branch). Every such comment routes the next reader — or the next
  agent — to the wrong conclusion about whether the path is safe to delete.
  After adopting a strict mode, grep the retired tier's name for the words
  *fallback / fall back / legacy path / degrades to* and re-read each hit
  against what the code now does.

### Technique: resolve a `.shadergraph` MERGE by re-running the wirers, never by hand

Origin: the dither branch vs the Sparrow turret branch (2026-08-11). Both wired nodes
into `BlockGraph`/`ExplodingBlockGraph` — one moved the prism flight to the GPU clock,
the other added a debris erosion wipe and a back-face fade. Git produced **40 conflict
hunks of ShaderGraph JSON**. Resolving those by editing conflict markers is not a real
option: the format is concatenated documents with object-id cross-references, so a
plausible-looking textual merge silently yields duplicate ids, orphaned slots, or two
feeders on one input.

The move — and the reason to make every graph wirer idempotent in the first place:

1. **Take ONE side whole** (`git checkout --theirs <graph>`), normally the base branch's,
   so you keep whatever they did and owe only your own re-application.
2. **Re-run YOUR wirers.** An idempotent wirer that prints "already wired" on a no-op is
   also a merge-conflict resolver — it re-splices onto whatever it finds. This is the
   payoff for §1.2, and it is worth writing them that way even when you expect no merge.
3. **Re-run THEIR wirers too**, and confirm each reports "already wired". That proves your
   re-application did not retarget an edge they own.
4. **Verify by DUMPING THE RESOLVED EDGE LIST** (§2a), not by trusting the per-tool
   validators — each one only checks its own splice, and the defect a merge creates lives
   *between* them. Print the chain into `SurfaceDescription.Alpha` and read it:
   ```
   BlockGraph:          Alpha -> OcclusionFade -> BackFaceFade -> Alpha
   ExplodingBlockGraph: ErosionFade -> OcclusionFade -> BackFaceFade -> Alpha
   ```
   Then dump every node's INPUT slots and confirm each resolves — the cross-branch join
   (`ErosionFade.BaseOpacity <- PrismExplosionClock.Opacity`, one branch's node feeding
   the other's) is exactly the edge no single validator covers.
5. **Count-check against BOTH parents.** Merged node/edge counts must be a superset of
   each parent by exactly what your wirers reported adding, and **property counts should
   match the side you took** — a property count above it means both branches added the
   same property and you now have two.

Assert the JSON analog of `CS0102` while you are there: duplicate `m_ObjectId`, duplicate
property reference names, registry entries that do not resolve, dangling edge endpoints,
and any input slot with more than one feeder. All five are ~20 lines over the parsed model.

### Trap: two graph-edit failures that ship silently — cycles, and slot-type mismatch

Both shipped and cost a playtest round each (2026-08, shield-shatter branch):

1. **An edge CYCLE makes the whole graph magenta.** Splicing node A's output into node B
   while B (transitively) feeds A creates a cycle ShaderGraph rejects at import — every
   material on the graph renders magenta, including effects your edit never touched.
   Before writing any edge, walk the edge list and assert acyclicity of the whole graph,
   not just the nodes you added.
2. **A property NODE cloned from the wrong donor carries the donor's SLOT TYPE.** A
   Vector3 property exposed through a node cloned from a Vector1-slot donor wires
   "successfully" and delivers nothing — no import error, no magenta, the vector is
   silently zero. Assert that every property node's slot type matches its property's
   kind after synthesis.

Both checks are cheap to run over the parsed JSON and are now standing assertions in
`PrismClockWiringValidator` + `PrismShieldMorphTests` — copy that shape into any new wirer.

### Trap: a clean merge can still be a semantic conflict (duplicate members)

Origin: `Flora.LeafSize` (2026-08). Two branches each added the SAME member to
the same class at DIFFERENT file offsets — one above `Grow()/Plant()`, one below.
Git saw two non-overlapping hunks, auto-merged both, reported no conflict, and
shipped a `CS0102: already contains a definition` that only surfaced when Unity
compiled. **Zero conflict markers is not evidence the merge is correct.** Two
branches converging on the same idea is exactly when this fires — and the more
similar the sibling branches, the likelier it is.

Detection, after any merge of long-lived sibling branches:

1. **Find the files the merge actually combined** — the ones changed relative to
   BOTH parents. Everything else is a fast-forward of one side and cannot have
   this defect:
   ```sh
   for f in $(git diff --name-only $M^1 $M -- '*.cs'); do
     git diff --quiet $M^1 $M -- "$f" || a=y
     git diff --quiet $M^2 $M -- "$f" || b=y
     [ "$a$b" = yy ] && echo "BOTH-SIDES: $f"; a= b=
   done
   ```
   This narrowed a 26-file merge to the single genuinely-combined file.
2. **Scan those files for repeated member names** (regex the
   `public|protected|private|internal … Name =>` / `{ get` declarations per file
   and report `Counter` entries > 1). Expect false positives from generated
   input-action assets (per-map wrapper classes) and generic `Singleton<T>`
   variants — same name, different enclosing type. Verify the enclosing class
   before calling one a defect.

Fix by MERGING the two doc comments into one declaration, not by deleting one —
each side wrote its comment for a reason and the surviving comment should carry
both meanings.

### Trap: an empty slice makes `str.replace("")` shred the file

`t.replace(t[i:j], new)` is the natural way to swap a block out of a source file — and when
`j < i` (the two anchors are in the opposite order to the one you assumed) the slice is `""`,
`str.replace("", new)` inserts `new` **between every character**, and the file is destroyed in
one line. It happened here on a Python fitter whose `space_leaf` was defined *after* the
function used as the end anchor.

Never slice-and-replace on unverified anchor order. Instead:

```python
def sub(old, new, label):
    assert old in t, f"ANCHOR MISS: {label}"
    assert t.count(old) == 1, f"AMBIGUOUS ({t.count(old)}x): {label}"
    return t.replace(old, new)
```

…and for a block, assert `j > i` before slicing. Then re-parse the result (`ast.parse` for
Python, a compile for C#) and check the line count moved by roughly what you intended — a file
that grew 40x is the signature of exactly this bug.

### Technique: make the harness EXTRACT the shipped block, and run the shipped TEST

The §4 harness is only worth what its fidelity to the real file is worth, and a hand-retyped
copy drifts the moment you fix a typo in one and not the other. Slice it out of the shipped
file instead, so the thing you prove is byte-identical to the thing you commit:

```python
src   = pathlib.Path(REAL_FILE).read_text()
block = src[src.index(START_MARKER) : src.index(END_MARKER)]      # assert both markers found
pathlib.Path("Program.cs").write_text(STUBS + block + DRIVER)
```

Two things this buys beyond a compile:

- **Unity-flavoured null checks work with a three-line stub.** Nearly every gameplay block that
  touches `UnityEngine.Object` needs only its implicit-bool operator to run headlessly:
  `public static implicit operator bool(Object o) => o is { Destroyed: false };`. That covers
  `if (!component)`, `x ? a : b`, and the destroyed-object prune idiom in one go.
- **You can execute the shipped TEST FILE's own assertions**, not a paraphrase of them: strip
  `#if UNITY_EDITOR` / the NUnit `using`, swap `[Test]` for a plain method and `Assert` for a
  four-line shim, and drive it by reflection. What you then prove is literally what the edit-mode
  suite will assert when a human opens Unity, which is a much stronger claim than "it compiles".
  This is the closest you can get to running the repo's tests without the editor — do it whenever
  a branch adds a test whose subject is a pure function.

Use it for the pure/static core of a change (a predicate, a mask, a formula). It still cannot see
name resolution or whole-class consistency — see the two traps below.

**For an `#if UNITY_EDITOR` TEST file, skip the extraction — compile the WHOLE file, unmodified,
and drive it by reflection.** A test file's Unity surface is usually small and entirely stubbable
(`Vector3`, `Mathf`, `Mesh`'s vertex/UV accessors, `Object.DestroyImmediate`), and NUnit is ~40
lines of stub (`[Test]`, `Assert.IsTrue/IsNotNull/AreEqual/That`, `Is.GreaterThan/LessThan` as a
tiny constraint interface). Compile it together with the REAL subject files it tests, `-main:` a
driver that reflects over `[Test]` methods, and you have executed the shipped assertions against
the shipped code. This is what makes the body-level blindness above survivable: the 2026-08-24
table says the no-stubs pass proves nothing inside a method, and a whole-file test harness proves
everything inside every method the suite covers. Two mechanics that cost a cycle each:

- **`rm` the output assembly before every rebuild.** A failed build leaves the previous `.dll` in
  place, the driver runs it, and a *broken* file reports the previous run's "7 passed" — the exact
  false green a gate exists to prevent.
- **Run from the PROJECT ROOT**, not the harness directory: Unity runs edit-mode tests with cwd =
  project root, so every `File.Exists("Assets/...")` in the suite is project-relative and fails
  everywhere else. Four passing tests read as four failures until you notice.

And prove the harness the way the table above was produced — inject each defect class you care
about, confirm it fires, restore, `cmp`. A run that only ever passes is indistinguishable from a
run that cannot fail.
### Trap: a SHIELD's size is not the prism's size, and the two tiers scale DIFFERENTLY

Origin: the Scarab wing dais (2026-08-18). A super-shielded "sun core" was sized so its
*bounding box* matched the space it had to fill; it rendered 73% too big and the human
caught it by eye. The measurement was not sloppy — it was the wrong measurement, and the
class of error generalises to anything you place next to shielded mass.

`PrismStateManager` swaps a prism's MESH when a shield engages, and both meshes are built from
the box HALF-extents times `CIRCUMSCRIBING_SCALE` (3). So for a prism of full size `S` on an
axis, the semi-axis is `1.5 S` — **three times the box's own extent** — and neither tier is
"the prism, slightly bigger". Read the generators, not the field names:

| tier | mesh | vertices at | extent along an axis | **circumscribed diameter** |
|---|---|---|---|---|
| plain | box | `±S/2` | `S` | `S·√3` |
| shielded | octahedron | `(±1.5S, 0, 0)` &c — **ON THE AXES** | `3S` | `3S` |
| super-shielded | stella octangula | spikes at `(±1.5S, ±1.5S, ±1.5S)` — **the CUBE CORNERS** | `3S` | `3S·√3 ≈ 5.196S` |

The two shield tiers have the **same axis extent and different apparent size**, because the
stellation's spikes point at the corners. Size a stella by its bounding box and you understate
what the player sees by `√3`. That is the whole bug, and it is invisible to every check that
measures axis extents — including a top-down render, where the spikes project to `1.5S·√2`.

Rules that fall out:

- **Decide which measure the design cares about, and derive the authored scale from it.**
  "Fits this slot" is a bounding-box question (`S = slot / 3`). "Reads this big" is a
  circumscribed-sphere question (`S = size / 3` for shielded, `S = size / (3√3)` for
  super-shielded). Name the measure in the field's tooltip so the next person cannot pick
  the other one.
- **Derive the factor from the generator's own constant**, never a literal:
  `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`, and `× √3` for the stellation. A hard-coded
  3 or 5.196 silently rots if the constant is ever retuned.
- **A tier change is a SIZE change unless you fit for it.** Cycling plain → shielded along a
  row of same-sized prisms triples every third one. Fitting the prism (authored scale
  `× 1/CIRCUMSCRIBING_SCALE`) restores the envelope exactly and is uniform, so the prism's
  aspect — its identity — survives; `Docs/ECOSYSTEM.md §35` is the ruling.
- **Check clearance against the SHIELD's silhouette, not the box's — and the silhouette is a
  function of the POSE.** In-plane, a shielded prism is a rhombus with semi-axes `1.5·(w, L)`.
  A super-shielded one reaches `1.5S·√2` toward its in-plane corners *only while it is
  axis-aligned*: rotate it so a spike lies in your plane (aiming `(1,1,1)` at something) and the
  reach becomes the full `1.5S·√3`, 22.5% more, silently. So compute a stella's outline as the
  projected convex hull of its eight spike tips under its own rotation — the stellation's hull IS
  that cube — rather than a hard-coded octagon of alternating radii, which is the outline of one
  particular pose. An exact 2D separating-axis test over those silhouettes is ~40 lines and is the
  only way to claim "no overlaps or clipping" honestly.
- **The taper is a design tool, not just a cost.** An octahedron's side faces slope at
  `atan(halfWidth/halfLength)` from its axis while a box's are parallel, so a run of
  flush-tiled boxes cannot turn and a run with an octahedron in it turns by exactly
  `2·atan(w/L)` at that prism — pivoting about its root TIP, which all three prisms share.
  Curvature in a tiled prism structure is therefore *placed* (where the shielded prisms go),
  not tuned. Get the pivot wrong — rotate about the chain point instead of the shared root
  tip — and every hinge overlaps, at a rate that looks like a global spacing problem.

### Technique: a synthetic CALL-SITE PROOF for files too dependency-heavy to compile

A change to a shared helper's signature has to be checked at its callers, and the callers are
often the very files whose dependency chain makes them unstubbable (a toy that pulls in the whole
toy framework, UniTask, Netcode). Stubbing all of that to type-check three lines is the wrong
trade. Instead write a small synthetic file into the harness that REPRODUCES the caller's argument
shapes against the real signatures:

```csharp
internal static class _CallSiteProof
{
    static readonly List<Transform> HullBodies = new();   // the real field's type
    internal static void RealCallSite(ToyContext ctx, VesselClassType v, float r)
    {
        if (Roster.TryBuildLiveHull(ctx, v, r, out var model)) HullBodies.Add(model.transform);
        foreach (var body in HullBodies) Roster.ApplyDomain(ctx, body, Color.white);
    }
}
```

It proves the thing actually at risk — that the overloads still bind for those argument types —
without pretending to compile the caller. Do the same for every PRE-EXISTING overload shape you
preserved when adding a new one: three one-line calls prove you did not silently break the two
other files that use the old forms. Pair it with a CS1xxx-only syntax check of the real callers
(compile them alone, filter for `error CS1[0-9]{3}`; the flood of CS0246 is just missing Unity).

### Trap: an HLSL→C++ transform written per-SIGNATURE stops translating when the file grows

The clang harness (§4.5c) works by applying a short list of mechanical substitutions to the
shipped shader. It is tempting to write them as exact strings — `src.replace("out float3 Color",
"float3 &Color")`. That is a landmine: the day the file gains a SECOND `out` parameter, the
substitution silently does not apply to it, and the harness fails to compile for a reason that
looks like a shim gap. Write the substitutions as patterns over the LANGUAGE feature, not over the
current text (`re.sub(r"\bout (float3|float2|float) ", r"\1 &", src)`), and keep the guard-rail
loop that asserts every function name you expect is still present in the file.

Two more shim gaps from the same family, both of which read as "my code is broken": HLSL's `max`
and `min` promote across float and double literals, so `max(x, 1.0)` is legal HLSL and will not
bind to `std::max`; and swizzles you have not shimmed (`.zyx`) are member-access errors, not
maths errors. Add float/double overloads and accessor methods rather than editing the shipped file
to suit the harness.

### Trap: a source-census needle counts itself, and matches its own PREFIXES

A gate that enforces "this call has exactly one call site" is written by grepping the codebase for
`"Namespace.Method"` — and the file that DECLARES that string as a constant is itself a hit, so
the gate reports two sites and fails on a correct tree forever. Adding the opening parenthesis
(`"Namespace.Method("`) fixes it, and does a second job that is easy to lose: it keeps the census
off any longer symbol that starts with the same name. One session added a deliberate sibling
(`Stamp` for vessels, `StampDisplayModel` for display-only props) and the parenthesis was the only
thing separating them — so the sibling got its own test asserting it is NOT counted, which is what
fails if someone later renames the primary to a prefix of something else.

### Trap: a stub-harness error is a STUB GAP until proven otherwise — but not always

Running the shipped file against transcribed stubs means every compile error has two possible
causes, and the likely one is the harness. In one session the harness raised nine errors:
`Mathf.Rad2Deg`, `Mathf.Acos`, `Vector2.zero`, `Vector2`/`Vector3` unary minus, `Vector3.Scale`,
`Quaternion.x/y/z/w`, `Prism.Damage`, `AstroLeagueBall.Velocity` — **eight were missing stub
members** that real Unity has, and patching the shipped code for any of them would have been a
regression invented by the tool.

So the discipline is: **grep the real repo for the member before touching your file.** Two
`grep -n` calls settle it — the type's own source, or any existing call site.

The ninth was real (a missing `using CosmicShore.Utility;` in a new editor window, which would
have broken the whole editor assembly), and that is the point: the eight false alarms are the
price of the one catch, and the catch is a build break nobody would have seen until Unity opened.
Do not stop running the harness because it cries wolf; just always ask *whose* fault it is first.
Keep the stub file, too — it accumulates, and the next session starts with nine fewer gaps.

### Trap: a test that pins an ABSOLUTE number fails on the first legitimate retune

When the subject is authored geometry — a generated structure with a dozen coupled dials — a
human WILL retune it, and every assertion phrased as a fixed multiple then fails for the wrong
reason. Three of one session's tests failed the moment the designer's own parameters were adopted,
and all three were the test being wrong, not the shape:

| pinned | broke because | rewritten as |
|---|---|---|
| "the inner edge is within 1.15× the ring" | the reach is a DIAL, and a second dial swings it further | "outside the mouth, and within a quarter of the structure's OWN radius" |
| "every hinge opens 1.2× the plain step" | the mechanic strengthens along the run (1.17× → 1.62×) | "every hinge beats its neighbours, AND the last beats the first" |
| "some prism goes under the pool's scale floor" | one dial moved and nothing did any more | ceiling on the shipped shape, floor on an explicit thin variant |

The rule: **assert relationships that scale with the structure — monotonicity, ordering, ratios
against the thing's own dimensions — and reserve absolute numbers for genuine physical limits**
(a pool clamp, a collider budget, an arena radius). When an absolute number really is the point,
assert it against a settings variant you construct in the test, so the shipped tuning stays free.

### Trap: compiling a COPY cannot see whole-class consistency

The harness pattern in §4 — paste the block under test into a stub file and compile it — proves
the block's *contents* against real types, which is its whole value. It cannot see anything about
the block's RELATIONSHIP to the rest of the real class: a member you added that duplicates one
already there (`CS0102`), a name that collides with a base-class member, an override whose base
signature changed. Those are only found by compiling the real file, or by Unity.

So: after any patch that ADDS a member to a large existing class, grep that class for the
member's own name and confirm exactly one declaration. This session shipped a duplicate field
that the harness compiled clean and Unity rejected.

### Trap: a stub-reference compile is BLIND to `System`/`UnityEngine` name collisions

The §4 harness compiles against .NET reference assemblies with no `UnityEngine.dll`, so every
Unity type is already unresolved and gets filtered out as expected noise. That filtering hides a
whole error class: **`CS0104` ambiguity cannot occur when one of the two colliding types does not
exist.** Add `using System;` to a file that already has `using UnityEngine;`, and a bare `Object`
compiles clean in the harness while Unity rejects it — `Object` resolved only to `System.Object`
because `UnityEngine.Object` was never loaded. A session shipped exactly this by adding
`using System;` for a `Func`/`Action` field and leaving two `Object.Instantiate` / `Object.Destroy`
calls alone; the human's first Editor compile was what found it.

The offline pass proves SYNTAX and STRUCTURE. It cannot prove NAME RESOLUTION. Do not report it as
"compiles clean" without that qualifier.

The collision set is small enough to check exhaustively, so grep instead of hoping — in any file
carrying BOTH usings, the ambiguous names are exactly **`Object`** and **`Random`**:

```sh
for f in <changed .cs files>; do
  grep -qE '^using System;' "$f" && grep -qE '^using UnityEngine;' "$f" || continue
  grep -nE '(^|[^.[:alnum:]_])(Object|Random)\s*[.(<]' "$f" | grep -v 'UnityEngine\.'
done
```

Fix by fully qualifying (`UnityEngine.Object.Instantiate`), never by dropping `using System;` —
the file needs it. Same shape applies to `using System.Diagnostics;` + `Debug`.

## 4.5 Technique: offline simulation of a deterministic generator

Origin: the Caldera/Ourobor cell rework (2026-08). §6 below used to list
"play-mode measurements (baselines)" as editor-only, with the workflow being
"human runs the measurer, pastes the output, you author from it." For a
**deterministic** generator you can skip the human entirely — port it and
measure offline.

When it applies: the generator's output is a pure function of serialized inputs
(a seed + authored constants), which is exactly the contract
`CellEnvironmentSpawnableBase` already enforces so clients agree without seed
sync. Anything driven by play-mode state (physics, timing, live gameplay) is
NOT this and still needs the editor.

**The method — and the step that makes it trustworthy:**

1. Port the generator to Python (or any host), mirroring emit ORDER exactly.
2. **Validate the port against a KNOWN-GOOD baseline before trusting it for a
   new one.** The shipped Caldera's authored `PhaseThresholds` encoded its
   baseline (thresholds = baseline + fixed deltas), so the port had a target to
   hit: it reproduced 31,194 prisms / 430,691 volume **to the unit**. Only then
   was it used to author new numbers. Without this step you have a plausible
   number, not a measured one — which is worse than no number.
3. Measure whatever the authored asset needs (count, volume, per-kind and
   per-domain splits, min/max radius, per-family breakdown for tuning).
4. Iterate design changes against the sim, then hand-sync to the C#.
5. **Cross-check the sync mechanically.** After hand-syncing, extract every
   numeric literal of a given class from BOTH files and compare as multisets
   (e.g. every `new Vector3(x,y,z)` / `Plate(x,y,z)` scale tuple). A silent
   divergence between generator and sim means wrong authored numbers with no
   symptom until someone re-measures in-engine.
6. **Render the point cloud** (matplotlib, three orthographic projections,
   coloured by domain). This catches geometry errors no assertion will — a pass
   that measured fine read as squat blobs rather than mountains until it was
   plotted, and the fix was an aspect-ratio change, not a count change.

Also: assert the spatial invariants the design claims. "Nothing inside the
nucleus control radius" is one line over the emitted points, and it caught that
the SHIPPED build had 89% of its mass in there.

**Trap: a validated SIMULATION does not validate the SHIPPED CONSTANTS — verify
the file, and never derive one row from another.** A measured table can be proven
end-to-end offline and still ship wrong, because between the measurement and the
asset there is a TRANSCRIPTION, and that step is invisible to both the sim and
code review. The gyroid octagon tables (2026-08-16) were validated as rotation
*matrices* — 273-plant colony, zero overlaps, bijective on the reference lattice —
then hand-carried into C# as *quaternions* with only the first of four block types
pasted from the emit; the other three were filled in by a plausible mirror-symmetry
ansatz. The conjugation does not act on `LookRotation` frames that way, so 12 of 16
seed rotations were wrong by up to 179°, and it cost five playtests, because each
plant was internally perfect and only the JOINS were wrong.

Two rules fall out, and they generalise to any generated-constant pipeline:

1. **Write a verifier that parses the SHIPPED artifact** (not the intermediate
   JSON, not the sim's own state) and re-proves it against a fresh reference walk —
   including the representation you converted into. Yes, this means re-implementing
   the engine's quaternion convention; that is the point, since the conversion is
   exactly what was never checked. `Tools/Build/verify_gyroid_octagon_tables.py` is
   the worked example, and it turned a five-playtest hunt into one command.
2. **Delete the transcription step.** Make the measuring tool emit the finished
   target-language block, ready to paste verbatim, and assert per-entry
   self-consistency as it prints (each emitted pose must reproduce the quantity it
   is supposed to encode). Emit exact measured samples, never an *average* of
   rotations — an averaged-then-orthonormalised matrix is where a det −1 reflection
   hides.

Symmetry shortcuts are the specific temptation: they look like insight, they halve
the work, and a chiral structure punishes them silently.

## 4.5b Technique: offline simulation of a FRAGMENT SHADER

Origin: the prism occlusion corridor (2026-08-04). §4.5 covers deterministic
*generators*; the same move works on a fragment shader, and it answers questions
the editor is slow at: what shape is this field, does this gradient read as an
edge, which of twelve dither kernels looks right.

**Port the SHIPPED HLSL, not an idealized version of it.** Every constant, every
early-out, every clamp — including the ugly ones (the strictly-inside-(0,1) nudge,
the epsilon in a divide). A sim of what you *meant* to write validates nothing. When
the port and the HLSL disagree later, that divergence is the finding.

The method:
1. Port the fade/threshold function to numpy, vectorized over a pixel grid.
2. Add a trivial ray-cast rasterizer (AABB slabs + a depth buffer, ~40 lines) so you
   can render the effect *in situ* — on a wall of boxes, at real hull scale — not
   just as an abstract ramp. In-situ and abstract disagree: a kernel measured at a
   fixed radius made corridor-relative patterns look perfect and screen-space ones
   look bad, exactly backwards from how they render.
3. Rasterize only inside each box's projected screen bbox. A full-image raycast per
   box is what turns a 20-second render into a 10-minute one that times out.
4. Render candidate sheets and **look at them** before implementing anything.
5. Re-render from the shipped code after implementing, and confirm it matches.

**Measure coverage fidelity for any dithered/screen-door effect.** The number that
decides whether a short gradient reads as a fade or as a hard edge is
|kept-fraction − alpha|, binned by alpha over a real render. Under ~0.01 reads
smooth; 0.1+ reads as banding. Measure it, don't eyeball it — of twelve kernels
tried, the three that survived were not the three that looked best on a ramp.

**A bad metric is usually fixable by remapping it through its own CDF.** Raw Worley
cell-distance clusters around 0.43 with nothing at the extremes, so it scored 0.140 —
unusable. Fitting a `smoothstep(a,b,·)` to its measured CDF took it to 0.0048, a 19×
improvement for ONE instruction, and because the remap is monotonic the pattern's
shape is completely unchanged: only the rate at which it fills in as alpha sweeps.
Two consequences worth internalising: any monotone threshold field can be made
coverage-correct this way, and **the fit is tied to the field's parameters** — change
the cell size, the jitter, or add animation, and the constants must be re-fitted or
the error silently returns. Verify the fit across the whole range you intend to use
(here: rate 0 through t=400s).

## 4.5b-geo Technique: prove GENERATED GEOMETRY against a closed form

Origin: the Dolphin's `BlastProfileGraphic` and `EchoSightHalo` (2026-08-17). §4.5b samples a
fragment function to judge a LOOK. This is its vertex-side sibling: when you generate a mesh, a
UI outline, or a screen-space size in code you cannot run, transcribe the *same arithmetic* into
Python and assert the properties the shape must have. It catches a class §4.5b cannot, because a
wrong outline does not look noisy — **it renders a plausible WRONG SHAPE**, and reviewing the code
that produced it tends to re-confirm the author's own mental model.

The assertions that actually earn their keep, in the order they catch things:

| Property | How to assert it | What it catches |
|---|---|---|
| **Simple, non-self-intersecting loop** | sign of `cross(p[i], p[i+1], p[i+2])` constant around the ring | an outline walked in the wrong ORDER — the killer, because a centre-fan over a mis-ordered loop draws a bowtie with hollow wedges and no error |
| **Area vs the exact formula** | shoelace vs the closed form (a stadium is `πR² + 4LR`) | a whole dimension dropped or doubled; polygonal under-approximation shows as a clean few-% deficit that shrinks with segment count |
| **Max step between consecutive vertices** | should equal a KNOWN edge of the shape | a jump across the interior — the direct signature of the ordering bug above |
| **Aspect / units** | for screen-space math, assert x and y offsets subtend EQUAL PIXELS | an ellipse where a circle was intended (NDC x and y both span −1..1 over unequal pixel counts) |
| **The regime table** | tabulate the output across the input range | a `max()`/`min()` crossover in the wrong place, or a floor that never engages |

A worked instance: the stadium outline swept both end caps from the ACROSS basis vector instead
of ALONG, which left cap one ending at the far tip and cap two starting near the middle. Convexity
and max-step both failed instantly; area was 2% under the closed form at 10 segments per cap after
the fix, which is exactly the expected polygonal deficit. The same script then tabulated the halo's
angular size against depth and confirmed the constant-size floor engaged where intended.

Cheap to write (~30 lines), and the table it prints is evidence you can paste straight into the
doc and the PR.

## 4.5c Technique: COMPILE the shipped HLSL with clang (stronger than porting it)

Origin: the occlusion corridor's triangle/shatter kernels (2026-08-06). §4.5b ports a
shader to numpy to judge its LOOK. This compiles the **actual file from the repo** and
runs it, which answers a different and harder question: *does the source I am about to
commit compile, and does it do what my measurements say?*

A numpy port validates the design. Only compiling the real file validates the FILE — a
port cannot catch a typo, an unbalanced brace, a wrong swizzle, or a `#if` that excludes
the wrong block.

```python
# Read the shader from the repo, apply a SHORT, LISTED set of mechanical substitutions,
# #include it from a C++ shim, compile with -Wall, run it, diff against the numpy port.
SUBS = [(r"\[unroll\]", ""),          # HLSL loop attribute
        (r"\bout float\b", "float&"), # HLSL out-param -> C++ reference
        (r"\bfloat2\(", "mk2(")]      # vector constructor spelling
```

- **Rewrite `out` params to C++ references in the EXTRACT, never with a `#define out`.**
  An empty `#define out` compiles clean and silently makes every out-param pass by VALUE,
  so the harness runs, prints, and reports `0.000` for every result — which reads as a
  logic bug in the shader and sends you debugging correct code. `out float3 X` →
  `float3 &X` as a substitution on the extracted text (the SUBS list above already has the
  scalar form; the vector forms need the same). A harness whose output is uniformly the
  zero value is a harness bug until proven otherwise.
- **`__attribute__((ext_vector_type(N)))` is the whole trick.** clang's vector types give
  you elementwise arithmetic and *arbitrary swizzles* (`.xyx`, `.yzx`, `.zy`) for free, so
  hash functions written for HLSL compile unmodified. Only the `floatN(a,b)` constructor
  spelling needs substituting.
- **Keep the substitution list short, listed, and auditable.** Every constant and every
  expression must pass through untouched — those are what you are verifying. If the list
  starts growing, you are rewriting the shader, not testing it.
- **Build with `-Wall` and treat every warning as a finding, because C++'s scalar
  overloads are not HLSL's.** `abs()` on a scalar float resolves to C's INTEGER `abs`
  unless you supply a float overload, so the harness silently truncates `abs(0.004)` to
  `0` and you go tuning a shader against a render that never ran your math. Same class:
  `std::min`/`std::max` are type-strict and reject `max(someFloat, 1e-8)` outright (that
  one at least fails loudly). Define HLSL-shaped `abs`/`min`/`max` in the shim, and
  `#undef`/restore them after the `#include` so the harness's own C++ still compiles.
- **Rasterize the REAL geometry, not a test surface.** §4.5b's advice to render "in situ"
  goes further for a shader bound to one authored model: parse the source mesh (trap
  above), run the actual bake the runtime will run, and feed the shipped entry point per
  fragment through a ~60-line triangle rasterizer (project, screen bbox, 2D barycentrics,
  1/w perspective correction, depth buffer). Then a claim like "the arcs only run along
  crease edges" is something you LOOK at, and a claim like "it is not blown out" is a
  census (mean linear output, % of covered pixels over 1.0/2.0/4.0) rather than an
  opinion. Re-render after any later edit and `cmp` the output: byte-identical proves the
  edit was surface-neutral, which is exactly what you want to assert about a refactor.
- **Stub the URP built-ins** (`_WorldSpaceCameraPos`, `_ScreenParams`, `_Time`,
  `UNITY_MATRIX_V`, `TransformWorldToHClip`) as file-scope globals in the shim. Then the
  entry point compiles too, not just the leaf functions.
- **Compile EVERY `#if` branch.** A gate like `#define X_LIVE_TUNING 1|0` has two shapes;
  build both and diff their output. That is how you prove a "design mode" is genuinely
  free when it is off, instead of asserting it.
- **Then assert the dials actually DRIVE.** Set the globals from the shim and check the
  output changes — over a POPULATION of pixels, not one. A single sample matching proves
  nothing (a flip that affects half the cells legitimately leaves any given pixel alone).

This also verifies a source-rewriting tool end to end: bake values with the tool's own
regexes, compile the result, and confirm the round-trip back to the original values is
byte-identical.

**Use the harness to MEASURE a bound the CPU then has to encode, instead of guessing it.**
Compiling for correctness is the obvious use; the higher-value one is deriving a number that
must live on the other side of the CPU/GPU boundary. A vertex-displacing effect needs a
`RenderBounds` envelope, and the padding is whatever the shader can actually displace — so
sweep the shipped entry point over the real geometry and every t in the animation window, and
report peak displacement as a RATIO of the quantity the CPU already has (`radius × amplitude`
measured at 0.991, so 1.25 ships with headroom). The constant then arrives with its
derivation attached, and re-running the harness after any shader edit re-checks it. Assert the
ratio in the harness, so a later change to the motion that widens the envelope fails there
rather than as prisms popping at the screen edge.

## 4.5d Technique: offline simulation of a CONTROL LOOP (steering, pursuit, any feedback law)

§4.5 simulates a *generator* — deterministic, one pass, compare the output. A **control
loop** is the harder cousin: the thing you are changing feeds back into its own input, so a
change that looks locally sensible can be globally worse and neither review nor a unit test
will say so. Simulate it the same way, and let the simulation pick the tuning.

The shape:

1. **Extract the loop's pure math into a static class with no `UnityEngine` in it**
   (`PursuitReachability`: turning radius, the reachability test, the escape direction, the
   orbit detector). That class is what the shipped `MonoBehaviour` calls AND what the harness
   calls, so the tested path is the shipped path. This is the same discipline as §4.7's
   "extract the shipped block" — here it also buys the design a seam.
2. **Write a plant** — a few dozen lines integrating heading and position under a max turn
   rate. It does not need to be the engine's integrator; it needs the same *constraint*
   (bounded turn rate, constant-ish speed), because that constraint is what the law is
   fighting.
3. **Score over a randomized ensemble, not a scenario.** Hundreds of (start pose, objective)
   draws, fixed seed. Report reached/total and mean time-to-objective. One hand-picked
   scenario proves nothing about a feedback law.
4. **Measure candidate fixes against each other before shipping one.** Two plausible
   remedies for a late swerve were each simulated and REJECTED on the numbers (a commit-range
   gate traded swerves for orbits, 400→377 reached; a look-ahead factor multiplied break-offs
   28→247 at double the mean time) — which is what forced the search to continue until the
   real cause turned up. Rejecting on data is cheaper than shipping and re-playtesting.
5. **Sweep every dial you are about to author and report the curve, not the pick.** The
   away-bias in this session turned out to be a *dead dial* — 0…1.5 all reached 400/400
   within 0.06 s — so it shipped at its midpoint with that fact recorded, instead of being
   defended as a tuned value.

Two things the harness catches that reading cannot: a **ratcheting accumulator** (a
running-minimum "best distance so far" silently degrades a progress gate to "constant range
only"; visible instantly as a detector that never fires on an approach), and a **wrong
comparison of derived quantities** — see the squared-vs-linear trap in §5.

Limits, state them: the plant is not the engine, so the simulation bounds *behaviour of the
law*, never feel. Frame timing, replication, and the vessel's real thrust/grip model are out
of scope, and the human still playtests.

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

## 5. Traps learned the hard way (check these BEFORE debugging for an hour)

- **Play-mode edits: SCENE changes are discarded on Stop, SO ASSET changes are kept — and that
  asymmetry is what makes it baffling.** A human tuning your feature will edit both kinds in the
  same sitting: the `MenuCameraConfigSO` values they change while playing STICK (it is an asset),
  the checkbox they uncheck on the scene component silently REVERTS the moment they press Stop.
  `Ctrl-S` during Play does not rescue the scene half — it saves assets, not the scene. So the
  report you get is "half my changes keep undoing themselves", which reads like a save bug or a
  git problem and sends you looking in entirely the wrong place. **Ask which kind of object the
  field lives on, and whether they were in Play mode**, before investigating anything. Two fixes,
  and give both: edit scene fields with the editor STOPPED, or — better when the value is part of
  the change under review — set it in the scene YAML yourself and commit it, so it survives and is
  reviewable. Suggest `Preferences → Colors → Playmode tint` as the standing guard.
- **A worst case sampled over a CONVENIENT subset is not a worst case, and will send you to the
  wrong fix.** Asked how far a camera's aim could tilt, the first pass varied the target only
  along the axis that looked dominant (straight up/down) and reported 0.855 — comfortably near
  the threshold, which made "move the camera further out" look like the fix. Searching the whole
  spawn volume adversarially gave **0.9859**, i.e. the radius barely mattered and the real lever
  was a constant elsewhere in the code. The restricted model did not just understate the number,
  it inverted the conclusion. **When the output is a bound rather than a typical value, enumerate
  the full parameter space (grid + refinement) and say which parameters you searched**; if you
  quote a bound from a subset, label it as such. Re-deriving it honestly is minutes of compute
  and is the difference between a fix and a detour.

- **A binary FBX node can open an EMPTY scope, and dropping that 13-byte NULL record silently
  destroys data — with a byte-for-byte identical node tree.** A childless record that is
  nevertheless followed by a nested-list terminator is not the same thing as a leaf: the
  terminator is how a reader tells "this node opens a (empty) scope" from "this node has no
  scope". Blender writes it on a handful of nodes per file (7 in one 58 KB model, among them
  `AnimationLayer` and several `Properties70`). A naive reader parses those as childless, a naive
  writer then omits the record, and the file loses its ANIMATION — while a full-value comparison
  of every node and every property reports the two trees as *identical*, because the bit that
  differs is not in any node or property. Round-trip the flag (`empty_scope`: set it when a node
  has no children but `pos < end_offset`; emit the terminator when it is set). **The general
  lesson is about verification, not FBX**: a codec validated only against its own reader is
  validated against its own blind spots — the defect was invisible to the obvious check and
  instantly visible to an independent one (`assimp info` reporting `Animations: 0`). Get a second
  reader before you trust a writer.
- **Any "smoothing" mesh operation SHRINKS the model, and the size may be load-bearing.**
  Catmull-Clark converges to a limit surface strictly INSIDE its control mesh — measured 9.8%
  radially and 3.9% lengthwise on a missile after two levels. If anything downstream is written
  against the model's bounds (a growth factor derived from its launch length, a doc table, a test
  constant), a smoother model is wanted and a smaller one is a regression nobody will attribute to
  the subdivision. Renormalize the result affinely back onto the ORIGINAL bounding box, share one
  factor across the axes that must stay circular, and make the tool's `--check` fail if the box
  ever drifts. Subdivision also FILLETS sharp features — a hard shoulder becomes a curve, a near-
  point tip becomes a cap — so measure the radius profile end to end and state which features
  moved, rather than only reporting the poly count.
- **"Looks low poly" is a hypothesis about geometry that is usually a hypothesis about SHADING —
  measure which before fixing either.** The reflexive fix is to smooth normals; on the model that
  prompted it, the normals were already fully smooth (**zero** control points carried more than
  one normal) and the real defect was an eight-sided barrel, 7.61% off the circle it stood in for.
  The measurement is cheap: build `control point → set of distinct normals` from
  `PolygonVertexIndex` + `NormalsIndex`; all-ones means smooth-shaded, one-normal-per-FACE means
  faceted. Then count verts per ring along the long axis for the radial resolution. Two numbers,
  and they point at opposite fixes (an import-setting change vs. a mesh change).

- **`HideFlags.HideAndDontSave` includes `DontUnloadUnusedAsset`, so a runtime-created Mesh or
  Material with it LEAKS.** It is the reflexive flag for a procedurally-built helper object, and
  on a GameObject it is fine (a child dies with its parent regardless). On an *asset-like* object —
  `new Mesh`, `new Material` — it means the thing is never garbage collected AND never swept by
  `Resources.UnloadUnusedAssets`, so one accumulates per owner instance, forever, across vessel
  swaps and scene loads. The Echo Sight halo minted one quad Mesh per executor this way. Fix by
  making it a **static shared** instance (correct anyway when the geometry is identical for every
  user — size belongs in a shader property, not a transform scale), or by destroying it explicitly
  in the owner's teardown. The flag is right for the shared one and wrong for the per-instance one.
- **Anything that RESOLVES A CELL during the spawn chain must retry, not bind once.** CLAUDE.md
  documents this for the nucleus radius; the same 800 ms window bites anything else that looks a
  cell up at init. `Cell.Initialize` runs on `OnInitializeGame` behind `InitDelayMs` (1000 ms) while
  vessels spawn at `preSpawnDelayMs` (200 ms), so `Cell.FindCellContaining` /
  `FindNearestActiveCell` return **null** in a vessel component's `Initialize`. Binding a SOAP
  channel there fails silently and stays failed for the whole match — a HUD tally reading zero
  forever with nothing in the log. Resolve at USE time (the crystal seeding executor resolves per
  seeding) or late-bind on the first event that needs it, and keep the unsubscribe pointed at the
  channel you actually attached to so a mid-flight cell swap cannot strand it.

- **A ratio between two authored numbers is not a measurement until you have controlled for
  what else differs between them.** Chasing "why does this element render smaller?", the
  authored history looked like hard evidence: the gyroid flora set its *Mass* crystal to 4.0
  while every other flora set *Space* to 3.0 — a 1.33 ratio that almost exactly cancelled the
  Space prefab's 1.34 model-child multiplier. Two independent signals agreeing. Both were
  wrong: those are different plants at very different overall sizes, so the ratio is as easily
  a composition choice. The actual measurement (below) showed all four elements were already
  matched. **When authored numbers seem to encode a correction, find the thing they correct and
  measure it directly** — and if you ship on the inference anyway because a human reported a
  symptom, label the number as an eye-calibration, not as a result.
- **Raw mesh extents from two FBX files are not comparable — normalize by `UnitScaleFactor`
  first.** §4.8 gives the parse; the trap is forgetting the normalization when the question is
  "which of these models is bigger?". Four crystal models measured 2.03 / 1.96 / **156.46** /
  1.38 in raw file units, which reads as one model being 80× the others; the outlier's FBX just
  declares `UnitScaleFactor: 1` where the rest declare 100. Normalized (`raw × UnitScaleFactor
  / 100`, cross-checked against the `.meta`'s `useFileScale`/`globalScale`) they are 2.03 /
  1.96 / 1.56 / 1.38 — and after each prefab's own model-child multiplier, all four agree
  within 7%. The un-normalized read would have "proved" a defect that does not exist.
- **Before normalizing a transform value across a family of prefabs, check (a) what each one
  carries BELOW its root, and (b) whether that value is read as GAMEPLAY.** A family that looks
  uniform at the root can be maintaining its uniformity through per-item corrections on a child
  — flatten the root and you break a match that was already there. And when the root's scale is
  read by game logic (a pickup's reward, a buff magnitude computed from `lossyScale`), a purely
  visual fix applied there silently retunes balance. The correction belongs on the child; the
  root stays the number the game reads.

- **A Unity NullReferenceException names an exact LINE — mine it before theorising, and
  calibrate the trace's fidelity from the log itself.** Two steps, both cheap. (1) Confirm
  the reported line maps to the file on disk (`sed -n '113p' <file>`, and check the running
  revision matches — another frame in the same log usually pins it), then enumerate **every
  dereference on that line**. `effect.Execute(this, prismImpactee)` derefs exactly one thing:
  `this` is never null and `prismImpactee` was non-null by pattern match, so `effect` — an
  empty slot in a serialized array — was the only candidate, and no amount of guessing about
  the callee was needed. (2) Before trusting the deepest frame, check whether Mono is
  **inlining frames away**: find a known one-line delegating method in the *same log*
  (`ShardToggleActionSO.StartAction`, an expression-bodied `=> exec?.Toggle(...)`). If its
  frame is present, small calls are not being elided, so the deepest frame really is where
  the throw happened and the callee is exonerated. Skip this and you will go read six effect
  implementations looking for a null that was in the array all along.
- **Four different things make a serialized `UnityEngine.Object` array element null at
  runtime while the YAML looks healthy.** When chasing one, check all four, in this order —
  each is one grep: (1) the slot is literally `{fileID: 0}`; (2) the referenced **GUID
  resolves to no `.meta`** (deleted asset); (3) the asset exists but its own `m_Script` guid
  resolves to no `.cs` (missing script — Unity hands you null); (4) the asset's class does
  **not derive from the array's element type**, which Unity silently nulls on load. All four
  are verifiable from the repo in about a minute, and ruling them out is itself a finding:
  if the branch's data is clean, the hole is in the reporter's *working tree*, which is a
  different conversation than a code bug.
- **A feature can be dead in several places at once, and fixing the first one makes the
  SYMPTOM stop while the feature stays dead.** The Dolphin's shard toggle had an unwired SO
  reference (the error you could see), a bus whose two broadcast bodies were commented out,
  and a listener that neither registered with the bus nor still declared the methods those
  broadcasts call — three independent breaks. Wiring the reference would have silenced the
  console and shipped an ability that had never once moved a shard. **Before declaring a
  wiring fix complete, walk the chain to the CONSUMER** and confirm something at the far end
  actually acts on it. The `/ship` "find the PRODUCER" rule, run in the other direction.
- **A re-implemented validator can diverge from the shipped code and "pass" a model the
  engine rejects.** A Python port of a C# generator's profile functions silently applied a
  `max(0, …)` the C# did not, so the offline check reported clean geometry while Unity was
  refusing `localPosition` assignments (`{0, NaN, 2.616}`) and reporting `abnormal mesh
  bounds … -nan(ind)` on three meshes. §4.5's simulation technique is only sound when the
  simulation IS the shipped source: compile the actual `.cs` with `mcs` against a FAITHFUL
  stub (real `Mathf` over `System.Math`, real `Vector3` operators) and RUN it, reading the
  private state back by reflection. A hand-ported formula is a hypothesis about the code,
  not a test of it — and it fails in the one direction you cannot see, by being kinder than
  production.
- **`Mathf.Sin(Mathf.PI)` is NEGATIVE in float32** (≈ `-8.74e-8`), so `Mathf.Pow(that,
  fractional)` is `NaN`. Any profile of the shape `pow(sin(...), k)` with `0 < k < 1` NaNs at
  its endpoint. One NaN vertex poisons a whole mesh's bounds, and an invalid-bounds renderer
  stops updating — so the symptom is "my animation does nothing", not "my maths is wrong".
  Clamp before `Pow`, every time. (A sibling function escaped only because its `Max` clamp
  happened to sit in the right place — the presence of one clamp is not evidence of the other.)
- **A physics-layer pair can be DISABLED, so a correct-looking impactor case never fires.**
  Adding `case FooImpactor` to an `AcceptImpactee` switch compiles, reads correctly, passes
  review, and dispatches nothing if the two GameObjects' layers are off in
  `ProjectSettings/DynamicsManager.asset`. Check the matrix before trusting a trigger path:
  decode `m_LayerCollisionMatrix` (32 little-endian 8-hex words, bit `b` of word `a` = layer
  `a` × layer `b`) and read layer names from `TagManager.asset`. Live case: Crystals(9) ×
  Explosions(10) is **disabled**, so a blast could never reach a crystal through triggers —
  while Ball(0) × Explosions(10) is enabled and the identical shape worked.
- **A round-trip RPC placed behind an earlier server-only gate is unreachable plumbing.**
  Trace the GATE ORDER, not the intent: a client→server hop written so "a client's blast can
  still forge" was never called, because the crystal-consumption check (`!IsNetworkClient()`)
  ran first and returned. It read as a solved problem in three places — the RPC's own doc
  comment, the helper's class note, and the design doc — while delivering nothing, which is
  strictly worse than an honest gap. Before writing the fallback, follow every early-out
  between the entry point and the call site and confirm one of them is not the thing you are
  trying to work around.

- **A SERIALIZED value is not its C# field initializer — and the initializer's output is
  not what you remember it being.** Retiring a `[SerializeField]` whose default comes from
  a non-trivial expression (`AnimationCurve.EaseInOut(0,0,1,1)`, `new Gradient{…}`, a
  computed `Vector3`) means claiming an equivalence, and that claim has TWO halves, both
  checkable offline and both easy to get wrong:
  (1) **What does the constructor actually produce?** Do not recall it — find another asset
  in the repo whose field carries the *same* initializer and read the tangents/keys Unity
  wrote. (`AnimationCurve.EaseInOut` really is zero-tangent Hermite = `smoothstep`;
  `SpaceCrystalAnimator.shrinkCurve` on two fauna prefabs proved it in about a minute.)
  (2) **Which assets serialize the field at all, and are they at that default?** Only
  objects that were touched in the inspector carry a value; the rest take the initializer
  at runtime. Here, exactly two of the shield prefabs serialized the curves and *neither*
  was at the default — someone had dragged the tangents to 2, a fast-slow-fast shape 0.192
  away from `smoothstep` at its worst, on a prefab live in three multiplayer scenes.
  Sweep it mechanically: `grep -rl <scriptGuid> Assets --include=*.prefab --include=*.unity`
  then parse the field block out of each hit. The failure mode is silent and permanent —
  once the C# field is deleted, Unity drops the orphaned YAML keys on the next save, so the
  authored deviation disappears with no diff that mentions it.
- **Renaming a Unity SERIALIZED FIELD must sweep `Tools/**.py` too, not just C# + scenes +
  prefabs.** This repo authors scene/prefab YAML from Python generators
  (`Tools/Build/author_*_assets.py`), and several of them both WRITE and VALIDATE a field by
  literal name. Rename the C# field, migrate every scene, and the generator still emits the OLD
  key — so the next person who re-runs it silently reverts your change, and the generator's own
  `--check` "passes" while validating a name nothing reads any more. Sweep:
  `grep -rn '<oldFieldName>' Assets/ Tools/ Docs/`, and treat a hit in `Tools/` as a caller, not
  a comment. (Cost here: `anchorlessSpawnRadius` → `noNucleusSpawnRadius` was clean in the C#
  and all three scenes, and left `author_dogfight_assets.py` writing the dead name.)
- **A generator that CLONES a live scene as its donor rots the moment the donor changes.**
  `author_dogfight_assets.py` clones `MinigameRampage.unity` and asserts on the donor's exact
  field blocks; a rework of Rampage made it permanently un-runnable. That is the correct end
  state for a one-shot migration — but say so **in the file**, or the next reader spends an hour
  trying to satisfy asserts that describe a scene that no longer exists.

- **Unity's FBX importer derives subasset fileIDs from OBJECT NAMES, so two different FBX
  files that share object names mint the SAME local fileIDs.** Two consequences, one good,
  one a false-alarm generator. Good: a prefab's `m_Modifications` against model A's instance
  survive re-pointing the instance to model B when the node names match (the branch swap of
  SparrowModel1→SparrowModel4 worked this way), and a mesh reference like
  `{fileID: -3416553540687559647, guid: <fbx>}` is reproducible by committing the same FBX +
  `.meta` — no editor import needed to know the id. False alarm: grepping the repo for a bare
  local fileID to find "external references" hits every sibling asset that shares lineage
  (seven projectile prefabs all declare their own `&6972185831030386429`) — a REAL cross-asset
  reference must carry the target's `guid:` on the same line, so grep for the guid, not the id.
- **The deliverable you were asked to integrate may exist only on an abandoned remote branch.**
  Local clones here are shallow and single-branch: `git log --all --grep` + `git ls-remote origin`
  to find the branch, `git fetch origin <branch>`, then extract exact assets with
  `git show '<branch>:<path>' > file` and byte-verify (`md5sum` vs `git show | md5sum`).
  Keep the original `.meta` GUIDs so every reference authored against the asset on that branch
  (animator states by clip internalID, prefab mesh refs) resolves without rewiring — and diff
  the branch's version of any SHARED file against trunk's before adopting it wholesale: adopt
  only when the base is byte-identical and the diff is purely additive (the Sparrow animator
  controller was; the Sparrow prefab was NOT — it had swapped the visible model, which trunk
  must not).

- **`$` in a .NET regex does NOT match before `\r`, so every line-anchored pattern
  fails on a Windows checkout.** In multiline mode `$` matches before the `\n` but
  *after* any `\r`, so `^#define FOO (\w+)$` matches 0 times in a CRLF file. This is
  invisible on Linux and total on Windows: a source-rewriting tool shipped this way
  reported the file's state wrongly AND refused to write. Capture the ending and
  re-emit it — `(\r?)$` as a group, `${1}value${3}` in the replacement — which also
  stops the rewrite from silently converting line endings into diff noise. (Reading
  only? `\r?$` is enough. This repo's own `PrismOcclusionCoverageTests` carries the
  same warning for `.shadergraph` — heed it BEFORE writing the regex.)
  **Knowing the trap is not enough — SWEEP for it, because one outlier call site is
  the normal shape of this bug.** `PrismOcclusionDitherLab` got it right in its Anchor
  table and wrong in one method 50 lines away (`SetTuningFlag`), so its "enable design
  mode" button had never once worked on a Windows checkout and nothing said so. The
  detector is four lines over the file — pull every `@"..."` verbatim regex literal and
  flag any containing `$` without a preceding `\r?`:
  ```python
  for m in re.finditer(r'@"((?:\(\?m\))?\^[^"]*)"', src):
      if '$' in m.group(1) and not re.search(r'\\r\?\)?\$', m.group(1)): print(m.group(1))
  ```
  Then PROVE the fix rather than asserting it: under .NET's `$` semantics (matches
  before `\n`, after `\r`) the old pattern must measure 1 match on LF and **0 on CRLF**,
  the new one 1 on both. On Linux you cannot observe the failure any other way.
- **.NET does not throw on a replacement naming a group the pattern lacks — it emits
  the literal text.** `Regex.Replace(s, @"(a)(b)", "${1}x${3}")` writes `${3}` into
  your file. A rewriter that varies its patterns must carry the group count
  EXPLICITLY per pattern rather than inferring it. (Python is the opposite and throws
  — so a Python prototype will not warn you.) Related: prefer `${1}` over `$1`; `$1`
  followed by a digit (`$1` + `4.5`) parses as group 14.
- **A `CapsuleCollider` is scaled ANISOTROPICALLY, so its two dimensions need two
  different divisors.** Unity scales the `height` by the lossy scale along
  `direction`, and the `radius` by the LARGER of the other two axes. Under a uniform
  scale nobody notices; under a non-uniform parent — the Dolphin blast's container
  runs `(base, base, reach)` — dividing both by one factor puts the capsule visibly
  wrong in one dimension. Write each in local units as
  `world / (its own lossy factor)`, and remember `direction` is an INDEX (0/1/2), so
  the radial factor is `max(lossy[(d+1)%3], lossy[(d+2)%3])`. Two more edges on the
  same component: (a) the internal `height >= 2*radius` clamp is applied in LOCAL
  space, so with two different divisors it can bite at a world size where you'd
  expect no clamp — check `localHeight >= 2*localRadius`, not the world numbers;
  (b) a 90° child rotation under a non-uniform parent is an exact axis PERMUTATION,
  so `lossyScale` is exact there (no skew) — but do not hand-derive which axis is
  which. Resolve `direction` at runtime by dotting the world-space
  `transform.TransformDirection` of each local axis against the direction you want;
  it costs three dots once and survives someone changing the authored rotation.
- **A window measured with the other variable held fixed does not transfer.** A sweep
  of parameter B at one value of A yields a band for B that looks absolute and is not.
  Shipping it as a validation rule then flags perfectly good settings as failures —
  here a "wall 4–11 px" band, swept at a fixed 11 px polygon, condemned a 20 px wall
  in a 16 px polygon that actually measured BETTER than the shipped baseline. Sweep
  the RATIO (or the second variable at several values of the first) before publishing
  a window, and when a tool enforces one, have it say *"outside the measured range —
  measure it"* rather than *"wrong"*.
- **Stripping `[...]` attributes globally also eats `float[]`.** A C#-field
  scraper that does `re.sub(r'\[[^\]]*\]\s*', '', line)` turns
  `[SerializeField] float[] foo = …` into `floatfoo = …`, so the declaration
  stops matching and the field vanishes from your "serialized fields" set —
  which then reports FALSE mismatches against a perfectly good asset. Anchor the
  strip to the line start: `^(?:\[[^\]\n]*\]\s*)+`. (Same class of bug: a
  `^public` regex misses `[Min(1)] public int Foo` — strip first, then match.)
- **A prefab field pointing at its OWN asset GUID is an unknown — avoid needing
  one.** For "instantiate another of me" semantics, `Instantiate(this)` on the
  live component is simpler, deterministic, and inherits the runtime-correct
  serialized state; it needs no wiring to keep valid and no fallback path. Two
  caveats: cloning a live root also clones any RUNTIME-created children (find
  them by name and reuse instead of stacking duplicates), and private fields
  without `[SerializeField]` come back fresh on the clone (usually what you want
  for runtime lists).
- **Unity serializes by NAME, so the parity check is per-COMPONENT-document.**
  Validate hand-authored YAML by splitting on `--- !u!114`, mapping each doc's
  `m_Script` guid → its `.cs`, and asserting every top-level key is a serialized
  field of THAT class (plus its bases). Checking a whole file against one class
  produces noise — a prefab legitimately contains several components' fields.
- **`sed -i ... $(grep -rl ...)` SHREDS paths with spaces** — and Unity paths
  are full of them (`Assets/_SO_Assets/Cell Configs/...`). The unquoted
  expansion splits one path into two nonexistent ones, so those files are
  skipped while every space-free path IS rewritten: a half-applied edit across
  a scene and a dozen prefabs. Always drive multi-file rewrites from Python
  with an explicit file list, and `git status` immediately after.
- **`Material.HasProperty` can NEVER see an unexposed ShaderGraph property.**
  Unexposed properties are declared outside the `UnityPerMaterial` cbuffer and
  never enter the shader's property list, so a validator built on `HasProperty`
  reports "missing" for every material — including correctly-wired ones. Global
  uniforms set with `Shader.SetGlobalVector` are exactly this case. Check the
  **graph text** for the property reference instead (the existing
  `PrismClockWiringValidator` already did this; copying its shape would have
  saved the detour), or census by shader NAME.
- **`clip(0)` KEEPS the fragment, it does not discard it.** `frac()` can return
  exactly 0, so a dither threshold of 0 against an alpha of 0 survives on the URP
  variants that clip directly rather than through `AlphaDiscard`'s epsilon —
  leaving a sparse confetti of survivors in a region that is supposed to be
  completely gone. Nudge any computed clip threshold strictly inside (0,1)
  (`n * 0.998 + 0.001`).
- **A READ-ONLY editor tool must use `AssetDatabase.LoadAssetAtPath<GameObject>`, not
  `PrefabUtility.LoadPrefabContents`.** The asset representation already carries the
  merged hierarchy (nested prefabs included), so `GetComponentsInChildren` and every
  serialized field read the same as at runtime — while `LoadPrefabContents` opens a
  preview SCENE per prefab, which is far heavier AND a second failure surface: on a
  prefab with malformed data it spills native parse errors and callstacks before your
  code runs. An auditor that dies on the bad data it exists to find is worse than no
  auditor. Reserve `LoadPrefabContents` for tools that WRITE.
- **A guid-uniqueness assertion must be scoped to `.meta` files.** "This new guid appears in
  exactly one file" is the wrong invariant and produces a false failure the moment the guid is
  *used*: a script guid legitimately appears in its own `.cs.meta` AND in every asset whose
  `m_Script` points at it. The real invariant is **exactly one `.meta` OWNS a guid**; every
  other hit is a reference and is evidence the wiring worked. Assert
  `len(grep -rl <guid> Assets --include=*.meta) == 1`, and print the referencing files rather
  than failing on them.
- **A "dangling GUID on this prefab" is usually project-wide.** Before treating
  a missing asset reference as a local bug, grep the WHOLE Assets tree for that
  guid: a reference broken on four flora prefabs turned out to be broken on
  fauna prefabs, cytoplasm prefabs and three scenes too. That changes the fix
  from a one-liner into its own change with its own verification — decide
  deliberately, and say so, rather than sweeping scenes into an unrelated diff.
- **Missing keys take the C# field INITIALIZER, stale keys are ignored.**
  Adding a serialized field is safe for existing assets *iff* its initializer
  is the correct legacy default (verify that, don't assume). Conversely, YAML
  containing a key for a field that is no longer serialized (e.g. a
  `[Inject] protected` field) is harmless residue — not evidence the field is
  still serialized.
- **`[FormerlySerializedAs]` also resurrects stale prefab-instance OVERRIDES —
  and an override BEATS the value you just authored.** The attribute is
  described as "keep the asset's old value through a rename", which sounds
  purely helpful. But a nested prefab instance stores overrides as
  `propertyPath: <fieldName>` strings, and those get remapped by the same
  attribute. So renaming `boostFullColor` → `rollArmedColor` silently carries a
  *parent prefab's* year-old override onto the new field, and because an
  override wins over the source prefab, the new default you carefully authored
  never renders. Live case: the Sparrow's HUD instance overrode both retired
  boost-gauge colours to white; inherited through `FormerlySerializedAs`, the
  new "armed" and "spent" ring colours would both have been white — a state
  indicator that indicates nothing, with correct-looking code and a
  correct-looking source prefab. **Before renaming any serialized field, grep
  the whole repo for `propertyPath: <oldName>`**, then decide per field: keep
  the attribute where the old value is still the right one (object references,
  durations), and DROP it — deleting the override blocks outright — where the
  rename changed what the value MEANS. Say which you did, in a comment, next to
  the field.
- **A serialized field's C# initializer is NOT its runtime value — never reason
  from it.** The corollary of the rule above, and the one that actually bites:
  when the asset DOES author the key, the initializer in the `.cs` is dead text
  that only a fresh in-editor asset would ever see. Reading it and reasoning
  onward produces a confident wrong number. Live case: `LightFaunaDataSO.
  maxSpeed = 6f`, while both shipped assets author **25**
  (`MassBrittleStarFaunaDataSO`) and **35** (`MassSharkFaunaDataSO`) — a 4-6x
  error, which turned "the feeding creature is basically stationary, ~1.5u of
  drift" into the true ~6.25u (half the feeding cluster radius) and would have
  justified 'optimizing' a per-frame convergence refresh into a snapshot that
  visibly sucks mass toward where the creature was. **Before quoting any tuning
  constant, resolve the SO's script GUID from its `.cs.meta`, grep
  `Assets/**/*.asset` for it, and read the authored value from every instance**
  — and say which asset you read. Sibling assets often disagree (grazer vs
  predator), so "the value" may not be singular.
- **Validate an instanced-render contract by COUNT-MATCHING, not by reading.**
  For a `.shadergraph` + per-instance-ECS-component pair (Entities Graphics
  Hybrid Per Instance), dump the graph's properties with
  `overrideHLSLDeclaration:true, hlslDeclarationOverride:3` and compare that set
  one-for-one against the components the prototype adds and the spawn path
  writes. An exact match is strong evidence the stamp will land; a graph
  property with no component is a value stuck at its material default, and a
  component with no HPI property is a silently ignored write. This caught
  nothing on the suction batch (9 vs 9, clean) — which is precisely the value:
  it converts "I mirrored the explosion path, it should work" into a checkable
  claim before anyone opens Unity. Properties that are exposed but NOT hybrid
  (`_Move`, `_SqrDistance`) are per-material constants and correctly absent
  from the component set.
- **NEVER delete a code block with a regex.** A pattern like
  `r'public static X\(...\)\n\{(?:.*?\n)*?\}\n'` looks bounded but the lazy
  block matches across method boundaries: one such "remove two unused helpers"
  script silently ate 250 lines of `ToyFactory.cs` (every shape builder + the
  gate factory). Use `Edit` with exact anchors for deletions, and if you must
  script one, **verify after**: line count before/after, plus an inventory grep
  of the file's public API (`grep -n "public static ..."`). Recovery is
  `git checkout HEAD -- <file>` when the file was already committed — which is
  another reason to commit before scripted edits.

- **`re.S` + `.*?` over a whole Unity YAML file matches ACROSS documents, and the wrong
  match still validates.** "Find the MeshFilter whose `m_Mesh` guid is X, then take its
  `m_GameObject`" written as one `re.S` pattern latches onto the FIRST `MeshFilter:` in the
  file and lazily extends until it finds guid X *in some later document* — so it returns the
  wrong GameObject with total confidence. This is nastier than it sounds: the component gets
  attached to a real object, the fileID resolves, the dangling-reference self-check passes,
  the diff looks tidy, and the only symptom is a runtime behaviour that never fires. **Any
  predicate about "a document that contains A and B" must be evaluated WITHIN one document**
  — split first (see the newline trap below), then filter. And assert the cardinality you
  expect (`len(matches) == 1`) rather than taking `[0]`; a per-document filter that finds two
  MeshFilters on the crystal mesh is telling you something a `.search()` would have hidden.
  While you are there, print the human-readable NAME of the object you resolved
  (`m_Name`) — "attached to GameObject 2842750437815966001" is unreviewable, "attached to
  `chargeShell`" catches this bug by eye in one second.
- **Replacing a SHADER is an API change: sweep for every property the old one exposed.**
  Shader properties are a public surface driven from C# by string name
  (`Shader.PropertyToID`, `SetFloat`, `SetColor`, MaterialPropertyBlock), and dropping one
  fails **silently** — no compile error, no warning, the write just goes nowhere. Before
  swapping a material onto a new shader, list the old shader's properties (for a
  `.shadergraph`, §2a's dump) and grep the repo for each name. A charge-crystal rewrite
  dropped `_opacity`, which `FadeIn.cs` drives through a property block; nothing would have
  errored, the crystal would simply have POPPED into existence — breaking a platform-wide
  law with a green build. Decide per property: reimplement it, or prove nothing writes it.
- **Splitting Unity YAML on `--- !u!` and rejoining DOUBLES the newline — silently, in every
  document.** `^--- !u!(\d+) &(\d+)( stripped)?$` is line-anchored, so `$` matches BEFORE the
  `\n` and `match.end()` points AT it: the body slice `txt[m.end():end]` already STARTS with
  that newline. Rejoining as `header + '\n' + body` therefore inserts a blank line after every
  single header. It parses, it reimports, and it turns a pure-deletion change into a diff with
  thousands of phantom insertions — which is how you lose the ability to review your own work.
  Rejoin as `header + body`, and **verify by counting NON-BLANK insertions**, not by reading
  `git diff --stat`:
  ```sh
  git diff <base> -- "$f" | grep '^+' | grep -v '^+++' | sed 's/^+//' | grep -c '[^[:space:]]'
  ```
  For a deletion-only change that number must be **0**. Corollary: if two scripts each rewrote
  the same file, the artifact COMPOUNDS — a repair regex matching `header\n\n` strips only one
  of two blank lines and looks like it worked. Collapse with `\n\n+` and re-count.
- **A bare `{fileID: N}` is ALWAYS same-file; only `{fileID: N, guid: G}` crosses assets.** A
  sweep for "who else references this id" that ignores the guid is worthless in a Unity repo,
  because sibling **flat-copy** prefabs (Manta/Falcon/Shrike/Termite here) share identical
  internal fileIDs by construction — so every id appears in every sibling and the report is
  ~100% false positives. Match `fileID: (\d+),\s*\n?\s*guid: <target-guid>` and nothing else.
  (The guid can wrap onto the next line — allow the newline or you will miss real hits.)
- **Pre-filter a whole-project regex sweep by GUID substring, or it hangs.** Multiline
  `(?ms)` patterns with `.*?` over 1,500 prefabs and scenes backtrack catastrophically on the
  large ones. A file that does not contain the target prefab's guid cannot hold a record for
  its objects, so `if not any(g in txt for g in GUIDS): continue` turns a timeout into
  seconds. Then replace every `.*?` with `[^\n]*` — you are matching within a line anyway.
- **CRLF**: Windows checkouts deliver `\r\n`; `split("\n\n")` sees ONE block.
  Normalize line endings before splitting; preserve the file's own separator
  when writing.
- **Substring matching**: `EndsWith("BlockGraph")` also matches
  `"ExplodingBlockGraph"`. Match exactly (`== "Shader Graphs/X"` or
  `EndsWith("/X")`; paths: `endswith('/X.shadergraph')`).
- **Slot object-id vs integer m_Id**: edges use the INTEGER slot id; `m_Slots`
  lists OBJECT ids. Confusing them = StopIteration in the script (harmless,
  validate-before-write) or a silently wrong edge (the validator catches it).
- **Direction-mode Transform nodes NORMALIZE** (`TransformWorldToObjectDir`):
  magnitude destroyed, direction re-skewed by non-uniform scale. For a
  magnitude-carrying vector do the raw multiply in HLSL:
  `mul((float3x3)GetWorldToObjectMatrix(), v)`. Position-mode (full affine) is
  fine.
- **Clock domains**: URP's `_Time` ≠ `Time.timeSinceLevelLoad`. Never stamp
  from one clock and sample another — publish YOUR clock as a global uniform
  (`Shader.SetGlobalFloat` each frame from the same value the stamps use);
  equality then holds by construction.
- **Vertex-displacing animation needs a culling envelope**: entity transforms
  frozen at stamp ⇒ stale `RenderBounds` frustum-culls wrong both directions.
  Reset-to-mesh THEN expand at the stamp (reset first or pooled reuse
  compounds run over run).
- **GPU-first is a prompter law here**: never move per-frame or per-instance
  math from GPU to CPU. CPU-side one-shot computation of CPU-OWNED structures
  (culling bounds, colliders) is fine — name the distinction explicitly.
- **Unity asset edits the editor must bless**: after out-of-editor edits the
  human's next pull triggers reimport — if visuals look unchanged, suspect a
  stale Library (ask them to Reimport the asset) before suspecting the edit.
- **`System.Random` is the LEGACY Knuth subtractive generator when seeded**
  (not .NET Core's xoshiro — that is only the parameterless ctor). To mirror a
  Unity generator you must replicate it exactly, AND replicate the order it is
  consumed in: a helper like `Jit()` pulls from the shared stream, so reordering
  two emit families silently changes every jittered scale downstream.
- **Unity FBX import applies a cm→m conversion**: with `useFileUnits: 1` and the
  file's `UnitScaleFactor: 1`, mesh extents are divided by 100. A mesh whose FBX
  vertices span ±98 is ±0.98 Unity units, so a prefab at `localScale 400` has a
  world radius of ~392, not 400. Derive world sizes from the FBX + importer
  settings, never from `localScale` alone.
- **Two different "radius" values can coexist on the same object.** `Cell.
  NucleusRadius` returns `localScale.x` (400) while the node-control zone comes
  from `RefreshNucleusControlRadius` → **renderer bounds** (392). They are close
  enough to look interchangeable and are not; read the one the consumer reads.
- **Prism pose convention: `z` is forward.** `SpawnPoint.LookRotation(fwd, up)`
  puts local +Z on `fwd`, so a flat plate lying ON a surface is
  `LookRotation(normal, tangent)` with a SMALL z, and a chained ribbon is
  `LookRotation(p - prev, up)` with a LARGE z. Getting this backwards produces
  geometry that measures perfectly and renders as confetti.
- **Constant-coverage scaling**: to scale a sampled surface by k, multiply
  sampling spacing AND footprint by the same factor — coverage
  (count × footprint / area) is then exactly 1 by construction. The trap is a
  family whose count is **explicit** rather than spacing-derived: it does not
  self-correct and silently over-covers (one such family measured 32% of a
  cell's volume from 10% of its prisms). Set those to `base × k² / detail²`.
  Corollary worth stating out loud: at constant coverage and thickness, a 2×
  surface costs exactly 4× volume. That is geometry, not a tuning miss.
- **A tuning dial that a downstream clamp already saturates**: before "let's
  try turning X up", trace X to the value the SCREEN reads and check every
  clamp in between. If the input already exceeds the ceiling, the dial is dead
  — turning it up changes literally nothing, and you will burn a play-test
  round proving that. Cosmic Shore has bitten **three** times here (AOE blast
  `Inertia` vs `PrismExplosion.maxSpeed` 33.33 with a ~222 u/s input; the hull
  ram vs the same clamp's FLOOR; and **BLOOM**, below). Symptom to recognize:
  every instance produces the IDENTICAL magnitude regardless of cause. Fix by
  putting the path on a true-velocity contract that supplies its own ceiling —
  never by widening the shared clamp, which retunes every other consumer of it.
- **"Make it glow more" is a CLAMP question first, an HDR-colour question second.**
  URP's Bloom clamps the bloom SOURCE before thresholding, so the per-pixel bloom
  contribution is flat above `clamp` and every colour above it blooms identically.
  This project overrides `clamp` to **0.5** (URP's default is 65472) in the GamePlay
  and Commander profiles, which makes 56 of the 86 colours in `OriginalColorSetSO`
  — the danger rim at 1.498, AOE at 4.0 — bloom exactly the same, and makes
  `Docs/PALETTE.md` §3's "channels above 1.0 bloom" false as shipped. **Read the
  volume profile's `threshold`/`knee`/`clamp` and compute the response curve BEFORE
  authoring any HDR value**; URP's prefilter is
  `c=min(clamp,c); B=max3(c); soft=clip(B-thr+knee,0,2knee)²/(4knee); mult=max(B-thr,soft)/B`.
  Two consequences: raising a colour past the clamp is a no-op, and **inside** the
  clamp extra bloom is bought with bright **AREA**, not intensity — so find which
  property covers the most silhouette (next trap) instead of turning brightness up.
  Also check the tonemapper: at `mode: 0` (None) there is no shoulder, so channels
  above 1.0 clip hard and shift hue toward white — "brighter" silently means
  "less saturated".
- **Before tuning a colour, find out which property covers the AREA — it is usually
  not the one named "Bright".** Dump the graph (§2a) and read the composition: these
  crystal shaders are `lerp(dull, bright, fresnel)` with `fresnel = (1−N·V)⁴`, and at
  that exponent the rim is **2.5%** of a silhouette (area-weighted mean fresnel 0.067).
  So `_DullCrystalColor` is ~93% of the object and essentially all of its bloom, while
  `_BrightCrystalColor` is a hairline. Integrating the bloom response over the
  silhouette (`∫ bloom(lerp(dull,bright,f(r))) · 2πr dr`) turns "which knob?" into a
  number in ten lines of numpy. Note sibling graphs can SWAP the roles
  (`TimeCrystalGraph` does), which is a strong argument for expressing a per-variant
  difference as a **scalar on the pair** rather than a second authored pair: a scalar
  cannot move the hue and dims correctly whichever role each colour plays.
- **`Renderer.SetPropertyBlock` REPLACES the block, and `MaterialPropertyBlock.Clear()`
  discards EVERYONE's overrides, not just yours.** A component that owns a private block
  and pushes it wholesale will silently erase any other system's per-renderer tint on the
  same renderer — and if it `Clear()`s on completion, it erases it permanently. Live case:
  `FadeIn` drove `_opacity` this way on every crystal model, so `Crystal.ApplyColorSetTint`'s
  colour was wiped at the start of the fade and again at the end, and **no crystal in the
  game had ever displayed its intended colour** — each just settled back to its authored
  material, which looks deliberate. Always `GetPropertyBlock(block)` before `SetFloat`/
  `SetColor` (it clears and refills the block from the renderer, so it is a true
  read-modify-write), and retire an override by writing the material's own authored value
  back rather than clearing. Composing also makes the writers **order-independent**, which
  matters because `Start()` order between a parent and its child components is undefined.
  Detection: grep for `SetPropertyBlock` and check each call site is preceded by a
  `GetPropertyBlock` on the same renderer.
- **A guid grep of a prefab CANNOT see components that live in its nested prefabs.**
  `grep -c <FadeIn guid> Crystal.prefab` returned **0**, and the obvious conclusion — "the
  omni has no FadeIn, so its tint survives, and `DeactivateModels`'s unguarded
  `GetComponent<FadeIn>().StartFadeIn()` must be NREing" — was wrong on both counts: the
  four models are instances of `TrucatedOctahedron.prefab`, which carries the component. A
  `!u!1001 PrefabInstance` contributes its source prefab's whole component set at runtime
  while contributing only its OWN guid plus override rows to the file. So: resolve
  `m_SourcePrefab` guids and grep those files too (recursively) before concluding an object
  lacks a component — or load it the way §5's read-only-tool bullet says and ask the merged
  hierarchy.
- **A reference field can point at a DISABLED TWIN of the object doing the
  work.** When a prefab was migrated from a nested component-prefab to a
  bespoke object, the old instance often survives, inactive, still holding
  every reference — so the object with perfect wiring is not the object the
  system runs, and the object the system runs was never initialized. Nothing
  errors: the live one just never gets its `Initialize`, and whatever gates on
  "am I initialized" silently drops every event forever. The Dolphin's skimmer
  shipped this way for its whole life. **Resolve every `{fileID: N}` you rely
  on to its GameObject and assert `m_IsActive: 1` up the entire ancestor
  chain** — do not stop at "the component exists and looks right". Then write
  the fleet auditor, because if one prefab has it, others do (the Serpent did).
- **A gate that greps prefab YAML for a CLASS NAME can never fire.** Unity records a
  component only as `m_Script: {fileID: 11500000, guid: …, type: 3}`;
  `m_EditorClassIdentifier` is empty for default-assembly scripts, so the type name is
  simply not in the file. Worse, once the class is DELETED that component deserializes to
  a null entry, which every `GetComponentsInChildren<Component>()` sweep skips — so the
  two obvious ways to detect "this prefab still carries the retired component" are both
  blind, and a validator written from either reports a confident PASS on exactly the state
  it exists to catch. **Probe the script GUID**, and prove the probe works by running it
  against the commit BEFORE the removal (it must be true there and false now) — a gate you
  have not seen fail is not a gate.
- **A whole-file `Contains("<token>")` stops being a gate the moment the file grows a
  second occurrence of that token.** "The binding is guarded because the file mentions the
  guard somewhere" holds right up until an unrelated method adds its own mention; then the
  guard can be deleted from the site that matters with the check still green. Check each
  CALL SITE (token within N characters above it), not the file.
- **Edit-mode tests and `FrogletTools` validators cannot see each other.** Tests compile
  into `Assembly-CSharp` (there is no test asmdef here), validators into
  `Assembly-CSharp-Editor`, and the runtime assembly cannot reference the editor one. So a
  predicate both gates must share CANNOT live in the validator — put it in a runtime file
  whose whole body is `#if UNITY_EDITOR` (pattern 2 of
  `Docs/CONDITIONAL_COMPILATION.md`), which both can reach and which never enters a player
  build. Writing the rule twice is how the two gates drift apart.
- **Adding a `using` to an existing Unity file is a SEMANTIC change, not a formatting one —
  and a syntax parse cannot see the breakage.** `UnityEngine.Object` is referenced by its
  short name all over this codebase, so importing `System` into such a file makes every bare
  `Object` ambiguous (`CS0104: 'Object' is an ambiguous reference between 'UnityEngine.Object'
  and 'object'`). It bit `CSDebug.cs`, where `using System;` — added only to reach
  `[Flags]` — broke all seven `Log(object, Object context)` overloads at once. A Roslyn
  **syntax** pass (`CSharpSyntaxTree.ParseText` + `GetDiagnostics`) reports this file as
  clean, because it is a binding error, not a parse error; only a real compile catches it.
  Two habits: prefer the fully-qualified attribute (`[System.Flags]`) over importing a
  namespace into a Unity-facing file, and when you must add the import, grep the file for
  bare `Object`/`Random`/`Debug`/`Application` first — those four collide between `System*`
  and `UnityEngine`. When a namespace is deliberately absent, leave a comment saying so, or
  the next person re-adds it. The mirror also holds: before REMOVING a `using`, enumerate the
  types that namespace declares and grep the file's body for all of them — checking only the
  one symbol you deleted misses a sibling type that was riding the same import.
- **Verify the bug before fixing it.** A report describing code behaviour
  ("it's using the sphere centre") may predate a fix that already landed. Read
  the live path end to end and check `git log` on the file FIRST; report
  "already fixed in <sha>, here's the proof" rather than re-fixing correct code
  or, worse, inventing a change to look responsive.
- **A collider's authored number is not its world size — and for a SphereCollider the
  scale component that wins may be one nobody was thinking about.** Unity scales a
  `SphereCollider` by the **largest absolute lossy-scale component**, a `CapsuleCollider`
  by the max of the two axes perpendicular to its direction, and only a `BoxCollider`
  component-wise. So on a "dart" prefab — a unit sphere mesh at `m_LocalScale (1.5, 1.5,
  20)`, stretched on z for the tracer look — `m_Radius: 0.3` is not a 0.3 or even a 0.45
  world radius: it is `0.3 × 20 = 6.0`, thirteen times the projectile's visible 0.75
  cross-section and about as wide as the dart is long. Reading `m_Radius` out of the YAML
  and reporting it as the hit size is wrong every time the transform is non-uniform.
  Always compute `worldRadius = m_Radius × max(|sx|,|sy|,|sz|)`, and when writing one,
  invert it: `m_Radius = desiredWorldRadius / maxScaleComponent`. Sweep siblings while you
  are there — the same authored `0.3` sat in two more projectile prefabs in this repo.
  **The corollary is a FREE DIAL, and it is worth knowing deliberately**: when the winning
  component is an axis you are not touching, every *other* axis is a purely visual dial. The
  same dart's cross-section was later halved (`1.5 → 0.75`) with the hit radius provably
  unchanged at 0.825, because `z = 20` still won the `max`. Compute it and assert it rather
  than assuming either way — the identical geometry that once made a collider 8× too big is
  what makes this edit free, and only arithmetic tells you which case you are in.
- **An EFFECTIVE number that everything agrees on may never have been AUTHORED at all.**
  The mirror of "the authored number is not the effective one" (`/vessel` §2.4a): here the
  effective number was 12, three assets had been tuned to match it, a config default and a
  design doc both recorded it as intentional — and it was an accident, the arithmetic
  product of a mesh stretch leaking into a collider (trap above). Before you propagate a
  measured constant to a second system "for parity", find the line that CHOSE it. If no
  line did, you are about to enshrine an artifact, and every asset you align to it makes
  the eventual correction bigger.
- **HDR colour fields are LINEAR, and scaling them is not tuning**: in a Linear
  project (`ProjectSettings: m_ActiveColorSpace: 1`) a `[ColorUsage(true, true)]`
  field serialises **linear intensity** — Rec.709 luminance and CIELAB apply
  directly (no de-gamma), and channels >1 are legitimate, not corruption to clamp.
  Two traps follow: (1) **multiplying a colour PAIR by a constant changes
  brightness but leaves contrast identical** — a "halve it, it's too bright" pass
  fixes nothing (this is exactly how a prior shielded-prism fix shipped a no-op);
  (2) **HSV misleads across hues** — equal `V` is not equal brightness and HSV
  "saturation" is not perceptual chroma. Judge colour pairs in CIELAB: `L*` for
  brightness, `ΔL*` between them for contrast, `C*` for harshness. Derive new
  values in LCh (keep the asset's own hue, transplant the reference's `L*`),
  convert back, and assert no channel went negative before writing.
- **Shallow clone hides most of the repo** (Claude Code on the web): the working
  clone can be `--depth`-limited with only ~2 remote refs fetched while the server
  has hundreds. A "scan every branch" sweep over `git for-each-ref refs/remotes`
  then silently reports a CONFIDENT WRONG answer ("that work exists nowhere").
  Before any cross-branch history search, check `[ -f .git/shallow ]` and compare
  `git ls-remote --heads origin | wc -l` against `git for-each-ref refs/remotes | wc -l`;
  if they disagree,
  `git fetch --filter=blob:none --no-tags origin '+refs/heads/*:refs/remotes/origin/*'`
  first (blob-filtered, so hundreds of branches of a Unity repo land in ~a minute).
  Then dedupe by BLOB: `git rev-parse "$ref:$path"` per ref and group — N branches
  usually collapse to a handful of distinct file versions worth reading.
- **A leftover GUID hit after you remove a reference is usually a DEAD prefab-instance
  override, and proving it dead takes three checks, not one.** Unity never prunes an
  `m_Modification` whose `propertyPath` no longer resolves, so a prefab keeps writing a field
  that was commented out years ago — and a `grep -rl "guid: <x>"` sweep reports the vessel
  prefab as a live consumer of an asset you just unwired. Do not conclude "there is a second
  wiring path" (nor "it's fine, it's just an override") until you have checked all three, since
  any ONE of them being alive makes it real: (1) **does the field still exist?** — grep the
  target script for the `propertyPath`'s root; a `// [SerializeField]` line means the override
  can never deserialize; (2) **is the instance active?** — find the `PrefabInstance` block
  carrying that guid and read its `m_IsActive` override; (3) **does anything point at it?** —
  for a component the engine reaches through a reference (skimmers via
  `VesselStatus.NearFieldSkimmer`), resolve that fileID and check it is this instance. Cost
  here: `SkimmerFXPrismEffect` kept showing up in `Dolphin.prefab` after its container was
  cleaned; all three answers were dead (commented-out `skimmerPrismEffectsSO`, `m_IsActive: 0`,
  `_nearFieldSkimmer` → the *other* skimmer). Record the finding in a backlog rather than
  hand-editing prefab YAML to remove override entries — the sweep is what tooling is for.

- **A GENERATOR that has drifted behind its own output is a loaded gun, and `--check` will not
  catch it.** "The generator is the source, the assets are the build" only holds while the two
  agree. Hand-tune an asset the generator authors — four playtest rounds of it — and the next
  person who re-runs the generator silently reverts the lot, with the validator passing
  throughout, because a key-vs-C#-field validator checks that the emitted YAML is *well-formed*,
  not that it is *current*. `author_urchin_assets.py` had drifted far enough that a re-run
  reverted an entire firing pattern (`firingPattern 2 -> 0`, dropping four ring fields outright),
  both triggers' chain depth, and the spike dwell. **The test is one command**: run the generator
  and `git status --porcelain -- Assets`. Empty = the generator still reproduces what ships. Do it
  at ship time for every generator the branch touched, and re-sync the generator (not the assets)
  when it fails.
- **Never move an array's ORDER and the INDEX that reads it in the same change.** They cancel, the
  diff looks substantial, and the runtime behaviour is byte-identical — so the bug survives a
  playtest that specifically checked for it. If you find yourself editing both, you have not
  decided which one is wrong. (Cost here: a whole round on "the domain colour is still on the
  wrong submesh", where the material order and `_domainMaterialSlot` were swapped together.)
- **`using System;` + `using UnityEngine;` is CS0104 on `Object`, `Random` and `Debug`, and this
  repo's convention is a `using` ALIAS, not per-site qualification** (`using Object =
  UnityEngine.Object;` — see `InterfaceReference.cs`, `AOERadialBlocks.cs`, `CSDebug.cs`). A
  naive detector cries wolf on all of them: make it alias-aware (skip a file with
  `^using <Name>\s*=`) or it reports 65 hits where there are two. The alias also covers the
  *next* bare use, which per-site qualification does not.
- **A componentwise divide by `lossyScale` cannot cancel a NON-UNIFORM parent that is also
  ROTATED** — the product shears, and Unity bakes an approximation the moment you
  `SetParent(null, true)`. It is exact for a uniform parent, which is why it passes the case it
  was written for and fails silently elsewhere. Where the child detaches anyway, do not compensate
  at all: apply the intended WORLD size after the detach, when the parent chain is final. If the
  child then spawns more children from itself (a chain reaction), the error COMPOUNDS once per
  generation — the symptom is geometric, not additive.
- **A "this is non-reentrant" comment on static scratch is a hypothesis, and synchronous effect
  dispatch is how it dies.** Dispatching an impact runs its effect list inline; an effect that
  spawns a projectile runs that projectile's `async` body synchronously *up to its first await* —
  which can be past the child's own use of the same static buffers. The parent is then iterating a
  list the child just cleared. Rent buffers by depth (recursion is bounded by the feature's own
  depth cap) rather than asserting the property in a comment.
- **A hardcoded palette entry and a neutral base material look identical in the inspector.** Before
  deciding which material "wears the domain", compare each candidate's authored colours against
  the project's `SO_ColorSet` per-domain entries — `GreenAccentVesselMaterial` turned out to be
  exactly `JadeColors.ShipColor2 x 2`, i.e. one domain's colour welded into every vessel that used
  it. A material whose numbers ARE a domain's numbers is a placeholder, not a design choice.
- **Check "does every `m_Script` guid resolve?" DIFFERENTIALLY, against the merge base.** In a
  headless container `Library/PackageCache` is absent, so every TMP/Netcode/UI script guid looks
  unresolvable and a naive check reports dozens of phantom "Missing (Mono Script)" rows. Compare
  the unresolved set in your version against the unresolved set in `git show <base>:<file>`; only
  the difference is yours.
- **`field_parity.py` is LINE-BASED, so a WRAPPED attribute hides a serialized field from it —
  and it fails in the direction that reads as your fault.** The checker asks "does this line carry
  `SerializeField` or `public`?", so a declaration whose attribute spilled onto earlier lines —

  ```csharp
  [SerializeField, Tooltip("a long tooltip " +
      "wrapped over three lines")]
  Renderer[] additionalRenderedObjects;      // <- invisible to the checker
  ```

  — is simply not in the field set, and the parity run then reports the perfectly-correct YAML key
  you just authored as an *unknown key*. The temptation at that point is to go hunting for a typo
  in the asset, or to conclude Unity will not serialize the field: both wrong, and Unity is fine
  either way (it reads the compiled attribute, not the layout). **Keep attribute and declaration on
  one line** for anything you intend to verify — that is what every existing field in this repo
  does — and leave a comment saying why, or the next formatter re-wraps it and the field silently
  drops out of the checker again. The mirror failure is worse and quieter: a field that SHOULD be
  flagged but is wrapped will never be flagged at all.

- **A correction that runs BEFORE the step it corrects must not also be what CLASSIFIES the
  state.** Server-side containment (reflect + depenetrate) typically runs at the top of a
  FixedUpdate, before the solver integrates — so an object can legitimately END a tick slightly
  past the wall it was just reflected off. Harmless while there is one containment volume. The
  moment there are two mutually-exclusive regimes chosen by a bare position test ("inside the
  sphere" vs "outside it"), that overshoot re-classifies the object and the correction INVERTS:
  the regime that would have pulled it back is replaced by the one that pushes it away, and the
  volume leaks at exactly the moment it was working. Nothing errors, and it is rare enough to read
  as a physics glitch. Fix with a STICKY side plus a dead band sized from the physical bound —
  object radius (containment parks the centre one radius short of the wall) plus one tick of
  travel at top speed (`v * Time.fixedDeltaTime`, read LIVE so hitstop scales it too). Then check
  the regimes are self-reinforcing: each must push AWAY from the boundary, so neither can reach
  the other's flip threshold on its own and only a deliberate crossing (during a suspension of
  containment) ever switches sides.

- **A comparison that mixes a SQUARED quantity with a LINEAR one is invisible to review and to
  every static check, and its symptom is a behaviour nobody attributes to arithmetic.** A field
  named `MinDistance` held a *squared* distance (assigned from `sqrMagnitude` a few lines
  earlier), and the guard read `sqDistance < MinDistance * MinDistance` — a `d⁴` threshold that
  every candidate passed, so a "pick the nearest" loop reliably picked the **last** item and
  re-picked arbitrarily on every refresh. It compiles, it type-checks, the units are invisible
  because C# has none, and the resulting behaviour ("the AI dodges its objective at the last
  second") sounds like a steering bug — which is where two correct-looking steering fixes got
  measured and rejected before the real cause turned up. **When a symptom points at a control law,
  audit the SELECTION feeding it first**, and grep any distance-ish field for whether its writers
  and readers agree on squared-ness. Name squared fields `*SqrDistance` when you touch them.
- **A rule that belongs to a FALLBACK must not be inherited by an explicit provider.** A
  look-direction helper defended its default (a "point at some interesting mass" heuristic) with a
  sensible test — *skip if the target is already roughly ahead* — and when an opt-in provider was
  bolted on for a NAMED target, it inherited that test. The named case is exactly the one where
  "already ahead" is the BEST outcome, so the feature silently never fired: an AI that was
  supposed to swing its nose onto a rival turned away from precisely the rival it had lined up.
  Nothing errors, nothing logs, and the code reads as a careful guard. **When you add an explicit
  provider beside a heuristic default, re-derive which guards were about the HEURISTIC and which
  are about the OUTPUT**, and push the heuristic's own guards down into the heuristic.
- **An AI's engagement range is authored independently of its weapon's, and drifts.** A mode
  capped its AI aim range at 900 while the weapon prefab authored a reach of 2400, so the AI never
  aimed at anything it could actually hit. Two numbers, two assets, no relationship in code — the
  same class as the comeback-rate-vs-target trap. When a doc or a field claims "the AI engages at
  X", go read the WEAPON's authored reach and compare; if they must stay separate, derive one from
  the other or assert the ordering in a test.
- **`IsLocalPilot` and `IsOwner` coincide for every human and diverge for every AI**, so a gate
  that conflates them works perfectly until something autonomous uses it. An owner-write
  `NetworkVariable` published under `IsLocalPilot` is never written for a host-owned AI vessel,
  which means the AI's effect draws on **no machine at all, including the host's** — a failure
  mode with no error and no local symptom for the developer testing solo. Decide per write which
  question you are asking: "is a person flying this?" (`IsLocalPilot`) or "does this machine own
  the object?" (`IsOwner` / `IsNetworkOwner`), and say so in a comment at the gate.
- **Announce a batch removal BEFORE performing it, when the announcer is IN the batch.** A
  ClientRpc is sent through a `NetworkObject`, so a sender that its own call just despawned
  throws instead of broadcasting. The shape that hides it: "detonate everything in this cell,
  then tell everyone how many" reads naturally and is exactly backwards — the count is knowable
  before the loop (same predicate, same list, same frame), and the object is not knowable after
  it. This is not specific to detonation: any "clear/expire/collect all of X, then report"
  where the reporter is an X has the same defect. Send first, act second, and say in a comment
  that the order is load-bearing, because the next reader will want to "fix" it.
- **A per-object tick that can destroy `this` must tell its caller.** `Despawn(true)` destroys
  the GameObject, but Unity defers the destroy to end of frame, so the rest of `FixedUpdate`
  keeps running on an object that is no longer spawned — every NetworkVariable write after that
  point is a write on a despawned object. Give the tick a `bool` return ("I removed myself") and
  bail on it; a `void` tick that can self-destruct is a latent write-after-free that only shows
  up as Netcode warnings under a condition nobody reproduces on purpose.
- **Transform scale is not visible size when the shader displaces vertices — and the error is
  INVERSE in the scale.** Before resizing any model, read its shader for a vertex offset. This
  repo's `SpreadFresnelShader` does `v.vertex += normal * (_Spread / objectScale)`, so shrinking
  an object *grows* its displacement and the visible size does not track the transform. At the
  authored `_Spread: 0.01` a 2× shrink came out at 0.507× (fine, 1.4% off); at `_Spread: 1` the
  same edit would have moved a Ø1.75 dart to Ø1.38 — a 21% change sold as a 50% one. Compute
  `2 × (meshRadius + _Spread/scale) × scale` for both the old and new scale and check the ratio
  is what you promised, rather than reasoning from `localScale` alone. Note the property may be
  authored on a PARENT material and absent from the variant you are reading.
- **A rendered candidate judged at thumbnail size lies, and it lies by AVERAGING.** §4.5b says
  render the candidates and look at them; the completion is *look at them at the size they will
  be judged, and count pixels rather than trusting the look*. A 300 px contact sheet of an
  additive shell read as uniformly magenta and nearly triggered a colour retune; a hue census
  over the same shipped shader measured 7% magenta, and one large panel proved the census right
  — at that scale a blue arc and its red core simply average into the third colour. Build the
  census (bucket the output of the shipped entry point over a population of fragments, after
  tonemapping) *and* render one full-size panel before changing anything on the strength of a
  sheet.
- **A rule that only a SERVER can carry out must be gated on being one, or a local session
  announces work it cannot do.** `IsServer` is false in a no-network local session (the freestyle
  toys mint networked objects with no `NetworkManager`), and the surrounding code often runs
  there deliberately — `if (!IsSpawned || IsServer) ServerFixedUpdate();` is a real, correct
  idiom. So a new rule dropped into that tick inherits the local path for free, where its
  detonations silently no-op (each guarded on `IsServer`) while its RPC throws. Gate the whole
  rule on `IsSpawned && IsServer` rather than trusting the callees' own guards.

### Bundled tool: `field_parity.py`

Beside this skill. `serialized_fields(cs_path)` returns what Unity would serialize
from a C# file (the attribute-stripping trap above is already handled);
`asset_docs(asset_path)` yields `(script_guid, [top-level keys])` per MonoBehaviour
document. ~20 lines of glue maps guid → `.cs` and asserts `keys` are a subset of
`fields` for every doc in every asset you authored. Run it before committing any
hand-written YAML — it is what turns "looks right" into "provably resolves".

## 6. When the editor genuinely IS required

Device soak tests, profiling, visual judgment, play-mode-state measurements,
and final import verification of hand-authored assets. Even then: build the
measuring tool + validator so the human runs ONE menu item and pastes ONE
output back — then YOU act on the numbers (the PhaseThresholds re-baseline
pattern: they ran the measurer, the session authored the six configs from the
pasted output).

**Narrowed 2026-08:** "play-mode measurement" no longer covers a *deterministic*
generator's baseline — see §4.5, which measures it offline and uses the in-editor
measurer as a CONFIRMATION step rather than the source. Keep asking which half of
a measurement is actually play-mode-dependent; often it is neither.

**Visual judgment is the softest of these — simulate it rather than punting it.**
Once §2a has told you what the shader does with a value, you can reimplement that
one path offline and RENDER the candidates: rasterise/raytrace the actual geometry
(a box is a 10-line slab intersection), apply the traced formula, tonemap (ACES) and
sRGB-encode, and lay the options out as a labelled before/after sheet. `pip install
numpy pillow` if the sandbox lacks them. This turns "which of these four palettes is
best?" from a question you ask the human into one you answer and then have them
confirm — and it catches the option that's numerically perfect and ugly (matching a
cool reference hue's chroma exactly turns a warm hue to khaki; only the render shows
it). Ship the sheet WITH the change so the human's playtest starts from your read,
and still say plainly that the sheet is a simulation, not the engine.
