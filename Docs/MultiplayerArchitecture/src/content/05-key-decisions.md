<div class="sec-eyebrow">Part I · Overview</div>

# Key design decisions

Four decisions carry most of the system's weight. Each is *locked* — re-opening them is the most
common way recurring bugs come back — and each trades something away on purpose.

::: decision EAGER per-user Relay — the "Always-InParty" model
**Decision.** Every authenticated player creates their own Relay-backed party session the moment they
enter the menu, rather than lazily creating one when they send the first invite.
**Rationale.** Lazy creation meant an invite triggered *create session → shut down local host →
hand off → reconnect*, a multi-step cascade that was the root of nearly every recurring party-invite
bug. With eager creation an invite is a plain *join* against an id that already exists.
**Trade-off.** Every signed-in player holds a Relay allocation even when solo. We accept that cost in
exchange for eliminating the shutdown-and-recreate race class entirely.
:::

::: decision Single-writer SOAP for all shared state
**Decision.** Each piece of cross-system state has exactly one writer. `HostConnectionService` is the
sole writer of party/lobby state into `HostConnectionDataSO`; `FriendsServiceFacade` owns
`FriendsDataSO`. Everyone else **reads** through ScriptableObject events and lists.
**Rationale.** It makes data flow auditable — there is one place to look when "who is in the party"
is wrong — and it decouples UI from services entirely (the UI never calls the UGS SDK directly).
**Trade-off.** A little ceremony: new state needs a SOAP variable/event and a disciplined single
writer, rather than ad-hoc fields. The payoff is no two-writer races and trivially testable UI.
:::

::: decision Main-thread affinity via `.AsMainThread()`
**Decision.** Every `await` of a UGS or Netcode `Task` is wrapped in `.AsMainThread()`, which
re-asserts Unity's `SynchronizationContext` after the call.
**Rationale.** UGS continuations resume on the .NET thread pool, where touching any Unity object — or
raising a SOAP event whose listeners do — throws `EnsureRunningOnMainThread`. The wrapper guarantees
the code after every cloud call is back on the main thread.
**Trade-off.** A tiny per-await cost and the discipline to never forget the wrapper — enforced by a
runtime *canary* that logs loudly if a continuation reaches a hot UI path off-thread.
:::

::: decision Server-authoritative, unified spawning
**Decision.** Player and vessel spawning is server-driven and flows through **one** Netcode + SOAP
pipeline — the same path for menu autopilot vessels, AI opponents, and live gameplay.
**Rationale.** One spawn path means one set of bugs to fix. The server owns vessel identity, domain
(team), and ownership; clients reactively resolve player-vessel pairs as objects replicate, with zero
polling.
**Trade-off.** Spawn timing has to be choreographed carefully (deliberate pre/post-spawn delays, and
`destroyWithScene` tuning for AI) — but that choreography lives in one place instead of three.
:::

These four compose. Eager sessions keep the *state machine* simple; single-writer SOAP keeps the
*UI* honest about that state; `.AsMainThread()` keeps the *callbacks* safe; and server-authoritative
spawning keeps *who-owns-what* unambiguous. The next section shows what happened when each was
violated.
