<div class="sec-eyebrow">Part II · The record</div>

# Design-decisions ledger

The load-bearing decisions, each with what it buys and what it costs. These are *locked* — the
project treats re-opening them as the most common way solved bugs return — so each carries a written
rationale that a future change has to argue against explicitly.

| Decision | Pros | Cost / trade-off |
|---|---|---|
| **EAGER per-user Relay** ("Always-InParty") | Invites become joins, not create-then-handoff; eliminates the shutdown-and-recreate race class | Every solo player holds a Relay allocation |
| **Two-level sessions** (lobby + party) | Cheap global discovery; invites need no host privilege; gameplay transport stays small | Two sessions to keep coherent; the "lobby ≠ party" confusion if conflated |
| **Single-writer SOAP** | One place to debug shared state; UI fully decoupled and testable; no two-writer races | A little ceremony to add new state |
| **State machine over boolean flags** | Illegal transitions are loud and immediate; one logged lifecycle timeline | Must define every legal transition up front |
| **Session authoritative over presence** | Host roster can't be fooled by stale lobby properties (killed B8) | Presence data can be cosmetically stale (harmless) |
| **Live-`Instance` property for UGS singletons** | Never pins `null` from a constructor that runs before UGS init | Resolves at every use instead of once |
| **Decomposed transition primitives** | Each step has its own timeout; recovery is explicit; no "stuck mid-transition" | More methods than one monolithic call |
| **`.AsMainThread()` boundary helper** | Safe SOAP raises and Unity access after every cloud await; one uniform surface | Per-await wrapper discipline (canary-enforced) |
| **Fail-loud (no SOAP null-guards)** | Wiring errors surface instantly and obviously | A missing reference crashes rather than degrades |
| **Diagnostics separate from retry policy** | Log format never silently drives behaviour; the two can diverge | Two predicates to maintain |

## A few decisions worth their own note

::: decision One create surface: `EnsurePartySessionAsync`
The four old `RetryCreate*` call sites were not really retries — three were first-time creates and one
was recovery. Collapsing them into a single idempotent create-or-no-op removed a whole category of
"did we already create one?" ambiguity. Recovery is the only site that clears a stale session first.
:::

::: decision State-machine recovery, not null-checking
Runtime nulls inside a service method imply an invariant violation, so the response is to **log + drive
the state machine to a recoverable state** (typically `Disconnected`), letting the normal sign-in /
retry loop pick back up — rather than scattering defensive null-checks that hide the real problem.
:::

::: insight The decisions compose into the invariant
None of these decisions is exotic on its own. Their value is cumulative: eager sessions keep the state
machine small, the state machine keeps recovery honest, single-writer SOAP keeps the UI truthful,
`.AsMainThread()` keeps callbacks safe, and authoritative-session keeps two clients agreeing. Together
they are what makes "unbreakable under adversarial conditions" a checkable property rather than an
aspiration.
:::
