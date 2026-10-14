# prisma_party_scenarios — the party layer's Block 3 scenarios, five players, no Editor

```
bash Tools/Build/prisma_party_scenarios/run.sh          # ~10-15 min wall; exit 0 = every scenario passed
KEEP=1 bash Tools/Build/prisma_party_scenarios/run.sh   # keep the worktree, the logs and results.json
COSMIC_SHORE_NET_SIM=4g bash .../run.sh                 # every pilot on a simulated 4G line
COSMIC_SHORE_NET_TRANSPORT=tcp bash .../run.sh          # the TCP transport instead of the default UDP
```

Needs the .NET 10 SDK (`DOTNET10_ROOT`, default `$HOME/.dotnet10`), like
`../prisma_edit_mode_tests/run.sh`. The scratch dir is `$PRISMA_PARTY_WORK` (default
`$TMPDIR/prisma_party_scenarios`): `logs/<A..E>.log` is each pilot's full console, and
`results.json` is one row per scenario with the state it asserted on, plus `network`: each pilot's own
boot lines for its transport, its whole stack, the UGS project it went online to, its sign-in and its
simulated line, so a result names what it ran under. `logs/ugs-auth.log` has one line per sign-in, and
`logs/standins.log` a relay and Lobby counter line every 30 s.

It needs the **UGS-only engine** (Port/docs/MULTIPLAYER.md §6.10, 2026-10-14): `run.sh` stops with exit 2
if the engine has no `UgsMultiplayerService`. The shared-folder sessions (`COSMIC_SHORE_NET_DIR`) and
`COSMIC_SHORE_RELAY` / `--relay-server` it used before are gone from the engine, and so are its old
`PRISMA_RELAY` switches: every pilot now goes through the relay, as every START ONLINE game does.

## State on 2026-10-14: 8/14, blocked on the match's frame rate, not on the stand-ins

Ported to the UGS stand-ins on 2026-10-14 and run on a 4-core cloud container against the UGS-only engine:
**8/14**. T1, T5-accept, T2b, kick, leave, T2, T5-join and launch pass through the stand-ins (five
sign-ins, one per pilot; every pilot booted `[net] stack: simulated > relay > udp; sessions: faults > ugs`,
the relay forwarded the match). **T4 fails, and everything after it is `not run`.**

That failure is not the port's. The pre-port harness on the pre-port engine (shared folder, same game
code, same container) fails at exactly the same step with exactly the same error: **8/14**, T4,
`timed out after 180s waiting for PilotB's Ready button to show`. Measured cause:

- In the Bloomrush match each headless pilot runs at **~2.4 fps** with four matches on four cores,
  and at **3.5-5.4 fps even alone, offline and uncontended**. The menu runs at ~58 fps.
- The scene's live behaviour count keeps climbing: **~570,000-595,000 per pilot** a few minutes into the
  match, rising by ~130 a frame (PilotB read 87,643 on entering it).
- Headless ticks a fixed 1/60 s, so the 7.5 s pre-game cinematic takes ~190 s of wall time, past T4's
  180 s wait. A local run that pressed Skip and allowed 8× every wait (not committed) got Ready shown
  and counted on two pilots, but the host still had not counted the third press after 960 s.

So T3, T6, net, T7 and T4-lobby are **not yet verified on the stand-ins**. They need a match that runs at
playable speed in Amoebius first (profile the Bloomrush scene's object growth: `Docs/claude/SHADERS_AND_PERFORMANCE.md`).
The last 14/14 was on the shared folder, before 2026-10-14 (`Port/docs/MULTIPLAYER.md` §6.7-6.8).

## What it does

It builds Amoebius's player (`Port/`) against **this checkout's** `Assets/_Scripts` and launches five
instances, `PilotA`…`PilotE`. Each instance runs as its own process with its own `NetworkManager`,
its own save folder and its own control port. Each is online exactly as START ONLINE is: it signs in
to UGS, allocates on UGS Relay and holds its presence lobby, party and match sessions in UGS Lobby, over
Froglet's UDP transport. All three services are **local stand-ins**, so nothing reaches the live UGS
project:

| Service | Stand-in | Pilots read |
|---|---|---|
| Player Authentication (anonymous + session-token sign-in) | `ugs_auth_standin.py` | `COSMIC_SHORE_UGS_AUTH_URL`, `COSMIC_SHORE_UGS_PROJECT` |
| Relay (allocations REST + Unity Relay's UDP protocol) | `standins/`: the engine's `FrogletRelayServer` | `COSMIC_SHORE_UGS_RELAY_URL` |
| Lobby (create, query, join, update, heartbeat, remove, delete) | `standins/`: `Port/tests/CosmicShore.Tests/UgsLobbyStandIn.cs`, compiled in, so the engine tests and the harness share one Lobby stand-in | `COSMIC_SHORE_UGS_LOBBY_URL` |

The sign-in's id tokens (`id-PLAYER-N`) are what the Lobby stand-in reads as the caller, so five pilots
are five UGS players. Then `scenarios.py` plays one continuous session through
`driver.py`. It presses only what a person presses: the `party` console command
(`Assets/_Scripts/Controller/Party/PartyConsoleCommand.cs`, which calls exactly what the buttons
call), the HUD's Ready button, and the port's `arcade` / `vessels` / `score` verbs.

It sends **no key events**. A console line is set into the field through the control port, then
the overlay's own Run button is clicked. That button is on the topmost canvas, so the click cannot
reach the game. Typing plus Enter used to leak: on a menu that had just loaded, the Enter opened
the arcade card the screen had selected for gamepad navigation. It asserts on what each machine reports.

| id | closes | scenario |
|---|---|---|
| T1 | B2 / T1 | Invite → Accept: the guest is seated and both rosters agree |
| T5-accept | single-flight | A double-tapped Accept starts one accept flow. The second press finds the invite already consumed, so the controller's own Accept guard is not exercised here |
| **T2b** | **B25** | Two Joins on a 3/4 party, Run pressed on both consoles behind a barrier (skew < 1 ms): the host ends at **4/4**, one joiner is seated, the other is back in its own menu with "That party is full." `refused_by` says whether the loser passed the pre-flight, which means the session's 4 seats refused it — the race B25 is about — or was stopped by the pre-flight |
| kick / leave | — | The member lands in its own working menu, and the host recounts |
| T2 | B5 / T2 | Two Joins at once with room for both: both are seated. Every race starts only once each racer's row shows the host's real N/M (B29: polled presence lags, and the pre-flight refuses on the stale number) |
| T5-join | single-flight | A double-tapped Join starts ONE direct join. `second_tap_stopped_by` records which layer stopped the second press. If it lands first, the controller's `_transitioning` guard ignores it (run 6); if it lands after, the transition fade, which blocks raycasts, means it never ran (run 7) |
| launch | — | The party of four launches Bloomrush together |
| T4 | B20 | Three press the HUD's Ready button (once it shows) and the fourth leaves: the host's gate goes 3/4 → 3/3 and the countdown starts |
| T3 | B21 | A guest leaves mid-race: the host hands its ship to the AI (owner flips to the server, and the ship keeps moving), and its score row survives |
| T6 | spectator §6 | A spectator joins the running match: the host counts `humans=2 spectators=1`, and no vessel is added |
| net | Block 1 | While a host, a member and a spectator all exist, `net` names each one's role, and the host's dumped session record holds the lifecycle marks (`party`, `clientApproved`, `clientLeft`, `readyGate`, `leaverToAI`) |
| T7 | B10 | The host is `kill -9`ed: each remaining peer (a member and the spectator) lands in its own working menu |
| T4-lobby | B20 | A new host fills a party of four and opens a card. Three press Start, then the fourth leaves: the lobby launches the three. On 2026-10-08 this caught the lobby half of B20 never working: the commit-time head-count floor kept the gate at 3/4 |

The run stops at the first failure, because each scenario needs the party the previous one left.
Every later row then reads `not run`, never `pass`.

## What a pass proves — and what it does not

**Proves:**
- The shipped C# in the party, presence-session, ready-gate, AI-handover and spectator paths makes
  the right decisions when five real processes race each other through them.
- The verdicts come from each process's own view: its Netcode role, its party state and its roster.

**Does not prove:**
- **The Unity runtime.** Amoebius re-implements the engine. NGO's internals, Unity Transport and the
  UGS SDK are stood in for.
- **UGS itself.** The stand-ins follow UGS's public REST reference, not the live service. A full
  lobby is the Lobby stand-in's 409 "lobby is full", which `UgsMultiplayerService` turns into the
  SDK's "is full" `SessionException` (`Port/docs/MULTIPLAYER.md` §6.10); the real service's bodies,
  rate limits and inactive-lobby expiry (the stand-in counts heartbeats but expires nothing) are only
  shown by the owner's live test (`Docs/MULTIPLAYER_START_HERE.md` §5.3) and MPPM (Block 2).
- **The Unity SDK.** Amoebius talks to UGS over REST with its own client; the Unity build uses the
  Multiplayer Services SDK. Same service, different client.
- **Timing.** Every pilot is on one machine, so the relay is ~0 ms away. T7's wall time is a lower
  bound, not the player's experience.
- **The disconnect callback order.** The ready gate (B20) re-counts inside the disconnect callback.
  Both engines remove the id before invoking it: the port by construction, and NGO 2.13.3 in
  `NetworkConnectionManager.OnClientDisconnectFromServer` (`ConnectedClientIds.Remove` at line
  1537, then `InvokeOnClientDisconnectCallback` at 1567, read from the package source).
- **The offline cases (T8).** These need fault injection into the session stand-in. They are L1
  tests by design: see `HARDENING_PLAN_STEAM_LAUNCH.md` §4.1.

So a green run moves a 🟡 ticket to **"passed on Amoebius, <date>"**, not to 🟢. A 🟢 still means the
owner's MPPM run.

## Why processes and not NGO's in-process harness

Block 3 named NGO 2.13.3's `NetcodeIntegrationTest` (several `NetworkManager`s in one process).
Measured on 2026-10-08, it cannot test this layer in this project, for three reasons:

1. **The helpers can't be referenced.** They compile into `Unity.Netcode.Runtime.Tests`, which is
   `autoReferenced: false` and references `Unity.Netcode.TestHelpers.Runtime`, an assembly name
   absent from the package.
2. **The project's tests can't reach the code under test from an asmdef.** They compile into
   `Assembly-CSharp-Editor`, and an asmdef cannot reference `Assembly-CSharp`.
3. **Decisively, the code under test assumes one `NetworkManager` per process.** It reads
   `NetworkManager.Singleton` at **161 sites in 54 runtime files**. In one process, every gameplay
   path talks to whichever `NetworkManager` registered last.

A process per player gives each player its own `Singleton`, and runs the code unmodified.

## Game time runs on the wall clock

A `--headless` Amoebius ticks a fixed 1/60 s as fast as the CPU allows. An idle menu therefore runs
its game clock ~200× ahead of the session service and the network links, which both run on the wall
clock. Game-time timers then fire early against them. Run 3 showed this: an invite's 60 s lifetime
lapsed before the guest had polled it, and the Accept met a withdrawn invite. (The session service
was the shared folder then; it is the Lobby stand-in now, on the same wall clock.)

Unity has no such mode. So every instance runs with the engine's `--realtime` flag
(`Port/src/CosmicShore.Engine/SceneGraph/RealtimePacer.cs`, `Port/docs/MULTIPLAYER.md` §6.1): each
frame that finishes early sleeps out the rest of its 1/60 s. A late frame is caught up, but only up to a quarter second of debt, so a long
load is not followed by a burst of compressed time. Without this, a pass can depend on how busy
the machine was.

## Touches nothing committed

`run.sh` makes a `git worktree` of HEAD and copies this checkout's `Assets/_Scripts` over it, and builds
the stand-ins from a copy of `standins/` in the scratch dir against that worktree's engine. It
fills any engine API gaps (`../prisma_edit_mode_tests/gapfill.py`) in that worktree only. Every
gap it knew of on 2026-10-08 has since landed in the engine, so it currently changes nothing. It
removes the worktree on exit. (The pacing was a worktree patch, `pace_headless.py`, until the engine
gained `--realtime`.)
