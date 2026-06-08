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

## Three retry classifiers

Both create and join run inside retry loops keyed on three exception classifiers — each with its own
policy:

| Classifier | Retries | Backoff | Covers |
|---|---|---|---|
| `IsHostConflictException` | up to 2 | none | `NetworkManager` still shutting down from a prior host |
| `IsRateLimitException` (HTTP 429) | up to 3 | exponential (2 s base) | UGS read/write rate limits |
| `IsTransientSessionException` | up to 5 | exponential (1 s base) | SDK `SessionException` NRE, lobby-events 23006, non-fatal collisions |

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
