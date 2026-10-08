# Prompt — harden the multiplayer layer to a Steam launch

Paste everything below into a fresh session. The full reasoning, measurements and thresholds live
in `Docs/MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md`; this is the executable brief.

---

You are taking over the party / presence / offline / recovery layer and driving it to a state where
it can carry a Steam launch. Work the blocks in order. **Do not skip to a later block because an
earlier one needs a human run** — report the block as blocked and say exactly what run is needed.

## The premise, measured on `Ys-bleeding-edge @ dcead7312`

| Fact | Number | Consequence |
|---|---|---|
| `[Test]` + `[TestCase]` methods | 2,310 + 341 | The edit-mode suite is genuinely strong |
| Play-mode tests on the party flow | **1** | And it asserts an exception classifier, not a join |
| Offline-mode tests | **0** | Offline is the Steam requirement and is unproven |
| Party bugs at 🟡 *fixed, never played* | **7** (B18–B24) | Each is a "game is broken" report |
| Lobby reads at rest | **≈1.3/s/client** vs a **1/s** limit | Cause of B1, B6 and B24 |
| `com.unity.multiplayer.tools` 2.2.12 | installed, **0 C# references** | The diagnosis tooling is already paid for and switched off |
| `HostConnectionService.cs` | **2,547 lines**, ~11 responsibilities | The remaining god object |
| `HostConnectionService.Instance` call sites | **34 across 10 files**, 6 outside the party layer | The real blocker on isolating the layer |
| CI | **none** | Why 🟡 accumulates |

**The one-line diagnosis: this layer is not short of fixes, it is short of runs.** B24 was found by
the owner's first MPPM session in minutes, after weeks of reading. Treat a run as the deliverable
and a compile as a precondition.

## Standing constraints

- **`/verify-unity` gates every C# commit.** If the CLI is unavailable, say so explicitly in the
  commit and file it in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. **Never claim a verification that
  did not run.**
- **No file over ~400 lines** in this layer. If an extraction would push one past that, split the
  extraction, not the rule.
- **One cross-cutting decision, one owner.** B24 was four copies of one predicate and the three
  written sloppily were the three that worked. Before adding a rule, grep for whether it already
  exists somewhere else.
- The two **LOCKED** decisions are given and not up for discussion: the two-level
  presence-lobby + party-session model, and **eager per-user Relay** ("Always-InParty"). Every
  change fits inside them.
- Preserve the invariants listed in `ROADMAP.md` § "Genuinely strong": single-writer SOAP,
  `.AsMainThread()` at every UGS/Netcode await, `PartyStateMachine` as the only lifecycle
  authority, `NetworkSceneObjectGuard.Sweep` before every join, the 6-transport / 4-displayed slot
  split, every catch mapping to a named recovery.
- Branch and push per the repo's git rules. Do not open a PR unless asked.

---

## Block 1 — make the system observable (do this first; everything after it produces numbers)

1. **Wire `com.unity.multiplayer.tools`.** It is installed and unreferenced.
   - **Runtime Network Stats Monitor** behind the same dev-build gate as `DiagnosticsHUD` — live
     RTT, packet loss, bandwidth in a player build.
   - **Network Simulator** with latency/jitter/loss presets. This is what turns "pull the cable at
     8 seconds" from a person into a test fixture.
   - The **Network Profiler** module needs no code — confirm it reports and note which RPCs
     dominate.
2. **Add a `net` command to `DiagnosticsHUD`.** Use the existing extension point,
   `DiagnosticsHUD.RegisterCommand(name, handler)` — the same one `prof`, `cells`, `renderers`,
   `freeze` and `ab` already use. Write to the existing sink,
   `Application.persistentDataPath/PerfRuns/`. Do not build a new window or a new system.
3. **Emit the session JSON** in `HARDENING_PLAN_STEAM_LAUNCH.md` §6.3
   (`schema: cosmicshore.netsession.v1`): identity, UGS counters by operation key and failure
   class, retries, budget exhaustions, transport stats from RNSM, a timestamped `lifecycle` array,
   and a `verdict` block. `UgsRequestPolicy` already classifies every failure into eight classes
   and tracks in-flight operations — **surface those counters, do not invent new ones.**

**Gate:** a session file exists on disk with real, non-zero counters from a 4-client run.

## Block 2 — verify what already landed

Phases 0–1 of the review (one classifier, the request executor, the PENDING handshake deletion, the
pre-flight validator, the corrected timeout nest) are the largest body of unverified change in the
layer. They compile and their unit tests pass headlessly. **No invite has been observed working.**

Run `QA-NET-RATE-LIMIT-RETEST` (top of P1 in `Docs/QA/QA_BACKLOG.md`) and review §8 T1–T5 and T8.

**Gate — all of these, over a 10-minute 4-client run:** 0 × `RateLimited`, 0 retry-budget
exhaustions, 0 × `ForceReset`, 0 offline fallbacks entered while genuinely online, 0 unhandled
exceptions. Record the JSON. Any failure becomes a bug entry with the JSON attached.

## Block 3 — build the missing test level (the single biggest win)

`PartyAcceptFlowPlayModeTests.cs` says it in its own comment: the real tests *"require two
NetworkManager instances … cannot be launched from a single PlayMode test method … when a
single-process two-NM harness exists the tests below can be expanded."*

**That harness ships inside NGO 2.13.3**, in `Tests/Runtime/TestHelpers/`:
`NetcodeIntegrationTestHelpers.Create(clientCount, out server, out clients[], …, useMockTransport)`,
the `NetcodeIntegrationTest` fixture with `NumberOfClients` as a one-line override,
`StartServerAndClientsWithTimeTravel` for deterministic simulated frames (no wall-clock waits, so
no flake), and `MockTransport` for logic-only tests. **3,918 lines we do not write.**

> **Verify the wiring before relying on it.** There is no separate
> `Unity.Netcode.TestHelpers.Runtime.asmdef` in the shipped package; those files compile into
> `Unity.Netcode.Runtime.Tests.asmdef`, which is `autoReferenced: false`, gated on
> `UNITY_INCLUDE_TESTS`, and references an assembly name absent from the tree. The standard route
> is `"testables": ["com.unity.netcode.gameobjects"]` in `Packages/manifest.json` (none declared
> today) plus an asmdef reference. **If that does not compile, write the same shape against the
> public `NetworkManager` API — about 150 lines for a 2–4 client fixture — and keep the package
> version as the reference implementation.** Report which route you took.

Then write these eight, in order. Each one closes a 🟡 ticket, so each is a test *and* a bug fix
verification:

1. Accept → guest is a client, host roster has 2, both agree. *(T1)*
2. Two guests join-direct simultaneously → both seated, order irrelevant. *(T2, B5)*
3. Guest leaves mid-match → vessel keeps flying under AI, score survives on the scoreboard. *(B21)*
4. Guest leaves at the ready screen → remaining two proceed within a tick. *(B20)*
5. Double-tap Accept/Join → exactly one operation reaches UGS (exercises single-flight). *(T3)*
6. Spectator joins → no Player object, ready gate does not wait, `CountHumanClients` subtracts it. *(SPECTATOR §6)*
7. Host drops → each remaining client lands in its own working menu. *(B10)*
8. The seven offline cases in `HARDENING_PLAN_STEAM_LAUNCH.md` §4.1.

**Gate:** the eight run headlessly in under a minute. Then make them a required CI job —
`unity -runTests -testPlatform PlayMode -batchmode -testResults results.xml`. There is no CI today;
a single self-hosted Windows box with a licence is enough.

## Block 4 — offline mode, to launch grade

Offline is the Steam case, it is shipped, and it has **zero tests**. The design is right — a plain
`127.0.0.1` host so the whole spawn chain and AI backfill run unmodified, party layer stood down
for the session — so this block is proof, not redesign.

Cover all seven cases in §4.1. The invariant worth a test name of its own: **a late online success
must never tear down a live offline host** — auth can succeed while Relay keeps failing, and a late
retry calling `ShutdownAsync` would pull the loopback host out from under a running game. That is
why the flag is session-long.

Also decide, and say so in the UI either way: `ProgressionBackendGate.CloudEnabled` is false, so an
offline player's unlocks **do not persist across a quit**. A player who earns something and loses it
will report that the game lost their progress.

**Gate:** `QA-NET-OFFLINE-MODE` green on a **Windows IL2CPP player build**, not just the Editor —
every defect in that build chain was historically invisible in the Editor.

## Block 5 — Phase 2: push instead of poll

The highest-value change in the layer, because it removes the *cause* of B1, B6 and B24 rather than
their symptoms. The Sessions SDK already opens a Wire subscription per lobby and raises
`PlayerPropertiesChanged`, `PlayerJoined`, `PlayerLeft`, `SessionPropertiesChanged`, `Deleted`,
`RemovedFromSession`, `StateChanged`. We subscribe to **two** and poll instead.

Do it behind a **Strategy**: `IPresenceTransport` with a polling implementation and an
event-driven one, both testable at L1. That makes it a swap, not a rewrite, and keeps a rollback.

Delete, do not disable: the per-tick refreshes, the boost window, the 4-second converge, and the
pre/post-save refreshes. Keep **one** reconciliation poll at 20 s as a safety net.

**Gate:** GETs ≤ **4/min/client** at rest; invite visible on the recipient **< 1 s p95**; no
`LobbyPatcher` or stale-index lines in a 30-minute 4-client run.

## Block 6 — Phase 3: reconnection grace

Today an 8-second Wi-Fi blip permanently converts your vessel to AI and drops you in a solo menu.
**Every part needed already exists and none are wired:** `PartyState.Reconnecting`,
`ISession.ReconnectAsync()`, `MultiplayerService.ReconnectToSessionAsync(id)`, the Lobby service's
120-second retention of a disconnected player, `Player.NetUgsPlayerId` as a stable slot identity,
and a connection-approval payload that already carries a token (the spectator one).

Drive it through the existing state machine — `Reconnecting` is the state with nothing driving it.
Make the AI takeover **reversible** within the grace window.

**Gate:** pull a client's network for 15 s mid-match → the vessel is handed back and the player
never sees a menu. 60 s → AI becomes permanent and the roster is correct. Pre-match 20 s → the slot
is kept.

---

## Running alongside blocks 3–5 — decompose the god object

`HostConnectionService.cs` is 2,547 lines carrying eleven responsibilities. Extract **one seam per
block**, each mechanical and behaviour-neutral:

| Order | Extract | Why this seam first |
|---|---|---|
| 1 | `PresenceRefreshLoop` — the `Update()` tick + the error matrix | Block 5 replaces it; extracting first makes Phase 2 a swap instead of surgery |
| 2 | `PartyStatePublisher` — the four published keys, success-gated | Today the publish is interleaved with the scan that reads it |
| 3 | `PresenceScanner` — the invite and `joined_party` scans | Pure function: snapshot in, events out. Testable at L0 with no UGS at all |
| 4 | `PartyLifecycle` — create / join / leave / reset orchestration | What remains is genuine orchestration, and becomes readable |

Then migrate `HostConnectionService.Instance` (34 sites, 10 files) to injection —
`TODOS.md` TODO-1, already re-scoped after measurement. Six of those sites are production code
outside the party layer (the boot gate, `OfflineModeService`, `ReconnectService`), so this is a
real migration, not a cleanup.

**Patterns that fit, and nothing more:** Strategy for the presence transport, State for the party
lifecycle, a policy object for cross-cutting decisions (`UgsRequestPolicy` is the model), Observer
through SOAP (`SoapPartyEventBus` already does this). **Explicitly not wanted:** an event
aggregator over SOAP, a generic repository over `ISession`, or a mediator — each adds a layer whose
only job is indirection.

---

## Scale — what to worry about when

This game is **4-player parties inside one 100-player presence lobby**, so most "player count"
risk is lobby write pressure, not matchmaking.

| CCU | First thing to break |
|---|---|
| 1 | Offline mode, cold boot, the 3×15 s gate — the majority of launch day |
| 2–4 | Invite, join, spectate, leave, host loss — where all 21 party bugs live |
| 5–10 | Shared lobby write pressure; 429s start, and one player's spam degrades everyone's invites |
| 10–40 | Invite discovery is O(n²): every client scans every peer's payloads every tick |
| ~100 | **The design ceiling** — `PRESENCE_LOBBY` is created with max 100 |
| >100 | Sharding / query-based discovery. Plan it, do not build it |

**If week one is tens of concurrent players, the ceiling is not the problem — the read rate is.**

## Already decided with the owner (2026-10-08) — do not re-ask

| Question | Answer |
|---|---|
| Crash/exception reporter | **Unity Cloud Diagnostics** — package + toggle, since UGS is wired |
| Week-one CCU | **10–40**, so **Block 5 (push instead of poll) is a launch blocker**, and sharding is not |
| CI host | **A Windows box with a Unity licence** is available — build the headless runner for real |
| Where to start | **Block 1**, which is partly landed — see "Block 1 status" below |

### Block 1 status

The counters already existed (`UgsRequestTelemetry`, 14 of them, instrumented at the real call
sites), so Block 1 shrank on measurement. **Landed:** `NetSessionRecord`, `NetSessionRecorder`,
`NetSessionConsoleCommand` (`net` / `net dump` / `net reset` / `net mark`) and
`NetSessionRecorderTests` — compiled and run against shims, 12 checks green, `/verify-unity` not
run. **Still to do, in the Editor, because they need authored assets:** the
`NetStatsMonitorConfiguration` + `RuntimeNetStatsMonitor` attach, the `NetworkSimulatorPreset` set,
assigning the recorder's three providers, and adding `Mark(...)` at the lifecycle points. Do not
write the RNSM wiring blind — the component displays nothing without its configuration asset.

## The original asks, for the record

1. **A crash/exception reporter** for player builds (Unity Cloud Diagnostics is lowest friction
   since UGS is wired; Sentry/Backtrace are better products). *The only item here I would call
   mandatory* — without it a Steam launch is blind.
2. **A CI host** — one Windows box with a Unity licence that can run `-batchmode -runTests`.
3. **A UGS dashboard read** — Lobby/Relay request-rate graphs and any recorded throttling. It
   settles whether the 1/s limit bites per-client or per-project, which changes the 100-CCU maths.
4. **The real week-one CCU expectation.** "Tens" makes Block 5 comfort; "hundreds" makes Block 5
   plus sharding the launch.

A paid profiler is **not** needed. Unity's Profiler, the Multiplayer Tools module and the session
JSON cover this system.

## Definition of done for the whole programme

- Every entry in `BUGS.md` is 🟢-with-a-dated-run or 🔴-with-a-reproduction. **No 🟡.**
- A new party bug arrives as a **failing L1 test** before it is fixed.
- A 30-minute 4-client soak produces a session JSON whose `verdict` block is four zeros.
- Offline mode has a player-build pass recorded for all seven cases.
- CI goes red when any of the above stops being true.
