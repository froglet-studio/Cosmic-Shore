# Conditional Compilation — `#if UNITY_EDITOR` and Release builds

**Read this before writing any script that uses `#if UNITY_EDITOR`,
`#if DEVELOPMENT_BUILD`, or the `UnityEditor` namespace — especially tooling,
diagnostics, benchmark, or debug-overlay scripts.**

This mistake has broken the automated build more than once. It is easy to make,
impossible to notice while working in the Editor, and it always surfaces as a red
Cloud Build rather than as anything local.

---

## The rule

> **A `using` that an unguarded declaration depends on must itself be unguarded.**
>
> Put differently: the guard has to cover a *self-consistent unit*. If the class
> declaration is outside the guard, everything the declaration needs must also be
> outside it.

---

## Why it bites

Unity compiles your scripts several times with different symbols defined:

| Configuration | `UNITY_EDITOR` | `DEVELOPMENT_BUILD` |
|---|---|---|
| In the Editor | **defined** | not defined |
| Development player build | not defined | **defined** |
| **Release player build** | **not defined** | **not defined** |

Almost all day-to-day work happens in the first row, where `UNITY_EDITOR` is
defined and every guarded region compiles. The Release column is the only one
where a guarded region actually disappears — and the only build that exercises it
is the automated one. So a file can be broken for weeks and look perfectly fine.

---

## The exact bug that broke the build

`LoadInsightsRuntime.cs` shipped like this:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Netcode;
using UnityEngine;              // ← guarded
using UnityEngine.SceneManagement;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    public class LoadInsightsRuntime : MonoBehaviour   // ← NOT guarded
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /* ...body... */
#endif
    }
}
```

In the Editor: symbol defined, usings present, compiles. In a Release build the
usings are stripped but `: MonoBehaviour` remains, so:

```
LoadInsightsRuntime.cs(27,40): error CS0246:
  The type or namespace name 'MonoBehaviour' could not be found
→ Error building Player because scripts had compiler errors
→ FATAL: Unity player export failed!
```

One missing line of coverage took the whole build down.

### The fix

`using UnityEngine;` moves **outside** the guard, because the class declaration
that needs it is outside:

```csharp
using UnityEngine;              // ← unguarded: the declaration below needs it
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    public class LoadInsightsRuntime : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /* ...body... */
#endif
    }
}
```

In Release this now compiles to an empty `MonoBehaviour` subclass — harmless, and
exactly what `DiagnosticsHUD` (the file `LoadInsightsRuntime` says it mirrors)
has always done.

---

## The two patterns that are safe

### Pattern 1 — guard the body, keep the shell (editor-only *behaviour*)

Use when the type must still exist in a player build (it is referenced by a
prefab, a scene, or `AddComponent`), but should do nothing there.

```csharp
using UnityEngine;              // ← ALWAYS unguarded
#if UNITY_EDITOR
using UnityEditor;              // ← editor-only usings guarded
#endif

public class MyDebugThing : MonoBehaviour
{
#if UNITY_EDITOR
    void Update() { /* editor-only work */ }
#endif
}
```

Reference implementations: `DiagnosticsHUD.cs`, `LoadInsightsRuntime.cs`.

### Pattern 2 — guard the whole file (editor-only *type*)

Use when the type has no reason to exist in a player build at all. The `#if` is
the **first line** and the `#endif` is the **last** — nothing is left outside.

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class MyEditorWindow : EditorWindow { /* ... */ }
#endif
```

Reference implementation: `Assets/Resolvers/ObjectResolver.cs` — the only file in the
project that needs this shape. It is the *last* resort, and the count says so: the two
files this line used to name (`ActiveGameModesWindow.cs`, `LeaderboardConfigSOEditor.cs`)
were deleted with the per-mode leaderboard path, and every other whole-file-guarded type
in the tree also sits under an `Editor/` folder, where the guard is belt-and-braces rather
than the thing doing the work.

**Better still:** if the type is purely editor-side, put the file under an
`Editor/` folder. Unity then compiles it into `Assembly-CSharp-Editor`, which
never enters a player build, and no guard is needed at all.

---

## Checklist before committing a guarded script

1. Is every `using` that an **unguarded** declaration needs also unguarded?
   `UnityEngine` is the usual casualty.
2. Does anything reference `UnityEditor` outside a `#if UNITY_EDITOR` guard in a
   file that is **not** under an `Editor/` folder? That never compiles in a player.
3. If the type is editor-only, should it just live in an `Editor/` folder instead?
4. Does an unguarded caller elsewhere use a member you just guarded? Guarding a
   method also removes it from Release for everyone who calls it.
5. Run the checker (below). It takes about a second.

---

## The automated check

```bash
python3 Tools/Build/check_conditional_compilation.py            # scan Assets/
python3 Tools/Build/check_conditional_compilation.py --self-test # verify the checker itself
```

It simulates the Release preprocessor in plain Python — no Unity install, no
license, about a second for ~1,550 files — and enforces:

- **Check A** — a type declared outside all guards whose base list needs a
  `using` that is inside a guard. (The bug above.)
- **Check B** — a runtime file touching `UnityEditor` outside a
  `#if UNITY_EDITOR` guard.

It runs on every pull request via the `conditional-compilation` job in
`.github/workflows/unity-ci.yml`. That job deliberately runs on
`ubuntu-latest` rather than the Unity runner, so it executes even while the
Unity CI tiers are queued waiting for a self-hosted runner.

The checker carries its own fixture suite (`--self-test`, also run in CI) because
during development it silently no-opped twice — once from a `continue` that
skipped Check B, once from a UTF-8 BOM hiding `#if` on line 1 — and reported "OK"
both times. **If you extend the checker, add a fixture.** A detector that stops
detecting is worse than no detector, because it buys false confidence.

### Known limits

The checker is a lexical approximation, not a compiler. It will not catch:

- A guarded **member** whose unguarded caller lives in another file (checklist
  item 4). A real Release compile is the only reliable way to find those.
- Types pulled in via a `using` this file does not itself declare (implicit
  `global using`, or a partial class's other half).
- Guards other than `UNITY_EDITOR` / `DEVELOPMENT_BUILD` / `false` (e.g.
  platform symbols like `UNITY_ANDROID`) — Check B only reasons about editor
  exclusion.

A green checker means "not this specific, repeated mistake." It does not mean
"the Release build will succeed."
