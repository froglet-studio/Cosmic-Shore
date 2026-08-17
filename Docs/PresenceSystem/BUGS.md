# Presence System — Open Bugs

Living tracker for presence-lobby-side issues found in MPPM testing.
Companion to `ARCHITECTURE.md` (current state), `REFACTOR.md` (active
refactor queue), and `../NetworkDiagnostics/ARCHITECTURE.md` (catch-block
diagnostics).

Party-side bugs (B2, B3, B5, B7 from the old tracker) moved to
`../PartySystem/BUGS.md`.

Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

| ID | Title | Confidence | Status |
|----|-------|-----------|--------|
| B1 | `ArgumentOutOfRangeException` (LobbyPatcher) spam at game start | High (cause) | 🟢 (needs Editor retest) |
| B4 | TC1 second invite not delivered + party members vanish from 3rd player's panel | High, needs retest | 🔴 |
| B6 | TC3 NRE (`WrappedLobbyService`) + empty online/request lists | Medium | 🔴 |
| B11 | Presence stuck at `Announced` - peers render "CONNECTING…" forever | Confirmed | ✅ `a510bd51` **VERIFIED 2026-08-04** |
| B12 | Explicit leave took ~30 s to remove the player | Confirmed | 🟡 `a510bd51` — graceful path **untested**; see retest note below |
| B13 | Relay 500 on boot bricks the loading splash (no retry, no recovery) | Confirmed | ✅ `c3dbf682` **VERIFIED 2026-08-04** (4 instances; wider run pending) |
| B14 | `presenceState` change never repainted the row - "CONNECTING…" forever | Confirmed | ✅ `c49c8c91` **VERIFIED 2026-08-04** |
| B15 | Three players in ONE party render three different party sizes | Confirmed | ✅ **VERIFIED 2026-08-06** (4-VP) |
| B16 | Coalesced roster raise ran off the main thread — `EnsureRunningOnMainThread` | Confirmed | ✅ `4aece925` **VERIFIED 2026-08-06** |

> **Working order.** Diagnostics-first. The presence-lobby cluster (B4,
> B6) is the locked-design area — read `ARCHITECTURE.md` and
> `../PartySystem/ARCHITECTURE.md` before touching `PresenceLobbyService`
> or `HostConnectionService`. Do not reintroduce LAZY session creation.

---

## MEASURED (run 2): fault rate after `40226752` (2026-08-04)

Second sample, same counters, on a longer window and **after** push ticks
stopped fetching. Reported by the owner from a live Editor run:

```
[HostConnectionService] Benign SDK fault on the presence read - refresh tick VOIDED
  (roster diff / invite scan / member sync / publish all skipped this tick).
  defect=SdkStaleIndex | skips: presence=16, partySession=39
  NetDiag: class=Transient | reach=ReachableViaLocalAreaNetwork|monitor=Online|sinceChange=206.5s
```

Denominator assumption unchanged from run 1: only *poll* ticks fetch, so
fetch ticks ≈ elapsed / 1.5 s. `sinceChange` is used as the elapsed proxy.
Both are **upper bounds on the denominator** — the tick is additionally
gated on `IsOnMenuScene()` and the lobby mutex — so the true rates are at
least this high.

| Path | Run 1 (95.8 s) | Run 2 (206.5 s) |
|---|---|---|
| presence read | 13 / ~64 = **~20%** | 16 / ~138 = **~12%** |
| party-session read | 22 / ~51 = **~43%** | 39 / ~122 = **~32%** |

**Both rates fell by roughly a third.** Two effects, pointing the same way:

1. `40226752` — push ticks no longer fetch, so the SDK sees fewer reads
   against a cache it is concurrently patching.
2. **The fault is concentrated at startup.** Run 2 is 2.2× longer than run 1
   but carries only 1.2× the presence skips. A defect whose rate falls as
   the window lengthens is one that fires mostly in the first seconds —
   which is exactly the multi-client property-write burst B1 describes.

**This upgrades `TODO-P2` (coalesce startup property writes) from
"speculative" to the single highest-value lever on this defect**, and it is
now supported by two independent samples rather than one. Fewer startup
writes → fewer deltas → fewer stale-index opportunities, at the exact point
in the session where they cluster.

**Still not enough to unblock the safety-poll relaxation.** At ~12% a 10 s
nominal poll is a ~11.4 s effective backstop, and the party-session path is
still voided a third of the time. Re-measure *after* TODO-P2 lands.

---

## MEASURED (run 1): B1/B6 stale-index fault rate (2026-08-02)

First real numbers, from the Commit 1 counters. **This is the data
`REFACTOR.md`'s `LobbyMembershipMonitor` extraction and `TODOS.md` TODO-P5 were
waiting on.** Verification guide Step 1c, "N climbing" outcome — decisively.

```
[HostConnectionService] Benign SDK fault on the presence read - refresh tick VOIDED
  defect=SdkStaleIndex | skips: presence=13, partySession=22
  NetDiag: class=Transient | reach=ReachableViaLocalAreaNetwork|monitor=Online|sinceChange=95.8s
```

Thrown from `WrappedLobbyService.GetLobbyAsync` → `LobbyHandler.RefreshLobbyAsync`
— i.e. the HTTP GET succeeded and the SDK failed while deserialising it against
its stale local cache. Exactly the B6 read-path surface.

**Rate.** ~96 s at the 1.5 s poll ≈ 64 fetch ticks:

| Path | Voided | Of | Rate |
|---|---|---|---|
| presence read | 13 | ~64 | **~20%** |
| party-session read | 22 | ~51 (only reached when the presence read succeeded) | **~43%** |

**What it costs.** A voided tick skips the roster diff, invite scan, acceptance
scan, member sync **and** the presence publish. It is not a correctness break —
the next tick retries and the roster converges — but it is a large, silent
reduction in the poll's effective cadence.

**Consequences to act on:**

1. **Do NOT relax the safety poll from 1.5 s to 10 s yet.** That change was
   staged in `PRESENCE_SYNC_PLAN.md` as prefab-only once push was confirmed. At a
   ~20% void rate a 10 s nominal poll is a ~12.5 s effective backstop, and the
   party-session path is worse. Re-measure after push is confirmed and after any
   write-coalescing work; decide then.
2. **`TODO-P2` (coalesce startup property writes) is now motivated by data**, not
   speculation — fewer property writes means fewer deltas means fewer stale-index
   opportunities.
3. The `LobbyMembershipMonitor` reconnect decision should treat `SdkStaleIndex` as
   explicitly NOT membership loss. At this rate an error-count watchdog that
   counted it would escalate constantly.

**Note.** After `40226752` (push ticks no longer fetch) only *poll* ticks can
produce this fault, so the absolute count should drop even if the per-fetch rate
does not. These numbers predate that change taking effect in a measured run —
re-measure.

**Re-measured — see § MEASURED (run 2) above.** The per-fetch rate fell too
(~20%→~12% presence, ~43%→~32% party-session), and the shape of the fall
identifies startup as the concentration point. Run 2 supersedes this block's
consequence list; the conclusions are unchanged in direction, sharper in
priority.

---

## B16. A new SOAP event raised off the main thread — `EnsureRunningOnMainThread` returned — ✅ `4aece925` VERIFIED 2026-08-06

**Self-inflicted, during the B15 fix.** Recorded in full because the lesson
generalises to every SOAP event anyone adds to this system, and because the
process failure that let it reach a build matters more than the code.

**Symptom.** After the B15 branch, a live build threw the old
`EnsureRunningOnMainThread` cascade in a system that had been working.

### Mechanism

`Docs/THREADING.md`: SOAP `Raise()` invokes listeners **inline on the calling
thread**, and any `UnityEngine.Object` access off the main thread throws —
**including a `== null` check**, which routes through `op_Equality`.

`OnPartyRosterChanged` (added in `f6aecb6a`) has three listeners:

| Listener | Touches | Always live? |
|---|---|---|
| `FriendsInitializer.HandlePartyRosterChanged` | `hostConnectionData == null` → `op_Equality`, then an `async void` UGS call | **YES — persistent GameObject** |
| `FriendsListPanel.HandlePartyRosterChanged` | `PopulateOnlineSection()` → Instantiate/Destroy | only when open |
| `ArcadeLobbyList.HandlePartyRosterChanged` | `PopulateSlots()` | only when open |

Its request sites are **not** all main-thread. `PartyMemberService.SeedLocalPlayer`
is called by `HostConnectionService.ApplyPostLobbyJoinState` at four sites, each
immediately after `await _lobbyService.JoinOrCreateAsync(...)` — whose fallback
path ends:

```csharp
await CreateAsync(maxPlayers);              // ends .AsMainThread()  ✓
await UniTask.Delay(LOBBY_RACE_SETTLE_MS);  // ← no affinity guarantee
await ConvergeToCanonicalAsync(maxPlayers); // may return with NO await at all
```

Awaiting a `UniTask` does not restore the caller's thread
(`ConfigureAwait(false)` semantics), and UniTask's own primitives do not
reliably marshal to main on this version — both stated in `THREADING.md`. So
the continuation can land on the ThreadPool.

**Why it was new.** Before the branch, `SeedLocalPlayer` raised no SOAP event —
it only mutated the `PartyMembers` ScriptableList, and `FriendsListPanel`
subscribes solely to `OnlinePlayers` list events. That path could not reach any
Unity-touching listener. The coalesced channel connected it, including one that
is **always attached**.

### Fix

`RaisePartyRosterChanged` → `RequestPartyRosterChanged` (an `Interlocked` flag,
safe from any thread) + `FlushPartyRosterChanged` (the actual raise), drained at
the **top of `HostConnectionService.Update()`** before every gate. One deferral
covers all six request sites; making each *listener* thread-safe would have been
three fixes, and the next listener anyone added would have reintroduced it.

This is the same `Interlocked`-flag-drained-from-`Update` pattern
`PresenceLobbyService` already uses for every UGS push. **The correct shape was
already in the codebase and was not followed.**

Coalescing improved as a side effect: several roster changes in one frame now
collapse to one repaint.

### The rule this establishes

> **A SOAP event whose listeners touch Unity state may only be raised from a
> guaranteed main-thread context.** Where the raise site cannot prove that, defer
> it to a drain in `Update()`. Do not require every future call site to prove its
> thread — that is a contract nobody can keep.

Pinned by `PartyRosterEventTests.RosterChanged_IsNotRaisedUntilFlushed`.
`HostConnectionDataSO.RemovePartyMember` keeps a direct raise and is documented
**MAIN THREAD ONLY** — its sole caller runs before its first `await`, from a UI
button.

### Process failure — the more important half

`REFACTOR.md` requires a 3-VP MPPM smoke **per commit** and "push only after
explicit risk discussion". Seven commits were landed with **zero runtime
verification between them**, in the area the docs call the fragile locked-design
area, while the author could not compile. The plan was ordered by *value* when it
should have been ordered by *blast radius* with a hard stop after each step.

**The cheapest gate would have caught it**: single editor, enter play mode in
Menu_Main, watch for `EnsureRunningOnMainThread` and the
`SceneTransitionManager` canary. No MPPM, no party, no second player. That is now
step 3 of the standing verification order in `../PartySystem/TESTS.md`.

---

## B15. Three players in ONE party render three different party sizes — ✅ VERIFIED FIXED 2026-08-06

**Symptom** (reported from a live 3-player session; A hosts, B joins, then C joins):

| Panel | A's row | B's row | C's row |
|---|---|---|---|
| **A** | — | `IN YOUR PARTY 2/4` ❌ | `IN YOUR PARTY 1/4` ❌ |
| **B** | `3/4` ✅ | — | `3/4` ✅ |
| **C** | ❌ (like A) | ❌ (like A) | — |

Plus: after a client joined, its own row took **15–20 s** to go `1/4 → 2/4`,
alongside two `Benign SDK fault on party-session read - refresh tick VOIDED` logs.

**These are two different bugs that presented as one.**

### The count divergence is NOT a latency bug

`FriendsListPanel.ResolveRemoteStatus` assigned the member count once, up front,
from `player.PartyMemberCount` — the **remote peer's self-published `partyCount`
presence property** — and used it for every status, including `InYourParty`. So
the *bucket* ("is this person in my party?") came from local truth
(`IsInSameParty` → `PartyMembers`), while the *number* came from the peer's stale
self-report.

With N members that is **N independently-published scalars and N×(N−1)
independently-lossy read edges**, and nothing that reconciles them. No poll
cadence can make N scalars agree — which is why every previous fix in this area
(cadence, push channel, jitter, two-strike eviction, benign classification)
left it standing.

It violated two things already written down:

- the locked *"Session is authoritative over presence — the lobby is a hint,
  never the source of truth"* invariant (`MultiplayerArchitecture/ROADMAP.md`);
- `PartySystem/ARCHITECTURE.md` **exit criterion 3** — "host's view of party
  membership matches every client's view within one refresh tick".

`FriendsListPanel:386` was the **only** display consumer of the advertised count
in the codebase. `ArcadeGameConfigureModal:447`, `QuickPlayButton:95`,
`ScreenSwitcher:637`, `FriendsInitializer:154` all already read the local roster.
A lone outlier, not a pattern.

### Root causes

| | Cause |
|---|---|
| **RC1** | `FriendsListPanel:386` renders a party member's size from their advertised presence property instead of the local roster. **The reported divergence.** |
| **RC2** | `RefreshAsync` was one linear pipeline in one `try`; the lobby read is the first `await`, so a fault there unwound past the publish. Read voided ~12% of ticks. |
| **RC3** | No republish on roster change — `PublishPresenceImmediateAsync` fired only on Menu_Main load and presence-state transitions. |
| **RC4** | `PartySessionService` wired **1** UGS push event (`PlayerLeaving`) vs `PresenceLobbyService`'s **7**, so a party JOIN was discoverable only by a poll read voided ~32% of the time. **The 15–20 s.** |
| **RC5** | Three writers of `partyCount`; 12 key literals across 5 files; 6 keys written to the Relay session that nothing reads. |
| **RC6** | Two traps for anyone fixing RC1: `PartyMemberService.ReadMemberData` uses the identity-only ctor so every `PartyMembers` entry reports `AdvertisedPartyMemberCount == 0`; and `FriendsInitializer` only republished presence on join, so a 3→2 shrink left the advertised count pinned at 3. |

### The rule this established

> **Local authoritative state is never answered from a remote-published mirror.**
> "How big is MY party?" → `IPartyRoster` (local, 0 latency).
> "How big is THEIR party?" → `PartyPlayerData.Advertised*` (a hint).

Made structural by `IPartyRoster` (implemented by `HostConnectionDataSO`, no new
writer) and by renaming `PartyMemberCount`/`PartyMaxSlots` →
`AdvertisedPartyMemberCount`/`AdvertisedPartyMaxSlots`, so a call site cannot
mistake a hint for a fact.

### Fix (6 commits on `claude/lobby-sync-bugs-4n4nl2`)

| | |
|---|---|
| C1 | `IPartyRoster` + the rename + `FriendsListPanel` sources counts per tier. **Fixes the divergence.** |
| C2 | `OnPartyRosterChanged` — one raise per settled mutation, change-gated. Also fixes RC6's 3→2 shrink. |
| C3 | Republish presence the moment the roster moves (drained flag — the raise fires with the lobby mutex held). |
| C4 | Read and publish get separate `try`s, so a voided read no longer eats the publish. |
| C5 | Party-session push channel; push path syncs from the SDK's in-memory roster with **zero UGS reads**. **Fixes the 15–20 s.** |
| C6 | `PartyLobbyKeys` single owner; drop the 6 write-only Relay-session keys (partial TODO-P2). |

### Retest (MPPM, **unique tags mandatory**)

1. A starts, B starts, A invites B, B joins. C starts, A invites C, C joins.
2. All three panels must read `IN YOUR PARTY 3/4`.
3. C's row on A's panel must reach `3/4` **within a frame** of C's vessel
   appearing, not after a poll.
4. B leaves → all panels `2/4` within one refresh tick.
5. No `RaisePartyMemberJoined`/`Left` oscillation (B8 regression check).
6. `BenignPresenceSkips` / `BenignPartySessionSkips` should still climb — the SDK
   defect is untouched — **but the counts stay correct anyway**. That is the proof
   C4/C5 worked.

> ⚠ **Untagged MPPM clones reproduce this exact symptom for an unrelated reason**
> (shared `mppm-clone` auth profile → one UGS PlayerId → each join invalidates the
> previous clone's membership). Confirm tags before drawing any conclusion, same
> caveat as B4/B5.

---

## B14. `presenceState` changes never repainted the row — "CONNECTING…" forever — ✅ VERIFIED FIXED

> **Verified 2026-08-04** (owner, MPPM). Peers now promote from CONNECTING… to
> ONLINE. This was the larger half of the CONNECTING symptom; B11 was the
> smaller one.

**Found.** 2-instance MPPM run, 2026-08-02, immediately after the B11 fix. Both
instances flying the lava lamp; each showed the other as `CONNECTING…`.

**This was a regression I introduced in `641ec251`**, and B11 was partly a
symptom of it rather than the whole cause.

**Root cause.** `RefreshOnlinePlayersDiff` decides whether to replace a stored
roster entry with an inline comparison:

```csharp
bool changed =
    existing.DisplayName      != playerData.DisplayName      ||
    existing.AvatarId         != playerData.AvatarId         ||
    existing.PartyMemberCount != playerData.PartyMemberCount ||
    existing.PartyMaxSlots    != playerData.PartyMaxSlots    ||
    existing.MatchName        != playerData.MatchName;
```

`641ec251` added `PresenceState` to `PartyPlayerData` **and** to
`ReadOnlinePlayerData`, but **not here**. So:

1. A peer's row is created while they are still `Announced` (2) → renders
   `CONNECTING…`.
2. They reach `Present` and publish `presenceState = 3`.
3. The next diff reads the new value, compares the five fields it knows about,
   finds them identical, reports `changed == false`, and **does not replace the
   entry**.
4. The stored row keeps `PresenceState = 2` for the rest of the session.

Perfectly symmetric when two instances start together — both rows are created
while both are `Announced`. It also explains why the earlier 4-instance run
looked asymmetric: Ys1 started first, so its row on each peer happened to be
created *after* it had already reached `Present`, capturing 3 at insert time.

**Fix.** The comparison moves onto the struct as
`PartyPlayerData.HasSameDisplayDataAs`, next to the fields it compares, and the
diff calls that. Adding a render-relevant field now cannot silently skip the
differ.

**Lesson.** A struct whose `Equals` is deliberately identity-only needs an
explicit content comparison living beside its fields. Leaving that comparison
inlined at the call site guarantees it goes stale the first time a field is
added — and the failure is silent, because "no change detected" is
indistinguishable from "nothing changed".

`PartyMemberService.SyncFromSession`'s identity check is NOT affected: it
compares `DisplayName`/`AvatarId` only, which is correct for its data source
(party-session player properties carry nothing else), and its rows are built via
the 3-arg constructor, which defaults `presenceState` to `Present`.

---

## B13. Relay 500 on boot bricks the loading splash — ✅ VERIFIED FIXED (4 instances)

> **Verified 2026-08-04** (owner, 4-instance MPPM run — all four booted to
> Menu_Main, no permanent splash). Wider runs (4+ instances, where the Relay
> 500 is more likely to actually fire) still pending; the fault is upstream and
> intermittent, so absence of the error in one run is weak evidence. Keep an
> eye out for `Internal Server Error: allocation call failure` and confirm the
> instance either self-heals within ~31 s or lands on the **Retry** surface.

**Found.** 2026-08-02, intermittently on one MPPM instance at startup.

```
[Multiplayer]: Internal Server Error: allocation call failure
SessionException: Failed to create allocation
  RelayHandler.CreateAllocationAsync
  → ConnectionModule.CreateConnectionAsync
  → SessionManager.CreateAsync
  → PartySessionService.CreateAsync:184
  → HostConnectionService.EnsurePartySessionAsync:1324
  → HostConnectionService.EnsureInitializedAsync:867
  → HostConnectionService.HandleSignedInEvent:806   ← async void, UNHANDLED
```

Game stays on the loading splash, status "Loading", forever.

**Root cause — the UGS 500 is upstream and transient; the brick is ours.**

1. `IsTransientSessionException` did not match it. It required the OUTER
   exception to be a `SessionException` (true) and then matched only five
   message patterns on that outer message — none of which is
   `"Failed to create allocation"`. It also never walked `InnerException`, where
   the actual `"Internal Server Error: allocation call failure"` lives. So the
   retry loop in `CreateAsync` never engaged.
2. Nothing caught it after that. `EnsurePartySessionAsync` and
   `EnsureInitializedAsync` both have `finally` **but no `catch`**, so the
   exception propagated into `async void HandleSignedInEvent` and was reported
   as unhandled.
3. Therefore `_eventBus.RaiseHostConnectionEstablished()` never fired — and the
   auth scene's `LoadMainMenuNetworkedAsync` waits on exactly that signal before
   loading Menu_Main. Hence the permanent splash.
4. And it could not self-recover: the state machine was left in `HostingParty`,
   so `IsInitialized` was true and `IsInPresenceLobby` was true, meaning a
   re-fired sign-in hit `EnsureInitializedAsync`'s
   `if (IsInPresenceLobby || _joining) return;` guard and did nothing.

**Fix.**
- `IsTransientSessionException` walks the `InnerException` chain and treats **any
  UGS 5xx** (`RequestFailedException.ErrorCode` 500-599) as transient — the
  general rule the message patterns were specific instances of — plus explicit
  matches for `Failed to create allocation`, `allocation call failure`,
  `Internal Server Error`. `CreateAsync` already had a transient branch with 5
  exponential retries (1+2+4+8+16 s), so this alone makes the common case
  self-heal.
- `EnsurePartySessionAsync` catches, logs with NetDiag, and raises
  `OnHostConnectionLost`. `BootStatusBroadcaster` listens for that and swaps the
  splash to the **Retry** surface, whose button routes back via
  `bootStatusRetryRequestedEvent → HandleBootStatusRetryRequested →
  EnsurePartySessionAsync`. The state machine is deliberately left in
  `HostingParty`: `IsHostingParty` is false with no session, so the retry
  re-enters cleanly.
- `EnsureInitializedAsync` gains a matching backstop for everything before the
  session step (profile wait, identity sync, lobby join).

**The rule this encodes.** Nothing on the boot path may throw into an
`async void`. An unhandled boot exception is indistinguishable from a hang.

**Known limitation.** Worst-case retry budget is ~31 s, during which the splash
still reads "Loading" with no indication that a retry is in progress. Surfacing
"retrying…" is a follow-up.

---

## B11. Presence stuck at `Announced` — peers render "CONNECTING…" forever — ✅ VERIFIED FIXED

> **Verified 2026-08-04** (owner, MPPM). All instances reach `Present`; every
> peer renders ONLINE. Closed together with B14 — the two had to be fixed
> together to clear the symptom, since B11 dropped the signal and B14 dropped
> the repaint that would have shown it arriving.

**Found.** 4-instance MPPM run, 2026-08-01. Every instance flying the lava lamp
normally. `FriendsListPanel` on each:

```
Ys1 → Ys2 (connecting)  Ys3 (connecting)  Ys4 (connecting)
Ys2 → Ys1 (online)      Ys3 (connecting)  Ys4 (connecting)
Ys3 → Ys1 (online)      Ys2 (connecting)  Ys4 (connecting)
Ys4 → Ys1 (online)      Ys2 (connecting)  Ys3 (connecting)
```

Exactly one instance published `Present`; the other three never did. The
asymmetry is the diagnosis: it is a **race**, and one instance won it.

**Root cause.** `Announced → Present` depended on catching
`GameDataSO.OnClientReady`, a one-shot `ScriptableEventNoParam` with no replay.
There is no ordering guarantee between the presence-lobby join and the menu
vessel spawn, and the two race differently on the main editor instance than on
an MPPM virtual player. When the signal landed while the machine was still in
`Joining`, `Joining → Present` was illegal — `TryTransition` warned, returned
`false`, and **discarded the only signal that would ever arrive**.

**Fix** (`a510bd51`). Two parts, both needed:
1. `HandleLocalVesselReady` latches `_localVesselReady` unconditionally *before*
   attempting the transition, so a signal arriving in a state that cannot accept
   it is remembered.
2. `ReconcilePresenceState()` runs each refresh tick and re-derives the state
   from the observable **condition** (does the local vessel exist?) rather than
   from a caught event.

**Lesson.** A state machine must never depend on having *caught* a one-shot.
Latch the fact, reconcile the condition.

**Two related holes found while tracing it**, each able to reproduce the same
symptom independently:
- `BuildLivePresenceProperties` did not carry `presenceState`, so a converge
  migration rebuilt the player record without it. Same class as B4 — a new
  stateful key added without registering it with `LivePropertySource`.
- `PublishPartyStateIfChangedAsync`'s change-gate compared against trackers
  describing a *different* lobby, so after a migration it never re-published,
  masking the dropped key permanently. Trackers are now invalidated on lobby-id
  change.

---

## B12. Explicit leave took ~30 s to remove the player — 🟡 fix shipped, graceful path still UNTESTED

> **Retest 2026-08-04 — the observation is expected; the test was wrong.**
> Owner deactivated one MPPM virtual player (Ys4) and its row took **30–50 s**
> to clear on the peers, i.e. no better than before the fix.
>
> **That is the correct result for what was actually tested, and it does not
> mean B12 regressed.** Deactivating a virtual player in the Multiplayer Play
> Mode window **terminates the clone process**. No `Application.wantsToQuit`,
> no `EditorApplication.playModeStateChanged`, no code runs at all — it is a
> hard kill, the one class this bug's *Remaining floor* section already says
> cannot be beaten. 30–50 s is the UGS service-side reap.
>
> **The error was mine, in the test table**, not in the fix:
> `PRESENCE_SYNC_VERIFICATION.md` Step 2d listed
> *"MPPM: toggle the virtual player off / stop play mode → **< 1 s**"* as a
> single row. Those are two different mechanisms and only the second runs any
> code. Both that table and `TESTS.md` P5 are corrected.
>
> **Will it be faster on a separate PC?** Only if the app is *quit*, not
> killed. A second machine whose process is killed behaves identically — the
> latency is a property of the departure mechanism, not of MPPM. What MPPM
> genuinely cannot do is let one player quit gracefully while others keep
> watching: deactivation kills, and stopping play mode stops every virtual
> player at once, leaving no observer.
>
> **How to actually verify the fix:** run a **standalone build** alongside the
> editor (or on a second machine) and quit the build with alt-F4 / the in-game
> quit button. Expect a ~1.5 s pause, `Departure leave complete
> (leaveParty=True)` on the leaver, and the row gone from the editor in **< 1 s**.
> An editor-only "simulate departure" hook that would make this testable inside
> MPPM is tracked as `TODOS.md` § TODO-P10.
>
> **Status stays 🟡** until the graceful path is observed end-to-end. The
> named-id eviction path (`TryConsumeDepartedPlayerIds`) has never been
> exercised by a test — everything observed so far went through the reap.

**Found.** Same run. Turning off Ys2 left its row on Ys1/Ys3/Ys4 for ~30 s (the
UGS reap window) before it disappeared.

**Root cause — two independent failures stacked:**

1. **The id was thrown away.** The `PlayerLeaving` / `PlayerHasLeft` pushes carry
   the departing player's id as their payload. The handlers ignored it and only
   set the generic roster-dirty flag, so removal fell through to "absent from N
   consecutive reads" — processing an explicit, authoritative, instant leave as
   if it were an ambiguous absence.
2. **The leaver never left.** Stopping play mode does **not** raise
   `Application.wantsToQuit`, so in the editor — where MPPM lives — the
   departure path added in Commit 3 never ran, and no push was emitted at all.

**Fix** (`a510bd51`). `PresenceLobbyService` collects departed ids and exposes
`TryConsumeDepartedPlayerIds`; `RefreshOnlinePlayersDiff` drains them first and
evicts immediately, bypassing the two-strike rule (which exists for *ambiguous*
absences, not for the service naming the player). `ApplicationLifecycleManager`
also raises `OnAppQuitRequested` from `EditorApplication.playModeStateChanged /
ExitingPlayMode`.

**Remaining floor.** `ExitingPlayMode` cannot be deferred, so the leave is
dispatched best-effort rather than awaited. A true process kill or crash still
cannot notify anyone, and for a non-party peer there is no transport to detect it
— the UGS reap (~30 s) remains the floor for that case. See
`PRESENCE_SYNC_PLAN.md` § 6.

---

## B1 — `ArgumentOutOfRangeException` in `LobbyPatcher.ApplyPatchesToLobby` at game start 🟡 (console noise silenced; underlying SDK defect persists)

**Symptom.** Every client logs, at game start, an
`ArgumentOutOfRangeException` from `LobbyPatcher.ApplyPatchesToLobby` →
`LobbyHandler.OnLobbyChanged` → `LobbyChannel.ProcessEvent` /
`HandleLobbyChanges`.

**Root cause (high confidence).** The UGS Lobby SDK applies a WebSocket
"lobby changed" delta that references a player/data index not present
in the local cache (stale index). The exception is thrown **and logged
by the SDK itself** (`Unity.Services.Multiplayer.Logger.LogException`,
inside `LobbyChannel.HandleLobbyChanges`) on the SDK's own async event
task — **before any of our `await`s**. Therefore our
`IsBenignLobbyPatcherError` classifier (`HostConnectionService.cs:1852`,
used only in the catch blocks at `:1023` and `:1297`, which wrap *our*
`RefreshAsync` calls) **cannot** see or suppress this particular log.
It is already known-benign and self-correcting; the problem is purely
console noise we cannot `try/catch`.

**Why "at game start".** Multiple clients join the presence lobby
near-simultaneously and write player properties rapidly, so the SDK
receives bursts of deltas that race its local cache. Our
`LobbyPropertyWriter.SaveWithRetryAsync` also does a post-save
`lobby.RefreshAsync()` (`LobbyPropertyWriter.cs:147-153`) to reduce
stale deltas — which may add to the churn.

**Fix (shipped — option 1, iterated).** `BenignLobbyLogFilter`
(`Assets/_Scripts/Utility/BenignLobbyLogFilter.cs`). A
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` installs once
(idempotent) a decorator around `Debug.unityLogger.logHandler` that
drops **only** the benign `LobbyPatcher` `ArgumentOutOfRangeException`;
every other log is forwarded verbatim. Whole file is gated
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so release is unchanged.

- **v1** intercepted only `ILogHandler.LogException` (the route for
  `Debug.LogException`).
- **Retest #1 (user, Editor):** the `[BenignLobbyLogFilter] Installed`
  line printed (decorator active) but the error **still appeared** —
  confirming the SDK logs it via the **`LogFormat`** route
  (`Debug.LogError` / `unityLogger.Log(LogType.Exception, e)`), not
  `LogException`. The `Logger.LogException` frame visible in the
  console is Unity's captured call-site stack, not the `ILogHandler`
  entry point our decorator overrode.
- **v2 (current)** also intercepts `LogFormat` for
  `LogType.Exception`/`Error`, matching either an `Exception` argument
  (via the shared `IsBenignLobbyPatcherError` stack classifier) or a
  pre-rendered message string containing both `LobbyPatcher` and
  `ArgumentOutOfRangeException`. Rendering is defensive (try/catch →
  forward on failure), so a real error is never suppressed.

`Application.logMessageReceived` was rejected — it is a post-hoc
*notification* and cannot suppress. **Worst case the filter is a no-op
— no regression.**

**Needs Editor retest (v2 — `LogFormat` path).** Start a game with ≥2
VPs and confirm the `LobbyPatcher` `ArgumentOutOfRangeException` no
longer appears on **any** instance (the one-time
`[BenignLobbyLogFilter] Installed …` line confirms the decorator is
active; ordinary errors/warnings must still log). If it still leaks, it
is being logged as a plain message string with no type/stack in the
content — paste the exact one-line text and we add a literal-string
match.

**Evidence.** `HostConnectionService.cs:1852` (`IsBenignLobbyPatcherError`),
`:1023`, `:1297`; `LobbyPropertyWriter.cs:147-153`; `CSDebug.cs` (gates
our calls only); SDK stack in the report.

**MPPM Session 1 (2026-06-01) — confirmed still firing, two more leak
points found.** With the NetDiag overlay live, the B1 stale-index family
was observed firing continuously (~every 3 s) in solo Menu_Main, on two
SDK surfaces the `BenignLobbyLogFilter` does NOT cover:

1. **Write path** — `LobbyPropertyWriter.SaveWithRetryAsync` logs
   `Save failed (SessionException: Index was out of range … Parameter
   name: index) — retry 1/3…3/3`. The catch at `LobbyPropertyWriter.cs:158-160`
   already special-cases `"Index was out of range"` and retries, but the
   retry warning still reaches the console.
2. **Read path** — the subsequent `lobby.RefreshAsync()` /
   `PartySessionService.RefreshAsync()` NREs inside
   `WrappedLobbyService.GetLobbyAsync` (the B6 frame — see below), caught
   at `HostConnectionService.cs:1346` and logged + NetDiag-classified
   `Transient`.

Both are the **same SDK stale-index defect** as the `LobbyPatcher`
`ArgumentOutOfRangeException`, just on the Save and Get API surfaces
instead of the WebSocket-delta surface. `BenignLobbyLogFilter` matches
only the `LobbyPatcher` + `ArgumentOutOfRangeException` signature, so
neither of these is suppressed.

**Fix applied (option b — silence at the catch).** Chosen because the
catches were the closer fit: `LobbyPropertyWriter.SaveWithRetryAsync`
already explicitly filters to "Index was out of range" / "Too Many
Requests" via a `when` clause, so demoting the warning there is
surgical; and `HostConnectionService`'s two refresh catches already had
a `IsBenignLobbyPatcherError` discriminator branch, so adding a sibling
`IsBenignSdkStaleIndexNre` follows the existing pattern.

- **`LobbyPropertyWriter.cs:166`** — `Debug.LogWarning` → `CSDebug.Log`.
  The "Save failed (… Index was out of range …) — retry X/3" message
  now strips from release builds and respects runtime mute. Outer
  catch-on-exhaust path unchanged.
- **`HostConnectionService.cs` new method `IsBenignSdkStaleIndexError`**
  — matches a `SessionException` whose **structured `Error` property ==
  `SessionError.Unknown`** (the `[Error: Unknown]` prefix in the log).
  **NOT the message string.** Message-matching was abandoned after it
  turned into whack-a-mole — three message variants of the *same* SDK
  defect appeared across three MPPM restarts:
  1. `"Object reference not set to an instance of an object"` (NRE form)
  2. `"Index was out of range. Must be non-negative and less than the size of the collection."`
  3. `"Index must be within the bounds of the List."`
  All three are wrapped in a `SessionException` whose structured
  `Error` is `Unknown`. The structured reason is the stable signal: a
  genuinely actionable `SessionException` carries a *specific* reason
  (`SessionNotFound`, `SessionDeleted`, `NotInLobby`, `RateLimited`, …),
  and those are handled by the `[definite]` / rate-limit branches that
  run **before** this benign check at both catch sites. Only
  unclassifiable SDK-internal failures land on `Unknown`, and for those
  "log-silent, retry next tick" is already the correct and only
  recovery. Implemented as `se.Error.ToString() == "Unknown"` to avoid
  pinning the exact enum member across SDK versions.

  **Stack deliberately NOT used.** A first attempt matched on
  `StackTrace.Contains("WrappedLobbyService")` AND the message, but that
  silently failed in MPPM: `Exception.StackTrace` is unreliable after
  the exception crosses several async `SetException` boundaries
  (UniTask + Task continuations) before our catch — the stack shown in
  the Unity console is Unity's *captured* stack, not the exception
  object's own `.StackTrace` string (often null/truncated
  post-propagation).

  **Trade-off (accepted).** Matching `Error == Unknown` is broader than
  a message match — a future genuinely-actionable failure that also
  surfaces as `Unknown` would be silenced at these two refresh catches.
  Mitigated by ordering (the `[definite]` + rate-limit branches catch
  every *classifiable* reason first) and by the nature of `Unknown`
  (the SDK couldn't classify it → no actionable recovery exists anyway).
  If a real failure is ever masked here, this is the first line to
  revisit.

  `LobbyPropertyWriter.SaveWithRetryAsync` handles the same defect on
  the write path via a message filter (`"Too Many Requests" || "Index
  was out of range"`) — it does not have a structured `Error` to
  inspect at that callsite, so message-matching is unavoidable there;
  the write path has only ever shown the IOOR string.

  Consumed at two catch sites:
  - `HCS:1069` outer presence-lobby refresh catch: silence as a sibling
    of `IsBenignLobbyPatcherError` (no log, no counter increment, no
    state change).
  - `HCS:1346` party-session refresh catch: silence as a sibling of
    `IsBenignLobbyPatcherError` (early return).

Option (a) — broadening `BenignLobbyLogFilter` — was rejected:
`BenignLobbyLogFilter` exists for SDK-emitted logs that fire before
our catch can run (`LobbyChannel.HandleLobbyChanges` etc.). The two new
signatures both go through our own `Debug.LogWarning` calls inside our
catches, where we have full control without needing to hook the global
log handler.

Discriminator behaves gracefully in IL2CPP if stack info is unavailable
(returns `false` → exception falls through to existing transient log
path; we just see the warning again).

See `../PartySystem/MPPM_SESSION_LOG.md` Session 1, Pre-flight finding #2
for the discovery context.

---

## B4 — TC1: second invite not delivered + party members vanish from 3rd player's online panel 🔴

**Symptom.** VP1 invites VP3 → accept → ok (party of 2). VP1 then
invites VP2 → **VP2 never gets the invite**, and VP1/VP3's rows (shown
"In Lobby 2/4") **vanish from VP2's online panel**.

**Root-cause hypotheses (high, pending retest).**
- Once a party forms (`PartyMembers.Count > 1`), convergence is
  **paused** (`HostConnectionService.cs:~945-958`), which can **freeze
  a presence-lobby split** so VP2 ends up on a different lobby than
  VP1/VP3.
- `RefreshOnlinePlayersDiff` (~`:1150-1196`) **removes** any player not
  in the local presence lobby → VP1/VP3 drop from VP2's
  `OnlinePlayers`.
- On any lobby rejoin, `BuildLocalPlayerProperties`
  (`PresenceLobbyService.cs:~335-350`) **resets `invite_payloads` to
  empty** (documented in a code comment), wiping VP1's outgoing invite
  to VP2 before VP2 reads it.

**Open question (user to retest, after B1).** Do VP1/VP3 rows **come
back on their own** (transient split) or **stay gone** (frozen split)?
Determines whether the fix targets convergence-pause or the
diff/property-reset.

**Diagnostic upgrade (post commit `aaba872`).** Any `JoinOrCreateAsync`
fallback now emits `NetDiag: class=… | …` — if VP2 ends up creating its
own lobby (the split scenario), the catch on the failed join will
classify the cause. `class=Transient` or `class=Unknown` would
strengthen the convergence-pause hypothesis; `class=Offline` would
suggest a different problem.

**Constraint.** This is the fragile, locked-design area — **read
`../PartySystem/ARCHITECTURE.md` before touching**
`HostConnectionService` / `PresenceLobbyService` / invite services.
Likely wants more diagnostics first.

**Evidence.** `HostConnectionService.cs:~945-958, ~964-970, ~1150-1196`;
`PresenceLobbyService.cs:~204-239 (converge), ~335-350 (property reset)`.

**Fix shipped (2026-07-16, invite-chain Task 4) — MPPM retest required.**
Owner decision: allow lobby convergence while partied. Implemented as a
**state-preserving rejoin** plus removal of the convergence pause:

1. `IPresenceLobbyService.LivePropertySource` — a provider hook
   (`Func<IReadOnlyDictionary<string,string>>`) set once by
   `HostConnectionService` (`BuildLivePresenceProperties`). Every lobby
   (re)join path — initial join, reconnect, converge migration — now
   overlays LIVE values onto the property dict in
   `PresenceLobbyService.BuildLocalPlayerProperties`: outgoing
   `invite_payloads` (`InviteService.SerializeAll`), a guest's
   `joined_party` (current session id when `!IsPartyHost`), and
   `matchName`. The rejoin no longer wipes in-flight invites or a
   guest's party advertisement. `accepted_invite` is deliberately NOT
   preserved (fast-path hint only; the session member sync covers it,
   and carrying it across rejoins would make stale signals permanent).
   HCS remains the single writer of the values.
2. The `inActiveInviteOrParty` pause in `HostConnectionService.RefreshAsync`
   is **removed** — convergence now runs on its normal throttle even
   mid-invite / mid-party, so the frozen-split (this bug's scenario:
   partied players stuck in a non-canonical lobby, third player never
   receives the invite) self-heals.

**Retest (MPPM):** the B4 TC1 repro (VP1+VP3 partied, VP1 invites VP2),
plus the invite-chain S10 (member-sent invite) with a deliberately
split lobby; confirm the pending invite survives a converge migration
(sender's `invite_payloads` non-empty after "Converged to canonical"
log) and no B1/B6 stale-index regression from the extra rejoin writes.

**⚠ Repro validity caveat (2026-07-16).** A 4-instance session with
**untagged** MPPM clones reproduced B4-family symptoms (one-sided rows,
empty online lists on some clones) whose actual root cause was the
shared `mppm-clone` auth profile — all untagged clones sign in as ONE
UGS PlayerId, and each clone's lobby join invalidates the previous
clone's membership (dead handle → refresh errors → empty lists). Rows
appeared correct as soon as unique tags were assigned. The original
B4 TC1 session predates the tag prerequisite
(`../PartySystem/TESTS.md` § "MPPM prerequisites"), so this entry's
convergence-freeze hypothesis must be re-confirmed with **tagged** VPs
before any further B4-specific work — the identity collision may
account for part or all of the historical symptom.

---

## B6 — TC3: `NullReferenceException` (`WrappedLobbyService.GetLobbyAsync`) + empty online/request lists 🟡 (refresh-path noise silenced in MPPM Session 1; TC3 empty-lists symptom untested since fix)

**Symptom.** A variant of TC3: VP2 logs a UGS `NullReferenceException`
from `WrappedLobbyService.TryCatchRequest` / `GetLobbyAsync` during
`LobbyChannel.ProcessEvent`, and VP2's online list **and** request list
both go empty.

**Root-cause hypothesis (medium).** Same family as B1 — SDK-internal,
logged by the SDK before our catch — triggered when a lobby
subscription event fires against a stale/torn-down lobby reference
(premature `LeaveAsync`/`ForceReset` during the accept handshake). The
empty-lists symptom is likely our `OnlinePlayers`/requests going stale
when `ActiveLobby` becomes null and refresh early-returns.

**Approach.** Treat the NRE as trigger-reduction (don't leave/rejoin
mid-event; guard against stale refs). Investigate the empty-lists
recovery separately (does the UI repopulate after the next successful
refresh?). Likely bundle with B4 diagnostics.

**Evidence.** SDK stack (`WrappedLobbyService.cs:165/462`,
`LobbyChannel.cs:197`); our lobby leave / `ForceReset` /
refresh-early-return paths in `HostConnectionService` /
`PresenceLobbyService`.

**MPPM Session 1 (2026-06-01) — same SDK frame seen on the refresh
path.** The `WrappedLobbyService.GetLobbyAsync` NRE
(`WrappedLobbyService.cs:170` / `TryCatchRequest` `:497`) was captured
firing every ~3 s from `PartySessionService.RefreshAsync` →
`HostConnectionService.cs:1346`, not only during the accept-handshake
leave/rejoin this entry originally described. The HTTP GET succeeds; the
SDK NREs deserializing the response against a stale cache seeded by the
B1 write-path churn (see B1's Session 1 note). So B6's NRE and B1's
stale-index are the same SDK defect — B6 is the read-path symptom, B1
the write/delta-path symptom. Overlay classifies the read-path NRE
`Transient` and recovers (keeps session, retries next tick).

---

## How we work bugs

Method: see `../README.md` § "How we work bugs". This is the fragile,
locked-design area — read `ARCHITECTURE.md` and
`../PartySystem/ARCHITECTURE.md` first. Presence-side priority order:
**B1 retest → B4 → B6**.
