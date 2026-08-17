# Party System — Refactor Backlog

`HostConnectionService` was refactored in a 17-commit pass (formerly
tracked in `PARTY_SYSTEM_REFACTOR.md`, now archived via git history).
The three services below are the **remaining** party-side refactor
targets, in priority order.

> **Per-commit risk gate.** Every refactor commit ships with its own
> risk table (added inline as the commit is planned). No commit is
> pushed without first walking through which risks apply. See
> `../NetworkDiagnostics/ARCHITECTURE.md` for the diagnostic overlay that
> makes any regression interpretable.

## Shipped — roster-truth pass (2026-08-06, `claude/lobby-sync-bugs-4n4nl2`)

Six commits fixing `../PresenceSystem/BUGS.md` **B15** (three players in one
party rendering three different sizes) and its companion 15–20 s join latency.
Read B15 for the root-cause analysis; this is the change record.

| | Commit | What |
|---|---|---|
| C1 | `3e8885c2` | `IPartyRoster` + `Advertised*` rename + `FriendsListPanel` sources counts per tier. **Fixes the divergence.** |
| C2 | `f6aecb6a` | `OnPartyRosterChanged` — one raise per settled mutation, change-gated. Replaces 3 per-member subscriptions in each of 3 consumers. |
| C3 | `111676ba` | Republish presence on roster change (drained flag — the raise fires with the lobby mutex held). |
| C4 | `791c6d04` | Read and publish get separate `try`s; benign branches become exception filters carrying the rate-limit precedence explicitly. |
| C5 | `090f61a6` | Party-session push channel; push path syncs from the SDK's in-memory roster with **zero UGS reads**. **Fixes the latency.** |
| C6 | `4129b932` | `PartyLobbyKeys` single owner; drop 6 write-only Relay-session keys (partial TODO-P2). |
| — | `bae55ce0` | Docs: B15, the locked two-tier rule, stale `LeavePartyKeepHostAsync` refs. |
| — | `9ee67e5f` | Fail loud if `OnPartyRosterChanged` is unwired (it is now the only party repaint signal). |
| — | `5745ec0f` | Compile fix: missing `using Obvious.Soap` in the new tests. |
| **R1** | `4aece925` | **Defer the roster raise to the main thread** — fixes the B16 regression the above introduced. |
| — | `5b36156e` | Retire the `PARTY FULL` status; derive the invitability rule it was carrying. |

**Verified 2026-08-06** by the owner across multiple 4-VP MPPM configurations:
compiles, the `EnsureRunningOnMainThread` errors are gone, and invite, accept,
party formation and cross-panel agreement are all green.

**Merged into `Ys-bleeding-edge` on the owner's call with four checks
deliberately outstanding** — EditMode tests, S11, the stress gate, and C4's
error-matrix branches. They are listed with cost, failure mode and tick-boxes in
`TESTS.md` § "⚠ Outstanding verification". None is known to be broken; they are
unproven, not suspect. **S11 is the one worth doing first** — the ordinary
4-in-one-party session does not exercise it. If something odd surfaces later,
`791c6d04` (C4) is the first commit to revert: self-contained, never adversarially
reviewed, and its benefit is real but not urgent.

### Read this before the next branch here

Two things went wrong, and the second one is the one to internalise:

1. **B16** (`../PresenceSystem/BUGS.md`) — a new SOAP event was raised from a
   path with no main-thread guarantee, reintroducing `EnsureRunningOnMainThread`.
   The rule it establishes: *a SOAP event whose listeners touch Unity state may
   only be raised from a guaranteed main-thread context; otherwise defer it to an
   `Update()` drain.*
2. **The process.** Seven commits were pushed with zero runtime verification
   between them, in this fragile area, by an author who could not compile. The
   plan was sequenced by **value** when it should have been sequenced by **blast
   radius with a hard stop after each step**. `TESTS.md` now opens with a
   cheapest-gate-first verification order; step 2 of it (single-editor play mode)
   would have caught B16 in under a minute.

**Two extractions were planned and deliberately NOT done.** Both were scoped as
"extract a service", and in both cases the dependency count made the extracted
class a worse design than the code it replaced:

- **`PresencePublisher`** (planned for C4). The published-value trackers are
  entangled with `_presence.CurrentState`, `ResolveCurrentMatchName`,
  `_propertyWriter` and `_lobbyService`. The *decoupling* was the fix and it
  shipped; the class would have been four constructor arguments of indirection.
- **`PartyRosterCoordinator`** (planned for C7). Would need `_memberService`,
  `_partySessionService`, `_stateMachine`, `connectionData`, plus HCS-private
  `ResolveLocalPlayerId` and `ClearOutgoingInviteIfPresentAsync` (themselves
  reaching `_inviteService`, `_propertyWriter`, `_lobbyService`) — **seven
  dependencies to relocate ~60 lines**, producing a class that still could not
  be understood without reading HCS.

The real seam problem in `HostConnectionService` is that it owns presence AND
party AND invites AND netcode AND publishing. Extracting one method cluster does
not address that; the **cross-class refactor** below is where it gets addressed.
Recorded here so the next person does not re-derive the same conclusion.

## Refactor 1 — `PartyInviteController` (highest priority)

**Why first.** It's the most complex orchestrator in the party system.
Mixes Netcode transitions, lobby joins, error recovery, and UI state in
one ~450-line class. The NetDiag overlay added in commit `aaba872` now
makes its catch-block failure modes interpretable — so this refactor
can be planned against real log data rather than guesswork.

**Outline.** Extract three services from PIC:

| Extracted service | Responsibility |
|---|---|
| `TransitionVisualsCoordinator` | The 3-line UI prelude (SetFadeImmediate + ArmSplashFadeOnNextClientReady + PauseSystem.TogglePauseGame), duplicated today across Accept and Leave |
| `ClientReadyGate` | `WaitForClientReadyAsync` — the OnClientReady wait with re-check + linked-CTS-timeout |
| `TransitionRecoveryService` | Catch-block bodies + `BounceToSoloMenuAsync` + `RecoverFromFailedTransitionAsync` — owns the intentional `UniTask.Yield(PlayerLoopTiming.Update)` per `Docs/THREADING.md` |
| `IHostConnectionService` interface | DI alias for HCS so PIC stops using `HostConnectionService.Instance` directly |
| `MenuSceneReloader` | One named operation for the two hardcoded `nm.SceneManager.LoadScene("Menu_Main", Single)` blocks; reads `SceneNameListSO.MainMenuScene` instead of the literal |

**Reduces PIC from ~450 → ~150 lines.** Each commit is
behavior-preserving and individually reverte-able. Detailed commit
breakdown (C1-C8) lives in the project root `PLAN.md` plan file under
"Follow-up backlog (post-fix) — `PartyInviteController` refactor".

**Non-goals (locked).**
- Do not change PIC's external API. `Instance`, `IsTransitioning`,
  `AcceptInviteAsync`, `DeclineInviteAsync`,
  `LeavePartyAndReturnToMenuAsync` are reflected on by tests and read
  by HCS — frozen for this refactor.
- Do not change the YS2 race fix in `HostConnectionService.RefreshAsync`
  catch (commit `a1a8eb9`).
- Do not migrate tests off reflection. `_transitioning` field name and
  `IsTransitioning` property name stay exactly as they are.
- Do not remove `HostConnectionService.Instance` — `PartyInviteSystemTests`
  reflects on it. A future cleanup can delete the static accessor once
  tests migrate to DI.

## Refactor 2 — `PartySessionService`

**Why.** Already has the `IsTransientSessionException` retry, but the
retry policy is encoded inline in `CreateAsync` and `JoinByIdAsync`.
Hard to test, hard to extend to other operations.

**Outline.** Extract `SessionRetryPolicy` as a small testable strategy
object. Make every method that does a UGS call accept the policy as a
constructor dependency. The three classifiers
(`IsHostConflictException`, `IsRateLimitException`,
`IsTransientSessionException`) become methods on the policy.

**Pre-requisite signal.** Wait for NetDiag data from real MPPM runs to
tell us how often the existing retries actually fire vs. fail through.
If the retry exhausts and bounces are rare, this refactor is lower
priority than Refactor 3.

## Refactor 3 — `NetworkTransitionService`

**Why.** Shutdown / connect / scene-sync waits use `WaitUntil` polling
and ad-hoc timeouts. The diagnostic overlay will reveal which step
actually fails most often; the refactor consolidates them.

**Outline.** Replace polling with SOAP event subscriptions where
possible (Netcode already raises connect/disconnect/scene-load events).
Normalize timeouts via a single policy constant per step. Each `WaitFor*`
method becomes a thin wrapper around an event-driven wait with timeout.

**Pre-requisite signal.** Same as Refactor 2 — wait for NetDiag data.

## Cross-class refactor (deferred, after the three above)

Make `leave → reset → join` a single owned operation across PIC + NTS +
HCS + PartySessionService. Currently distributed across multiple
classes with implicit ordering contracts. This is the next layer up
once the three internal refactors are done.

## Deferred items (carried from the 17-commit pass)

These were captured in the old `PARTY_SYSTEM_REFACTOR.md` "Deferred" section
as future commits. None are started. Each is a dedicated commit, not comment
cleanup. Listed here so they survive the doc reorg.

### D1. Audit / remove the PENDING-sentinel three-phase acceptance protocol

With eager per-user Relay, invites carry the real session ID directly, so the
PENDING handshake may be dead code. The protocol spans
`InviteService.PENDING_SESSION_ID`, `AcceptanceSignalService.PublishSignalAsync` /
`WaitForRealSessionIdAsync` / `RepublishWithRealIdAsync`, and
`LobbyRefreshScheduler`'s PENDING-republish boost window. **Action:** confirm no
live path writes PENDING, then remove the protocol across `InviteService`,
`AcceptanceSignalService`, `LobbyRefreshScheduler`, and their interfaces. Spans
5+ files. (Touches presence-side refresh — coordinate with
`../PresenceSystem/REFACTOR.md`.)

### D2. Extract `RefreshErrorPolicy` helper

Fold `_rateLimitBackoffUntil`, `_consecutiveRefreshErrors`,
`MAX_REFRESH_ERRORS_BEFORE_RECONNECT`, and the benign/transient/definite
classification predicates out of `HostConnectionService` into a single testable
policy object. This is the same surface the YS2 two-layer guard touches (see
`BUGS.md` B-series), so do it *with* the cross-class refactor so the refresh loop
observes one transition gate instead of inferring it from
`PartyInviteController.IsTransitioning`.

### D3. `GameDataSO` Single-Responsibility split

`GameDataSO` owns far more than session state. Pull session ownership
(`ActiveSession` + related) into a dedicated SOAP container so the
single-source-of-truth field (Q4 in `ARCHITECTURE.md`) lives in a focused
container rather than the catch-all game-data SO. Large, cross-cutting — affects
every `ActiveSession` reader (HCS, MultiplayerSetup, MultiplayerMiniGameControllerBase,
Player). Plan carefully; touch only after the three service refactors stabilize.

### D4. MPPM-driven play-mode integration tests

Automate accept / decline / leave / refresh-fail / session-gone-auto-recovery as
play-mode tests so the exit criteria 6-8 (`ARCHITECTURE.md`) stop depending on a
manual MPPM pass. See `TESTS.md` for the manual procedures these would automate.

### D5. Event-driven `EnsureInitializedAsync` refactor

Replace the sequential awaits in HCS init with state-machine-driven SOAP
transitions: add `WaitingForProfile` / `JoiningPresenceLobby` states to
`PartyStateMachine` and delete the `_joining` flag. Lets UI direct-subscribe to
init progress instead of polling state. (Touches the presence-lobby join step —
coordinate with `../PresenceSystem/REFACTOR.md`.)

## Sequencing

1. **Diagnostics first (DONE, commit `aaba872`).** NetDiag overlay
   added so every refactor commit can be diagnosed if it regresses.
2. **Refactor 1 (PIC).** Plan it next — the catch decoration now in PIC
   will tell us which Accept/Leave failures are most common after the
   refactor, validating no regression in the catch paths.
3. **Wait for data.** Run MPPM smokes after Refactor 1 lands. NetDiag
   counts will indicate whether Refactor 2 or Refactor 3 is the
   noisier failure surface.
4. **Pick the noisier one next.** Sequence Refactor 2 and Refactor 3
   in order of failure frequency, not file-list order.
5. **Cross-class refactor last.** Touch only once the three internal
   refactors stabilize.

## Per-refactor commit cadence

Within each refactor:

- One concern per commit — extract one service, then commit.
- Each commit compiles, passes existing tests, and is independently
  buildable.
- 3-VP MPPM smoke required per commit (accept, decline, leave, second
  accept after leave).
- Risk table for the specific commit added inline before push.
- Push only after explicit risk discussion.

## Per-commit revision protocol

This is the working method that produced the original 17-commit pass, kept
as the standard for every party-side refactor commit. Before starting any
commit N:

1. **Read the relevant source files fresh** (`HostConnectionService.cs`,
   `PartySessionService.cs`, `PresenceLobbyService.cs`,
   `PartyInviteController.cs`, etc.). Do not trust line numbers cached in
   this doc — the file moves underneath us.
2. **Present the current state of every method this commit touches**:
   - The full method source (verbatim from the file).
   - Every caller / callsite (file:line + the calling method's name).
   - Every method this method calls into (file:line + the called method's name).
   - A 1-2 sentence explanation of what the method currently does and what
     we'll change.
3. **Re-check whether the assumptions in this commit's section still hold**
   (line numbers, method signatures, surrounding catch behavior).
4. **Update the relevant doc** (this file / `ARCHITECTURE.md` / `BUGS.md`) to
   reflect any new findings, including the method dumps from step 2. Note
   anything that affects later commits.
5. **Then start coding.** Commit. Note any unexpected behavior in the
   commit's section.
6. **Re-evaluate the exit criteria** (`ARCHITECTURE.md` → "Unbreakable exit
   criteria"). If satisfied, stop. Otherwise, continue to commit N+1.

This is how the plan stays accurate as the codebase changes underneath it,
and how the prompter keeps visibility into every method touched.

## Related docs

- `ARCHITECTURE.md` — current state, locked design, key files
- `BUGS.md` — open bugs to consider during refactor
- `TESTS.md` — manual test procedures
- `../NetworkDiagnostics/ARCHITECTURE.md` — diagnostic overlay every refactor commit ships behind
