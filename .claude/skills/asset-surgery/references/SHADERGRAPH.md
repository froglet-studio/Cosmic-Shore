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

