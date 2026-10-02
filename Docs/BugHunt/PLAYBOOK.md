# Bug Hunt Playbook

How to find, fix and prove the console problems this project keeps producing, by problem class.
Worked examples are in [`FIX_LOG.md`](FIX_LOG.md); the workflow is in [`README.md`](README.md).

## Getting the log

- **Windows:** `C:\Users\<user>\AppData\Local\Unity\Editor\Editor.log`. It is overwritten on
  every editor start, and the previous session is kept as **`Editor-prev.log`** in the same
  folder. Copy it before restarting twice.
- The Console clears on domain reload; the log doesn't. Anything printed at editor quit (leak
  reports) is only in `Editor-prev.log` after a restart.
- In-editor capture: the **Bug Ledger** auto-files every distinct error signature, and the
  **Crash Detector** leaves evidence when the editor dies. See [`../DIAGNOSTICS.md`](../DIAGNOSTICS.md).
- Handy patterns (`rg -n` or `grep -n`):

  | Pattern | Finds |
  |---|---|
  | `Leak Detected\|has not been disposed` | native leaks (§1) |
  | `not cleaned up when closing the scene` | objects spawned during teardown (§2); the object names are on the next lines |
  | `auto-created` | lazy singletons creating themselves; their stacks show the caller |
  | `Unable to parse file` | broken YAML assets (§3) |
  | `Reloading assemblies\|Entering Playmode\|Loaded scene` | session boundaries: which code/assets a later error ran with |
  | `Start importing .*<file>` | whether an asset was reimported after your fix |
  | `Mono JIT Code` | managed frames inside a native stack |

- **Match errors to builds.** A log often spans several sessions. Find the
  `Reloading assemblies after forced synchronous recompile` and the reimport of the files you
  changed. Only errors after that ran your fix. Line numbers in managed frames that match the new
  file are proof.

---

## 1. Native memory leaks

**Shows up as:** `Leak Detected : Persistent allocates N individual allocations` or `A Native
Collection has not been disposed, resulting in a memory leak`.

**Why it's easy to miss:**
- Leaks are reported only at **domain reload** (script recompile) or **editor quit**, never at
  play-mode exit.
- This project disables domain reload on entering play mode (`EditorSettings`
  `m_EnterPlayModeOptions: 3`; the log says `Entering Playmode with Reload Domain disabled`). So
  leaks from several play sessions pile up and are reported together, later.

**Reproduce and capture:**
1. Preferences > Jobs > Leak Detection Level > **Enabled With Stack Trace**.
2. Play the suspect path (e.g. a few arcade games, or several scene round trips), then exit play
   mode.
3. Either force a recompile (save any `.cs`) and watch the Console, or restart the editor and
   read `Editor-prev.log`.

**Find the owner:**
- With stack traces, the allocation site is named (e.g. `BlockDensityGrid.Init`).
- Without them, **divide N by the per-object allocation count.** A `Cell` owns 4 density grids ×
  6 persistent `NativeArray`s = 24, and the reports were 24, 48, 168 and 264, all multiples of 24
  (BH-1.8).
- To find candidates: `rg -n "Allocator.Persistent"`, then check each owner for a matching
  `Dispose` on every exit path.

**Fix pattern:**
- Dispose in the owner's `OnDestroy` (and in any rebuild path), null-safe.
- Make `Dispose` idempotent: an `initialized` flag plus `if (arr.IsCreated) arr.Dispose()`.
  Complete an in-flight job first.
- If you clear a container on destroy, make late readers use `TryGetValue`, not the indexer.
  Objects torn down after the owner still call into it.

**Verify:** the same path plus a recompile gives no leak report whose stack points at your
allocation site.

## 2. Objects re-created during teardown

**Shows up as:** `Some objects were not cleaned up when closing the scene. (Did you spawn new
GameObjects from OnDestroy?)`, followed by object names such as `[PrismTimerManager]`. The native
stack says when it happened: `UnloadGameScene` (a scene switch) or
`RestoreSceneBackups`/`ExitPlayMode` (stopping play).

**Why it happens:** unloading a scene or exiting play mode **disables and destroys every
object**, so `OnDisable`/`OnDestroy` run for everything, in no guaranteed order. If any of them
reaches a lazy `EnsureInstance()`/`EnsureHost()` after that singleton was already destroyed, a
new GameObject is created in the closing scene. DontDestroyOnLoad hosts are hit on play-mode exit.

**Find the source:**
- Grep the log for the singleton's `auto-created` warning just before the error; its managed
  stack names the `OnDisable` caller.
- If the host doesn't log when it's created, search for its constructor
  (`new GameObject("[Name]")`) and for callers of its `Ensure*`, then keep the ones reachable
  from `OnDisable`/`OnDestroy`.

**Fix pattern (CC-3, CC-4):**
- Stop the cascade at its source: during unload, don't call methods that start gameplay
  side-effects. The scene is going away anyway.
- Guard the lazy creator. Keep a static `IsQuitting` set from **`Application.quitting`**, which
  also fires on editor play-mode exit and needs no live instance, unlike `OnApplicationQuit`.
  Reset it in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
  method, because domain reload is off.
- Optionally, add a "my own scene is unloading" flag set in `OnDestroy` and cleared on
  `SceneManager.sceneUnloaded`.
- Refuse to create while quitting or when `!Application.isPlaying`, and return null.
- Make every caller null-safe: `EnsureInstance()?.Foo()`. Existing examples:
  `PrismTimerManager`, `PrismEffectsManager`, `PrismRenderService.IsQuitting`, `PrismDebris`.

**Verify:** enter play mode, switch scenes, then stop play. The error must not appear, and the
`auto-created` warning should only show during normal scene loads.

## 3. YAML parse errors in `.asset` files

**Shows up as:** `Unable to parse file <path>: [Parser Failure at line N: Expect ':' between key
and value within mapping]`. The asset loads as nothing. Unity's line number can be several lines
past the real cause.

**Causes seen here:**
- A multi-line **plain (unquoted)** string containing `: `. Unity breaks when a wrapped line
  **ends** in `:`; a mid-line `: ` is tolerated by Unity but is still invalid YAML.
- Tab indentation.
- Two keys on one line (`HeadshotImage: {fileID: 0} IconActive: …`).

**The real source is usually a generator.** Many assets are written by `Tools/Build/author_*.py`
or `*_intensity.py`. `rg -l "<asset name>" Tools/` finds it. Fix the generator's string emitter
too, or the next regeneration brings the bug back. Then regenerate and run its `--check` if it
has one.

**Fix:** emit or rewrite the string as a single-quoted scalar (`'` … `'`, with `''` for an
apostrophe), the way Unity serializes long strings. Keep the line breaks; folding makes the
loaded text identical.

**Strict-parse every asset.** There is no checker in `Tools/` yet. This snippet (`pip install
pyyaml`) strips Unity's headers and parses each file:

```python
import re, subprocess, yaml
hdr = re.compile(r'^--- !u!\d+ &-?\d+( stripped)?\s*$')
Loader = getattr(yaml, 'CBaseLoader', yaml.BaseLoader)   # libyaml if available (much faster)
files = subprocess.run(['git', 'ls-files', '*.asset', '*.prefab', '*.unity'],
                       capture_output=True, text=True).stdout.splitlines()
for f in files:
    raw = open(f, 'rb').read()
    if not raw.startswith(b'%YAML'):
        continue                      # binary-serialized
    text = '\n'.join('' if l.startswith('%') else '---' if hdr.match(l) else l
                     for l in raw.decode('utf-8', 'replace').splitlines())
    try:
        list(yaml.load_all(text, Loader=Loader))
    except yaml.YAMLError as e:
        print('FAIL', f, getattr(e, 'problem', e), getattr(e, 'problem_mark', ''))
```

**Verify:** the snippet passes for your files, and the Editor log shows the file reimported
(`Start importing <path>`) with no `Unable to parse file` after it.

## 4. Teardown-order `NullReferenceException`s

**Shows up as:** an NRE or `MissingReferenceException` from a coroutine, scheduled callback or
event handler, usually right before a scene switch or play-mode exit. Example: `Crystal.ActivateCrystal`
reading `cellData.Cell.transform` from `LightFauna.WitherCoroutine` (open, see FIX_LOG).

**Why it happens:** the object the callback uses (a cell, a player, a manager) was destroyed
first. Coroutines on a still-active object and timer callbacks keep running during teardown, and
`?.` does not see Unity's destroyed objects as null.

**Find it:** read the managed stack, then check whether the log shows `UnloadGameScene`/`ExitPlayMode`
or a `Loaded scene` right after. If so, it's ordering, not a gameplay bug.

**Guard patterns:**
- Unity-null checks (`if (!cell) return;`), not `?.`, on anything that can be destroyed.
- Stop the coroutine in `OnDisable`, and unsubscribe events there too.
- Skip the work while quitting or while `!gameObject.scene.isLoaded`.
- Owners cancel their scheduled callbacks on destroy (`PrismTimerManager.CancelScheduledActions(this)`).

Fix the missing guard; don't reorder destruction.

**Verify:** repeat the scene switch / play-mode exit a few times with no NRE.

## 5. Materials leaked through `renderer.material`

**Shows up as:** growing memory and material count over a session (Memory Profiler: many
`Material (Instance)` objects). There is usually no console error.

**Why it happens:** `renderer.material` **clones** the material on first access, and each clone
lives until destroyed. `new Material(renderer.material)` leaks the implicit clone (FIX_LOG BH-1.10, the Crystal
colour lerp, is this pattern). Also track any copy you make, and destroy it in `OnDestroy` too.

**Find it:** `rg -n "\.material\b|\.materials\b|new Material\(" Assets/_Scripts`, then check that
each clone is destroyed.

**Fix pattern:**
- Read from `sharedMaterial`.
- If you need a per-object copy, create it once, keep it, and `Destroy` it when done and in
  `OnDestroy`.
- Prefer a `MaterialPropertyBlock` for per-object colours; the prism code already does.

**Verify:** the Memory Profiler material count stays flat across repeated triggers.

---

## 6. Input held after the active input changes (stuck triggers, drift, abilities)

**Shows up as:** a vessel keeps an ability, drift or charge "held" after the player switches input
(touches the keyboard or mouse while holding a pad trigger) or pauses. No console error. FIX_LOG
BH-1.2 is this pattern; BH-1.1 (AI held drift) is the same idea for the AI pilot.

**Why it happens:** each input strategy turns "held" into press/release events by remembering the
previous frame (`prevLeftTriggerActive` and friends). When the strategy stops being live,
`ProcessInput` stops running, so the release edge never fires and the remembered state goes stale.

**Fix pattern:** every strategy overrides `OnStrategyDeactivated` and `OnPaused` to release held
triggers and speed effects (through the same dispatch code a real let-go uses) and reset its own
state. `KeyboardInputStrategy` is the reference; `GamepadInputStrategy` now matches it. Any new
strategy (touch, AI, etc.) needs both overrides.

**Re-verify if it comes back:** with a pad connected, (1) hold a trigger, then move the mouse or tap
a key, and the ability should end; (2) hold a trigger and pause, and it should release, and the pad
should work normally after resume. If it recurs, check first that `InputController` still calls
`OnStrategyDeactivated` on a switch and `OnPaused` from `SetPause`.

---

## 7. A timeout `catch` that runs on the wrong thread

**Shows up as:** `EnsureRunningOnMainThread` exceptions (or a frozen screen) right after a timeout
or cancellation, especially at boot with a slow or unreachable UGS. FIX_LOG BH-1.3.

**Why it happens:** `CancelAfter` and `new CancellationTokenSource(ms)` cancel from a timer thread.
`AttachExternalCancellation` does not marshal, so when it sits outside `.AsMainThread()` the
`catch (OperationCanceledException)` and everything after it resumes on that thread.

**Fix pattern:** make the first statement of each such `catch` `await MainThreadDispatcher.SwitchToMainThreadAsync();`
(and use `.AsMainThread()` rather than `.AsUniTask()` on the awaited task). Find candidates with
`rg -n "CancelAfter|new CancellationTokenSource\(" Assets/_Scripts` and check what each `catch`
touches. Full background: [`../THREADING.md`](../THREADING.md).

**Related, BH-1.4:** a facade call that reports failure through an event (for example
`AuthenticationServiceFacade.OnSignInFailed`) does not throw, so a bare `await` is not proof of
success. Check the state afterwards (`_facade.IsSignedIn`) before moving on.

**Verify:** shorten the timeout to ~0.1 s (or go offline) and boot: no `EnsureRunningOnMainThread`
error, and the flow carries on. With no network and no session, Guest shows the error at once.

---

Threading errors (`EnsureRunningOnMainThread`, UGS callbacks off the main thread) have their own
guide: [`../THREADING.md`](../THREADING.md).
