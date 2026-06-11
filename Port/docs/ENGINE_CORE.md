# Engine Core — Design Reference

The decisions that make ported gameplay files compile and behave **verbatim** without
Unity. Locked as of iteration 2; extend rather than relitigate.

## Component / scene model

**Decision: keep the MonoBehaviour-shaped API exactly.** Ported files keep their
original private zero-arg lifecycle methods (`void Update()` etc.); the engine
discovers them reflectively per concrete type and compiles them to cached delegates
(`LifecycleHooks`). This is what lets ~1,300 gameplay files port without signature
rewrites.

Class hierarchy (namespace `CosmicShore.Engine`, flat — `using CosmicShore.Engine;`
is the port of `using UnityEngine;`):

```
Object                      — name, fake-null contract, Destroy/DestroyImmediate
├── ScriptableObject        — data assets (JSON-backed registry in content phase)
├── GameObject              — component container, activeSelf/activeInHierarchy, layer
└── Component               — gameObject/transform accessors, GetComponent* family
    ├── Transform           — local TRS authoritative; world composes via parent chain
    └── Behaviour           — enabled / isActiveAndEnabled
        └── MonoBehaviour   — lifecycle wiring, execution order, exception isolation
```

### Lifecycle semantics (parity contract)

| Event | When |
|---|---|
| `Awake` | Once: on AddComponent to an active-in-hierarchy object, or on first activation. Fires even if the behaviour is disabled. |
| `OnEnable` | Behaviour enabled while object active; on activation; after Awake on attach. |
| `Start` | Once, at the top of the first Tick where the behaviour is active+enabled (before FixedUpdate/Update that frame). Deferred if disabled before it ran. |
| `Update` / `FixedUpdate` / `LateUpdate` | Only when `isActiveAndEnabled` and started. Exceptions are caught per-behaviour and logged (one bad script can't break the frame). |
| `OnDisable` | Disable, deactivation, or destruction (before OnDestroy). |
| `OnDestroy` | End-of-frame for `Object.Destroy`, immediate for `DestroyImmediate`. Only if Awake ran. |

`[DefaultExecutionOrder(n)]` sorts all phase iteration (lower first, insertion order
tiebreak). The fake-null contract (`destroyed == null` → true, implicit bool false)
flips at actual destruction time — end of the Destroy frame — matching the original.

**Known deviations** (acceptable, documented): a behaviour enabled mid-frame gets its
Start/first-Update the *next* frame (the original could run it later the same frame).
`Instantiate` (prefab cloning) is deferred to the content-pipeline phase — prefabs
become factory descriptions. Coroutines (`StartCoroutine`) are not yet ported; most
gameplay code uses UniTask-style async, which maps to GameTask (below). Physics
(colliders/rigidbodies) is a phase-2 design.

## Frame loop (`GameLoop`)

One loop per process (fail-loud on a second), owning Scene, Scheduler, and
SynchronizationContext. `Tick(dt)`:

```
Time.Advance(dt)
SyncContext.Pump()            ← external-thread continuations land here
drain Start queue
FixedUpdate × accumulator     ← Time.deltaTime reports fixedDeltaTime inside
Update
Scheduler.RunFrame()          ← Yield/Delay/WaitUntil resume (coroutine timing point)
LateUpdate
Scheduler.RunEndOfFrame()
flush destroy queue           ← deferred destruction lands; fake-null flips here
```

Headless first: tests call `Tick`/`Run(frames, dt)` directly with fixed deltas
(deterministic); realtime hosts (CLI `--render`, later) drive it from a wall clock.
`Time.timeScale` scales frame time and gates fixed steps; `unscaled*` variants bypass.

## Async model (`CosmicShore.Engine.Tasks`) — replaces UniTask

Custom awaitables that enqueue continuations into the loop's `GameTaskScheduler`:
`GameTask.Yield / Delay(s|ms) / WaitUntil / WaitWhile / WaitForEndOfFrame /
SwitchToMainThread`, plus `Task.Forget()` (logs faults, swallows cancellation).
Async methods are plain `async Task`.

Two contracts ported code depends on, both guaranteed structurally:

1. **Main-thread affinity.** Continuations resume on the loop thread during scheduler
   phases. `GameSynchronizationContext` is installed for the duration of every Tick,
   so awaiting *external* Tasks (sockets, file IO, backend SDKs) marshals back to the
   loop automatically. The entire Unity-era `MainThreadDispatcher` / `.AsMainThread()`
   discipline (Docs/THREADING.md) is retired — there is no off-thread resume to guard
   against. SOAP raises stay inline and therefore stay on the loop.
2. **Synchronous cancellation.** `cts.Cancel()` runs the awaiting continuation (its
   catch/finally) *before Cancel returns*, via `CancellationToken.Register` on a
   once-only `ScheduledItem`. Ported patterns like TransformExtensions.ResizeForSeconds
   (cancel old animation → its finally restores state → start new one) depend on this
   ordering; the regression test covers it.

`WaitUntil` completes synchronously if the predicate is already true, and predicate
exceptions fault the awaiting task (both UniTask parity).

## DI (`CosmicShore.Engine.Injection`) — replaces Reflex

`Container` with the registration surface AppManager's composition root uses:
`RegisterValue<T>(instance)` (contract-exact, no assignable scanning),
`RegisterFactory<T>(f)` (lazy singleton, created once on first resolve),
`Resolve<T>` (walks parent chain, throws with a clear message when missing — fail
loud), `CreateChild()` (Bootstrap root → per-scene scopes), `Inject(target)`
([Inject] fields and settable properties, private and inherited included, cached
plans), `InjectGameObject(go, recursive)` (the GameObjectInjector.InjectRecursive
replacement), and `IInstaller`.

Injection timing in ported scenes will follow the original contract (inject after
Awake, before Start) once scene loading exists; until then call sites inject
explicitly after construction.

## Logging

`CosmicShore.Engine.Debug` (Log/Warning/Error/Exception/Assert + Format/context
overloads) → pluggable `ILogSink` (`ConsoleLogSink` default, `CapturingLogSink` for
tests). `CSDebug` is ported verbatim on top; its compile-time stripping maps
`[Conditional("UNITY_EDITOR")/("DEVELOPMENT_BUILD")]` → `[Conditional("DEBUG")]`
(Release builds strip info logs, same intent).

## Using-directive mapping (additions this iteration)

| Unity-era | Port |
|---|---|
| `using Cysharp.Threading.Tasks;` | `using CosmicShore.Engine.Tasks;` + `using System.Threading.Tasks;` |
| `async UniTask` / `UniTaskVoid` | `async Task` (+ `.Forget()` at fire-and-forget sites) |
| `UniTask.Yield(PlayerLoopTiming.Update[, ct])` | `GameTask.Yield([ct])` |
| `UniTask.Delay / WaitUntil / WaitWhile` | `GameTask.Delay / WaitUntil / WaitWhile` |
| `.AsMainThread()` (UGS awaits) | delete — affinity is structural now |
| `Debug = UnityEngine.Debug` alias | `Debug = CosmicShore.Engine.Debug` |
