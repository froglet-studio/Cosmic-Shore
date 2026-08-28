<div class="sec-eyebrow">Part I · Overview</div>

# Five bugs that taught us the most

The full catalogue is in Part II. These five are the most transferable — each one is a lesson about
distributed, async, event-driven systems, not just about Unity.

::: bug The off-thread crash — UGS callback raises a SOAP event {fixed}
**Symptom.** Random `EnsureRunningOnMainThread` crashes after party operations, deep in UI code.
**Root cause.** A UGS `Task` continuation resumed on the .NET thread pool and raised a SOAP
`ScriptableEvent`. SOAP invokes listeners **inline**, so a UI listener touched a `CanvasGroup`
off-thread and Unity threw. Worse, the obvious fix — `UniTask.SwitchToMainThread()` — is a *no-op* on
this UniTask version (it reports "already complete" from the pool and runs inline).
**Fix.** A `MainThreadDispatcher` built on Unity's own `SynchronizationContext`, exposed as
`.AsMainThread()` and applied at every UGS/Netcode await, plus a canary that detects regressions.
**Lesson.** Know which thread your callbacks resume on, and don't trust a library's thread-switch
primitive without verifying it on your version.
:::

::: bug LAZY Relay creation — the shutdown-and-recreate cascade {fixed}
**Symptom.** Invites intermittently failed; the second joiner of two often couldn't connect; vessels
sometimes lingered or vanished.
**Root cause.** Creating the party session *on first invite* forced a host shutdown, session create,
and reconnect — a cascade with many failure points and timing windows.
**Fix.** Make session creation **eager** so every player always has a live session; an invite becomes
a join. The entire bug class disappeared.
**Lesson.** Sometimes the cheapest fix for a flaky multi-step transition is to *delete the transition*
by making its precondition always true.
:::

::: bug The UGS singleton pinned to `null` in a constructor {fixed}
**Symptom.** A service's calls to `MultiplayerService.Instance` always NRE'd, even long after UGS was
initialised.
**Root cause.** The service cached `MultiplayerService.Instance` in its **constructor**. Lazy DI
singletons are constructed during Bootstrap — *before* `UnityServices.InitializeAsync()` completes —
so the cached value was `null` forever.
**Fix.** Resolve at use time via a property: `private IMultiplayerService _svc => MultiplayerService.Instance;`.
**Lesson.** Never cache a service-locator singleton at construction time when construction can outrun
initialisation. Resolve lazily.
:::

::: bug Host phantom-rejoin — two views of "who's in the party" disagree {fixed}
**Symptom.** After a client left, the host's party slot flickered the departed player in and out
forever (~every 3 s), spamming join/leave events.
**Root cause.** Two scans disagreed each tick: the authoritative **session** scan correctly removed
the player, while a **presence-lobby** scan re-added them from a stale `joined_party` property.
**Fix.** Make the presence scan cross-check the authoritative session player list before re-adding —
the session is the source of truth; the lobby is only a hint. (Plus an awaited property clear on
leave, for hygiene.)
**Lesson.** When two data sources can disagree, declare one authoritative and make the other defer to
it — never let a *hint* override *truth*.
:::

::: bug Two vessels and dead controls — spawn vs. scene-reload ordering {fixed}
**Symptom.** After leaving a party, a player's solo menu showed **two** vessels; the controllable one
wouldn't steer and its AI stopped seeking.
**Root cause.** The leave flow recreated the solo session (spawning a vessel) **before** the
`Menu_Main` reload finished; the reload's fresh initializer then spawned a *second* vessel, and the
first — a scene survivor (`destroyWithScene=false`) — was orphaned with no player pairing.
**Fix.** Despawn the surviving vessel before the reload, and sequence the flow to mirror cold-boot
exactly (tear down → leave → shut down NM → load scene → recreate session).
**Lesson.** Object lifecycle and scene lifecycle are different clocks; when they overlap during a
transition, order them explicitly or you get orphans.
:::
