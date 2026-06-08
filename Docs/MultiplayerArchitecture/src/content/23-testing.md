<div class="sec-eyebrow">Part II · Verification</div>

# Testing strategy

The party system is verified at three levels: fast edit-mode unit tests for the data and control
logic, a play-mode test for the accept flow, and manual Multiplayer Play Mode (MPPM) procedures for
the multi-client behaviour that can't be unit-tested.

## Automated tests

| Suite | Scope |
|---|---|
| `PartyInviteDataTests` | Invite payload value semantics |
| `PartyPlayerDataTests` | Player-data equality (by `PlayerId`) and party-state fields |
| `PartyInviteControllerTests` | Controller transition logic (uses reflection on `_transitioning`) |
| `PartyInviteSystemTests` | Invite system integration |
| `HostConnectionDataSOTests` | SOAP container behaviour |
| `DomainAssignerTests` | Team assignment / balancing determinism |
| `PartyAcceptFlowPlayModeTests` | The accept flow under a real play-mode network |

::: insight Test the seams, not the cloud
The interface-per-service decomposition is what makes the orchestrator testable without standing up
real UGS sessions. The control logic — state transitions, payload parsing, member diffing, team
balancing — is exercised in fast edit-mode tests; only the genuinely networked accept flow needs a
play-mode harness.
:::

## Manual MPPM procedures

Behaviour that depends on several real clients is covered by documented manual procedures, run in
Unity's Multiplayer Play Mode (several virtual players in one editor):

- **Party smoke / stress (S-series):** accept, decline, leave, and a stress run of five consecutive
  accepts with random declines/leaves interleaved.
- **Presence procedures (P-series):** discovery, simultaneous-create convergence, invite delivery
  across a lobby split.
- **Diagnostics procedures (A–E):** verify the NetDiag classifier and `NetworkMonitor` accuracy,
  including the case where the Editor's `internetReachability` lies.

A chronological **MPPM session log** captures session-scoped findings — the timeline view matters
because many bugs (like B3.b and B8) were only understood by reading the exact ordering of log lines
across two clients.

## The per-commit gate

The eight unbreakable exit criteria are the acceptance bar. Criteria 1–5 (no fatal failure, no stuck
UI, no silent divergence, reversible transitions, idempotent retries) are demonstrated to hold;
criteria 6–8 (3-VP smoke, 3-VP stress, 4-VP concurrent invites) are re-verified per commit. A change
isn't "done" until that gate is green.
