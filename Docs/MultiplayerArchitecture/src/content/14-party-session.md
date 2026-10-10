<div class="sec-eyebrow">Part II · The gameplay layer</div>

# The party session & state machine

`PartySessionService` owns the **Relay-backed** session — the one that actually carries gameplay. It
is created eagerly per player and is the thing an invite ultimately joins.

## One create surface, idempotent

There is exactly one public create-or-no-op entry point: **`EnsurePartySessionAsync`**. It no-ops if
the player is already hosting a party and creates otherwise. The various `RetryCreate*` wrappers that
once existed were all deleted — three of their four call sites were really first-time creates, and the
fourth (recovery) explicitly clears the stale session first.

`ActiveSession` reads and writes a single backing field on `GameDataSO`, so there is one source of
truth for "which session am I in" and it is never nulled outside an intentional leave.

## One classifier, one retry executor

Both create and join run under `UgsRequestPolicy.ExecuteAsync` (2026-10-07; it replaced three retry
loops and three private classifiers here, and their copies in four other files):

| `UgsFailureClass` | Retries | Backoff | Covers |
|---|---|---|---|
| `RateLimited` (HTTP 429) | up to 3 | `min(8 s, 1 s · 2^n)`, jittered half-to-full | UGS read/write rate limits |
| `Transient` / `Benign` | up to 3 | `min(4 s, 0.5 s · 2^n)`, jittered | SDK `SessionException` NRE, lobby-events 23006, the stale-index family, 5xx |
| `Conflict` | exactly 1 | 250 ms | `NetworkManager` still shutting down from a prior host |
| `Gone` / `Full` / `Fatal` | none | — | propagate |

Retries draw on a per-client budget (10 per rolling minute); `party:create` and `party:join:{id}`
are single-flight, so two racing callers share one request.

Non-transient errors propagate to `HostConnectionService.AcceptInviteAsync`, which logs and rethrows
so `PartyInviteController` fails fast into its recovery path. A freshly-provisioned session can
transiently 404 on refresh, so a **4-second post-creation grace period** skips refreshes that would
otherwise misclassify a brand-new session as "gone".

## The 7-state lifecycle

`PartyStateMachine` replaced a scatter of booleans (`_initialized`, `_isHost`, `_inviteSent`,
`_joining`, `_leaving`) that drifted out of sync across async paths.

::: figure party-state-machine
The validated party lifecycle. `InParty` is the persistent baseline — every player always has a live
Relay session. `Disconnected` is reachable from anywhere as the emergency exit.
:::

| State | Meaning |
|---|---|
| `Disconnected` | Not in any UGS session — initial, sign-out, or fatal error. |
| `InPresenceLobby` | Transient — auto-advances to `HostingParty` as the solo Relay session is created. |
| `HostingParty` | Transient — a Relay session is being created/recreated (~1–2 s). |
| `InParty` | The persistent baseline — live Relay session (solo or with members), vessel spawned. |
| `Inviting` | Sent at least one invite; session already exists, no NM change. |
| `JoiningParty` | Accepted someone's invite — shutting down own session, joining the host's. |
| `Reconnecting` | Connection lost; recovering back to `InParty` or down to a fresh `HostingParty`. |

::: decision A state machine over boolean flags
Illegal transitions are rejected with an immediate, explicit warning rather than a silent downstream
failure. "Why was the vessel destroyed?" used to trace back to a flag left in the wrong state after a
failed transition; now it traces to a single, logged `from → to` timeline visible in the MPPM console.
:::
