<div class="sec-eyebrow">Part I · Overview</div>

# The player journey

Here is the whole lifecycle, from launch to flying with a friend, as one player experiences it.

::: figure player-journey
The end-to-end flow: authenticate, join the presence lobby, eagerly create your own Relay party
session, then invites become simple joins. Both players end up flying in the shared menu.
:::

1. **Boot & authenticate.** The Bootstrap scene initialises UGS and signs the player in anonymously,
   producing a stable `PlayerId`. Nothing networked happens until this completes.
2. **Become discoverable.** On sign-in, `HostConnectionService` joins the global presence lobby
   (tagged `PRESENCE_LOBBY`), so the player appears in everyone's "online" list.
3. **Eagerly host a party.** Immediately, the same service creates the player's **own** Relay-backed
   party session. The player is now `InParty` — solo, but fully hosted — and a menu vessel spawns on
   autopilot. This is the "Always-InParty" baseline.
4. **Send an invite.** Tapping a friend writes `invite_target` + `invite_data` (carrying the real
   session id) into the sender's presence-lobby properties. No session is created or destroyed.
5. **Receive an invite.** The recipient's refresh loop (every few seconds) scans lobby properties, finds
   an invite addressed to it, and raises a SOAP event that pops the Accept / Decline panel.
6. **Accept = join.** On Accept, the recipient shuts down its own solo session, joins the sender's
   session over Relay, and waits for the Netcode connection and scene sync to settle.
7. **Spawn & fly.** The host spawns a vessel for the joiner (autopilot on), replicates all existing
   player-vessel pairs to the new client, and both players fly together in the lava-lamp menu — each
   independently toggling its own freestyle control.

::: insight The invite is a join, not a handoff
Because step 3 already guaranteed a live session with a real id, step 6 is just "leave mine, join
yours." There is no window where a session is being created on demand while another player waits to
connect — the class of race conditions that window produced is exactly what the eager model
eliminates (see the next section).
:::

Every reversible failure along this path — a join that times out, a dropped connection mid-transition
— returns the player cleanly to a solo, controllable menu with no leftover network objects. That
*reversibility* is a first-class requirement, not an afterthought.
