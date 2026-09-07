# Threading & Main-Thread Affinity

**Audience:** anyone awaiting a UGS / Netcode `Task` or a `UniTask` that crosses the thread pool.
**TL;DR:** wrap every cross-thread `await` in `.AsMainThread()`. Don't use
`UniTask.SwitchToMainThread()` or `UniTask.Yield(PlayerLoopTiming.Update)` for thread
marshaling — they don't work reliably on this UniTask version.

---

## 1. The problem in one paragraph

UGS-SDK (`Unity.Services.Multiplayer`, `Unity.Services.Friends`, etc.) and Netcode-for-GameObjects
methods return `System.Threading.Tasks.Task`. When you `await` one, the continuation lands on
whatever thread the SDK's HTTP / WebSocket pump completes on — typically the .NET ThreadPool.
From the ThreadPool:

- Any `UnityEngine.Object` access (incl. `obj == null`, which routes through
  `Object.op_Equality` → `GetInstanceID` → `EnsureRunningOnMainThread`) throws.
- Any `Obvious.Soap.ScriptableEvent.Raise()` invokes its listeners **inline on the calling thread**
  (confirmed by reading `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEventNoParam.cs`) —
  so if a listener touches Unity state, it crashes too.

If you wire SOAP events to UGS-callback chains naively, your `OnEstablished` listener fires on
the ThreadPool, that listener does `tcs.TrySetResult()`, which synchronously fires every awaiter
of `tcs.Task` (still on the ThreadPool), and somewhere downstream a UI MoveNext touches a
`CanvasGroup` and the editor blows up with `EnsureRunningOnMainThread`.

---

## 2. Why the obvious UniTask primitives don't fix this

UniTask 2.x **intentionally bypasses `SynchronizationContext` and `ExecutionContext`** as a
documented design decision. The official docs say it explicitly:

> *"UniTask always works like `Task.ConfigureAwait(false)` and is not guaranteed that the thread
> before awaiting UniTask may match the thread after awaiting."*

That single sentence is why every UniTask-native main-thread switch we tried failed:

| Primitive | What it claims | What it does on `com.cysharp.unitask@86b6e6a2e286` | Verdict |
|---|---|---|---|
| `await UniTask.SwitchToMainThread()` | Switch to Unity main thread | Awaiter's `IsCompleted` returns `true` when called from ThreadPool → continuation runs **inline** on ThreadPool. No switch. | **Broken on this version.** |
| `await UniTask.Yield(PlayerLoopTiming.Update)` | Yield until next PlayerLoop.Update | Yields. But UniTask's `ContinuationQueue` does **not** capture or marshal through `SynchronizationContext`, so the resumption can run on a worker thread. | **Broken on this version.** Known issue ([UniTask#561](https://github.com/Cysharp/UniTask/discussions/561)). |
| `await UniTask.NextFrame()` / `DelayFrame(1)` | Wait one frame | Same as `Yield(Update)` internally — same bypass, same flaw. | **Broken on this version.** |

References (recorded here so the next session doesn't have to re-find them):

- [Cysharp/UniTask#319 — Thread Context Preservation](https://github.com/Cysharp/UniTask/issues/319) — confirms the SyncContext bypass is by design.
- [Cysharp/UniTask#561 — "Yield(PlayerLoopTiming.Update) is not resuming at Update sometimes"](https://github.com/Cysharp/UniTask/discussions/561) — exact symptom we hit.
- [Cysharp/UniTask#151 — SwitchToMainThread quirks](https://github.com/Cysharp/UniTask/issues/151).
- [Unity Discussions — request to expose `Object.CurrentThreadIsMainThread()`](https://discussions.unity.com/t/could-object-currentthreadismainthread-be-exposed-publicly/749484) — confirms no public Unity API for the check.

---

## 3. The fix — `MainThreadDispatcher` + `.AsMainThread()`

We bypass UniTask's bypass by going through **Unity's own `SynchronizationContext`**, which is
properly main-thread-bound.

### 3.1 `MainThreadDispatcher` (the primitive)

`Assets/_Scripts/Utility/MainThreadDispatcher.cs`

```csharp
public static class MainThreadDispatcher
{
    static SynchronizationContext _mainContext;
    static int _mainThreadId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        _mainContext  = SynchronizationContext.Current;
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public static bool IsOnMainThread =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    public static UniTask SwitchToMainThreadAsync()
    {
        if (IsOnMainThread) return UniTask.CompletedTask;
        var tcs = new UniTaskCompletionSource();
        _mainContext.Post(_ => tcs.TrySetResult(), null);
        return tcs.Task;
    }
}
```

Why this is reliable:

- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` is documented (and empirically reliable) to
  run on Unity's main thread before any scene loads. So `SynchronizationContext.Current` at that
  point is Unity's `UnitySynchronizationContext`, and `Thread.CurrentThread` is Unity's main thread.
- `SynchronizationContext.Post(...)` enqueues a callback onto that context. Unity's context drains
  on the main thread. When the callback runs, we're on the main thread.
- The awaiter completes from inside the callback → the continuation is also on the main thread.

### 3.2 `.AsMainThread()` (the boundary helper)

`Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs`

```csharp
public static async UniTask AsMainThread(this Task task)
{
    await task;
    await MainThreadDispatcher.SwitchToMainThreadAsync();
}
// + overloads for Task<T>, UniTask, UniTask<T>
```

Four overloads cover every flavour of awaited cross-thread work. **The same surface for callers
regardless of whether the SDK returns `Task` or `UniTask`.**

### 3.3 The contract callers follow

> Every `await` of a UGS-SDK call, Netcode call, or any other Task that may complete off-thread
> uses `.AsMainThread()`.

```csharp
// Bad — continuation may land on ThreadPool:
await MultiplayerService.Instance.CreateSessionAsync(opts);
connectionData.IsPartyHost = true;          // off-thread → SOAP raise crashes

// Good — continuation guaranteed on main thread:
ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(opts).AsMainThread();
connectionData.IsPartyHost = true;          // safe
```

Chain of UGS awaits stays on the main thread because each `.AsMainThread()` reasserts the
invariant at every boundary.

---

## 4. The canary — `SceneTransitionManager.SetFadeImmediate`

`Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs` line ~271:

```csharp
public void SetFadeImmediate(float alpha)
{
    if (!MainThreadDispatcher.IsOnMainThread)
    {
        Debug.LogError(
            "[SceneTransitionManager] SetFadeImmediate called off main thread — " +
            "caller forgot `.AsMainThread()` on a UGS / Netcode Task await " +
            "(see UniTaskExtensions.cs). Ignoring to avoid EnsureRunningOnMainThread.");
        return;
    }
    // ...
}
```

This is the trip-wire. `SetFadeImmediate` is on a hot path (every scene transition fades it),
and it touches a `CanvasGroup` — exactly the kind of access that crashes off-thread. If a new
UGS call site is added later and someone forgets `.AsMainThread()`, the canary's `Debug.LogError`
fires immediately with `EnsureRunningOnMainThread` mentioned in the message, naming the helper
to use. Cheaper than a crash.

Both `SetFadeImmediate` and `AsMainThread()` read `MainThreadDispatcher.IsOnMainThread` — one
source of truth, no risk of divergent capture sites.

---

## 5. When to use which primitive

| Situation | Use |
|---|---|
| `await` a UGS / Netcode / any cross-thread `Task` | `.AsMainThread()` |
| `await` a `UniTask` you wrote that internally awaits UGS | `.AsMainThread()` at the inner site, no wrapping needed at the caller |
| Need to switch to main thread *without* a Task to attach to (e.g., at the start of a `catch` block whose body touches Unity state) | `await MainThreadDispatcher.SwitchToMainThreadAsync()` |
| Need to yield one frame so PlayerLoop processes pending work (NOT for thread marshaling) | `await UniTask.Yield(PlayerLoopTiming.Update)` — fine, just don't rely on it for thread affinity |
| Asserting we're on main thread (debug only) | `MainThreadDispatcher.IsOnMainThread` |

There are exactly three `Yield(PlayerLoopTiming.Update)` calls remaining in
`Controller/Party/PartyInviteController.cs` (in catch / recovery blocks) — they're "wait for the
next PlayerLoop tick" semantics, not threading. Leave them alone.

### 5.1 An `async UniTaskVoid` "pump" does its first unit of work on the CALLER's frame

Not a thread-affinity issue — a *scheduling* one, and it defeats the entire point of a
work-spreading loop if you miss it. **A C# async method body runs synchronously on the caller's
stack until its first suspension.** So this:

```csharp
async UniTaskVoid Pump(CancellationToken ct)
{
    while (...)
    {
        DoOneExpensiveThing();                              // <-- runs on the caller's frame
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
}
```

started from a hot frame (`OnClientReady`, a spawn loop, an `Update` that noticed a state change)
does `DoOneExpensiveThing()` **on that frame**, no matter how carefully the rest is spread out.
The toy-emblem streamer shipped this way: the first toy's icon — a `Shader.Find` chain, a save-file
read and a mesh build — landed on the exact Menu_Main spawn frame the streamer existed to protect,
and a later rebuild ran a 57k-vertex mesh assembly inline inside `Update`.

**Fix: yield before the first unit of work**, not after it.

```csharp
await UniTask.Yield(PlayerLoopTiming.Update, ct);   // first statement
while (...) { DoOneExpensiveThing(); await UniTask.Yield(...); }
```

Note that the same synchronous prefix is load-bearing elsewhere and must NOT be "fixed": a
bloom-in helper relies on it to zero a transform's scale before the first render
(`ToyFactory.ScaleInFromZero`), and a `_running` re-entrancy guard set before the first await only
works because of it. Know which one you're writing.

---

## 6. History of the fix (for future regressions)

| Commit | What it tried | Verdict |
|---|---|---|
| `4d7ce98c5` | `await CreateOwnPartySessionAsync()` race-guard inside `SendInviteAsync` | **KEEP** — separate concern, real race, unrelated to threading |
| `c2865c8b0` | First `await UniTask.SwitchToMainThread()` inside `PartySessionService.CreateAsync` | Removed — broken on this UniTask version |
| `7e096d4d1` | Broad `SwitchToMainThread` migration across 25 sites | Removed — same flaw, scaled |
| `124d07c6c` | `SwitchToMainThread()` *after* UGS awaits in HCS / AuthSceneController / PartyInviteController; thread-id canary; full-exception logging | Canary and full-exception logging **KEPT**; SwitchToMainThread inserts removed |
| `e39c22893` | Replaced SwitchToMainThread with `Yield(PlayerLoopTiming.Update)` | Removed — yields, but doesn't guarantee main-thread resumption |
| `e67cf819c` | One `.AsMainThread()` boundary helper, ~32 call sites | Wrapper retained; internals replaced again in next commit |
| `6a544e30e` | `MainThreadDispatcher` + `.AsMainThread()` rebuilt on Unity's `SynchronizationContext` | **CURRENT FIX.** |

If you find yourself reaching for `UniTask.SwitchToMainThread()` or
`Yield(PlayerLoopTiming.Update)` to fix a "thing-runs-off-main-thread" bug, **stop and re-read
this document first**. We have already tried both, and they don't work.

---

## 7. Verification recipe

1. **Run the host alone.** Console must show:
   - `[HostConnectionService] Solo party session ready: <id> — InParty, vessel will spawn.`
   - `[AuthScene] Relay session confirmed live (attempt 1/3).`
   - `[AuthScene] Loading Menu_Main via network scene management...`
   - **No** `[SceneTransitionManager] SetFadeImmediate called off main thread` warning.
   - **No** `EnsureRunningOnMainThread` exception.

2. **Grep should pass:**
   ```
   grep -rn "SwitchToMainThread" Assets/_Scripts/
   ```
   should return zero hits outside `UniTaskExtensions.cs`'s doc comment.

   ```
   grep -rn "MainThreadDispatcher" Assets/_Scripts/
   ```
   should return ~6 hits: the dispatcher file, the four `AsMainThread()` overloads, and the
   canary in `SceneTransitionManager`.

3. **If the canary fires after a new UGS-aware feature ships:** the new code missed an
   `.AsMainThread()` somewhere. The canary's `Debug.LogError` is the call-graph anchor —
   read the stack and add the wrapper at the offending await.

---

## 8. Files to know

| File | Role |
|---|---|
| `Assets/_Scripts/Utility/MainThreadDispatcher.cs` | The primitive. SynchronizationContext-based switch. |
| `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs` | `.AsMainThread()` overloads (Task, Task<T>, UniTask, UniTask<T>) + `WaitOneFrame` helpers. |
| `Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs` | Canary at `SetFadeImmediate`. |
| `Assets/_Scripts/Controller/Party/HostConnectionService.cs` | Heaviest caller of `.AsMainThread()`. |
| `Assets/_Scripts/Controller/Party/PartyInviteController.cs` | Second-heaviest caller. Three retained `Yield(Update)` calls in catch blocks — those are intentional. |
| `Assets/_Scripts/Controller/Party/Services/PartySessionService.cs` | UGS session lifecycle, every UGS call uses `.AsMainThread()`. |
| `Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs` | UGS lobby lifecycle, same pattern. |
| `Assets/_Scripts/Controller/Party/Services/LobbyPropertyWriter.cs` | UGS property writes, same pattern. |
| `Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs` | Only awaits our own UniTask facades — no direct UGS Task, so no `.AsMainThread()` needed at this layer. |
| `Assets/_Scripts/System/FriendsServiceFacade.cs` | UGS Friends SDK, every call uses `.AsMainThread()` (12 sites). |
| `Assets/_Scripts/System/AuthenticationSceneController.cs` | `LoadMainMenuNetworkedAsync` — uses `.AsMainThread()` on every relay-wait. |

## `.AsMainThread()` covers the SUCCESS path only (2026-08-27)

```csharp
try   { await SomethingAsync(linkedCts.Token).AsMainThread(); }
catch (OperationCanceledException)
{
    // ← resumes on the TIMER's thread. The marshal above never ran: the exception
    //   propagated out of the inner await, before .AsMainThread()'s continuation.
    await MainThreadDispatcher.SwitchToMainThreadAsync();   // REQUIRED
    ...Unity APIs...
}
```

Shipped instance: `AuthenticationSceneController.LoadMainMenuNetworkedAsync` read
`Application.internetReachability` in its post-loop offline fallback and threw
`get_internetReachability can only be called from the main thread` whenever the Relay wait timed
out — i.e. on every offline boot, the one path that most needed to work.

**Rule:** any `catch` after a cancellable await, and any code after a loop containing one, must
marshal explicitly. On these paths a timeout is not an edge case, it is the path. See
`Docs/OFFLINE_MODE.md` §9.2.

## The Cloud Save + Auth boundary had NO marshal at all (2026-09-03)

`grep -c AsMainThread` returned **0** for `UGSDataService.cs`, `CloudDataRepository.cs`,
`UGSCloudSaveProvider.cs` and `AuthenticationServiceFacade.cs` — the four files that make up the
entire boot chain between "signed in" and "the main menu is usable". Every UGS `Task` in them was
awaited bare.

Two of the resulting continuations touch Unity immediately:

| Site | What runs on the continuation |
|---|---|
| `UGSDataService.InitializeAsync` → `await Task.WhenAll(…10 repository loads…)` | `SyncHangarToVessels()`, which writes `SO_Vessel` assets |
| `AuthenticationServiceFacade` → `await SignInAnonymouslyAsync()` | `OnSignInSuccess()` → `authenticationData.OnSignedIn.Raise()`, whose listeners instantiate `NetworkManager`, start the presence lobby, and load cloud data — **inline**, because SOAP raises inline |

Both throw `EnsureRunningOnMainThread` off-thread, and in both cases the throw is **swallowed**:
`PlayerDataService.HandleSignedIn` is `async void` with a catch that logs one line, and the
facade's own `catch` treated it as a sign-in *failure*. So the observable symptom was neither an
exception nor a failure — it was a boot that sat in the Authentication scene, with the only clue
`[Analytics] DROPPING EVENTS - UGS sign-in has not completed` at quit.

Three companion defects in the same file, each of which alone can produce the same silence:

1. **`AuthenticationService.Instance` THROWS, it never returns null** (verified against
   `com.unity.services.authentication` 3.6.1). Every `Instance != null` guard in the facade was
   dead code that raised `ServicesInitializationException` instead of taking its own guarded
   branch. Routed through `TryGetAuthService(out …)`.
2. **`EnsureInitializedAsync` trusted our own SOAP mirror** (`AuthenticationData.State`) instead
   of `UnityServices.State`. `State` is a plain auto-property on a class held by a
   ScriptableObject: Unity does not serialize it and SOAP never resets it, so with this project's
   disabled domain reload (`m_EnterPlayModeOptions: 3`) the *second* Play of an editor session
   started holding the *first* session's `SignedIn` — and skipped `UnityServices.InitializeAsync`
   entirely. Meanwhile the SDK resets itself on every Play
   (`[RuntimeInitializeOnLoadMethod] ResetStaticsOnLoad`), so the mirror and the truth were
   guaranteed to disagree. The guard now asks the SDK; the facade, as single writer, resets the
   mirror in its constructor (CLAUDE.md's SOAP runtime-state rule).
3. **`OnSignInSuccess()` sat inside the sign-in `try`**, so a throwing *listener* was reported as
   a failed sign-in — flipping the state to `Failed` on a session that had in fact signed in.

**Rules this leaves behind.** A mirror of another system's state is a *report*, never the
authority — reconcile with what is independently readable (the same rule
`AnalyticsServiceFacade` records for its `_signedIn` latch). A raise is not part of the operation
that triggered it: keep it out of the operation's `try`. And a failure on a path whose only
symptom is *waiting* must log unprompted — `AuthenticationServiceFacade.LogFailure` is
deliberately not gated on the verbose flag.
