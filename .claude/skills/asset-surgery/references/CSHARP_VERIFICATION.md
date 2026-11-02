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

To RUN a harness rather than only compile it, emit an exe (`-out:x.exe -main:Driver`) and drop a
`x.runtimeconfig.json` beside it naming the installed shared framework version, or the host aborts
with `libhostpolicy.so ... not found` / "was run as a self-contained app" — which reads as a broken
install and is one missing file:
`{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"<ls dotnet/shared/Microsoft.NETCore.App>"}}}`

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
**whole-assembly** type check used to be impossible and the no-stubs filter below was the fallback.

**Since 2026-10 a whole-assembly check against REAL Unity references exists: run it first.**
`bash Tools/Build/unity_refcompile/run.sh` fetches the 6000.0 engine reference DLLs and every
package at its locked source, then binds ALL of `Assembly-CSharp` (method bodies included) the way
a player build would; `Tools/Build/unity_refcompile/README.md` says exactly what it does and does not
prove. Then `python3 Tools/Build/check_generated_assets.py` audits every changed `.asset` / `.prefab`
/ `.unity` against the schema that compile wrote (serialized keys, enum values, guid/fileID
references, m_Script classes). What cost time on the first runs (2026-10-06 to 10-08):
- **`DOTNET_ROOT` must hold a .NET SDK, 8.0 or newer** (a runtime alone has no reference pack, and
  the tool says so). Until 2026-10-08 it had to be 8.0 exactly: a .NET 10-only `DOTNET_ROOT` died with
  `IndexError: list index out of range` after the full ten-minute fetch. Any SDK from 8.0 works now
  (8.0 and 10.0 verified). Install a channel per-user (above) and point `DOTNET_ROOT` at it;
  `TMPDIR` decides where the ~550 MB cache lands.
  **`run.sh` falls back to `$HOME/.dotnet`, but `check_generated_assets.py` does NOT**: it looks under
  `/usr/lib/dotnet/sdk` and exits **2** with `csc.dll not found` (2026-10-22, `cece/dazzling-volta-apzw2m`).
  Pass `DOTNET_ROOT=$HOME/.dotnet` to it explicitly; exit 2 is "called wrong", not a finding.
- **It catches URP API drift that reads fine from memory.** On URP 17, `UniversalAdditionalCameraData
  .volumeFrameworkUpdateMode` is `internal` (use the `Camera.SetVolumeFrameworkUpdateMode` extension) and
  `clearDepth` has no setter (overlays already clear depth). Both failed this compile on 2026-10-22. The URP
  source it compiled against is under the cache's `graphics/Packages/com.unity.render-pipelines.universal/`:
  read it rather than guessing an API.
- **A warm run takes ~30 s, and the `cached` lines are the evidence.** Count them
  (`grep -c ' cached$'`): about 86 package assemblies should come from the cache. Until 2026-10-08
  NONE ever did, so every run recompiled them all. A stub DLL rebuilt each run sat in every
  package's fingerprint. Nobody noticed, because a cache that never hits looks exactly like a cache.
  A cold count after a warm one means an input moved (a new SDK, a re-fetch). Runs that share a
  cache take turns (`waiting for another unity_refcompile run`). Before that lock, a player run and
  an editor run launched together rewrote the shared DLLs under each other, and `Unity.Entities`
  "failed" with 53 false project errors downstream.
- **`check_generated_assets.py` needs the SAME `DOTNET_ROOT` and `TMPDIR` the compile ran with.**
  Run it bare after a compile that used a per-user SDK and it says `no Roslyn tools in
  <TMPDIR>/unity_refcompile_cache/tools - run run.sh first` — while the tools sit in that very
  directory. It is not missing the cache; it cannot find `csc.dll` under the system
  `DOTNET_ROOT`. Export both for the audit too (2026-10-08). Negative-control it the same way as
  the compile: misspell one key in a changed asset, confirm `[field] 1` names it, restore.
- **"218 errors" is not 218 errors.** Read the `ERRORS in project code:` line. The large bucket is
  files that `using` a UGS package no mirror carries (Multiplayer, Friends, Leaderboards); they are
  counted, not judged. Check your own files are not in that bucket (they would be unverified):
  grep the run's `report.json` for each file you changed.
- **`check_generated_assets.py` used to call a VARIANT-DERIVED fileID "missing"; it resolves them
  since 2026-10-14.** A variant of a variant (Skim Race's `SkimRaceRingCrystal` → `BigCrystalVariant`
  → `Crystal.prefab`) overrides, and is referenced through, objects its source INHERITS without
  writing them out, whose ids are `(instanceFileID XOR sourceFileID) & INT64_MAX` and appear in no
  document of the source file. The reference check only knew anchors, so all four correct overrides
  (root `m_LocalScale`, `SphereCollider.m_Enabled`) came back as dangling. `Audit.has_object` now
  recurses through the source's `PrefabInstance`s; its self-test carries a positive and a negative
  control. If an older checkout still reports such ids missing, check the XOR before believing it —
  the old scene reference to `BigCrystalVariant`'s Crystal (`6588436222817790495`) was exactly that
  kind of id and loaded in play for months.
- **`check_generated_assets.py` used to report every authored UnityEvent as a dropped field; it reads them
  since 2026-10-24.** `m_OnClick: m_PersistentCalls: ...` came back as "`m_PersistentCalls` is not a
  serialized field of UnityEngine.UI.Button.ButtonClickedEvent" (80 findings on shipped prefabs since
  `363d153b9^`). The base chain was walked, but `UnityEventBase` is declared in `UnityEngine.CoreModule`,
  which the schema never read. `Schema/Program.cs` now reads the `UnityEngine.Events` namespace from that
  (depublicized) DLL, so persistent calls, their `ArgumentCache` and both enums are checked like any field.
  If an older checkout still reports `m_PersistentCalls`, it is this false positive, not a defect.
  The same day, the package twin: Netcode's `#if UNITY_EDITOR [SerializeField]` foldout flags
  (`ShowTopMostFoldoutHeaderGroup` on every `NetworkBehaviour`, `NetworkTransformExpanded`) read as dropped
  (73 findings), because package types came only from their player-built DLLs. The schema now also parses
  each package's sources with `UNITY_EDITOR`, so any package's editor-only field counts, on its own class only.
- **The `editor` config is RED on bleeding-edge itself (measured 2026-10-14): 3 errors, all in
  `Assets/_Scripts/System/AuthenticationServiceFacade.cs`** (`Unity.Multiplayer.PlayMode` /
  `CurrentPlayer` — the Multiplayer Play Mode editor package, not obtainable here). A/B it on a
  detached worktree of the base before attributing it (`git worktree add --detach <tmp>
  origin/bleeding-edge`); the line to read is "`0 in files changed since origin/bleeding-edge`".
- **`--config editor` compiles the Editor-folder scripts as their own `Assembly-CSharp-Editor`**,
  referencing the runtime, as Unity does. Before 2026-10-08 it merged the changed ones INTO the
  runtime compilation, so any branch touching one of the 123 `namespace CosmicShore.Editor` files got
  false `CS0118 'Editor' is a namespace but is used like a type` errors in untouched runtime
  `#if UNITY_EDITOR` files. A run from an older checkout still does. Only the CHANGED Editor files
  gate, and the working tree counts (uncommitted and untracked files too), so run it before you
  commit; the run names them after `gated: the N changed since …`. Until 2026-10-08 only COMMITTED
  files counted, and an edited test passed green without being compiled (`SkimRaceAITests.cs`,
  2026-10-06). The errors listed for unchanged files (37 on 2026-10-08) are reference-set artifacts
  (2021.1 `UnityEditor`, no test framework). `EDITOR_REFERENCE_GAPS` in `build.py` matches each one, so they stay unverified
  even in a file your branch changes. A Unity 6 editor API your branch starts using reads as an error
  until it gets an entry there.
- **`check_generated_assets.py` needs `DOTNET_ROOT` exported too, and lies about why when it
  is not.** Without it the audit says `no Roslyn tools in …/tools - run run.sh first` even though
  `run.sh` just populated that folder — it looks for `csc.dll` under `$DOTNET_ROOT`, not under the
  cache. Export the same `DOTNET_ROOT`/`PATH`/`TMPDIR` you gave `run.sh`. It also audits only what
  is COMMITTED since the base (`audited 0 added + 0 modified` on an uncommitted tree is not a pass).
- **In a SPARSE checkout, `check_generated_assets.py`'s `[guid]` findings are mostly the checkout,
  not the asset.** `build_guid_index` walks `Assets/` on DISK, so every `.meta` outside the sparse
  cone is invisible and a reference to it reads `resolves to no asset in Assets/`. A package script
  that a generator ADDED is flagged the same way even though the docstring says package references
  are "counted, not judged": the new `UnityEngine.UI.Image` components on `SerpentHUDVariant.prefab`
  (`m_Script` guid `fe87c0e1cc204ed48ad3b37840f39efc`, the ugui Image) came back as findings, while
  the 14 Images already in that prefab were suppressed as pre-existing. Resolve each `[guid]` line
  before calling it a defect: `git grep -l <guid> HEAD -- '*.meta'` finds an asset the cone hides
  (it found `_Models/Testing/Prism.asset`, guid `8f4a329b…`), and a guid already used elsewhere in
  the same file as an `m_Script` is a package script. Measured 2026-10-13 on
  `claude/serpent-mass-seed-wall`: 3 findings, all 3 false. The fix belongs in the tool (index guids
  from `git ls-tree` plus the refcompile package cache), not in a habit.
- **`check_generated_assets.py` reports a guid-KEEPING RENAME as a `[deleted]` reference.** `git mv a.png b.png`
  with its `.meta` keeps the guid, so every reference still resolves — but the audit sees `a.png` deleted since the
  base and reports `b.png.meta: references a.png (guid …), deleted since the base`. The file it names IS the owner
  now. Confirm with `grep -c "^guid: <g>$"` over the `.meta` set (exactly one owner, the new path) and call it out
  as the tool's false positive (Thresher Heavy Iron → Mass Mode icon, 2026-10-13).
- **`check_generated_assets.py` reports only findings that are NEW against the base's copy of the
  asset — so a negative control must inject a defect the BASE never had.** Re-adding a key you just
  removed (a retired field's `minExplosionScale: 60`, still present in the base asset) is suppressed
  as pre-existing and the audit stays green, which reads as "the audit is blind". Commit a key the
  base never carried (`negativeControlKey: 1`), confirm `[field] 1` names it, then
  `git reset --hard` back (2026-10-08, Rhino crystal-burst field removal).
- **`--config editor` puts a test's `LogAssert` in the "unverified" bucket, not the error count.**
  `UnityEngine.TestTools.LogAssert` lives in the test-framework DLL, which is not among its
  references, so a new edit-mode test that uses it reports `CS0103 'LogAssert'` under *unverified*.
  It is an `EDITOR_REFERENCE_GAPS` entry, so a typo such as `LogAsert` still fails the run. Do
  read the bucket, because everything ELSE in your test file was bound for real.
- **Files that `using` an unobtainable UGS package are bucketed, so your edits in them are not
  gated.** `HostConnectionService`, the party services, `MultiplayerSetup` and `GameDataSO` all
  `using Unity.Services.Multiplayer`. Every method body is still BOUND, so the diagnostics exist
  in `report.json` - intersect them with your diff's changed lines (parse `@@ +a,n @@` from
  `git diff -U0 <base>...HEAD -- <file>` and look for any error at those line numbers). Zero hits
  on changed lines is the evidence; "the run was green" is not.
- **Negative-control both tools before quoting them**: plant a call to a missing member, or a
  misspelt name, in a file you changed. Until 2026-10-08 a missing NAME (`CS0103`, `CS0246`) never
  gated: three packages always fail, and missing-type errors were all bucketed while any had, so such
  a plant only moved the *unverified* count (the 2026-10-08 bug-hunt records describe exactly that).
  Now only a name a failed package declares is bucketed; still read both numbers. The compile must
  fail with that file tagged
  `[CHANGED-TONIGHT]`. For the asset audit, misspell one key in an asset you changed (the audit
  must name the file and the key). Restore, then `git status --short` the paths. Both discriminated
  on their first try here.

**`unity_refcompile`'s engine DLLs are REFERENCE assemblies: they bind, they do not RUN.** Calling
`Vector3`/`Quaternion` from them in a harness throws `NullReferenceException` on the first operator, so
a compile against them cannot execute the shipped maths. To RUN a pure-math class (a predictor, a field
law, a layout routine), compile the SHIPPED file unmodified beside a ~60-line stub `namespace
UnityEngine` (`Vector2`/`Vector3`/`Quaternion` with `AngleAxis`, `*`, `Slerp`, and `Mathf`). Then drive
it from a scratch `Main`, and lift the test bodies out of the `[Test]` file with a ten-line NUnit
`Assert` shim, so they run too. On 2026-10-13 this ran the Stoat's `StoatDipoleMath` and five of its
edit-mode tests, and measured the bug a play report described: the instant path predictor was 15–73 u
off a frame-stepped model of `VesselTransformer` within 3 s. Keep the stub in the scratchpad, never in
the repo, and add members only as the compile asks for them. Also: `verify_black_hole_lens.py` needs
`glslangValidator`; a root container gets it from `apt-get install -y glslang-tools` (after an
`apt-get update`).

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

**When the declaration is not on disk AT ALL, the rule bends and the CLAIM shrinks.** "Grep it,
don't remember it" assumes the type is in the tree; a third-party SDK usually is not — a remote
container has no `Library/PackageCache`, so `Unity.Services.Leaderboards`, FMOD, Netcode and
friends exist only as a line in `Packages/manifest.json`. **Transcribe from the vendor's own
API reference for THAT EXACT VERSION** (read the version out of the manifest first, then fetch
`docs.unity3d.com/Packages/<pkg>@<major.minor>/api/...` — the per-version page, never a search
result or a memory of a different major), and check the members you actually touch: a property's
nullability changes the call (`GetScoresOptions.Offset` is `int?`, inherited from
`PaginationOptions`, so an `int` only compiles via implicit conversion), and a collection's
concrete type decides whether `.Count` exists (`LeaderboardScoresPage.Results` is
`List<LeaderboardEntry>`).

Two rules keep such a harness honest. **Say in the stub file which stubs came from the tree and
which from docs**, one comment line — a reader must not mistake it for a transcription of source.
And **state the claim correctly in the report: a docs-transcribed harness proves YOUR C#, not the
SDK contract.** It catches your typos, wrong arity, bad control flow and dead branches; it cannot
catch "that method does not exist", because you wrote the stub that says it does. So it is worth
running (a 2026-09 session type-checked a rewritten UGS leaderboard service clean this way) and
it is worth labelling — the one thing it can never do is verify the assumption it is built on.

**A file with NO third-party dependency at all gets the strongest form and costs nothing: compile
it for real, then RUN it.** Pure logic — a parser, a formatter, a filter, a math helper — usually
lives in `_Scripts/Data` or `_Scripts/Utility` and imports only `System`, so it needs zero stubs,
and a ~60-line `Driver.cs` with a `Main` that asserts against real inputs is executable proof
rather than a type check (emit `-target:exe -main:Driver` and drop the `runtimeconfig.json` beside
it, per the recipe above). Reach for this FIRST and split the work toward it: the same session put
its hand-rolled metadata parser and its list filter in the dependency-free struct and left only
wiring in the SDK-facing service, which turned the interesting half of the branch into 47 executed
assertions with negative controls.

**Trap: `csc | grep` reports GREP's exit status, not the compiler's.** A shell pipeline exits with
its LAST command, and `grep -v` returns 1 when it filters everything out — so a perfectly clean
compile piped through a banner filter prints `exit=1` and reads as a failure. Redirect to a file
and check `$?`, or read `${PIPESTATUS[0]}`. Never let a filter stand between you and a verdict you
are about to report.

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

**The corollary for a BASELINE ERROR-SET DIFF — the standard "before == after, so my edit is
clean" move.** That diff is only evidence if the harness can see the lines you changed, and the
blindness above is per-file: a file whose base class is unresolved contributes a fixed set of
declaration-level diagnostics that is *identical* before and after any body edit, so the diff
comes back empty for a correct change and for a broken one alike. Measured in one session: a
reduced island around `ExplosionImpactor` reported 14 errors with a merge resolution intact and
**the same 14** with a call in that very hunk renamed to a nonexistent method. The fix is cheap —
add the real dependency files until the base binds (four here: the base class, its interface, one
struct, plus a small stub file for the engine types), then inject the typo INTO YOUR OWN HUNK and
confirm exactly one new `CS0103` appears. Only then is "before == after" worth reporting, and only
then should you diff your merged file against **both** parents rather than one.

**A second, distinct blind spot: an error-typed OPERAND suppresses diagnostics on the whole
expression it sits inside — even in a class whose base DOES bind.** The table above is about a
base class failing to resolve (`MonoBehaviour`/`NetworkBehaviour` with no Unity assemblies), which
stops an entire method body from being checked. This is different and applies even to an ordinary
`EditorWindow` subclass, whose declaration and body both bind fine: **any single unresolved API
inside an expression** (a no-stub reference-less compile means `EditorGUILayout.HelpBox`,
`MessageType`, and every other editor-only type ARE unresolved) makes that whole expression
error-typed, and Roslyn does not bother emitting secondary diagnostics — `CS1503` (argument type
mismatch), `CS0201` (invalid expression statement) — on an expression it has already flagged as
broken by an unrelated unresolved symbol. A merge-damaged `HelpBox(...)` call that had silently
gained a third string argument (a diff3 "keep both" resolution splitting one string-concat argument
into two — see the ship skill's "keep both is wrong inside a chain") and a `return` statement split
into two statements by the same pattern **both compiled with zero errors** under the no-stub
30-second pass, because the call target `EditorGUILayout.HelpBox` was itself unresolved and every
diagnostic downstream of that got swallowed. Reproduced directly: compiling the actual broken
commit with the no-stub filter reported NOTHING; the faithful-stub harness (real `EditorWindow`,
real `EditorGUILayout.HelpBox(string, MessageType)`, real `MessageType` enum) caught both errors
immediately. **The practical rule: the no-stub 30-second pass is not a safe substitute for the
stub harness on ANY file that calls into an API area you have not stubbed — including plain editor
utility code, not just `MonoBehaviour`/`NetworkBehaviour` bodies** — and a "verified clean" claim
from the no-stub pass alone, on a file touched by an additive merge resolution, should be treated
as unverified until re-checked with real signatures for the specific APIs that expression calls.

**Stub a reference type as a `class`, never a `struct` — the diagnostic does not name the
mistake.** Getting a stub's KIND wrong is a different failure from getting its members wrong:
a member gap says `CS1061 'Foo' does not contain a definition for 'Bar'` and points straight at
the fix, while declaring `PrismProperties` as a `struct` when the real type is a `class` surfaces
as `CS0173: Type of conditional expression cannot be determined because there is no implicit
conversion between 'PrismProperties' and '<null>'` — an error about the CALLER's ternary, in a
file you did not write, naming nothing about the stub. Anything the target code compares to
`null`, pattern-matches with `is { … }`, or assigns `null` to must be a class. Grep
`class X` / `struct X` in the real source rather than inferring from usage; it is one grep and it
is the difference between a five-minute harness and a confusing one.

**A stub of a PROJECT type cannot verify an accessor PATH through it — compile the real file
instead.** The rules above are about stubbing ENGINE types faithfully. The trap is different when
the type you stub is one of ours: writing the stub is the moment you decide what shape it has, so
the harness confirms whatever you assumed and the assumption is precisely what you needed checked.
A session stubbed `SO_ColorSet` with a `DarkCTA` field, compiled `theme.ColorSet.DarkCTA` green,
and shipped a file Unity rejected with `CS1061` — the real `DarkCTA` lives on a nested
`EnvironmentColorSet` reached through `ColorSet.EnvironmentColors`. No amount of stub discipline
finds that, because the stub IS the claim under test. **So: never stub a first-party type whose
member layout your new code depends on. Add its real `.cs` to the compile** (it usually drags in
only a couple more engine stubs) and put the expression you are about to write in a two-line probe
file next to it:

```csharp
// ColorProbe.cs — compiles the exact accessor chain the real call site will use.
public static Color Lime(ThemeManagerDataContainerSO theme)
    => theme && theme.ColorSet ? theme.ColorSet.GetCtaSignalColor() : Color.white;
```

Then **prove the gate**, the same way the base-class table above was produced: compile the probe
with the WRONG path first and confirm you get the exact `CS1061` the editor gave, before fixing it.
A harness that has not failed on the defect you are hunting is not a harness — and here the
negative control is one line, so there is no excuse for skipping it.

**The sibling of this in ASSET space: a value read off a class's field initializer is not the value
the game runs on.** The same session read `DarkCTA`'s meaning from the C# and the shipped palettes
disagreed — two of the three author it `(0,0,0,0)`. Whenever the code you are writing turns on an
authored value, grep the `.asset` YAML for every instance of it and tabulate the real spread before
deciding anything; the class tells you the type, the assets tell you the number.

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

### Technique: SOLVE a UI rect in canvas pixels offline (and prove two widgets are disjoint)

"Does this button sit on top of that one?" is answerable from the scene YAML alone, and it is
the question a rendered-frame report ("a strange button over the play button, it goes away when
clicked") reduces to. Walk the `RectTransform` chain from the canvas root, taking the root's size
from the scene's `CanvasScaler.m_ReferenceResolution` (never from the root's own anchors — a
canvas root is `(0,0)-(0,0)` with zero `m_SizeDelta` and measures 0×0 walked naively), and for
each level compute `size = (amax − amin) ⊗ parentSize + sizeDelta`, the anchor reference point
`amin ⊗ parentSize + pivot ⊗ (amax − amin) ⊗ parentSize`, then
`left/bottom = ref + anchoredPosition − pivot ⊗ size`. `Tools/Build/author_arena_launch_panel_layout.py`
carries the worked version (`solve_rect`, `overlaps`) and uses it as a GATE: it places the widget
AND asserts the two rects are disjoint, so the clone-and-forget below cannot pass `--check` again.
Watch the assert fire once on a negative control (an oversized `size`) before trusting it.

### Trap: a widget CLONED from a sibling inherits the sibling's PLACE

Duplicating a button to make a second one copies its `RectTransform` verbatim — same parent, same
anchors, same pivot, same offset — and the copy renders exactly on top of the original until
someone moves it. Nothing fails: both draw, both raycast, the top one wins the press. The arena
launch panel shipped its SELECT VESSEL button as a byte-for-byte clone of the Play button's rect,
hidden on confirm, so it read as "a button over Play that vanishes when pressed" and survived every
static check because a rect is not a reference. When a report is about what is ON SCREEN and the
scene shows two siblings with identical `m_AnchorMin/Max`, `m_AnchoredPosition`, `m_SizeDelta` and
`m_Father`, that IS the finding — solve the rects (above) rather than reading the component fields,
which will all look correct.

### Trap: a sprite guid no `.meta` owns draws a SOLID WHITE QUAD, not nothing

A `UnityEngine.UI.Image` whose `m_Sprite` guid resolves to no asset keeps drawing — its quad, in
its tint, at its rect. The Urchin's class asset pointed `IconActive`/`IconInactive` at art deleted
before the clone's history begins, and the arena carousel showed a white square and called it the
Urchin. Same family as the `RawImage` frame trap in CLAUDE.md, and the same invisibility: a
reference check by guid is the only offline test (`Tools/Build/check_vessel_class_icons.py`
resolves every `SO_Class_*` icon against the `.meta` set; `--self-test` fires on a dangling and an
empty guid). When a surface reads as a blank rectangle, grep the guid it names BEFORE reading the
component — the component is correct.

### Trap: an untextured URP Particles/Unlit material draws SQUARE particles — same family, one layer over

`new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))` with no `_BaseMap` draws
each particle as a solid QUAD, which reads as confetti rather than sparks. Every runtime-built
particle system on `AstroLeagueBall` shipped that way. The fix is a shape in the SHADER, not a
texture: `Resources/SoftSpark.mat` (`CosmicShore/SoftSpark`, a radial falloff with a white-hot core
off the quad's UV, tinted by vertex colour) — load it from `Resources/` so the shader ships, since a
`Shader.Find` on a shader nothing references is stripped from a player build. Grep for the bare
`Shader.Find(".../Particles/Unlit")` pattern when a VFX reads as "cheap".

### Technique: preview a hand-written shader's LOOK offline before the editor sees it

A fragment function is a pure function of (uv, vertex colour, a few uniforms, `_Time`), so it
transcribes into numpy line for line: parametrise the mesh in image space (for a trail, column =
along-trail age, row = across-width coordinate scaled by the width curve), evaluate the transcribed
fragment per pixel, tonemap with ACES, and render the OLD look beside the new one at the same scale.
It is the cheapest way to make a look call honestly without an editor, and it catches the dumb
failures (a term that never reaches the screen, an effect too faint to read at its real pixel size).
State plainly that it is a render of the MATH, not a capture — it proves the shape, not the
compile, the render state or the bloom.

### Trap: a SEE-THROUGH shell on a Fresnel ramp washes to its BRIGHT colour — the back faces did it

`lerp(bright, dark, (1 + N·V) / 2)` (`SpreadFresnelShader` and its transcriptions) is built for an
OPAQUE body: the only faces you see face you, so the ramp spans dark centre → bright silhouette. Put
it on a transparent `Cull Off` / `ZWrite Off` shell and every BACK face is drawn too, with N·V < 0 —
the bright half of the ramp. Compiling the fragment with clang over a sphere (§4.5c): **76% of a
back face reads bright vs 26% of a front face**, so the Mass crystal's shells read "too white" /
"too lime" on the same colour pair the opaque Space and Time crystals wear well. Tuning the colours
cannot fix it. Measure N·V on the camera side (`rim = 1 − |N·V|`, `_FaceForward` on
`OmniShepardFresnelShader`) and shape it with a power (`_RimPower`); ship both as OPT-INS whose
defaults reproduce the old formula, and PROVE that with the harness (max |Δ| 6e-8) so every other
material on the shader is untouched. Whenever a look complaint is "washed out" on a transparent
mesh, count the back faces before touching a colour.

### Technique: a FRESNEL TUBE on a LineRenderer, and sizing a rim for a SMALL object

A `LineRenderer` (VIEW alignment) is a camera-facing ribbon — every normal faces the lens, so a mesh
fresnel on it is flat. But a view-aligned ribbon IS a cylinder's silhouette, and across a unit
cylinder seen side-on the surface at signed distance `x` from the axis has `N·V = sqrt(1 − x²)`
exactly. `uv.y` runs 0..1 ACROSS a LineRenderer's width in every texture mode, so the fragment
`x = 2·uv.y − 1; rim = pow(1 − sqrt(saturate(1 − x²)), p)` is the tube's true fresnel: one renderer,
no mesh, no normals (`ThresherChainFresnelShader`, 2026-10-09). Drive its colours through a
`MaterialPropertyBlock` on the LineRenderer, `Cull Off` (the ribbon flips winding over the lens axis).

**Size the rim to the object's SCREEN size, not to the crystals' power 4.** Area-weighted over a
sphere's disc the mean rim weight of `(1 − N·V)^p` is `2 / ((p+1)(p+2))`: **0.067 at p = 4** (a
hairline — right for a crystal tens of units across), 0.229 at p = 1.5, more again below 1. A ball a
few units across seen from 100+ units, or a chain a few pixels wide, needs p ≈ 1.5 / 0.8 or the rim
is sub-pixel and the object reads as a flat dull blob. Check the choice with a numpy render of the
fragment maths (sphere raycast or a ribbon strip) before committing — it is the cheapest honest look
call without an editor.

### Technique: MEASURE a prefab's real size offline (transform tree + nested instances + FBX bounds)

"How big is this thing?" is answerable without Unity, and the naive version is wrong by ~7x on
exactly the prefabs you care about. Three things have to compose or the number is fiction:

1. **The transform tree**, walked DOWN from the root composing
   `world = parentPos + parentScale ⊗ localPos`. Walking UP and accumulating is the tempting
   version and it multiplies in the wrong order. Rotation can be ignored deliberately — it cannot
   change a node's ORIGIN distance from the root and can only redistribute an extent between axes,
   so the estimate stays a bound and stays comparable across assets.
2. **Nested prefab instances**, which are `!u!1001` blocks and NOT `!u!4` documents — their pose
   lives as `m_LocalPosition.*` / `m_LocalScale.*` rows inside `m_Modifications` (walk the LINES;
   the rows wrap, so a one-line regex matches zero of them). Their CONTENTS are not in the file at
   all: recurse into `m_SourcePrefab`'s guid and measure that prefab too, scaled by the instance's
   pose. Skip this and a prefab whose whole body is one nested instance measures as ZERO — which
   is not a small error you would notice as an error, it is a plausible small number.
3. **Model mesh bounds**, which no transform records. A rigged creature measures fine from
   transforms alone (its bones ARE transforms); a creature drawn from one FBX mesh measures at a
   seventh of its size. Resolve `MeshFilter`/`SkinnedMeshRenderer` → `m_Mesh`'s guid → the FBX,
   read `Objects/Geometry/Vertices` (§4.8), and **normalize by `UnitScaleFactor`** — raw extents
   from two FBX files are not comparable, and Unity's importer also applies the cm→m divide.

A node's own extent is then `max(what it SCALES, what it DRAWS, what its nested source CONTAINS)`.
Validate against something independent before trusting it: an authored collider, a documented
figure, or simply the ORDERING (if your measurement says the tadpole is bigger than the shark, the
walk is broken, and ordering catches that where absolute values do not).

### Technique: resolve a `.unity` / `.prefab` MERGE per OBJECT, never per line

Origin: the arcade launch branch vs bleeding-edge's offline work (2026-08-27). Both branches
had spent weeks appending objects to `Menu_Main.unity`. Git produced **36 conflict hunks that
split individual objects in half** — hunk 1 pitted our `--- !u!1 &819777475` against their
`--- !u!1 &909973148`, hunk 6 pitted 124 of our lines against 39 of theirs. Resolving that by
editing conflict markers cannot work: a line merge is aligning two unrelated objects by file
offset, so every "keep both" produces an object with fields from two different objects.

**The file is not lines, it is a stream of documents keyed by a unique fileID** — so "who
changed this object" is answerable per id, and the merge is a three-way map merge:

```python
for fid in set(base) | set(ours) | set(theirs):
    b, o, t = base.get(fid), ours.get(fid), theirs.get(fid)
    if   o == t: pick = o          # both agree (including both deleted)
    elif o == b: pick = t          # only THEY touched it
    elif t == b: pick = o          # only WE touched it
    else:        pick = resolve(fid, b, o, t)   # a GENUINE conflict — decide on merit
```

Measured on that scene: **4,980 objects identical, 548 ours-only, 45 theirs-only, and exactly
ONE genuine conflict** — a GameObject where we swapped a Graphic and they added a component.
36 hunks of unreviewable YAML became one decision a human can check in a sentence.

Five assertions turn it from plausible into proven, all before writing:

- **Round-trip all three parents byte-exactly first.** The codec is the whole risk; a merge
  built on a lossy parser is worse than no merge. `assert emit(parse(x)) == x` for base, ours
  and theirs.
- **No NEWLY dangling local reference.** Collect bare `{fileID: N}` (no `guid:` — those are
  cross-asset), subtract the anchors, and diff against the same set computed for all three
  parents. Unity scenes carry pre-existing danglers; only new ones are yours.
- **Every object either side ADDED survives**, counted per side.
- **`SceneRoots.m_Roots` is a superset of both sides'** — a root list is the one place a
  per-object merge can silently lose a whole hierarchy.
- **A line-anchored document count**, not `str.count('--- !u!')` (see the trap below).

Then prove the gates fail: drop one of theirs' objects and confirm the "lost" assertion fires;
skip the conflict resolution and confirm that assertion fires. A merge harness that has only
ever passed is indistinguishable from one that cannot fail.

Emit in OURS' document order with theirs-only additions appended — Unity does not care about
document order, and a stable spine keeps the diff against your own branch readable.

**Trap: a LINE-list parser has the mirror of the newline-doubling bug, and `str.count` will not
catch it.** The trap above (§5) warns that regex-slicing a document body leaves a leading `\n`,
so `header + '\n' + body` doubles it. Parse into line LISTS instead and you get the opposite:
each body has no trailing newline, so `header + '\n' + body` glues the NEXT header onto the
previous document's last line — producing `m_Pivot: {x: 0.5, y: 0.5}--- !u!1 &1588431009219518451`
and a 5,573-document file that contains **two** parseable documents. The assertion that should
have caught it, `out.count('--- !u!') == len(merged)`, passed: `str.count` matches substrings
anywhere, including mid-line. Reassemble by concatenating LINE LISTS and joining once
(`'\n'.join(preamble + [ln for fid in order for ln in docs[fid]])`), which reproduces the file
exactly including its trailing newline, and count headers with a line-anchored regex.

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

### Technique: UN-SPLICE a node by reusing its wirer's own migration path

Origin: scoping a morph out of a branch (2026-09-25). A graph wirer that carries a MIGRATION
(unsplice an old signature, re-splice the current one) already contains the exact removal you
want, and it is written against slot DIRECTIONS rather than a table of known signatures — so it
runs on a *correctly*-wired node too. Import the wirer as a module and call the migration half
alone:

```python
import wire_prism_wake as W                 # the wirer whose node you are removing
for path in W.GRAPHS:
    docs = W.load_docs(full)
    if W.find_cf(docs, W.FUNCTION_NAME) is None: continue      # already unwired
    W.unsplice_foreign(docs)                # hands the feeders back, drops node+slots+edges,
    open(full, "w").write(W.dump_docs(docs))#   sweeps orphans, validates expect_wired=False
```

Four reasons this beats hand-editing or a `git checkout` of the pre-splice version:

- **It restores the DOWNSTREAM node's feeders**, which is the part a textual removal gets wrong —
  the node you delete sat between two things, and both ends have to be rejoined.
- **It sweeps the ORPHANS** the splice added (a feeder node whose only consumer was the node you
  just removed compiles into the graph and reads as somebody's live input).
- **It ends in the wirer's own `validate(..., expect_wired=False)`**, so the removal is checked by
  the same invariants the addition was.
- **`git checkout <pre-splice>^ -- <graph>` is usually WRONG**, because the base branch has
  touched the graph since. Check with `git log <splice>^..origin/<base> -- <graph>` before
  reaching for it; if anything comes back, the checkout throws upstream work away.

Then **re-run every sibling wirer with `--check`**: removing a node renumbers object ids, which is
the same exposure adding one has. And delete the unwire script — it is scaffolding, and the wirer
it imported is about to be deleted too.

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

### Trap: a file-mode Custom Function's call is ALL INPUTS, THEN ALL OUTPUTS

Slot IDs do **not** decide the argument order. ShaderGraph emits a file-mode Custom
Function node's call as every INPUT slot in slot order, then every OUTPUT slot in slot
order — so an HLSL signature whose parameter list interleaves them, or that declares a
new input **after** an existing `out`, is called with an input where an output is
expected. The graph then fails to compile and **every material drawn with it renders
unmaterialed**, with nothing in the console naming the HLSL file or the node.

That shipped (2026-09, prism corridor branch): adding an `ErosionThreshold` input to
`PrismOcclusionFade_float` after its two `out` parameters took BOTH prism graphs down, and
the report was the maximally unhelpful *"all prisms had no materials."* It is the same
failure surface as the cycle trap above — whole-graph, silent about its cause — and it is
reached by an edit that looks purely additive.

Two consequences worth carrying:

- **Declare every input before the first `out`.** When a variant needs a different value
  for one argument, prefer TWO THIN WRAPPERS over one shared body (`…Impl`) to adding a
  slot: the wrappers keep the node's slot shape byte-identical, so the graph edit is a
  `m_FunctionName` string swap with no slot or edge churn at all, and the two variants
  provably cannot drift in shape. `PrismOcclusionFade_float` /
  `PrismOcclusionFadeDebris_float` over `PrismOcclusionFadeImpl` is the worked example.
- **Gate it.** `Tools/Build/check_shadergraph_custom_function_signatures.py` parses every
  `.shadergraph`'s custom-function nodes, resolves each to its HLSL by guid, and asserts
  the declared parameter order matches the node's slot groups. It carries a `--self-test`
  that reproduces the shipped failure as a negative control — write that control, because
  a gate for a whole-graph failure is one nobody will otherwise watch fail.

### Trap: an idempotent wirer's "already wired" exit silently skips the slot you just added

Every graph wirer decides "already wired" by finding ONE artifact of its splice (usually its
first property) and exiting. Extend its recipe with a new slot/property/edge and the fresh-pass
code is correct — and it never runs, because every shipped graph already has that first
artifact. `--check` goes green, the HLSL now takes an argument no node feeds, and the graph fails
the way the ALL-INPUTS-THEN-OUTPUTS trap above describes. So a wirer that GROWS needs an
**UPGRADE pass**: detect the new artifact separately, and when the old splice is present without
it, add exactly the new pieces — the property (cloned from a same-FILE donor of the right
declaration kind: a Hybrid-Per-Instance Vector1 such as `_ShieldMorphDuration` for per-prism
data), one property node cloned from its sibling, the slot INSERTED ahead of the output in
`m_Slots`, one edge — then validate slot ORDER against the recipe, not just the slot set.
`wire_prism_destruction_sight.py`'s `upgrade_super_shield` is the worked example (2026-09-28). It
also makes the wirer a merge resolver for graphs taken whole from a branch that predates it.

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

**Slice by METHOD SIGNATURE + brace matching, not by markers, when the change is a handful of
methods inside a huge file.** `START_MARKER`/`END_MARKER` needs stable text you are not editing,
which is exactly what a working session keeps moving. A ~30-line extractor that takes a LIST of
signature regexes and walks braces from each match to its closing one is immune to that: the
harness re-derives itself from the shipped file after every edit, so `extract → generate → compile`
becomes one command you re-run between patch rounds. One session type-checked five edited bodies
out of a 2,400-line `NetworkBehaviour` this way across six rounds of edits, in seconds each.

```python
def extract(sig_regex):                      # find the signature, then brace-match to the end
    for i, l in enumerate(lines):
        if re.search(sig_regex, l):
            depth, started, out = 0, False, []
            for j in range(i, len(lines)):
                out.append(lines[j])
                depth += lines[j].count('{') - lines[j].count('}')
                started |= '{' in lines[j]
                if started and depth == 0: return "\n".join(out)
    sys.exit("not found: " + sig_regex)      # HARD FAIL — see below
```

**Brace-matching alone MIS-EXTRACTS an expression-bodied member, and the error it produces names
the wrong thing.** `public bool HasVotedRematch => IsSpawned && NetRematchVote.Value;` has no
braces, so the walker runs straight past it and swallows the NEXT member — which the list then
extracts a second time, giving `CS0111: already defines a member`. That reads exactly like the
duplicate-member defect §5 warns about, in code that has no duplicate at all, and the instinct is
to go looking in the real file. Terminate on the first `;` when a `=>` appears before any `{`.

**The extractor must HARD-FAIL on a signature it cannot find, and that line is the whole gate.**
A signature list silently stops covering a method the moment you rename or delete one — and the
harness then compiles clean, reports `errors: 0`, and is proving nothing. This is gate erosion by
your own refactor: the failure looks exactly like success. `sys.exit` on a miss makes the harness
break loudly the moment its subject moves, which is the only way you find out you renamed
something. Pair it with the §4 rule (inject a defect, confirm it fires, restore, `cmp`) **after
every restructuring pass**, not once at the start — the run that matters is the one against the
code you are about to commit.

**For an `#if UNITY_EDITOR` TEST file, skip the extraction — compile the WHOLE file, unmodified,
and drive it by reflection.** A test file's Unity surface is usually small and entirely stubbable
(`Vector3`, `Mathf`, `Mesh`'s vertex/UV accessors, `Object.DestroyImmediate`), and NUnit is ~40
lines of stub (`[Test]`, `Assert.IsTrue/IsNotNull/AreEqual/That`, `Is.GreaterThan/LessThan` as a
tiny constraint interface). Compile it together with the REAL subject files it tests, `-main:` a
driver that reflects over `[Test]` methods, and you have executed the shipped assertions against
the shipped code. This is what makes the body-level blindness above survivable: the 2026-08-24
table says the no-stubs pass proves nothing inside a method, and a whole-file test harness proves
everything inside every method the suite covers. Three mechanics that cost a cycle each:

- **`rm` the output assembly before every rebuild.** A failed build leaves the previous `.dll` in
  place, the driver runs it, and a *broken* file reports the previous run's "7 passed" — the exact
  false green a gate exists to prevent.
- **…and `rm` every GENERATED SOURCE too, and abort when its generator fails.** A harness that
  extracts the shipped methods into `Foo.g.cs` and then compiles has two artefacts that can go
  stale, not one. If the extractor hard-fails (as it should, on a signature it cannot find) and the
  build line runs anyway, the compiler happily reuses the PREVIOUS `Foo.g.cs` and the run reports a
  pass for code that is no longer the tree. It happened re-verifying a branch after merging the
  base: the extractor read its "pre-fix" side from `git show HEAD:`, which stopped being pre-fix the
  moment the fix was committed, so it failed — and the merged-tree "re-verification" printed ALL OK
  for the pre-merge code. So: delete generated sources and outputs first, chain the generator with
  `|| exit`, and pin any "before" side of a before/after harness to a COMMIT (`<fix>^`, the merge
  base), never to `HEAD`, which moves under you.
- **Run from the PROJECT ROOT**, not the harness directory: Unity runs edit-mode tests with cwd =
  project root, so every `File.Exists("Assets/...")` in the suite is project-relative and fails
  everywhere else. Four passing tests read as four failures until you notice.

And prove the harness the way the table above was produced — inject each defect class you care
about, confirm it fires, restore, `cmp`. A run that only ever passes is indistinguishable from a
run that cannot fail.

**The most common way that goes wrong is not a weak assertion — it is a file the build never
compiled.** A harness `.csproj` that enumerates its inputs explicitly
(`<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` plus a literal
`<Compile Include="..."/>` list — which is the right shape, because it keeps the harness pinned to
the files you meant) will silently ignore a new `.cs` you drop into its directory. You add a probe,
run the build, read **`Build succeeded`**, and conclude the code is good; nothing was checked. The
negative control is what separates those two states, and it takes one command:

```sh
sed -i 's/<the call you probe>/&; obj.NoSuchMember();/' Probe.cs
dotnet build -v q --nologo | grep -E 'error|Build'      # MUST report CS1061
git checkout -- Probe.cs || mv Probe.bak Probe.cs       # restore, rebuild, expect success
```

If the deliberate error does not fire, the file is not in the build — add it to the `<Compile>`
list and start over. Treat "I added a file to the harness" as requiring this check every time; the
failure mode is a green build that proves nothing, which is the exact thing a harness exists to
rule out.

### Technique: run a NetworkBehaviour as TWO MACHINES — partial class + an RPC router

A question about REPLICATION ("does every peer run the same thing for this press?") is invisible
to any single-machine test and normally needs an MPPM session or two devices. When the replication
is by RE-EXECUTION (an RPC carries an input and each peer resolves it itself), it can be answered
out of editor from the shipped file, in seconds:

- **Two edits to the real file, nothing else.** Make the class `partial`, and cut its `[ServerRpc]`
  methods out (signature regex + brace-match, HARD-FAIL if absent). A generated partial supplies
  same-signature replacements that loop over a `List<Self> Copies` and call each copy's REAL
  `[ClientRpc]` method — the owner's own copy included, as Netcode does. Everything the RPCs
  resolve and run is the shipped code; the generator re-derives the signature from the file, so
  one driver runs an OLD revision (`git show <rev>:path`) and the new one alike — which is the
  repro and the proof in one tool.
- **One object per machine, each with its OWN view of the replicated state** (here: a fake input
  status per copy, so the peer can disagree with the owner about the device). Actions are
  recorders that log into the status passed to them, so "what did each machine run" is a list
  compare. Feed it every shipped prefab's data, parsed from YAML, not a hand-built case.
- **Give every mechanism of the fix its own scenario, or its negative control cannot fire.** The
  2026-10 press fix had two release mechanisms that back each other up on the ordinary networked
  path (the owner sends the pressed device; every copy also prefers its own record). Removing
  either one alone changed NOTHING there — the harness looked blind. Only adding the path each
  covers alone (the non-networked single machine; a peer that joined mid-hold and never ran the
  press) made each removal fail. **A mutation that does not fire may mean a sibling covers it, not
  that the harness cannot see** — find the scenario where it is the only cover before concluding
  either way. Ship the mutations as `--self-test`.
- It proves what each machine RESOLVES and RUNS, never Netcode itself (delivery, ordering,
  ownership) — say so. Worked example: `Tools/Build/peer_press_harness/` (`R_VesselActionHandler`,
  `R_VesselActions/SQUIRREL_DRIFT.md` §11).

### Technique: gate a DTO round-trip BY REFLECTION, not field by field

A payload struct that crosses the wire through a hand-written DTO (Unity Netcode's
`INetworkSerializable` conversion structs, any `FromX`/`ToX` pair) has a failure mode nothing
catches: add a field to the payload, forget the DTO, and the far side reconstructs it at its
**default**. It compiles, it reads correctly at every call site, and it is silent on every peer —
including the host, which runs the ClientRpc too. Worse, the LOCAL path usually bypasses the DTO
entirely, so the field works in exactly the solo session you would test it in.

A field-by-field test does not help, because the person who forgot the DTO also forgets the test.
Drive it off the payload type's OWN fields:

```csharp
object boxed = default(Payload);
foreach (var f in typeof(Payload).GetFields(Public | Instance))
    f.SetValue(boxed, DistinctValueFor(f));           // a NON-default value per field
var got = Dto.FromPayload((Payload)boxed).ToPayload();
foreach (var f in fields) Assert.AreEqual(f.GetValue(boxed), f.GetValue(got), f.Name);
```

Two details are what make it a gate rather than a formality: **a field type the generator cannot
populate must `Assert.Fail` by name, never be skipped** — a silent skip restores the exact blind
spot — and the failure message should name all five places the field belongs (DTO field,
`NetworkSerialize`, constructor, both converters). Pair it with one explicit test of the flag you
actually care about, so a reflection sweep that finds zero fields cannot pass vacuously.
Negative-control it by reintroducing the omission; it should go from all-pass to all-fail.

### Technique: when you WIDEN a pure function, pin the old behaviour as a whole-domain test

Extending a formula — a two-stage curve becoming four, a flag gaining a mode, a cap gaining a
second cap — always ships with the claim *"and everything that doesn't opt in is unchanged."*
That claim is free to make, invisible to review, and the single most likely thing to be wrong,
because the new branch structure has to reproduce the old one exactly rather than approximately.

Make it a test, and make it cover the whole domain rather than a few sample points:

```csharp
for (float t = 0f; t <= 30f; t += 0.05f)
    Assert.AreEqual(OldFormula(t), New(t, optedOutStages), 1e-5f, $"held {t}s");
```

Three things that turn it from a formality into a real gate:

- **Test every opt-out half INDEPENDENTLY.** A guard like `rate > 0 && headroom > 0` has two
  ways to be off; a test that only zeroes both proves neither.
- **Keep the old signature as an overload that constructs the opted-out case**, rather than
  deleting it. Every existing caller then keeps compiling *and* is provably on the old curve,
  so the blast radius of the change is exactly "the assets that authored the new fields."
- **Report the measured delta, not "identical".** Run the old closed form alongside in a driver
  and print `max |Δ|`; `0` is a fact, "should be equivalent" is a hope. (This session's staged
  gun-spread curve measured exactly 0 over 20,001 samples — which is what made it safe to say
  the change could not reach any other vessel.)

Note for the `mcs` fallback: pass **`-langversion:latest`**. mcs 6.8 defaults to C# 7.0 and will
reject `readonly struct` / `in` parameters that Unity's C# 9 accepts, which reads as a genuine
error in your new file rather than as a harness limitation.

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

### Technique: SYNTAX-only compile — prove a file parses without stubbing its whole world

A merge-resolved file usually cannot be compiled: it reaches into fifty Unity types you would
have to stub. But the failure a merge introduces is almost always STRUCTURAL — two statements
where one expression belonged, a lost `return`, an argument list that gained a member — and
structure is exactly what the parser sees before it ever needs a type.

So compile the file **alone, with no stubs at all**, and classify the errors:

```sh
dotnet build -p:TARGET=/abs/path/File.cs 2>&1 | grep -oE "error CS[0-9]{4}"
```

- `CS0246` / `CS0103` / `CS0234` / `CS1061` / `CS0117` / `CS0535` — *missing type or member*.
  Expected, and means nothing: you removed its world.
- **`CS1xxx` — a SYNTAX error.** `CS1002` (`;` expected), `CS1003`, `CS1519`, `CS1525`, `CS1513`.
  Nothing but a genuine structural break produces these, so any hit is a real defect.

One `.csproj` with `<Compile Include="$(TARGET)" />` and `EnableDefaultCompileItems=false`
serves every file, so this is a loop over the whole changed set rather than a project per file.
It caught nothing here — but only because it was negative-controlled first: injecting the exact
defect a bad keep-both produces (splitting an `&&` chain into two statements) raised `CS1003`,
and the restored file went back to zero. **A gate you have not watched fail is not a gate.**

Two limits worth knowing. It cannot see semantic breaks — a `+` chain that gained a third
ARGUMENT is well-formed C#, so for those extract the one method and compile it for real against
tiny stubs (`DescribeBuildValues` compiled and RAN in about thirty lines of stub, and printing
its output proved both modes landed on their own lines). And a whole-file `#if` still makes the
compile see nothing, per the trap below.

### Technique: write the DECISION as a Unity-free pure static, then actually RUN it

The syntax-only compile above is the fallback for a file you cannot stub. The better move,
when you are writing the file rather than merging it, is to make the interesting part
stubbable *by construction* — because then you get a real compile AND a real test run, and
the difference between "it parses" and "it is correct" is the whole point.

The shape: take the one decision the feature turns on (an ordering, a fold, a threshold, an
address calculation), put it in a `static` class with **no Unity, no UGS, no Netcode types**,
and have the caller pass in whatever it needs from those worlds as a delegate or a plain
value. A party-seating rule that needs each member's replicated Netcode `OwnerClientId`
takes a `Func<string, ulong>`; the MonoBehaviour supplies the real lookup and the test
supplies a dictionary. Nothing about the logic knows Netcode exists.

What that buys, measured on one session: the helper plus its twelve NUnit tests compiled and
**ran** out of editor against a **four-line** `UnityEngine` stub (just `SerializeFieldAttribute`,
for the one serialized struct it referenced) — and the single most valuable assertion in the
suite was one no single-machine play test could ever make: *three devices, three different
input orders, one identical output*. A per-device seating bug is invisible from one device by
definition.

```xml
<!-- the whole harness: src/ holds the shipped .cs files, copied not rewritten -->
<PropertyGroup><TargetFramework>net8.0</TargetFramework><LangVersion>9</LangVersion>
  <DefineConstants>$(DefineConstants);UNITY_EDITOR</DefineConstants>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup><Compile Include="src/**/*.cs" />
  <PackageReference Include="NUnit" Version="3.14.0" />
  <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" /></ItemGroup>
```

`LangVersion 9` matches Unity 6. Copy the shipped files in rather than rewriting them (per the
"compiling a COPY" trap below, re-copy on every edit — or the harness stops being a gate).

**The payoff is not only the tests.** A real compile of the pure helper is the only thing in
this repo that resolves types across the SOAP/Unity boundary without the editor, and it caught
a signature that would have been an editor-only error: **`Obvious.Soap`'s `ScriptableList<T>`
implements `IList<T>` and NOT `IReadOnlyList<T>`**, so the natural read-only parameter type
does not accept one. Any helper taking a SOAP list must take `IList<T>`.

### Technique: test a BINARY-FORMAT reader against the real tool's output, not hand-written bytes

A reader for a format you did not author (a protobuf manifest, an ELF header, a signature
block) is only proven against bytes the real producer wrote. Writing them by hand repeats your
own understanding of the format back to you. Google's Android tools are reachable from this
container, so `AndroidBundleCheck` (`Assets/_Scripts/Editor/Build/`) was proven against real
output (claude/serene-edison-lfv24f, 2026-10-13):

- **A real aapt2 protobuf manifest.** Download aapt2's Linux jar
  (`dl.google.com/android/maven2/com/android/tools/build/aapt2/<ver>/aapt2-<ver>-linux.jar`) and
  `android.jar` from `dl.google.com/android/repository/platform-36_r02.zip`, each unpacked into
  its own empty folder. Then run
  `aapt2 link --proto-format -I android.jar --manifest AndroidManifest.xml -o x.apk`. The
  `AndroidManifest.xml` inside `x.apk` is the exact protobuf `XmlNode` an App Bundle carries.
  Link a clean manifest and one that is wrong in every field you check.
- **Real ELF alignments.** The repo's FMOD arm64 `.so` has LOAD alignment `0x4000` and Nice
  Vibrations' `liblofelt_sdk.so` has `0x1000`. Confirm both with `readelf -lW` before relying on
  them.
- **Real signatures.** `keytool` and `jarsigner` are installed. Sign one fixture with a
  release-named key and one with `CN=Android Debug`.

Then build one fixture per verdict (good, unsigned, debug-signed, no 64-bit libraries, not a
bundle) and require each verdict to differ. **That set caught a check that could never fire:**
the "no arm64 libraries" error was gated on the list of libraries the check MEASURED, which held
only 64-bit ones, so a 32-bit-only bundle passed. A check whose precondition is computed from the
thing it looks for is always silent. Test the precondition on the raw input instead.

### Trap: an SDK-style harness project compiles EVERY `.cs` under its folder

`dotnet` project files glob `**/*.cs` by default, not only the files you `<Compile Include>`.
A deliberately broken copy of the shipped file (a mutant for a negative control) left in the
harness folder is compiled beside the real one. Every member then reports
`CS0111 … already defines a member`, which reads exactly like the shipped file got duplicated.
The repo file was fine; only the harness was wrong. Moving the mutant into its own folder is
not enough on its own. If that folder's project also `<Compile Include>`s the mutant, the SDK
stops with `NETSDK1022 Duplicate 'Compile' items` before any test runs, and a quiet grep for
`Passed!|Failed!` then prints nothing, which a hurried reader takes as a pass. Give every harness
project `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` and list its files.
Check `git show HEAD:<file>` before believing a duplicate-member error in a harness. Read the
test count line before believing a mutant run: no line means no run.

### Trap: you cannot see a PACKAGE API's SHAPE here, so a test written against one is a guess

The absent `Library/PackageCache` is usually discussed as a guid problem (see §5's differential
`m_Script` check). It has a second consequence that bites when you are writing a TEST: the
package's *source* is not on disk either, so you cannot answer "is this constructor public?",
"does this overload exist?", "is this method an extension?" for anything in `Unity.Netcode`,
`TMPro`, `Unity.Collections` or any other package — and a test that guesses wrong is a compile
error in `Assembly-CSharp-Editor`, which takes the whole edit-mode suite down for everyone.

The tempting case is a DTO round-trip. §4's reflection gate is the right shape when the type
round-trips through your OWN converters (`FromExplodeParams`/`ToExplodeParams`), and it is
unreachable when the only path is `INetworkSerializable.NetworkSerialize`, because driving that
needs a `BufferSerializer<T>` whose constructor accessibility you cannot check from here.

So: **write the half you can see, and say in the test's own doc comment which half you could
not.** For a hand-written `NetworkSerialize`, the reachable half is `Equals` — assert by
reflection that every field participates in it, which is worth more anyway: a field missing from
`Equals` means the `NetworkVariable` never dirties, so the serializer never gets the chance to be
wrong. Do NOT substitute a `Assert.AreEqual(12, fields.Length)` "did anyone add a field?" guard;
the next person fixes it by bumping the number.

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

### Trap: a harness that RECONSTRUCTS the design validates the design, not the ship

The compile-and-run pattern is only as honest as the frame it evaluates in. A validator that
assembles the artifact its OWN way — rather than replaying the code that assembles it at
runtime — tests the design and silently exempts the emitter. A procedural vessel hull was dumped
and rendered in HULL space (every part's verts in the shared design frame, where everything is
correct by construction) while the shipped emitter subtracted each part's pivot and restored it
as a child transform; the root part had no transform to restore it to, so the belly/clypeus drew
0.70 u above the shell **for the whole life of the hull**. Six render passes across three
sessions were structurally incapable of seeing it — the defect lived entirely in the step the
harness replaced.

So: when validating anything that is ASSEMBLED (parts + transforms, submeshes + materials,
prefab instance + modifications), make the harness run the real assembly path, or at minimum
reproduce its frame arithmetic and assert the composition — "does part *i* land where
`EmitParts` will put it", never just "is part *i*'s geometry right". The tell that you are
reconstructing rather than replaying: your dump/render code contains an offset, a parent
multiply, or a pivot decision that ALSO exists in the shipped code. That duplicated line is the
one nobody is testing.

### Trap: a whole-file `#if` makes the compile see NOTHING, and that reads as clean

The traps above are about what a compile can and cannot BIND. This one is a rung below: it may
not have compiled a single line. **78 of this project's 111 test files open with
`#if UNITY_EDITOR`** (it is the convention for a test under an `Editor/` folder), so a §4 harness
that does not set `<DefineConstants>UNITY_EDITOR</DefineConstants>` compiles an empty file and
reports zero errors — indistinguishable from a clean pass, and arrived at faster.

The tell is the error COUNT, not its absence: a real Unity gameplay or test file compiled without
`UnityEngine.dll` produces *hundreds* of `CS0246`s. **Zero unresolved-type errors on a file full
of `MonoBehaviour`s means the compiler never saw the file.** Check that before believing a green
run, and grep the file's first line for a guard before writing the csproj. The same applies to
any `#if` a file is wrapped in — `DEVELOPMENT_BUILD`, a package define, a custom symbol.

### Trap: compiling a COPY cannot see whole-class consistency

The harness pattern in §4 — paste the block under test into a stub file and compile it — proves
the block's *contents* against real types, which is its whole value. It cannot see anything about
the block's RELATIONSHIP to the rest of the real class: a member you added that duplicates one
already there (`CS0102`), a name that collides with a base-class member, an override whose base
signature changed. Those are only found by compiling the real file, or by Unity.

So: after any patch that ADDS a member to a large existing class, grep that class for the
member's own name and confirm exactly one declaration. This session shipped a duplicate field
that the harness compiled clean and Unity rejected.

### Trap: an UNRESOLVED BASE TYPE makes the compile blind to the whole class body

The filtered-noise compile (§4, and the trap above) is weaker than it looks the moment inheritance
is involved: **Roslyn abandons class-body binding when the base type is unresolved.** So for
`class Foo : SomethingInAssembly-CSharp`, an `override` naming a member the base does not declare,
a missing implementation of an abstract member, or a signature that no longer matches is reported
as *nothing at all* — the same blind spot CLAUDE.md records for enum members inside serialized
field defaults. A refactor that reparents a class onto a shared base therefore gets **zero**
coverage from this harness, however clean the run looks.

When you cannot resolve the base (you usually cannot — it is in the monolith), audit the fit
TEXTUALLY and make the audit a gate:

- every `abstract` member of the base has a matching `override` in the subclass;
- every `override` in the subclass names a member the base declares `abstract` or `virtual`;
- any member the design depends on is present on both sides.

Regex both sides for `\b(public|protected|internal)\s+(abstract|virtual|override)\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]`,
which catches expression-bodied properties and methods alike, and **negative-control it in both
directions** (delete an implementation; add an override of a member that does not exist). A session
that reparented a 1,255-line controller onto a shared base had this as its only out-of-editor
safety net.

### Trap: a harness that compiles COPIES stops being a gate the moment the tree moves

Distinct from the trap above, and more embarrassing: a `.csproj` that lists files **copied into the
scratch directory** proves something about the copies. They drift the instant you edit the tree, and
"I widened the harness" then widens a set of stale files. A session shipped a compile error twice in
a row this way, the second time immediately after saying the harness had been widened.

Point `<Compile Include>` at the **real paths** — `/repo/Assets/.../Thing.cs` — so the gate cannot
describe anything but what is about to be committed. Stubs (Unity attributes, `Mathf`, NUnit's
`Assert`) stay local; the code under test never does. The same applies to a test runner: run the
SHIPPED test file, not a copy of it, or `84/84 passing` is a claim about a snapshot.

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

