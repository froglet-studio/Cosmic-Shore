# unity_refcompile — compile the game's C# against real Unity references, headless

```
bash Tools/Build/unity_refcompile/run.sh                       # player config (the shipped IL2CPP release)
bash Tools/Build/unity_refcompile/run.sh --config player-dev   # + DEVELOPMENT_BUILD / ENABLE_PROFILER / DEBUG
bash Tools/Build/unity_refcompile/run.sh --config editor       # APPROXIMATE: UNITY_EDITOR branches + Assembly-CSharp-Editor
bash Tools/Build/unity_refcompile/run.sh --quiet-buckets       # count, don't list, the unverifiable buckets
```

Exit 0 = **no compile error in project code**; exit 1 = errors, listed, with every one in a file
changed since `origin/bleeding-edge` (committed or not, untracked included) tagged `[CHANGED-TONIGHT]`;
exit 2 = offline with no cache.
First run ~10 min (the fetch, then ~2 min compiling ~100 assemblies). Later runs take ~30 s: package
assemblies are compiled once, player-mode, into a cache keyed by input fingerprint and shared by every
config and worktree, and only Assets assemblies recompile. Runs that share a cache or `TMPDIR` take
turns (a lock), so starting several at once is safe.
Needs a .NET SDK, **8.0 or newer** (8.0 and 10.0 both verified), under `DOTNET_ROOT` (`run.sh` falls
back to `$HOME/.dotnet`); the helper tools are built for the newest runtime + reference pack it holds.

**The `editor` config is approximate.** It compiles the project's runtime code with `UNITY_EDITOR`
(Mono, collections checks, `UNITY_INCLUDE_TESTS`). It also compiles the loose `Editor/`-folder
scripts the way Unity does: as their own **`Assembly-CSharp-Editor`** (`-Editor-firstpass` under
`Plugins/` and `Standard Assets/`), with NUnit (`com.unity.ext.nunit`), referencing the runtime
assemblies, which cannot see it. So an Editor file's `namespace CosmicShore.Editor` no longer shadows
UnityEditor's `Editor` for a runtime `#if UNITY_EDITOR` class, and runtime code that names an
Editor-folder type fails as it does in Unity. Only errors in the Editor-folder files **changed since
`--changed-base`** gate: committed, uncommitted and untracked alike, so run it before you commit. The
unchanged ones are compiled as context, so a changed tool binds against `FrogletTool`,
`FrogletEditorPalette` and the rest, and their errors are listed separately (37 on bleeding-edge on
2026-10-08, every one a reference-set artifact, see "Editor reference gaps" below). When
`Assembly-CSharp` emits no DLL here (in any run that could not fetch the five registry-only packages,
because their files always fail it), the editor assembly is **bound against its source**
(`Diagnose --source-ref`): a runtime error is reported once, in the runtime file, not as missing types
in the Editor files. The references are the newest
**non-publicized `UnityEditor.dll` obtainable, 2021.1**, and the 6000.0 engine DLLs. Those engine
DLLs are player builds, so their own `#if UNITY_EDITOR` members are missing: for example,
`UIBehaviour.OnValidate` and `Reset` are reported as "unverified", not as errors. Packages stay
player-compiled, and editor-only asmdefs (`Obvious.Soap.Editor`, `FMODUnityEditor`, package editor
assemblies) and the test framework are not compiled. Use it to catch errors in editor branches. A
green editor run is weaker evidence than a green player run.

## What it does

A small re-implementation of Unity's script pipeline (`build.py`):

1. **Discovers every `.asmdef`/`.asmref`** in the fetched package sources and `Assets/`, evaluates
   `includePlatforms`/`excludePlatforms` (WindowsStandalone64), `defineConstraints`,
   `versionDefines`, `overrideReferences`/`precompiledReferences`/`autoReferenced`, name and GUID
   references.
2. **Assigns loose scripts** to `Assembly-CSharp-firstpass` (`Assets/Plugins`, `Standard Assets`)
   and `Assembly-CSharp`; `Editor/` folders are skipped in the player configs and go to
   `Assembly-CSharp-Editor(-firstpass)` in the editor config.
3. **Precompiled DLLs** (DOTween, FMOD, ILSupport, ...): managed-only, platform and
   `isExplicitlyReferenced` read from each `.meta`'s PluginImporter block.
4. **Source generators** labelled `RoslynAnalyzer` run for the owning asmdef and every assembly that
   references it (Unity's rule) — Entities' `SystemBase`/`ISystem`/`IJobEntity` generators and Reflex's
   injector generator really run.
5. Compiles in dependency order with Roslyn `csc`: `-langversion:9.0`, netstandard 2.1 (+ the
   NETStandard 2.0 facades, as Unity's API profile), the project's scripting defines
   (`ProjectSettings.asset` Standalone: `DOTWEEN;SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`, IL2CPP,
   `ENABLE_INPUT_SYSTEM` only — `activeInputHandler: 1`) and `UNITY_6000_3_17` version defines.
6. For project assemblies it re-runs the compile through `Diagnose/` (the Roslyn API): **csc stops
   after declaration errors**, so one file naming an unfetchable package would otherwise hide every
   method-body error in every other file. `Diagnose` binds all bodies regardless. It names each
   compilation after its `-out:` file, as csc does, so `[InternalsVisibleTo("Assembly-CSharp-Editor")]`
   (`Assets/_Scripts/AssemblyInfo.cs`) applies. With `--source-ref <dep>.rsp` it binds against a
   failed dependency's source instead of its (missing) DLL. Each error that is not itself an
   unresolved name gets the unresolved types its expression involves appended, e.g.
   `[unresolved types: IReadOnlyPlayer]`.
7. Buckets the errors: **project errors** (the gate), **unobtainable** (below), **missing-type errors
   naming a type or namespace that a failed package declares** or (editor config) an
   `EDITOR_REFERENCE_GAPS` entry, and (editor config) **errors in Editor-folder files not changed since
   `--changed-base`**.

   **unobtainable** applies only when this run could not fetch the five packages needle-mirror does
   not carry (Services.Multiplayer / Friends / Leaderboards, Multiplayer.Playmode / Widgets) from
   packages.unity.com either. When fetch got them, they compile like any other package and the
   bucket is `(none)`, with 0 errors. When it did not, the bucket takes an error only when it can
   stem from one of them. A missing-name error (CS0246, CS0234, CS0103, CS1069, CS0012, CS0538) must
   name a type, namespace or assembly that the committed `unobtainable_declarations.tsv` (below) says
   they declare, which is the same test `names_failed_package` applies to a failed package. A cascade
   (CS0165, CS0019, CS1061) must carry one of those types among its `[unresolved types: …]`. For
   example, `p.Properties.TryGetValue(k, out var v) && int.TryParse(v.Value, out int n)`, with `p`
   an unknown `IReadOnlyPlayer`, gives a CS0165 on `n`. Diagnose follows a `var` local that was
   inferred from an unresolved expression back to its declaration. Every other error in those files
   gates: a misspelled local or type, a missing member of a known type, and a real unassigned local.
   Before 2026-10-08 the bucket took every one of those codes in any file that `using`d one of
   the five namespaces, which covered 15 runtime files, the party services among them.
   `python3 Tools/Build/unity_refcompile/build.py --self-test` checks the rule on fixtures, and
   checks the snapshot against `packages-lock.json`. It takes about a second and needs no .NET SDK
   and no cache.

## Where the references come from (fetched by `fetch.py`, cached, never committed)

Cache: `${UNITY_REFCOMPILE_CACHE:-$TMPDIR/unity_refcompile_cache}` (~550 MB; files a compile does
not read are pruned on fetch). Network needed once: `api.nuget.org` and `github.com` (read-only git
clones), and `packages.unity.com` (which redirects to `cdn.packages.unity.com`) for the five packages
needle-mirror lacks. Without that last host the run still works, and those five go to the
`unobtainable` bucket (step 7). Offline with no cache → exit 2 with a message; offline with a cache →
reuses it.

| What | Source | Real or not |
|---|---|---|
| UnityEngine modules, UnityEngine.UI, TextMeshPro | nuget `Digitalroot.References.Unity` **6000.0.75** (no 6000.3 build is published anywhere reachable) | real engine metadata, **publicized** by the re-packer — see below |
| Accessibility oracle | nuget `Unity3D.SDK` 2021.1.14.1 `UnityEngine.dll` (non-publicized) | real |
| netstandard facades | nuget `NETStandard.Library` 2.0.3 + `NETStandard.Library.Ref` 2.1 (the SDK's; nuget's when the SDK no longer bundles it, as 10.0 does not) | real |
| Every registry package in `packages-lock.json` (Entities 1.4.2, Entities.Graphics 1.4.15, Collections 2.6.6, Mathematics 1.3.3, Netcode 2.5.0, Transport 2.6.0, InputSystem 1.14.2, Cinemachine 3.1.2, Services.Core/Auth/CloudSave/Analytics, Purchasing, Splines, Timeline, ...) | source at the **exact locked tag** from the `needle-mirror` GitHub mirrors | real source |
| UniTask, Reflex, ParrelSync | source at the locked commit | real source |
| SRP Core / URP / URP-config / ShaderGraph / VFX | `Unity-Technologies/Graphics` branch `6000.0/staging` | real source, **17.0.x not the locked 17.3.0** (17.3 needs 6000.3-only engine API the references lack) |
| Burst | needle-mirror `1.6.0-pre.2` | **substitute** for locked 1.8.29 (newest mirrored tag) |
| `UnityEngine.UnityConsentModule` | `stubs/UnityEngine.UnityConsentModule.cs` (3 members Analytics uses) | **stub** — 6000.0.75 only type-forwards to it |
| Services.Multiplayer 1.1.8 / Friends 1.1.1 / Leaderboards 2.3.3, Multiplayer.Playmode 1.6.1 / Widgets 1.0.1 (not on needle-mirror) | the **Unity registry's own tarball** (`packages.unity.com`) at the locked version, sha1-checked against the registry's record, marked `.registry` in the cache | real source — or, where that host is blocked, **absent**: errors naming their types are bucketed, not gated (step 7) |

### Making the engine references honest (`Depublicize/`)

The 6000.0 DLLs are "publicized" (every member public). Left alone that would (a) accept project
code touching Unity internals and (b) reject every legal `protected override` (CS0507). Depublicize
rewrites a copy with Mono.Cecil: accessibility is copied from the non-publicized 2021.1 engine for
every type/member that existed then, and `depublicize_overrides.txt` corrects the rest. That file is
**learned automatically from package code only** (packages compile in Unity, so a package's
`protected override` proves the member is protected, and a package's use of a member the 2021
oracle calls internal proves it became public) — never from Assets code — plus a short manual
section for documented uGUI API no package overrides (`Graphic.OnPopulateMesh`).

### Two Unity versions on purpose

Assets code is compiled with the project's real defines (`UNITY_6000_3_OR_NEWER`, ...). Package code
is compiled with the **6000.0.75** defines/versionDefines, matching the references, so packages pick
code paths whose engine API the DLLs actually contain. A 6000.0→6000.3 engine gap can therefore only
surface in Assets code, where a human judges it.

### What the registry-only packages declare (`unobtainable_declarations.tsv`)

This file lists the types and namespaces each registry-only package declares, at its locked version,
with the tarball's sha1, for the assemblies Assets code references. They are read with
`declared_names`, the same reading a failed package gets. A run that could not fetch these packages
uses the file to tell a UGS name from a typo. It is generated, never hand-edited, by a run that DID
fetch them:

```
bash Tools/Build/unity_refcompile/run.sh --write-declarations
```

Refresh it whenever `packages-lock.json` moves one of these packages. Until then the build prints a
warning and `--self-test` fails. If a package is neither fetched nor in the file, the build prints a
warning naming it, and errors that name its types gate. That fails loudly, never as a silent pass.

### Source patches (`source_patches.json`)

Body-local edits to fetched package sources where a package head needs engine API newer than the
references (one today: `GPUDrivenPackedMaterialData.hasTessellation`). Never applied to Assets.

## What a green run proves, and what it does not

Proves: every project `.cs` compiled into the player assemblies (Assembly-CSharp, -firstpass,
CosmicShore.Data, Obvious.Soap, FMOD, NiceVibrations, ...) type-checks against the real package
sources at their locked versions and the real (6000.0.75) engine API — including source generators.

Does not prove:
- `#if UNITY_EDITOR` blocks and `Editor/` folders against the real Unity 6 `UnityEditor`: none is
  obtainable. The `editor` config checks them against 2021.1, and gates only the changed Editor-folder
  files; editor-only asmdefs and the files inside them are not compiled at all.
- Engine API added between 6000.0.75 and 6000.3.17 (would show as a false error, not a false pass);
  engine members whose accessibility changed in a way neither the 2021.1 oracle nor package code
  reveals (stay public: a possible false pass on use of an engine internal).
- When packages.unity.com was blocked (the `unobtainable` bucket names packages): how project code
  uses those five packages' types. For example, a wrong member, argument or overload on an `ISession`
  is not reported, because the type itself is unresolved. Errors that do not involve those types still
  gate. The bucket test goes by name, so a misspelling that happens to be a type those packages declare
  is bucketed. When fetch reached the registry, the packages are real source and this item does not
  apply.
- ILPostProcessors (Netcode/Burst/Entities codegen after compile), Burst compilation, IL2CPP.
- Package assemblies listed as "did not compile" (Purchasing.Stores/Codeless, InputSystem.ForUI) —
  dependents were compiled without them, and a project use of a type they declare is listed as
  unverified, not judged.

## Outputs, and the asset audit that reads them

Assets assemblies and their `.rsp` files land in `$TMPDIR/unity_refcompile_out/<tree>-<hash>/<config>/`, one
directory per working tree, so several worktrees can share a TMPDIR without reading each other's compile.
Package assemblies (and the `UnityEngine.UnityConsentModule` stub) are shared in
`$TMPDIR/unity_refcompile_out/_packages/`. `Schema/` binds the same
compilation, once with the player defines and once adding `UNITY_EDITOR`, and writes every type's serialized
fields, bases and enum members. `Tools/Build/check_generated_assets.py` audits YAML assets against that output,
so run this tool before it.


## Editor reference gaps (`EDITOR_REFERENCE_GAPS` in `build.py`)

Compiled together on bleeding-edge (2026-10-08), the 322 loose Editor-folder scripts give 37 errors in
10 files, and none of them is the code's fault. Both numbers move with the tree: the run prints the
first (`editor config: + N Editor-folder script(s)`) and lists the second, so re-read them there. Unity 6 editor API that the 2021.1 `UnityEditor`
reference lacks, and the test framework, which is not fetched.

| Gap | Errors | Files |
|---|---|---|
| `MaterialProperty.propertyType` (Unity 6) | 13 | `SSUShaderGUI.cs`, `CodingHelper.cs` |
| `NamedBuildTarget`, `PlayerSettings.Get/SetScriptingDefineSymbols` (2021.2) | 5 | `Build/CosmicShoreBuildPipeline.cs` |
| `PrefabStageUtility` out of `Experimental` (2021.2) | 3 | `CanvasUpgrader/CanvasUpgraderWindow.cs` |
| `EditorUtility.EntityIdToObject` (6000.2) | 1 | `FrogletTools/GameCanvasUnifier.cs` |
| `UnityEditor.TestTools` (test framework) | 10 | `AI/SkimRaceBenchmarkRemote.cs` |
| `LogAssert` (test framework) | 5 | four test files |

Each gap is matched on its whole error message and listed as "unverified" wherever it appears, so a
branch that changes one of these files is not failed by the reference set, and a typo on the same API
(`propertyTyp`, `LogAsert`) still gates. A newly used Unity 6 editor API reads as a project error until
it gets an entry: add one only for documented Unity API, with the version that introduced it.

## Known issues (open)

- **`check_generated_assets.py` audits committed changes only.** It lists assets with
  `git diff --name-status <base>...HEAD` (`changed_assets()`; re-grep, ~line 607), while `build.py`'s
  `changed_since()` counts uncommitted and untracked files too. So an audit run before committing
  reports `audited 0 added + 0 modified` and passes without having read your assets. Fix: reuse
  `changed_since()` and read changed assets from the working tree. The audit's base-relative
  "new findings only" filter needs the base blob, which `git show <merge-base>:<path>` still gives.
- **Burst is still needle-mirror's `1.6.0-pre.2` substitute, although the locked `1.8.29` is now
  reachable.** fetch.py falls back to packages.unity.com only when needle-mirror has NO tag for a
  package. Burst has an older tag there, so `NEAREST_TAG` keeps substituting it. On 2026-10-08,
  `curl -s https://packages.unity.com/com.unity.burst` listed 146 versions, 1.8.29 among them. Fix:
  try the registry tarball at the locked version before `NEAREST_TAG`, and keep the substitute as the
  fallback. Done when Burst compiles at 1.8.29 against the 6000.0 references with the player run
  still at 0 project errors. If it needs engine API those references lack, keep the substitute and
  say so here. SRP 17.3 and ugui 2.0 are builtin packages that the registry does not carry (checked
  the same day), so they stay as they are.
