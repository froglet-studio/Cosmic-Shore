<div class="sec-eyebrow">Part II · Cross-cutting</div>

# Threading & main-thread affinity

This is the subtlest part of the stack and the source of the hardest-won lesson. The one-sentence
rule: **wrap every cross-thread `await` in `.AsMainThread()`** — and do *not* reach for UniTask's own
thread-switch primitives, because they don't work reliably on this version.

## The problem

UGS and Netcode methods return `System.Threading.Tasks.Task`. Their continuations land on whatever
thread the SDK's HTTP/WebSocket pump completes on — typically the .NET ThreadPool. From there:

- Any `UnityEngine.Object` access — *including* `obj == null`, which routes through
  `Object.op_Equality → EnsureRunningOnMainThread` — throws.
- Any SOAP `ScriptableEvent.Raise()` invokes its listeners **inline on the calling thread**, so a UI
  listener that touches a `CanvasGroup` crashes one level deeper.

::: figure threading-cascade
Left: a naive await lets a ThreadPool continuation raise a SOAP event whose listener touches Unity
state — crash. Right: `.AsMainThread()` re-asserts Unity's `SynchronizationContext` so the
continuation and any SOAP raise run on the main thread.
:::

## Why the obvious UniTask primitives don't fix it

UniTask 2.x intentionally bypasses `SynchronizationContext` ("UniTask always works like
`Task.ConfigureAwait(false)`"). The consequence, verified on this version:

| Primitive | What it does here | Verdict |
|---|---|---|
| `UniTask.SwitchToMainThread()` | Awaiter reports complete from the pool → continuation runs **inline** on the pool. No switch. | Broken |
| `UniTask.Yield(PlayerLoopTiming.Update)` | Yields, but the continuation queue doesn't marshal through the SyncContext, so it can resume on a worker. | Broken |
| `UniTask.NextFrame()` / `DelayFrame(1)` | Same bypass internally. | Broken |

## The fix

A `MainThreadDispatcher` built on **Unity's own** `SynchronizationContext`, captured before any scene
loads:

```csharp
public static class MainThreadDispatcher
{
    static SynchronizationContext _mainContext;
    static int _mainThreadId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        _mainContext  = SynchronizationContext.Current;       // Unity's context
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public static bool IsOnMainThread =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    public static UniTask SwitchToMainThreadAsync()
    {
        if (IsOnMainThread) return UniTask.CompletedTask;
        var tcs = new UniTaskCompletionSource();
        _mainContext.Post(_ => tcs.TrySetResult(), null);     // drains on main thread
        return tcs.Task;
    }
}
```

`.AsMainThread()` is the boundary helper callers actually use — four overloads cover `Task`,
`Task<T>`, `UniTask`, and `UniTask<T>`, so the surface is identical regardless of what the SDK returns:

```csharp
// Continuation guaranteed on the main thread:
ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(opts).AsMainThread();
connectionData.IsPartyHost = true;   // safe — raising SOAP here won't crash
```

## The canary

`SceneTransitionManager.SetFadeImmediate` is on a hot path and touches a `CanvasGroup`. It checks
`MainThreadDispatcher.IsOnMainThread` and logs a loud `Debug.LogError` (naming the helper to add) if it
is ever reached off-thread. If a future feature forgets a `.AsMainThread()`, the canary fires
immediately instead of producing a mysterious crash later.

::: insight The history is the warning
The fix took seven commits — two broad `SwitchToMainThread` migrations and a `Yield(Update)` attempt
were all tried and reverted before the `SynchronizationContext` approach stuck. That history is kept
in the docs precisely so the next person who reaches for `SwitchToMainThread()` stops and reads first.
:::
