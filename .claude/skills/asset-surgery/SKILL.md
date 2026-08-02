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
- **Blast radius**: before deleting/renaming any member, grep for every caller
  (`\.Member\b` patterns); after editing, sweep again — the deleted surface
  must appear ZERO times outside historical docs.
- **Comment hygiene**: after deleting a system, grep its NAME across code AND
  docs; rewrite comments that describe the dead architecture as live (they
  become false doctrine — this session found its own outdated rationalization
  quoted in a header comment).

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

## 6. When the editor genuinely IS required

Device soak tests, profiling, visual judgment, play-mode-state measurements,
and final import verification of hand-authored assets. Even then: build the
measuring tool + validator so the human runs ONE menu item and pastes ONE
output back — then YOU act on the numbers.

**Narrowed 2026-08:** "play-mode measurement" no longer covers a *deterministic*
generator's baseline — see §4.5, which measures it offline and uses the in-editor
measurer as a CONFIRMATION step rather than the source. Keep asking which half of
a measurement is actually play-mode-dependent; often it is neither.
