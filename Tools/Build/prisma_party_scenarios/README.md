# prisma_party_scenarios — the party layer's Block 3 scenarios, five players, no Editor

```
bash Tools/Build/prisma_party_scenarios/run.sh          # ~10-15 min wall; exit 0 = every scenario passed
KEEP=1 bash Tools/Build/prisma_party_scenarios/run.sh   # keep the worktree, the logs and results.json
```

Needs the .NET 10 SDK (`DOTNET10_ROOT`, default `$HOME/.dotnet10`), like
`../prisma_edit_mode_tests/run.sh`. The scratch dir is `$PRISMA_PARTY_WORK` (default
`$TMPDIR/prisma_party_scenarios`): `logs/<A..E>.log` is each pilot's full console, and
`results.json` is one row per scenario with the state it asserted on.

## What it does

It builds Prisma's player (`Port/`) against **this checkout's** `Assets/_Scripts` and launches five
instances, `PilotA`…`PilotE`. Each instance runs as its own process with its own `NetworkManager`,
its own save folder and its own control port. Netcode's model runs over TCP, and UGS Lobby + Relay
are a shared session directory. Then `scenarios.py` plays one continuous session through
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
| T5-accept | single-flight | Double-tapped Accept starts one accept flow |
| **T2b** | **B25** | Two Joins on a 3/4 party, Run pressed on both consoles behind a barrier (skew < 1 ms): the host ends at **4/4**, one joiner is seated, the other is back in its own menu with "That party is full." `refused_by` says whether the loser passed the pre-flight, which means the session's 4 seats refused it — the race B25 is about — or was stopped by the pre-flight |
| kick / leave | — | The member lands in its own working menu, and the host recounts |
| T2 | B5 / T2 | Two Joins at once with room for both: both are seated |
| T5-join | single-flight | Double-tapped Join starts one direct join, and the controller ignores the second |
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
- **The Unity runtime.** Prisma re-implements the engine. NGO's internals, Unity Transport and the
  UGS SDK are stood in for.
- **UGS error shapes.** UGS's error for a full lobby, or for a 429, is not reproduced byte for
  byte. The stand-in throws `SessionException("Session is full.")`, which reaches
  `UgsRequestPolicy`'s message floor. If the real SDK's shape differs, only MPPM (Block 2) would
  show it.
- **Timing.** TCP sees a dead peer at once, but UTP waits for its disconnect timeout (10 s here).
  T7's wall time is therefore a lower bound, not the player's experience.
- **The disconnect callback order.** The ready gate (B20) re-counts inside the disconnect callback.
  Both engines remove the id before invoking it: the port by construction, and NGO 2.13.3 in
  `NetworkConnectionManager.OnClientDisconnectFromServer` (`ConnectedClientIds.Remove` at line
  1537, then `InvokeOnClientDisconnectCallback` at 1567, read from the package source).
- **The offline cases (T8).** These need fault injection into the session stand-in. They are L1
  tests by design: see `HARDENING_PLAN_STEAM_LAUNCH.md` §4.1.

So a green run moves a 🟡 ticket to **"passed on Prisma, <date>"**, not to 🟢. A 🟢 still means the
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

A `--headless` Prisma ticks a fixed 1/60 s as fast as the CPU allows. An idle menu therefore runs
its game clock ~200× ahead of the session directory and the TCP links, which both run on the wall
clock. Game-time timers then fire early against them. Run 3 showed this: an invite's 60 s lifetime
lapsed before the guest had polled it, and the Accept met a withdrawn invite.

Unity has no such mode. So `pace_headless.py` makes each frame that finishes early sleep out the
rest of its 1/60 s. A late frame is caught up, but only up to a quarter second of debt, so a long
load is not followed by a burst of compressed time. Without this, a pass can depend on how busy
the machine was.

## Touches nothing committed

`run.sh` makes a `git worktree` of HEAD and copies this checkout's `Assets/_Scripts` over it. It
fills the engine's API gaps (`../prisma_edit_mode_tests/gapfill.py`) and applies the pacing
(`pace_headless.py`) in that worktree only, per Port/CLAUDE.md (the engine changes only in a port
session). It removes the worktree on exit.
