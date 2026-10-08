# unity_refcompile — compile the game's C# against real Unity references, headless

```
bash Tools/Build/unity_refcompile/run.sh                       # player config (the shipped IL2CPP release)
bash Tools/Build/unity_refcompile/run.sh --config player-dev   # + DEVELOPMENT_BUILD / ENABLE_PROFILER / DEBUG
bash Tools/Build/unity_refcompile/run.sh --config editor       # APPROXIMATE: UNITY_EDITOR branches + changed Editor-folder files
bash Tools/Build/unity_refcompile/run.sh --quiet-buckets       # count, don't list, the unverifiable buckets
```

Exit 0 = **no compile error in project code**; exit 1 = errors, listed, with every one in a file
changed since `origin/bleeding-edge` tagged `[CHANGED-TONIGHT]`; exit 2 = offline with no cache.
First run ~10 min (fetch + ~90 assemblies), later runs ~3 min (package assemblies are compiled once,
player-mode, into a shared cache keyed by input fingerprint; only Assets assemblies recompile).

**The `editor` config is approximate.** It compiles the project's runtime code with `UNITY_EDITOR`
(Mono, collections checks, `UNITY_INCLUDE_TESTS`). It also compiles the `Editor/`-folder files changed
since `--changed-base`, into the same compilation with NUnit (`com.unity.ext.nunit`). The references
are the newest **non-publicized `UnityEditor.dll` obtainable, 2021.1**, and the 6000.0 engine DLLs.
Those engine DLLs are player builds, so their own `#if UNITY_EDITOR` members are missing: for example,
`UIBehaviour.OnValidate` and `Reset` are reported as "unverified", not as errors. Packages stay
player-compiled. Use it to catch errors in editor branches. A green editor run is weaker evidence than
a green player run.

## What it does

A small re-implementation of Unity's script pipeline (`build.py`):

1. **Discovers every `.asmdef`/`.asmref`** in the fetched package sources and `Assets/`, evaluates
   `includePlatforms`/`excludePlatforms` (WindowsStandalone64), `defineConstraints`,
   `versionDefines`, `overrideReferences`/`precompiledReferences`/`autoReferenced`, name and GUID
   references.
2. **Assigns loose scripts** to `Assembly-CSharp-firstpass` (`Assets/Plugins`, `Standard Assets`)
   and `Assembly-CSharp`; `Editor/` folders are skipped in the player configs.
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
   method-body error in every other file. `Diagnose` binds all bodies regardless.
7. Buckets the errors: **project errors** (the gate), **missing-type errors in files that `using` a
   package that cannot be fetched**, and **unverified**: a missing-type error (CS0246, CS0234,
   CS0103, CS1069, CS0012, CS0538) that names something a **referenced assembly that did not
   compile** declares. `Diagnose --declarations` reads each failed assembly's own sources, with its
   own defines, for the namespaces and top-level types they declare. The error is unverified only
   when it names that assembly (CS0012/CS1069), a namespace only it declares, or one of its types
   that the file can see: the type's namespace is `using`d (or `using static`), encloses the file,
   or the name is fully qualified. Every other missing-type error gates, so a misspelled local
   (CS0103) or type (CS0246) fails the run, even though three package assemblies fail on every run.
   In `--config editor`, a changed Editor-folder file also gets the declarations of
   `com.unity.test-framework`, which is never fetched. `test_framework_declarations.tsv` holds them,
   with the command that made it, so `LogAssert` and `[UnityTest]` are unverified there and a
   misspelling still gates.
   `python3 Tools/Build/unity_refcompile/build.py --self-test` checks the rule on fixtures in about
   a second, with no .NET SDK and no cache.

## Where the references come from (fetched by `fetch.py`, cached, never committed)

Cache: `${UNITY_REFCOMPILE_CACHE:-$TMPDIR/unity_refcompile_cache}` (~550 MB; files a compile does
not read are pruned on fetch). Network needed once: `api.nuget.org` and `github.com` (read-only git
clones). Offline with no cache → exit 2 with a message; offline with a cache → reuses it.

| What | Source | Real or not |
|---|---|---|
| UnityEngine modules, UnityEngine.UI, TextMeshPro | nuget `Digitalroot.References.Unity` **6000.0.75** (no 6000.3 build is published anywhere reachable) | real engine metadata, **publicized** by the re-packer — see below |
| Accessibility oracle | nuget `Unity3D.SDK` 2021.1.14.1 `UnityEngine.dll` (non-publicized) | real |
| netstandard facades | nuget `NETStandard.Library` 2.0.3 + the SDK's `NETStandard.Library.Ref` 2.1 | real |
| Every registry package in `packages-lock.json` (Entities 1.4.2, Entities.Graphics 1.4.15, Collections 2.6.6, Mathematics 1.3.3, Netcode 2.5.0, Transport 2.6.0, InputSystem 1.14.2, Cinemachine 3.1.2, Services.Core/Auth/CloudSave/Analytics, Purchasing, Splines, Timeline, ...) | source at the **exact locked tag** from the `needle-mirror` GitHub mirrors | real source |
| UniTask, Reflex, ParrelSync | source at the locked commit | real source |
| SRP Core / URP / URP-config / ShaderGraph / VFX | `Unity-Technologies/Graphics` branch `6000.0/staging` | real source, **17.0.x not the locked 17.3.0** (17.3 needs 6000.3-only engine API the references lack) |
| Burst | needle-mirror `1.6.0-pre.2` | **substitute** for locked 1.8.29 (newest mirrored tag) |
| `UnityEngine.UnityConsentModule` | `stubs/UnityEngine.UnityConsentModule.cs` (3 members Analytics uses) | **stub** — 6000.0.75 only type-forwards to it |
| Services.Multiplayer / Friends / Leaderboards, Multiplayer.Playmode / Widgets | not mirrored anywhere reachable | **absent** — files that `using` them are bucketed, not gated |

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

### Source patches (`source_patches.json`)

Body-local edits to fetched package sources where a package head needs engine API newer than the
references (one today: `GPUDrivenPackedMaterialData.hasTessellation`). Never applied to Assets.

## What a green run proves, and what it does not

Proves: every project `.cs` compiled into the player assemblies (Assembly-CSharp, -firstpass,
CosmicShore.Data, Obvious.Soap, FMOD, NiceVibrations, ...) type-checks against the real package
sources at their locked versions and the real (6000.0.75) engine API — including source generators.

Does not prove:
- `#if UNITY_EDITOR` blocks and `Editor/` folders against the real Unity 6 `UnityEditor`: none is
  obtainable. The `editor` config checks them against 2021.1, and only the changed Editor-folder files.
- Engine API added between 6000.0.75 and 6000.3.17 (would show as a false error, not a false pass);
  engine members whose accessibility changed in a way neither the 2021.1 oracle nor package code
  reveals (stay public: a possible false pass on use of an engine internal).
- Code in files that `using` an unfetchable package, for errors that involve those types.
- ILPostProcessors (Netcode/Burst/Entities codegen after compile), Burst compilation, IL2CPP.
- Project code that uses types from package assemblies listed as "did not compile"
  (Purchasing.Stores/Codeless, InputSystem.ForUI). Dependents were compiled without them, so any
  such use is reported as **unverified**, by name, and does not gate (step 7). No Assets file uses
  their namespaces today.

## Outputs, and the asset audit that reads them

Assets assemblies and their `.rsp` files land in `$TMPDIR/unity_refcompile_out/<tree>-<hash>/<config>/`, one
directory per working tree, so several worktrees can share a TMPDIR without reading each other's compile.
Package assemblies are shared in `$TMPDIR/unity_refcompile_out/_packages/`. `Schema/` binds the same
compilation, once with the player defines and once adding `UNITY_EDITOR`, and writes every type's serialized
fields, bases and enum members. `Tools/Build/check_generated_assets.py` audits YAML assets against that output,
so run this tool before it.


## Known issues (open)

- **`--config editor` reports false `CS0118 'Editor' is a namespace but is used like a type`.**
  Seen 2026-10-06 on `claude/serene-edison-lfv24f`: 4 errors in three untouched runtime files
  (`Controller/Camera/CameraSettingsSOEditor.cs:8`, `UI/ResourceDisplay.cs:286`,
  `UI/UniversalStatsProviderEditor.cs:14` plus a follow-on CS1503 at `:40`), all from a branch that
  changed `Assets/_Scripts/Editor/UrpAssetPlayModeRestore.cs` (`namespace CosmicShore.Editor`). The
  editor config compiles changed Editor-folder files INTO the runtime compilation, so a runtime class
  inside `namespace CosmicShore.*` that derives from UnityEditor's `Editor` resolves the name to the
  namespace first. 123 Editor-folder files declare `CosmicShore.Editor` and no runtime file does,
  so any branch touching one of them gets these. Fix: compile changed Editor-folder files as a
  separate Assembly-CSharp-Editor that references Assembly-CSharp. Done when such a branch reports
  0, and a planted error in an Editor file and in a runtime `#if UNITY_EDITOR` block still fail.
- **`--config editor` only sees Editor-folder files COMMITTED since `--changed-base`.** The set
  is `git diff --name-only <base>...HEAD`, so a NEW test file that is still untracked or only
  staged is not compiled at all — and a planted error in it passes green (seen 2026-10-08 on
  `cece/dreamy-fermat-szo9ck`: a runtime plant failed the run, the same plant in a new
  `Tests/Editor/*.cs` did not). Commit first, then run the editor config; the run then lists the
  file under `editor config: + N Editor-folder file(s)`. Fix: union in `git status --porcelain`
  paths (an unmerged branch, `claude/hopeful-heisenberg-murjor`, does this).
- **`depublicize()` needs a net8.0 reference pack under `DOTNET_ROOT`.** With only a .NET 10 SDK it
  raises `IndexError: list index out of range` (`build.py`, the `Microsoft.NETCore.App.Ref/*/ref/net8.0`
  glob) after the full fetch. Workaround: point `DOTNET_ROOT` at an 8.0 install. Fix: accept any
  installed ref pack, or say which one is missing.
