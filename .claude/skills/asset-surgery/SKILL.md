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
- Unity reimports on pull; the in-editor validator + a shader-error check
  (`ShaderUtil.ShaderHasError`) confirm; magenta = `git checkout` the graph.

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
- **Re-author SO numbers**: regex with `re.subn(..., count=1)` + assert n==1
  per field; print the before/after table so the human can eyeball the math.
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

## 4. Technique: C# verification without a compiler

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

## 5. Traps learned the hard way (check these BEFORE debugging for an hour)

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
- **Verify the bug before fixing it.** A report describing code behaviour
  ("it's using the sphere centre") may predate a fix that already landed. Read
  the live path end to end and check `git log` on the file FIRST; report
  "already fixed in <sha>, here's the proof" rather than re-fixing correct code
  or, worse, inventing a change to look responsive.

## 6. When the editor genuinely IS required

Play-mode measurements (baselines, profiling), device soak tests, visual
judgment. Even then: build the measuring tool + validator so the human runs ONE
menu item and pastes ONE output back — then YOU act on the numbers
(the PhaseThresholds re-baseline pattern: they ran the measurer, the session
authored the six configs from the pasted output).
