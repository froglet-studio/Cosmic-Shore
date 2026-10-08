# Multiplayer hardening plan — to a Steam launch

**Written:** 2026-10-08 · **Tree:** `Ys-bleeding-edge @ dcead7312` · **Scope:** party, presence,
invite/join/spectate, offline mode, failure & recovery, plus the testing and diagnosis needed to
trust them.

**Companion documents.** `REVIEW_INVITE_AND_RESILIENCE.md` is the root-cause analysis this plan
executes; `ROADMAP.md` is the live queue; `../PartySystem/BUGS.md` and `../PresenceSystem/BUGS.md`
are the bug registers. This file is the **ordered programme** and the **measurement strategy** —
what to do, in what order, and how to know it worked.

---

## 0. The three sentences that matter

1. **The layer is not short of fixes. It is short of runs.** 2,310 `[Test]` methods exist and
   exactly **one** play-mode test touches the party flow — and it tests an exception classifier,
   not a join. Seven party bugs sit at 🟡 *fixed, never played*. B24 was found by the owner's
   **first** MPPM session, in minutes, after weeks of reading.
2. **The one package that would diagnose all of this is already installed and has zero
   references.** `com.unity.multiplayer.tools` 2.2.12 ships the Network Profiler, the Runtime
   Network Stats Monitor and a latency/loss simulator. Nothing in `Assets/_Scripts` mentions it.
3. **Offline mode is the launch risk nobody is measuring.** It is shipped, it is the Steam
   requirement, and it has **no tests at all** — the grep for a test referencing
   `IsOfflineSession` returns nothing.

Everything below is ordered so that the cheapest things that stop a refund come first.

---

## 1. Where we are, measured

| | Measured | Reading |
|---|---|---|
| `HostConnectionService.cs` | **2,547 lines** | Still the god object after three extraction phases |
| Party-layer services extracted | 9 | `PartySessionService`, `PresenceLobbyService`, `InviteService`, `PartyMemberService`, `NetworkTransitionService`, `JoinTargetValidator`, `LobbyPropertyWriter`, `LobbyRefreshScheduler`, `SoapPartyEventBus` |
| `[Test]` + `[TestCase]` | 2,310 + 341 | A genuinely strong edit-mode suite |
| Play-mode tests on the party flow | **1** | And it asserts a classifier, not a flow |
| Offline-mode tests | **0** | |
| Party bugs / unverified | 21 entries, **7 at 🟡** | Fixed, never played |
| Presence bugs open | 3 (B1, B4, B6) | All three are symptoms of the poll rate |
| Lobby reads at rest | **≈1.3 / s / client** | Against a documented **1 / s** |
| Lobby reads boosted | **≈2.7 / s / client** | 15 s after every user action |
| `com.unity.multiplayer.tools` | installed, **0 references** | The free win |
| CI | **none** | Builds and tests are manual |

---

## 2. The scale ladder — what actually breaks, and when

This game is **4-player parties inside one 100-player presence lobby**. So "player count" means
two different things, and only one of them is a matchmaking problem.

| Concurrent users | What breaks first | Why |
|---|---|---|
| **1** | Offline mode; cold boot; the 3×15 s gate | A solo player who never invites anyone still walks the whole party bring-up. This is the majority of a Steam launch day. |
| **2–4** | Invite, join, spectate, leave, rematch, host loss | One party. This is where all 21 party bugs live. |
| **5–10** | The presence lobby's write pressure | 10 clients × 1.3 reads/s ≈ **13 reads/s** into one lobby, plus a property write per action. The 429s start here and they are *shared* — one player's spam degrades everyone's invites. |
| **10–40** | Invite discovery latency; roster churn | Every client scans **every** peer's `invite_payloads` on every tick. The scan is O(players) per tick per client, so total work is O(n²). |
| **~100** | **The design ceiling** | `PRESENCE_LOBBY` is created with max 100. At 100 CCU the lobby is full and the 101st player has no discovery at all. |
| **>100** | Needs sharding or query-based discovery | Not a tuning problem — an architecture change. Roadmap item, not launch. |

**The honest launch read.** The owner's week-one estimate is **10–40 concurrent** (decided
2026-10-08), which lands squarely in the band where **the read rate is the problem and the ceiling
is not**. So: Phase 2 is a launch blocker, and sharding is not. Plan the shard, do not build it.

---

## 3. The ordered programme

Each phase is independently shippable, has a gate, and the gate is a **run**, not a compile.

### P0 — Launch blockers. A refund comes from any one of these.

| # | Item | Why it is P0 | Gate |
|---|---|---|---|
| P0.1 | **Verify Phases 0–1 with 4 players** (the whole landed refactor: request policy, handshake deletion, pre-flight, timeout nest) | The largest body of unverified change in the layer. Everything below builds on it | `QA-NET-RATE-LIMIT-RETEST` + review §8 T1–T5, T8: 0 × 429, 0 × `ForceReset`, 0 offline fallbacks over 10 min |
| P0.2 | **Offline mode, properly tested** | Steam requirement; zero tests; a player with no connection must reach a playable game every time | `QA-NET-OFFLINE-MODE` green on a **player build**, plus the new L1 offline tests (§5.1) |
| P0.3 | **Clear the 7 🟡 party bugs** (B18–B23 + B24) | Each is a "the game is broken" report: can't leave, black screen, stranded ready gate, lost score, dead Scoreboard exit, lobby doesn't follow the host | One dated run per bug, per its own QA item |
| P0.3b | **B25 — nothing enforces the four-player party size** | 🔴 open, diagnosed 2026-10-08. Two Joins inside one refresh window on a 3/4 party seat a fifth: every check of the GAME's size (4) runs on the *joining* client against *polled* data, while the session holds the *transport* size (6) and nothing on the **host** compares the live count with `PartyDisplaySlots`. A 4-seat card then launches with five humans. At 10–40 CCU simultaneous joins are ordinary | The L1 test 2b above, plus a host-side authoritative check |
| P0.4 | **Phase 2 — push instead of poll** | This is the single highest-value change in the layer. It removes the cause of B1, B6 and B24 rather than their symptoms, and it is what makes 10+ CCU safe | GETs ≤ **4/min/client** at rest; invite visible < 1 s p95; no `LobbyPatcher` lines in a 30 min 4-client run |
| P0.5 | **Crash/exception reporting from player builds** | Without it, a Steam launch is blind. An unhandled exception in the party layer on someone's machine must reach you | One deliberate test exception from a player build appears in the dashboard |

### P1 — First-patch work. Not launch blockers; visible within a week of players.

| # | Item | Why |
|---|---|---|
| P1.1 | **Phase 3 — reconnection grace** | Today an 8-second Wi-Fi blip permanently converts your ship to AI and dumps you in a menu. Every part needed already exists and none are wired: `PartyState.Reconnecting`, `ISession.ReconnectAsync()`, a 120 s lobby retention window, `NetUgsPlayerId` as a stable slot identity, and an approval-payload token |
| P1.2 | **Host-loss resilience → true migration** | A host drop currently ends the party; the clean-reform half is done (B10). Keeping four people together through one disconnect is a retention feature |
| P1.3 | **The 90 s / 120 s timeout inversion** | `SceneLoader`'s client follow (90 s) sits *inside* NGO's scene-load timeout (120 s). Wrong way round: a lost scene load reports as a follow failure before NGO ever gives up. Tighten NGO to 60 s or raise the follow to 150 s |
| P1.4 | **One roster** | Three disagreeing views of party membership (§7 of the diagram). The flicker they produce is why `maxPartySlots` carries spare seats — a transport parameter widened to hide a reconciliation artefact *(2026-10-08: the spare seats are gone - one party size, 4, which is the session's seat count; `HasOpenSlots` counts distinct ids instead. B25.)* |
| P1.5 | **Invite scan cost** | O(n²) across the lobby. Irrelevant at 4, measurable at 40. Phase 2's event subscription removes most of it |

### P2 — Scale and polish. Only if the numbers say so.

| # | Item | Trigger |
|---|---|---|
| P2.1 | Presence lobby sharding / query-based discovery | Sustained CCU approaching 100 |
| P2.2 | Friends messaging as the invite channel | Phase 2's counters still show lobby-write pressure |
| P2.3 | Relay allocation reaping + cost telemetry | Monthly UGS bill becomes a line item |
| P2.4 | Dedicated-server or distributed-authority topology | Only if parties grow past 4–6, or cheating appears |

---

## 4. Offline mode — what "works very well" has to mean

Offline is called out separately because it is the Steam case, it is shipped untested, and its
failure mode is the worst one: **a player who paid cannot start the game.**

**The design is sound** and should not change: a plain `127.0.0.1` host, so the entire Netcode
spawn chain, AI backfill and every arcade mode run unmodified, with the party layer stood down for
the whole session. What it lacks is proof.

### 4.1 The seven offline cases that must each have a test

| Case | Today | Needed |
|---|---|---|
| Cold boot, no NIC at all | Skips the 3 attempts, goes straight offline | L1 test: the gate takes door 2 without burning 45 s |
| Cold boot, NIC up but UGS unreachable (airport wifi, firewall, UGS outage) | Walks all 3 attempts, then offline | L1 test: offline reached, `IsOfflineSession` true **before** `StartHost` |
| Player *chose* offline from the menu | Skips attempts, no notice | L1 test: no "unwanted offline" notice |
| Boot succeeded, then the network dies mid-session | `NetworkMonitor` 5 s poll → notice | L1 test: no attempt to promote the live loopback host |
| Offline, then the network returns | Stays offline for the whole session, by design | L1 test: `EnsurePartySessionAsync` no-ops; `SendInviteAsync` returns early |
| Offline → the player presses Reconnect | Full re-boot through the Authentication scene | L1 test: party layer reset, NM down, scene reloaded |
| `StartHost` itself refuses | Last-resort unbounded retry panel | L1 test: the flag is **cleared** again on failure |

### 4.2 The offline invariant worth writing into a test name

**A late online success must never tear down a live offline host.** Auth can succeed while Relay
keeps failing; if a retry landed later and called `ShutdownAsync`, it would pull the loopback host
out from under a running game. The flag is deliberately session-long for this reason, and that is
the assertion.

### 4.3 Also offline: progression

`ProgressionBackendGate.CloudEnabled` is false, so unlocks do not persist. For a Steam launch that
is a real decision, not a detail: an offline player who earns something and loses it on quit will
say the game lost their progress. Either persist locally or say so in the UI.

---

## 5. Testing strategy — four levels, and the one that is missing

The suite today is a barbell: 2,310 fast unit tests, one play-mode test, and a manual MPPM
procedure. **The missing middle is where every party bug lives.**

```
L0  edit-mode unit          2,310 tests, < 10 s        EXISTS, strong
L1  in-process multi-NM     server + N clients, 1 proc  MISSING  <-- build this first
L2  MPPM scenario           2-4 real processes          manual today, scriptable
L3  soak / load             N synthetic clients, 30 min MISSING
```

### 5.1 L1 — the in-process harness (the single biggest test win)

`PartyAcceptFlowPlayModeTests.cs` says it in its own comment: *"require two NetworkManager
instances … cannot be launched from a single PlayMode test method … when a single-process two-NM
harness exists the tests below can be expanded."*

**That harness already exists, inside NGO 2.13.3.** `Tests/Runtime/TestHelpers/` ships:

- `NetcodeIntegrationTestHelpers.Create(clientCount, out server, out clients[], targetFrameRate, serverFirst, useMockTransport)` — builds a server plus N clients in **one process**.
- `NetcodeIntegrationTest` — an abstract fixture with `NumberOfClients` as a one-line override, `StartServerAndClients`, per-client spawn waits and teardown.
- **`StartServerAndClientsWithTimeTravel`** and ~75 time-travel call sites — deterministic simulated frames instead of wall-clock waits, so the tests are fast and do not flake.
- `MockTransport` — run with no sockets at all when the test is about logic, not transport.

That is **3,918 lines of harness we do not have to write.**

> **One Editor check before relying on it.** The shipped package has no separate
> `Unity.Netcode.TestHelpers.Runtime.asmdef` — those files compile into
> `Unity.Netcode.Runtime.Tests.asmdef`, which is `autoReferenced: false`, gated on
> `UNITY_INCLUDE_TESTS`, and lists a reference to an assembly name not present in the tree. The
> standard way in is `"testables": ["com.unity.netcode.gameobjects"]` in `Packages/manifest.json`
> (the manifest declares none today) plus an asmdef reference from our test assembly. **Verify
> that compiles before planning on it.** If it does not, write the same shape ourselves against
> the public `NetworkManager` API — roughly 150 lines for a 2–4 client fixture — and keep the
> package version as the reference implementation.

**What L1 buys, that nothing else can:** every one of the 21 party bugs becomes a test that runs
in CI in seconds, with no human, no second machine, and no MPPM. Invite, accept, join-direct,
spectate, leave-mid-match, host loss, ready-gate-with-a-leaver, AI takeover, score survival — all
of them are "server + 2 clients, do a thing, assert the state on all three".

**Write these first, in this order** (each maps to a 🟡 bug, so each one closes a ticket):

1. Accept → the guest is a client, the host's roster has 2, both agree. *(T1)*
2. Two guests join-direct simultaneously, into a party with **room for both** → both seated,
   order irrelevant. *(T2, B5)*
2b. **Two guests join-direct simultaneously into a 3/4 party → the party ends at 4/4, not 5/4.**
   *(B25.)* Test 2 as first written passes on exactly the case that breaks, which is why B25 names
   it: the failure needs the party to be **exactly one short** and both joins inside one
   presence-refresh window. Both pre-flights read `3/4` and pass; both
   `JoinSessionByIdAsync` calls succeed because the session holds **6**. Assert the host's live
   member count against `PartyDisplaySlots`, not the published `partyCount`.
3. Guest leaves mid-match → vessel keeps flying under AI, score survives on the scoreboard. *(B21)*
4. Guest leaves at the ready screen → the remaining two proceed within a tick. *(B20)*
5. Double-tap Accept/Join → exactly one operation. *(T3, and the single-flight path)*
6. Spectator joins → no Player object, ready gate does not wait, `CountHumanClients` subtracts it. *(SPECTATOR §6)*
7. Host drops → each remaining client lands in its own working menu. *(B10)*
8. Offline: the seven cases in §4.1.

### 5.2 L2 — MPPM, scripted rather than remembered

MPPM stays the only test for things that need real processes and real Relay. Two corrections to how
it has been used:

- **MPPM additional instances are separate processes**, each with its own `/Library/VP/<id>/` — so
  each has its own `GameDataSO`. The claim in B23 that they "share one process and one
  `GameDataSO`" was false and has been corrected. MPPM *can* exercise replicated-state bugs.
- What clone editors genuinely share is the project's `Library` asset cache **and `PlayerPrefs`**
  (one OS user, one product name). Any state riding on either leaks between "players" on one
  machine. That — not the process model — is why three real machines remains the stronger test.

Make the §8 scenario table (T1–T8) a **checked-in script** with a pass line per row, so a run
produces a file rather than a memory.

### 5.3 L3 — soak and load, where the scale ladder gets proven

A synthetic client that signs in, joins the presence lobby, polls, and invites on a schedule —
headless, no rendering. Run **10**, then **40**, then **100** of them against the dev environment
and record §6's counters. This is the only honest way to answer "does it hold at 100?" before
players do.

Keep it cheap: it is the party layer only, no gameplay. A `-batchmode -nographics` player with a
`--synthetic-clients=N` argument, or a small console app against the UGS SDK.

### 5.4 The test runner and CI

There is **no CI**, and it is the reason 🟡 accumulates. Minimum viable, in order:

1. **A headless edit-mode run on every push.** `unity -runTests -testPlatform EditMode -testResults results.xml -batchmode`. The suite is 2,310 tests and already green, so this is pure protection.
2. **The L1 play-mode suite in the same job** once §5.1 lands (`-testPlatform PlayMode`).
3. **The existing textual gates** (`check_conditional_compilation`, `check_console_logging`, `check_using_directives`, the generator `--check`s) — they already run locally; make them a required job.
4. **A nightly player build** (IL2CPP, Windows) with `BenchmarkBuildAutoRunner` producing its JSON. The Windows IL2CPP player has its own QA item precisely because every defect in that chain was invisible in the Editor.

Self-hosted on one Windows box with a Unity licence is enough; this does not need a farm.

---

## 6. Diagnosis — the data to collect, and how

### 6.1 Turn on what is already paid for

`com.unity.multiplayer.tools` 2.2.12 is in the manifest with **zero references**. It gives three
things, all of which answer questions currently answered by guessing:

| Tool | What it answers |
|---|---|
| **Network Profiler** (Profiler window module) | Per-message, per-object bandwidth. "Which RPC is the traffic?" — today unanswerable |
| **Runtime Network Stats Monitor (RNSM)** | Live RTT, packet loss, bandwidth, as an overlay **in a player build**. Put it behind the same dev-build gate as `DiagnosticsHUD` |
| **Network Simulator** | Injects latency, jitter and packet loss from presets. **This is how §5's failure tests become deterministic** — "pull the cable at 8 s" becomes a preset, not a person |

Wiring RNSM and the simulator is a prefab + a few lines. It should happen in the same week as P0.1,
because it makes every subsequent run produce numbers.

### 6.2 Extend the console that already emits JSON

`DiagnosticsHUD` has a clean extension point — `RegisterCommand(name, handler)` — and already hosts
`prof` (Profiler hierarchy → JSON), `cells` (ecology state → JSON), `renderers`, `freeze`, `ab`,
with reports written to `Application.persistentDataPath/PerfRuns/`. **Add one command, `net`,** in
the same shape. No new system, no new window: one more handler and the same JSON sink.

### 6.3 The session record — what one JSON file should contain

One file per session, appended as events arrive, written on quit and on demand via `net dump`.
This is the artefact to attach to a bug report, and the thing to diff between runs.

```jsonc
{
  "schema": "cosmicshore.netsession.v1",
  "session":  { "id": "...", "startedUtc": "...", "build": "...", "commit": "dcead7312",
                "platform": "WindowsPlayer", "role": "host|client|spectator|offline" },
  "identity": { "ugsPlayerId": "...", "partySessionId": "...", "presenceLobbyId": "..." },

  // §6.4's counters, sampled every 10 s AND totalled at the end
  "ugs": { "reads": 0, "writes": 0, "byOperationKey": { "party:create": 1, "party:join:…": 1 },
           "failures": { "RateLimited": 0, "Transient": 0, "Conflict": 0, "Gone": 0,
                         "Full": 0, "Benign": 0, "Fatal": 0, "Cancelled": 0 },
           "retries": 0, "budgetExhaustions": 0, "singleFlightCoalesces": 0 },

  "transport": { "rttMsP50": 0, "rttMsP95": 0, "packetLossPct": 0.0,
                 "bytesInPerSec": 0, "bytesOutPerSec": 0 },   // from RNSM

  "lifecycle": [   // the timeline that makes a bug reproducible
    { "t": 0.00, "event": "signedIn" },
    { "t": 1.42, "event": "presenceJoined",  "lobbyId": "…", "players": 7 },
    { "t": 3.10, "event": "partyCreated",    "sessionId": "…", "attempt": 1 },
    { "t": 3.11, "event": "stateChange",     "from": "HostingParty", "to": "InParty" },
    { "t": 61.5, "event": "inviteSent",      "targetId": "…" },
    { "t": 62.9, "event": "inviteSeen",      "latencyMs": 1400 },
    { "t": 70.2, "event": "joinRefused",     "verdict": "PartyFull", "cost": "none" },
    { "t": 95.0, "event": "disconnect",      "reason": "TransportFailure", "wasHost": false },
    { "t": 95.4, "event": "recovery",        "path": "BounceToSoloMenu", "durationMs": 2250 }
  ],

  "verdict": { "offlineFallbackEntered": false, "forceResets": 0, "bounces": 1,
               "unhandledExceptions": 0 }
}
```

Three properties make this worth building: it is **diffable** between runs, it is **attachable** to
a QA failure, and `verdict` is directly assertable — a soak run passes or fails on four integers.

### 6.4 The counters, and the thresholds that mean "stop"

| Counter | Source | Launch threshold |
|---|---|---|
| Lobby reads / s / client, at rest | `net` counter | **≤ 0.07** (4/min) after Phase 2 |
| `RateLimited` classifications | `UgsRequestPolicy` | **0** in a 10-minute 4-client run |
| Retry-budget exhaustions | `UgsRequestPolicy` | **0** |
| `ForceReset` count | `HostConnectionService` | **0** |
| Offline fallbacks entered while genuinely online | boot gate | **0** |
| Invite visible on the recipient, p95 | lifecycle timeline | **< 1 s** |
| Join success rate, 4 concurrent | lifecycle timeline | **100%**, repeatably |
| Unhandled exceptions | crash reporter | **0** |

`UgsRequestPolicy` already counts in-flight operations and classifies every failure into eight
classes — **the counters are nearly free**; they need surfacing, not inventing.

### 6.5 Decided with the owner, 2026-10-08

| Question | Answer | What it changes |
|---|---|---|
| Crash/exception reporter | **Unity Cloud Diagnostics** | P0.5 is a package + a toggle, not an integration. Lowest friction since UGS is already wired |
| Week-one CCU | **10–40** | **Phase 2 is a genuine P0, not comfort.** Shared lobby write pressure starts in this band and one player's spam degrades everyone's invites. The 100-player ceiling is NOT a launch concern |
| CI host | **A Windows box with a Unity licence** | The headless runner in §5.4 is real work with a home, not a hypothetical |
| Start where | **Block 1 — observability** | Done in part; see §10 |

The original four asks, for the record:

1. **A crash/exception reporter choice** for player builds. Unity Cloud Diagnostics is the
   zero-friction option since UGS is already wired; Sentry and Backtrace are the better products.
   Without one, a Steam launch is blind. *This is the only item here I would call mandatory.*
2. **A CI host** — one Windows machine with a Unity licence that can run `-batchmode -runTests`.
   Self-hosted is fine. This is what stops 🟡 from accumulating again.
3. **A UGS dashboard read** — current Lobby/Relay request-rate graphs and any throttling already
   recorded on the project. It would tell us whether the 1/s limit is per-client or
   per-project-per-client in practice, which changes the 100-CCU arithmetic materially.
4. **Your real CCU expectation for week one.** "Tens" and "hundreds" imply different P0s: at tens,
   Phase 2 is comfort; at hundreds, Phase 2 plus sharding is the launch.

Not needed: a paid profiler. Unity's own Profiler plus the Multiplayer Tools module plus the JSON
above covers this system.

---

## 7. Refactors — measured, and only where the measurement justifies them

The standing instruction is short files, SOLID, real patterns. Three of the four services extracted
in Phases 9–12 are good: single responsibility, injectable, headlessly testable
(`JoinTargetValidator` is 115 lines, pure static, engine-free, and has its own test suite). The
problem is what did not move.

### 7.1 `HostConnectionService` — 2,547 lines, still the god object

It currently owns: initialisation, identity sync, presence join/converge, the `Update()` polling
loop, invite send/expire/scan, acceptance scanning, the join-target validation entry point, party
member reconciliation, party state publication, kick, leave, offline guards, error classification
routing, and the state machine's transitions. That is **eleven responsibilities** in one file.

**Extract by seam, in this order** — each step is mechanical, behaviour-neutral, and provable
without a compiler because the extracted type's inputs and outputs are explicit:

| Extract | Lines of concern | Why this seam |
|---|---|---|
| `PresenceRefreshLoop` | the `Update()` tick + the error matrix | The whole of §7's diagram. It is a **state machine over a clock**, and Phase 2 rewrites it; extracting it first makes Phase 2 a replacement rather than surgery |
| `PartyStatePublisher` | `partyCount` / `partyMax` / `matchName` / `partySession` writes | One writer, four keys, success-gated. Today the publish is interleaved with the scan that reads it |
| `PresenceScanner` | the invite / `joined_party` scans | Pure function: lobby snapshot in, events out. **Testable at L0 with no UGS at all** |
| `PartyLifecycle` | create / join / leave / reset orchestration | What is left is genuinely orchestration, and it becomes readable |

Target: **no file in the layer over ~400 lines**, and `HostConnectionService` becomes a thin
coordinator that owns no rules.

### 7.2 The patterns that actually fit here

Not pattern-collecting — these four each solve a problem the code currently has:

| Pattern | Applied to | The problem it removes |
|---|---|---|
| **Strategy** | `IPresenceTransport` with a polling and an event-driven implementation | Phase 2 becomes a swap with both paths testable, instead of a rewrite with a flag |
| **State** | `PartyStateMachine` (already there) extended to own `Reconnecting` properly | Phase 3's grace period has nowhere to live today; the state exists and nothing drives it |
| **Policy / Strategy object** | `UgsRequestPolicy` (already there) | Done, and it is the model for the rest: one object owns one cross-cutting decision |
| **Observer via SOAP** | `SoapPartyEventBus` (already there) | Done — one `Raise` per event instead of a dozen scattered null-guarded calls |

Explicitly **not** wanted: an event aggregator over the top of SOAP, a generic repository over
`ISession`, or a mediator. Each would add a layer whose only job is indirection.

### 7.3 The SOLID violations worth naming

- **SRP** — §7.1. The 2,547-line file.
- **DIP** — `HostConnectionService.Instance` is reached by **34 call sites across 10 files**, six of
  them production code *outside* the party layer (the boot gate, `OfflineModeService`,
  `ReconnectService`). That is the real blocker on testing the layer in isolation, and it is
  `TODOS.md` TODO-1, re-scoped after measurement from "one test uses it" to a ten-file migration.
- **OCP** — a new presence transport cannot be added without editing the refresh loop. §7.2's
  Strategy fixes exactly this.
- **ISP** — fine. The extracted interfaces are already narrow.
- **LSP** — not relevant here.

### 7.4 The rule that keeps this from regressing

**One cross-cutting decision, one owner.** B24 was four copies of one predicate, and the three
written sloppily were the three that worked. `UgsRequestPolicy` is now that owner for retries, and
since 2026-10-08 the rule is **mechanical**: `Tools/Build/check_ugs_request_discipline.py` fails on
a UGS call that bypasses the executor and on any private failure classifier outside
`UgsRequestPolicy.cs`. That gate caught a real escape — the converge join bypassed the executor
for a day after Phase 1 claimed "every UGS call", and only a merge re-check found it. Add it to CI
in Block 3.

The same question should be asked of every other rule that appears more than once — who owns "is
this session gone", "is this player in our party", "may this player be invited" — and each answer
should get a gate like this one, because a convention nothing checks drifts back.

---

## 8. Sequencing — the next six working blocks

| Block | Content | Gate before moving on |
|---|---|---|
| **1** | Wire RNSM + Network Simulator + the `net` command and its JSON | A session file appears with real counters |
| **2** | Verify Phases 0–1 with 4 players; clear B24 | §6.4 thresholds met in a 10-minute run |
| **3** | Stand up L1 (the in-process harness) + the first 8 tests | The 8 tests run in CI in under a minute |
| **4** | Offline mode: §4.1's seven cases as L1 tests + a player-build pass | Offline is green on a build, not just in the Editor |
| **5** | Phase 2 — push instead of poll, behind §7.2's Strategy | GETs ≤ 4/min/client; B1/B6 silent for 30 minutes |
| **6** | Phase 3 — reconnection grace | 15 s network pull mid-match returns the vessel; 60 s makes AI permanent with a correct roster |

`HostConnectionService`'s decomposition (§7.1) runs **alongside** blocks 3–5, one extraction per
block, because each one makes the next test easier to write.

---

## 9. What success looks like

Not "no bugs" — this layer will always have some. The measurable version:

- Every bug in `BUGS.md` is 🟢-with-a-dated-run or 🔴-with-a-reproduction. **No 🟡.**
- A party bug arrives as a **failing L1 test** before it is fixed.
- A 30-minute 4-client soak produces a JSON whose `verdict` block is four zeros.
- Offline mode has a player-build pass recorded for all seven cases in §4.1.
- CI is red when any of that stops being true.

---

---

## 10. Landed so far

**Block 1, partly done (2026-10-08).** Measuring first shrank it: two of its three parts already
existed.

| Part | State |
|---|---|
| **The counters** | **Already done before this plan was written.** `UgsRequestTelemetry` carries 14 counters — `Requests`, `LobbyReads`, `Retries`, `Coalesced`, `BudgetExhausted`, the eight failure classes, `PresenceForceReset`, `OfflineFallback` — with `InLastMinute`, `Total`, `Describe` and `Reset`, instrumented at the real call sites in `UgsRequestPolicy`, `LobbyPropertyWriter`, `PresenceLobbyService`, `PartySessionService` and `HostConnectionService`. Nothing to build |
| **The session record + timeline + `net` command** | **Landed.** `NetSessionRecord` (the schema as a DTO), `NetSessionRecorder` (the timeline and the JSON writer), `NetSessionConsoleCommand` (`net`, `net dump`, `net reset`, `net mark <text>`), plus `NetSessionRecorderTests` |
| **RNSM + Network Simulator** | **Not done, and deliberately not written blind.** `RuntimeNetStatsMonitor` is a `MonoBehaviour` in an `autoReferenced` assembly, so it needs no asmdef edit and can be added at runtime — but it displays nothing without an authored `NetStatsMonitorConfiguration`, and the simulator needs a preset asset. Writing C# that expects unauthored assets is the "ship a placeholder" trap, so both are listed as Editor tasks below |

**What the recorder deliberately does not do:** count anything. `UgsRequestTelemetry` owns every
counter; a second counter here would be a second place to forget, and the two would disagree the
first time somebody added a call site. The recorder reads that owner and adds the one thing it has
no opinion about — **when** things happened, in order. `net reset` resets both owners, because a
measurement that starts with yesterday's counters and today's timeline is worse than none.

**Proof.** The three shipped source files were compiled and **run** against engine shims (the real
`UgsRequestTelemetry.cs` included, unmodified; the `UgsFailureClass` shim's member values checked
against the real enum). 12 checks pass, including the two that are easy to get wrong: a record
built at zero duration reports `lobbyReadsPerSecond` as **0, not Infinity** — an Infinity
serialises and then poisons every later average — and an offline fallback counts toward the verdict
**only when the device was online**, because a player with no wifi going offline is correct
behaviour and B24's shape is the other one. All gates green, all four files parse clean.
**`/verify-unity` did NOT run — no Unity in the authoring container.**

### The Editor tasks this leaves

1. Author a `NetStatsMonitorConfiguration` (RTT, packet loss, bandwidth in/out) and attach
   `RuntimeNetStatsMonitor` behind the same dev gate as `DiagnosticsHUD`.
2. Author a `NetworkSimulatorPreset` set and wire the simulator — this is what makes §5's failure
   tests deterministic.
3. Assign the recorder's providers once, wherever role and offline state are known
   (`MultiplayerSetup` / `OfflineModeService`): `RoleProvider`, `OfflineProvider`,
   `DeviceOnlineProvider`, and call `MarkOfflineFallback(deviceWasOnline)` from the one place that
   sets `IsOfflineSession`.
4. Add `Mark(...)` calls at the lifecycle points in §6.3's example timeline. Until then the record
   carries counters and a verdict but a thin timeline.

*Measured, not asserted: file sizes from `wc -l`; test counts from `grep -c '\[Test\]'`; the
harness API from `com.unity.netcode.gameobjects@2.13.3/Tests/Runtime/TestHelpers/`; the read-rate
arithmetic from `REVIEW_INVITE_AND_RESILIENCE.md` §3.1; the unused-package finding from a
zero-result grep for `Unity.Multiplayer.Tools` across `Assets/_Scripts`.*
