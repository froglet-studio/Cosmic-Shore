---
name: asset-surgery
description: Do "editor-only" Unity work programmatically instead of handing the human an in-editor checklist - ShaderGraph node wiring via JSON synthesis, prefab/scene YAML component surgery, SO asset re-authoring, C# verification without a compiler. Use whenever the plan is drifting toward "I'll prepare instructions and you do it in the editor", whenever a task touches .shadergraph/.prefab/.unity/.asset files directly, or when the human says some flavor of "you can do this" / "write a tool for it". The human's editor time is for PLAY TESTING and things that genuinely need the running editor - not for mechanical asset edits you can machine-validate.
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
- **Splice an edge**: edges are
  `{m_OutputSlot:{m_Node:{m_Id}, m_SlotId}, m_InputSlot:{...}}`. To intercept
  a feed, RETARGET the existing edge's end (don't add a duplicate feeder into
  the same input slot — assert exactly one feeder per input).
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
  fileID and assert the literal does not already occur in the target file —
  any int64 works, but keep a readable family (e.g. `…778`, `…779`) so a human
  reading the diff can see they are yours; (3) insert
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
- **Self-check the surgery by resolving every local fileID.** After any add or
  excise, parse the file: collect `^--- !u!\d+ &(\d+)` as definitions and
  `fileID: (\d+)\}` as references, then report references with no definition —
  filtering the ones followed by `guid:` (those are cross-asset and legitimate).
  A survivor is either a dangling ref you just created or, just as usefully, a
  **pre-existing** one you must not be blamed for: run the same check against
  `git show HEAD:<file>` to tell the two apart before reporting anything.
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
- **Read authored MESH geometry without opening Unity** (which end is the apex?
  where's the pivot? is +Y up?): a `.asset` mesh carries `m_LocalAABB` for
  extents and the vertex buffer as hex in `m_VertexData`/`_typelessData`.
  Derive the stride from `m_Channels` (offsets + dimensions; pos is offset 0,
  3 floats), then `struct.unpack_from('<fff', bytes.fromhex(h), i*stride)` per
  vertex. Bucketing the positions by one axis answers orientation questions
  outright — a cone's apex is the axis end with ONE unique (x,z), the base is
  the end with a ring of them. This is how a claim like "the apex sits at the
  container origin" gets PROVEN instead of assumed from a comment.

## 4. Technique: C# verification — get a real compiler first

**Try to actually compile before falling back to inspection.** `apt-get install
mono-mcs` gives you `mcs`, and a Unity gameplay file usually touches a small,
stubbable surface. Recipe (validated 2026-08 on two ~400-line generators, both
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
   THROWAWAY COPY, never the real file: target-typed `new(...)` → `new T(...)`,
   `x is A or B` → `(x == A || x == B)`. Assert zero bare `new(` remain, or the
   parse dies at the first one and every later error is cascade noise that will
   waste your time.
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

## 5. Traps learned the hard way (check these BEFORE debugging for an hour)

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
  round proving that. Cosmic Shore has bitten twice here (AOE blast `Inertia`
  vs `PrismExplosion.maxSpeed` 33.33 with a ~222 u/s input; the hull ram vs the
  same clamp's FLOOR). Symptom to recognize: every instance produces the
  IDENTICAL magnitude regardless of cause. Fix by putting the path on a
  true-velocity contract that supplies its own ceiling — never by widening the
  shared clamp, which retunes every other consumer of it.
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
- **Verify the bug before fixing it.** A report describing code behaviour
  ("it's using the sphere centre") may predate a fix that already landed. Read
  the live path end to end and check `git log` on the file FIRST; report
  "already fixed in <sha>, here's the proof" rather than re-fixing correct code
  or, worse, inventing a change to look responsive.

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
output back — then YOU act on the numbers.

**Narrowed 2026-08:** "play-mode measurement" no longer covers a *deterministic*
generator's baseline — see §4.5, which measures it offline and uses the in-editor
measurer as a CONFIRMATION step rather than the source. Keep asking which half of
a measurement is actually play-mode-dependent; often it is neither.
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

## 6. When the editor genuinely IS required

Play-mode measurements (baselines, profiling), device soak tests, visual
judgment. Even then: build the measuring tool + validator so the human runs ONE
menu item and pastes ONE output back — then YOU act on the numbers
(the PhaseThresholds re-baseline pattern: they ran the measurer, the session
authored the six configs from the pasted output).

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
