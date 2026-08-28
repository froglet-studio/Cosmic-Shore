<div class="sec-eyebrow">Part II · The gameplay layer</div>

# The invite protocol, end to end

An invite is a small state exchange over presence-lobby properties, followed by a Netcode host↔client
transition. Because sessions are eager, the payload always carries a real id.

::: figure invite-flow
Send → scan → accept → handshake → join. The host spawns the joiner's vessel and replicates all
existing pairs; the host's roster scan cross-checks the authoritative session to ignore stale
properties.
:::

## The payload

`InviteService` serializes each outgoing invite as a single line and writes the set into the sender's
lobby properties:

```text
targetPlayerId | senderPlayerId | sessionId | senderDisplayName | senderAvatarId
```

A legacy **`PENDING` sentinel** protocol existed for the lazy era — invites could be sent before a
session existed, carrying `sessionId = "PENDING"`, and were patched in place once the session came up.
With eager creation the id is real from the first write, so the sentinel path is effectively dead — a
good example of an architectural decision retiring an entire sub-protocol.

## Dedup guards

Properties are read repeatedly by the refresh loop, so the same invite would otherwise fire every
tick. Two guards prevent that:

- **SDK-side:** `_lastFiredInvite` / `_lastInviteResolved` caches suppress re-firing `OnInviteReceived`
  for an invite already surfaced.
- **Lifetime:** an outgoing invite times out after **30 s**, and is cleared on presence-leave or on a
  successful party-join, so stale invites don't linger.

## Accept = a choreographed transition

`PartyInviteController.AcceptInviteAsync` decomposes the host→client switch into explicit primitives,
each with its own timeout, rather than one monolithic operation:

::: figure host-client-transition
Accept is a sequence of bounded steps: fade, shut down own NM, join the host's session, wait for the
Netcode connection and scene sync, wait for the local vessel, then signal completion. Any timeout
routes to a clean recovery.
:::

1. Transition visuals — fade to black, arm the splash-fade for the next `OnClientReady`, unpause.
2. `NetworkTransitionService.ShutdownAsync` (8 s timeout) — wait for the local host to fully reset.
3. `HostConnectionService.AcceptInviteAsync` — publish the acceptance signal, then `JoinById(host)`.
4. `WaitForClientConnectionAsync` (8 s) — poll `IsConnectedClient`.
5. `WaitForSceneSyncAsync` (8 s) — await the first client `SceneEvent`.
6. `WaitForClientReadyAsync` (10 s) — wait for the local vessel to spawn.
7. Raise `OnPartyJoinCompleted` — party UI refreshes.

::: insight Decompose the transition, make recovery explicit
Because each step is a named primitive with a timeout, a failure anywhere routes to
`RecoverFromFailedTransitionAsync`, which mirrors the clean-leave sequence to return the player to a
solo, controllable menu. There is no "stuck mid-transition" state because every step either advances
or recovers.
:::
