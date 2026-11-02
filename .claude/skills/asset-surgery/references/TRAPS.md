## 5. Traps learned the hard way (check these BEFORE debugging for an hour)

- **The Write tool turns a `\u2028` / `\u2029` escape in the text you write into the RAW character.** A regex or a
  string that names the Unicode line separators (`/[\u2028\u2029]/`, `'\u2028'`) lands on disk holding U+2028 itself,
  which JavaScript and C# both read as a LINE BREAK: node fails with `Invalid regular expression: missing /`, Roslyn with
  `CS1010: Newline in constant` (claude/affectionate-bardeen-pzh99d, 2026-10-27: `faction-chat.js`, `FactionChat.cs` and
  its tests, all three). After writing any file that names them, re-escape on disk:
  `t.replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")` in Python, or write them as `(char)0x2028`.
- **Unity wraps a long reference line AFTER the guid, so `"guid: X, type: 2"` is not how every reference looks.**
  `vesselImpactorDataContainerSO: {fileID: 11400000, guid: 769c…,\n    type: 2}` and the same in a nested-prefab
  `objectReference:` override. A guid swap or a presence check written as the one-line form silently matches nothing
  (it did, 2026-10-22, `author_goby_assets.py`'s container swap; its `--check` caught it). Match `guid: X,` alone:
  guids are unique, the `type` adds nothing.

### Trap: a guid sweep written as `for f in $(git ls-tree ...)` silently drops every path with a SPACE

`for f in $(...)` word-splits on whitespace, so `Assets/Shift - Complete Sci-Fi UI/Textures/...`
arrives as the four tokens `Assets/Shift`, `-`, `Complete`, `Sci-Fi`, and every `.meta` under that
tree contributes nothing. The sweep does not error — it completes, prints a total, and the total is
**plausible**: a removal proof that should have found 9 referenced vendor guids found 5, with the
four missing ones being exactly the pack whose name has spaces in it. Nothing distinguishes that
from a correct answer except knowing what the answer should be.

This project has at least two such trees (`Assets/Shift - Complete Sci-Fi UI/`,
`Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/`), so any `.meta`/guid/asset sweep must be
null-delimited:

```sh
git ls-tree -r -z --name-only "$REF" -- 'Assets/Some Tree With Spaces' \
  | while IFS= read -r -d '' f; do
      case "$f" in *.meta) git show "$REF:$f" | sed -n 's/^guid: //p';; esac
    done | sort -u
```

`find -print0 | while IFS= read -r -d ''` and `git grep -z` are the same shape. The tell that you
have been bitten: a count that is lower than expected and whose *missing* members all live under
one directory.

**The general rule — a measurement that cannot fail loudly must be cross-checked against a second,
independent derivation.** Here the cross-check was free: the re-point tool's own remap table names
exactly which guids should have been found, so comparing the sweep's output against it turns a
silent undercount into an immediate mismatch. Prefer a sweep you can check against something you
already know over a sweep you can only read.

### Trap: a fault that comes and goes across builds has an UNCONTROLLED VARIABLE, not a cause in your diff

You ship a change, the human playtests, it is broken. You ship another, it works. Another, broken
again. The temptation — and it is very strong, because it is the only data you have — is to diff
your own commits and blame whatever correlates. Over four builds a HUD widget correlated *perfectly*
with mouse steering dying, the flight path was proven byte-identical between the working and broken
builds, and the widget was defaulted off on that basis. It was wrong: the fifth build had no widget
and no steering, and the real cause was a **gamepad plugged in on the human's desk**, actuating on
its own and taking the input family every frame. It had been present and varying the whole time,
mentioned once in passing (*"it shouldn't matter if I have my game pad on"*), and read as a
requirement rather than as evidence.

So: **before believing a correlation across playtest runs, enumerate what else changed between the
runs** — hardware attached, settings, which scene, whether they went through a menu, how long they
played. Ask. A correlation over four samples with an uncontrolled variable is a hypothesis; shipping
it as a finding costs a real change (here, disabling a working feature) and buys nothing. The
counterpart is cheap and should come first: **make the system report its own state** so the next run
produces a fact instead of another correlation — one unconditional warning naming which link in the
chain is dead beats four rounds of inference. A diagnostic behind a log channel you must enable
first is one nobody has when the fault happens.

### Trap: when something WORKS and you cannot run it, do not refactor it for elegance

A self-installing UGUI widget was drawing correctly. While hunting an unrelated bug it got tidied —
the explicit `typeof(CanvasRenderer)` dropped in favour of `Graphic`'s `[RequireComponent]`, and the
component moved out of the `GameObject` constructor to an `AddComponent` after parenting, reasoned
from how `Graphic.OnEnable` caches its canvas. Every step was defensible and the widget stopped
drawing entirely, costing a playtest round to find and a revert to fix. **Empirical known-good beats
a tidier construction order every time in code you cannot execute.** If a cleanup is worth doing,
do it in its own commit with nothing else in it, so the next playtest bisects it in one step —
never fold it into a fix for something else.

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
- **…and a fifth that the repo cannot show at all: a BRAND-NEW ScriptableObject type whose first
  asset was written outside Unity can load as null in the playtester's editor with all four
  checks passing.** The Butterfly bloom's `ExplosionScaleDustPrismEffectSO` asset did exactly
  that three playtests running (`explosionPrismEffects[0] is empty`), and changing the asset's
  bytes to force a re-import did NOT fix it — so "imported before its script compiled" was a
  guess, not the cause, and the cause was never found because nobody looked at the asset in the
  inspector. Two rules came out of it. **Ask for the inspector first**: one sentence from the
  human ("select X — what does the inspector show?") names the cause, where each speculative fix
  costs a full playtest round. **Prefer a field on something already proven to load**: the fix
  that worked moved the reference onto a MonoBehaviour on the blast prefab (a new script too, but
  a component), pointing at an EXISTING-type asset the game already loaded elsewhere — and an
  `IExplosionPrismPayload` seam on the impactor took the place of the container slot. When a new
  SO type is unavoidable, have the human open its first asset once before the playtest.
- **The Amoebius port is a real compile of `Assets/_Scripts`, available in about a minute.**
  `Port/src/CosmicShore.Player` live-compiles the game's own source against the port's Unity API
  surface, so `dotnet build Port/src/CosmicShore.Player` binds method bodies — not the
  syntax-only Roslyn pass §0.05 of `/ship` warns about. No SDK in the container? `curl -sSL
  https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /opt/dotnet`
  works through the agent proxy. The build is RED on bleeding-edge for reasons outside your diff
  (engine gaps: on 2026-10-08, `Graphics.CopyTexture` in `WormholeMouth.cs` and
  `Physics.OverlapBoxNonAlloc` in `NestedGyroidFlora.cs`), so filter the error list to the files
  you touched rather than reading "N errors" as yours, and confirm the synced copy of each new file
  is under `Port/src/CosmicShore.Live/obj/live-src/`. It caught nothing on the bloom branch and
  that is the point: "no errors in any touched file" is evidence a Roslyn syntax pass cannot give.
  It does NOT exercise Unity's asset loader — the null-loading asset above compiled cleanly here.
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
- **A SIGNATURE stub harness cannot be used to MEASURE behaviour, and it answers confidently.**
  The harness you build to type-check a file (§4) is deliberately signature-only — `Mathf.Min(a, b)
  => a`, `Mathf.Clamp01(v) => v`, enough to bind and no more — and that is correct for its job. Run
  the same file to read a NUMBER out of it and every one of those stubs is a silent lie: a session
  reused its type-check harness to measure an eased envelope and got `E(h)/h = 999.99994`, which
  looks like a real measurement of a broken function rather than a broken measurement of a real
  one. The two harnesses want the same source and DIFFERENT stubs, so keep them as separate
  projects — one with signature stubs for binding, one with a faithful `Mathf`/`Vector3` for
  execution — and say in each stub file which it is. *Whenever a harness starts producing numbers
  instead of diagnostics, re-read its stubs before you believe one.*
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
- **A port that skips a hunk because "the asset it names is gone" must resolve the GUID, not the
  name.** The Stoat port (`16b41b045`) left out the source branch's Cell Selector entry in
  Menu_Main, reasoning that it named a "Black Hole cell" the branch no longer carried. That cell had
  been RENAMED to the Crystal Wormhole Cell with its guid kept (`git log --name-status` shows
  `R100`), so the entry named the very config the port brought over. The cell then sat selectable
  by nothing until `CrystalWormholeTests` went red. Before dropping a hunk as dangling, grep the
  hunk's `guid:` against the source branch's `.meta` files (`git grep <guid> <branch> -- '*.meta'`).
  A hit means the reference is live, whatever the folder is called now.

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
- **Adding a property to a shader SUBSCRIBES it to every component that already drives that name —
  sweep the WRITERS of each name you add, not just the readers of each name you drop.** The mirror
  of the bullet below, and it shipped (2026-10, omni crystal): the new omni shaders honoured
  lowercase `_opacity` "so the crystal blooms in", which wired them to `FadeIn`, whose curve is slow
  and back-loaded (under 10% for the first second, full at 2.9 s at 60 fps). The old ShepardGraph
  shells only had uppercase `_Opacity`, so the omni had always appeared at once; the playtest report
  was "the new crystal takes longer to appear" in Skim Race. Before giving a shader a property
  name, `grep -rn 'PropertyToID("<name>")'` and read what each writer DOES with it, at what rate.
- **A domain/team material set is an index-wise CLONE of a base set, so re-authoring a prefab's
  per-slot materials without the base set leaves its team version on the old ones.** `ThemeManager`
  builds each domain's crystal materials by `new Material(BaseMaterialSet.CrystalMaterialN)`, and
  `Crystal.ChangeDomain` swaps slot N to that clone — so a prefab rebuilt around new materials still
  turns into the OLD geometry the moment a domain owns it (the omni's team version ran a Shepard band
  on its body: the "scaling issues" report). When a prefab's slot materials change, grep
  `GetTeam*Material(` and the base set's fields, and repoint both in the same change.
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
- **A Unity fileID is SIGNED, so `&(\d+)` silently skips every document with a negative anchor.**
  The §3 add-a-component bullet already warns that a fileID is a signed int64 on the WRITE side
  (a 19-digit random overflows it); the READ side has the mirror hazard and it is quieter. A
  census regex of the shape `^--- !u!(\d+) &(\d+)$` parses most of a file perfectly and drops
  the handful of documents Unity happened to number negatively — so an audit reports a clean
  subset and you conclude the thing you were looking for is not there. Cost here: a
  "which vessels carry which transformer" sweep lost the Grizzly entirely and reported six
  one-thumb hulls instead of seven, with every other row correct. Match `&(-?\d+)`, and
  sanity-check any census against a total you already know (`ls *.prefab | wc -l`) rather than
  against how plausible the output looks.
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
- **"Referenced by nothing" is measured against whatever you grepped, and an ANIMATOR references
  CLIPS by the model's guid.** A liveness sweep over prefabs and scenes is the obvious one and it
  misses the case that costs you: a model with ZERO prefab references can still be supplying
  animation clips to a shipped object, because the reference lives in an `AnimatorController`'s
  `m_Motion` entries and points at the model's guid, not at the model as an object. Two Cosmic
  Shore vessel models sat on a delete list that way — one supplying 7 clips to NINE vessels — and
  the documentation that cleared them was written from a prefab-and-scene sweep. Resolve liveness
  in TWO steps and never one: grep the guid across `*.controller`/`*.overrideController` as well,
  then **resolve each referring controller to the prefabs that use IT**, because a controller can
  itself be dead (this project had two same-named `MantaAnimatorController`s, and the one five
  vessels use is not the one in `_Animations/`). A reference count is not a liveness measurement
  until every referrer is itself resolved.
- **An overlap score is meaningless when the baseline overlap is already ~0 — run the control
  against the SHIPPED asset before reading a low score as a regression.** Validating "does the new
  geometry still sit inside the collider that was authored for it" by scoring containment gives a
  number that looks decisive and is not: if the collider never bounded its own geometry in the
  first place, the score is ~0 either way and reads as "my change broke it". Measured on the
  Urchin: 3.58% for the shipped hull against 3.54% for the replacement, i.e. the swap was exactly
  neutral and the colliders were already loose — a real but SEPARATE pre-existing defect, and not
  a reason to hold the change. Always compute the same score for the asset you are replacing.
  Its companion: **a weak discriminator collapses onto the dominant element.** Matching a part to
  "the nearest bone" by centroid, and then by nearest skinned vertex, both picked the body bone
  for every appendage on a radially symmetric hull and flagged a correct mapping as wrong twice.
  Score by something DIRECTIONAL and size-aware (what fraction of each bone's geometry falls
  inside this part's own volume), and treat two cheap metrics agreeing as one metric.
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
- **A shader parameter documented as "unit-free" is unit-free only under a UNIFORM scale, and a
  header claiming otherwise will name its own counter-examples.** `SpindleSway.hlsl` bends a limb
  with a first-order shear, `offset.x = Amplitude * PositionOS.z * sin(...)`, so `Amplitude` is a
  dimensionless SLOPE and the header said it therefore "transfers across meshes that disagree
  about scale by three orders of magnitude". It does not: the shear is evaluated in OBJECT space,
  so a renderer carrying `localScale (sx, sy, sz)` deflects its tip by `atan(Amplitude * sx / sz)`
  in WORLD terms — a mesh stretched along its own bend axis bends that much LESS. At the shared
  0.08 the uniformly-scaled creature spindles leaned 4.57° and every branch-family spindle
  0.64–1.48°, i.e. the lattice species read as dead while wearing the material that made the fish
  wave. The reassuring clause in the header (*"every shipped spindle prefab is scaled on z to
  match (Branch 6.2, TadpoleSpindle 3.0)"*) named the two prefabs that DISAGREE — **a sentence
  offered as evidence for a claim is the first place to check the claim**, because whoever wrote
  it had the numbers in front of them and drew the wrong conclusion. Two general rules: any
  normalized/unit-free/"scale-free" parameter consumed in OBJECT space is a claim about the
  transform above it, so measure the tip deflection per prefab before sharing one material; and
  when the fix is per-mesh, prefer **per-mesh MATERIALS with a solved constant** over a per-mesh
  shader branch — the solve is offline arithmetic (`author_lattice_spindle_materials.py` reads
  each prefab's own stretch and back-solves the amplitude for one authored angle), and the shader
  stays one expression. Target the ANGLE, not the offset: equal angle is equal FRACTION OF THE
  LIMB, so one number serves a family whose limbs span 3–24 world units and survives a later
  uniform rescale of the whole family.
- **An asset re-pointer is idempotent only if it asserts the END STATE, never a swap COUNT.** The
  natural shape for "swap every renderer on this prefab onto the new material" is to count the
  `guid:` substitutions and `assert swapped == len(renderers)` — which passes on the first run and
  FAILS on the second with `expected 2 references to swap, swapped 0`, because the work is already
  done. That makes the tool un-re-runnable and makes `--check` impossible, which is the whole
  contract (§1.2). Assert instead that every named renderer now carries the new guid, and give the
  "carries neither the old nor the new one" case its own error message — that is the only genuine
  failure, and it is a hand-edit somebody else made, not your re-run.
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
- **`field_parity.py` reads ONE `.cs` and does not walk the INHERITANCE CHAIN.** A component
  whose class derives from another serializable MonoBehaviour (`WormSegmentFauna : Fauna`,
  and every `Flora`/`Fauna` subclass in this repo) has its base class's serialized fields
  written into the asset by Unity, and the checker — which only knows the leaf file — reports
  every one of them as an *orphan key*. On the worm prefabs that is eight phantom orphans
  (`cellData`, `domain`, `diet`, `starvationSeconds`, …) and a red `FAIL` on a tree that is
  perfectly correct. It reads exactly like the wrapped-attribute case below and has the
  opposite cause. **Union the chain before comparing**:

  ```python
  base   = serialized_fields("…/Fauna.cs")
  fields = serialized_fields("…/WormSegmentFauna.cs") | base
  ```

  A useful side effect: the *missing* direction (a C# field with no key in the asset) is the
  honest signal that a newly added field has never been written by an editor import — worth
  reading rather than filtering out.
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
- **A Shader Graph property's authored DEFAULT is not the shipped material's value, and
  `new Material(Shader.Find(...))` gives you the defaults.** Reading a `.shadergraph`'s
  property block feels authoritative and is the wrong source, exactly as an SO's field
  initializer is the wrong source for an authored value. On Cosmic Shore's `BlockGraph`
  the gap is fatal rather than cosmetic: `_Alpha` defaults to **0** while
  `PrismMaterial.mat` sets **1** with `_AlphaClip: 1` / `_AlphaToMask: 1` and the
  `_ALPHATEST_ON` keyword, so a bare mint is a correctly-tinted prism that alpha-clips
  to **nothing** — invisible, with no error anywhere. **Clone a shipped material
  (`new Material(template)`) rather than minting from a shader**: a clone carries every
  render-state property AND the shader keywords, while a synthesised material has to
  restate them and can only restate the ones you thought of. Dump the graph's defaults
  and the `.mat`'s `m_SavedProperties` side by side before trusting either;
  `m_ShaderKeywords` in the `.mat` is the half a property dump cannot show you.
  (`AstroLeagueBall` mints a `BlockGraph` material and sets `_Spread` but not `_Alpha` —
  a latent instance of exactly this, found by the same comparison.)
- **Confirm a magic string by finding an existing SHIPPED call site, not by deriving it.**
  A shader name (`"Shader Graphs/BlockGraph"`), a property name, an animator parameter, a
  `Resources.Load` path: deriving it from the asset (graph `m_Path` + filename, say) gets
  the right answer often enough to be dangerous. Grepping for another runtime call that
  already uses the identical string proves three things at once — the string is right, the
  property names alongside it are right, and the asset is reachable in a build (something a
  `.meta` file cannot tell you). One grep replaced three separate assumptions here. If no
  call site exists, you are the first, and the string is a hypothesis to be defended in the
  PR body rather than a fact.
- **A rule that only a SERVER can carry out must be gated on being one, or a local session
  announces work it cannot do.** `IsServer` is false in a no-network local session (the freestyle
  toys mint networked objects with no `NetworkManager`), and the surrounding code often runs
  there deliberately — `if (!IsSpawned || IsServer) ServerFixedUpdate();` is a real, correct
  idiom. So a new rule dropped into that tick inherits the local path for free, where its
  detonations silently no-op (each guarded on `IsServer`) while its RPC throws. Gate the whole
  rule on `IsSpawned && IsServer` rather than trusting the callees' own guards.
- **A mesh whose vertex shader REBUILDS geometry from per-vertex data cannot be moved by a CPU
  consumer that draws it on the same material - unless the shader's undriven path is the identity.**
  `OmniDepthPrismShader` rebuilt every vertex from TEXCOORD2 (its prism's centre) and stretched it by
  the material's `_Thickness` 6.25. `CrystalHullFusion` re-tessellates the crystal onto a vessel hull
  and draws that copy on the crystal's own material, with TEXCOORD2 holding ITS data (discharge
  heights, zero on filler vertices): the stretch ran against the wrong centres and blew the Mass
  faces 36 mesh units apart, and `normalize(0)` made filler vertices NaN. Fix shape: bake every
  shape parameter the shader applied into the mesh (the material ships the neutral value), make the
  undriven branch return the vertex untouched without reading the per-vertex channel, and have the
  consumer force undriven. Prove it with a formula identity (max |p - v| at float32) and a negative
  control that leaves the parameter in (238 units). Grep for every renderer that borrows a
  material's `sharedMaterials` before giving a shader per-vertex semantics.

### Trap: a generator that EMITS OVER the committed scene keeps every node it no longer writes

A `Patch`-style generator (`author_start_flow.py`, the `HM.Patch` family) reads the committed scene,
re-emits its nodes by deterministic key and writes the result. Removing a node from the recipe does
NOT remove it from the scene: the old GameObject, its children, and any component whose key the
recipe dropped (a `HorizontalLayoutGroup`, a `LayoutElement`) stay in the file - orphaned from their
GameObject's `m_Component` list or still listed under the parent - and `--check` passes, because the
recipe re-derives the same file it read. R9b (2026-10-18) removed the sign-in chooser's Steam button
and three kit-button parts per row; all of them survived the first regeneration. Fix in two halves:
an explicit retirement list the generator deletes BEFORE emitting (`remove_go` per retired key,
`sc.remove` per retired component key), and an integrity check in the validator that fails on a
transform its father does not list or a component its GameObject does not list (exempt stripped
prefab-instance owners - components added to an instance are legitimately unlisted). Negative-control
it by appending an orphan component doc. A prefab generator that writes the whole file from scratch
does not have this problem; a scene patcher always does.

### Trap: hand-edited single-quoted YAML with an apostrophe, and unquoted text with ": "

Two shapes of the same defect, both invisible to Unity's lenient loader in review and both fatal to a
strict YAML read (and to any audit that uses one). A single-quoted scalar must double its apostrophes
(`'the Rhino''s own blade'`); one `Rhino's` ends the string early and the rest of the line is garbage
(Garrett's hand-edited `Rhino.asset`, 2026-10-17, found by `check_hangar_ability_kinds.py`'s
`yaml.safe_load`). And a plain scalar cannot contain `": "` or be a lone `-`: `m_text: Core: skim...`
and `m_text: -` shipped from the kit's text writer until `author_ui_kit_prefabs.tmp` routed `m_text`
through `yaml_scalar`. When a generator writes text, quote it through one helper; when an asset was
hand-edited, `yaml.load` it with a tag-tolerant loader
(`L.add_multi_constructor('tag:unity3d.com', lambda l, s, n: None)`, documents split on `^--- `)
before trusting it - 58 assets parse in a few seconds.

### Technique: extracting a macOS `.dmg` on Linux with nothing installed

No `7z`, `hfsplus` or `dmg2img` in the container. A UDIF `.dmg` ends in a 512-byte `koly` trailer
whose offset 0xD8 gives the XML plist; its `resource-fork/blkx` entries are `mish` block tables
(40-byte runs: type, sector, count, compressed offset/length; `0x80000005` = zlib, `0x80000006` =
bz2, `1` = raw, `0`/`2` = zeros). Decompressing every run rebuilds the raw disk; the HFS+ volume
header sits 1024 bytes into the Apple_HFS partition (`H+`), and walking the catalog B-tree's leaf
nodes (folder records type 1, file records type 2, data fork extents at record offset 88) recovers
every file - about 90 lines of Python, used for Apple's Sign in with Apple download (2026-10-18).
**Read the licence that comes out of it before using anything else:** Apple Design Resources allow
mock-ups for Apple-only software and forbid embedding, so those logo files cannot ship in a
cross-platform build; Apple's button image service (`appleid.cdn-apple.com/appleid/button`) is the
route for embedded button art.

### Trap: the refcompile's .NET SDK can vanish from a long-lived container

`unity_refcompile/run.sh` answered `no .NET runtime under /root/.dotnet/shared/Microsoft.NETCore.App`
mid-session, though it had run hours earlier (the sentinels were still there; the runtime was not).
Reinstall with `dotnet-install.sh --channel 8.0 --install-dir /root/.dotnet` (retry with backoff:
the first TLS connect to `builds.dotnet.microsoft.com` can drop), and pass `DOTNET_ROOT=/root/.dotnet`
to `check_generated_assets.py`, which otherwise looks under `/usr/lib/dotnet` and stops.

### Trap: a planted defect that mutates "the first match" silently retargets when the scene grows

`author_start_flow.py --self-test` planted "the retry panel ships shown" by flipping the FIRST
`m_Alpha: 0 / m_Interactable: 0 / m_BlocksRaycasts: 0` block in the splash. When the welcome card (another hidden
group) was emitted before the retry window, the mutation flipped the welcome card instead, a different check caught
it, and the self-test still printed "caught" for the retry panel - a negative control that no longer tests its
subject, reported green (2026-10-18). Target a planted defect by the FIELD that names the object
(`_show_face(text, "retryPanel")`, a fileID read from the component), never by "the first occurrence of a value", and
read the caught message: it must be the check you meant, not a neighbour's.

### Bundled tool: `field_parity.py`

Beside this skill. `serialized_fields(cs_path)` returns what Unity would serialize
from a C# file (the attribute-stripping trap above is already handled);
`asset_docs(asset_path)` yields `(script_guid, [top-level keys])` per MonoBehaviour
document. ~20 lines of glue maps guid → `.cs` and asserts `keys` are a subset of
`fields` for every doc in every asset you authored. Run it before committing any
hand-written YAML — it is what turns "looks right" into "provably resolves".

**Two scope corrections, both learned by shipping a bug the tool would have caught.**
(1) **The ABSENT direction is the dangerous one, and it is not benign.** The trap above
frames a missing key as "silently takes its initializer", which sounds like a tuning
risk; when the field is a REFERENCE type the initializer is `null`, and a component
that guards on null and returns is not degraded — it is **inert**. Everything after the
guard never runs, so the object can spawn, render nothing, and behave not at all, with
one log line in one mode as the only evidence. Diff BOTH ways, always, and treat an
absent reference-typed field as a failure rather than a default.
(2) **Point it at the WHOLE project, not just at YAML you wrote.** The likeliest source
of key drift is not your authoring — it is a prefab Unity serialized years ago against a
field set that has since been refactored. Unity does not migrate those and does not prune
what it cannot resolve, so the dead keys sit there looking authoritative while the new
required one is simply absent. A repo-wide sweep (every `!u!114` doc → its `m_Script`
guid → that `.cs`'s field set) is a cheap standing audit that finds rot no diff will ever
show you. Cosmic Shore's `QuadFish.prefab` carried EIGHT dead keys and lacked its data SO
for long enough to ship inert into three game modes.

### Trap: a hand-written shader's on/off float wants `[ToggleUI]`, not `[Toggle]`

`[Toggle] _Foo ("Foo", Float) = 0` in a `.shader`'s Properties block is not just a checkbox: Unity also
enables a shader KEYWORD `_FOO_ON` when it is ticked. A shader that branches on the float itself (`_Foo > 0.5`)
and declares no `#pragma shader_feature _FOO_ON` then gets that keyword written into every material's
`m_InvalidKeywords` the first time someone ticks the box in the inspector - a diff on a material a generator owns,
so its `--check` goes red for a change nobody made on purpose. Use `[ToggleUI]` (a checkbox, no keyword) for a
uniform branch, and keep `[Toggle]` for a real keyword variant. Seen 2026-10-28 on `OmniCrystalFresnelShader`'s
`_ElementShades` (`cece/serene-cannon-93olp5`), caught before it shipped.

