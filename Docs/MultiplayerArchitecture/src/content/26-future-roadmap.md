<div class="sec-eyebrow">Part II · Looking ahead</div>

# Future improvements & roadmap

::: lead
An honest appraisal: the architecture is sound and the engineering discipline around it is unusually
strong. The work that remains is less about fixing the core design and more about scale, host-loss
resilience, and measuring reliability in the field. This section separates what is already on the
team's backlog from recommendations that go beyond it.
:::

## Already planned (the team's own backlog)

The `REFACTOR.md` and `TODOS.md` trackers already capture a granular, well-sequenced plan. Grouped by
theme:

| Theme | Planned items |
|---|---|
| **Decomposition & maintainability** | Extract `PartyInviteController` into `TransitionVisualsCoordinator` / `ClientReadyGate` / `TransitionRecoveryService` (Refactor 1); `SessionRetryPolicy` strategy object (Refactor 2); event-driven `NetworkTransitionService` (Refactor 3); the cross-class `leave → reset → join` operation; `RefreshErrorPolicy` (D2); `GameDataSO` session-state split (D3); remove the dead `PENDING` sentinel protocol (D1) |
| **Test automation** | Automate accept / decline / leave / refresh-fail / auto-recovery as play-mode tests so exit criteria 6–8 stop depending on a manual MPPM pass (D4) |
| **Rate-limit & SDK churn** | Coalesce startup property writes (TODO-8); ±10 % jitter on the refresh interval (P3); gate the online-panel diff on visibility (P7) |
| **UX** | "Reconnecting…" indicator (P6); per-`NetDiag`-class toast messages (TODO-5); auto-dismiss stale invites on `SessionGone` (TODO-6); an invite-freshness timestamp (TODO-7) |
| **Diagnostics** | `NetworkMonitor.BoostPolling` during a transition; active reachability probing; a `NetDiag Report` tool to tally failure-class frequencies; broader `NetDiag` adoption across auth / friends / IAP catches |

That is a healthy backlog — it shows the symptom-level issues are understood and queued.

## Recommended beyond the backlog

These are the bigger-picture gaps the current docs do **not** address. They are about resilience,
scale, and observability rather than the core design.

| # | Recommendation | Why it matters | Priority |
|---|---|---|---|
| 1 | **Host-loss resilience / migration** | The party host *is* a player (server + client). If the host drops or crashes, the whole party ends — there is no migration or promotion path today. For a "party game", surviving a host disconnect is close to table stakes. | <span class="badge open">High</span> |
| 2 | **Prove 3–4-player party reliability (close B5)** | The "second joiner fails to join" bug means parties beyond two players aren't yet dependable. Multi-joiner reliability should be a hard gate before any scale push. | <span class="badge open">High</span> |
| 3 | **Move invites/presence to a push model** | The whole discovery layer is *polling* lobby properties every few seconds. B1 and B6 (SDK stale-index churn) are symptoms of fighting that poll-plus-delta model, and it adds invite latency. Subscribing to lobby events instead would cut both. Jitter and write-coalescing (already planned) treat the symptom; this addresses the cause. | <span class="badge investigating">Med–High</span> |
| 4 | **A scale & cost story for eager Relay + the single global lobby** | Eager creation is the right call for *correctness*, but every signed-in player holds a Relay allocation, and discovery funnels through one 100-player `PRESENCE_LOBBY` with convergence races. Beyond a few hundred concurrent users this needs idle-allocation reaping, sharded or query-based discovery, and cost telemetry. | <span class="badge investigating">Med</span> |
| 5 | **Production observability** | `NetDiag` is excellent but **dev-only** (stripped from release). There is no release-safe signal for party success/failure rates or join latency in the field. Pipe a lightweight party funnel through the existing analytics managers so reliability is measurable on real devices. | <span class="badge investigating">Med</span> |
| 6 | **A CI gate for the automated tests** | There is no CI pipeline today, so even after D4 automates the tests, nothing runs them on a change. A CI job running edit-mode + headless play-mode tests on every PR would turn the exit criteria into an enforced gate rather than a manual ritual. | <span class="badge investigating">Med</span> |
| 7 | **Approval & reconnect hardening** | Connection approval is currently unconditional — it could validate the joiner against an active invite + capacity. And a dropped client bounces to solo rather than resuming into the same party; a reconnect-resume path would smooth transient drops. | <span class="badge deferred">Low–Med</span> |

::: insight The honest verdict
This is a well-architected, genuinely production-minded multiplayer system — the two-level model,
eager sessions, single-writer SOAP, the threading contract, and the per-catch resilience matrix are
the right bones, and the documentation culture (locked decisions, root-cause-first bug work, written
exit criteria) is better than most studios twice the size. The gaps that remain are the *expected*
next frontier for any party-networking stack: surviving host loss, proving the system at party sizes
above two, replacing polling with push, and measuring reliability in production. None of them require
unwinding the core design — they build on it.
:::
