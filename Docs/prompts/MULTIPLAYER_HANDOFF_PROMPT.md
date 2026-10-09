# Prompt — continue the multiplayer hardening work

Paste everything below into a fresh session. Written 2026-10-08, handing off from the session that
produced the hardening programme and landed Block 1's code half.

---

Continue the multiplayer hardening work on Cosmic Shore. Branch **`Ys-bleeding-edge`** (push only
there; no PR unless asked).

## Your working document

**`Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md` is the brief. Work its blocks, in order.** It
carries the measured premise, the standing constraints, Blocks 1–6, the god-object decomposition
that runs alongside them, and the decisions already made with the owner. Read it first and in full.

**`Docs/MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md` is reference only.**

> ⚠ **Ignore its Steam-launch framing.** We are **not** doing launch prep yet — no launch date, no
> P0/P1/P2 launch triage, no "refund" reasoning, no crash-reporter integration, no CI build
> pipeline, unless the owner asks for it later. The file is named for a launch because that was the
> framing the day it was written; what is still useful in it is the **technical** content:
>
> - **§5** the four test levels, and why the in-process harness is the missing one
> - **§6.3** the session-JSON schema, and **§6.4** the counter thresholds
> - **§7** the measured refactor list (file sizes, the 34 `Instance` call sites, which patterns fit
>   and which three are explicitly rejected)
> - **§10** what landed, and the Editor tasks it left
>
> Take the engineering, leave the launch programme. If the two documents disagree, the prompt doc
> wins.

Other records, for when you need the detail rather than the plan:

| Where | What |
|---|---|
| `Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md` | The root-cause review the whole programme executes. §3 is the three structural faults; §7 is the four phases; Appendix C is every timeout by layer |
| `Docs/PartySystem/BUGS.md` | **B2–B25.** The register. Anything 🟡 is fixed-but-never-played |
| `Docs/PresenceSystem/BUGS.md` | B1, B4, B6 — all three are symptoms of the poll rate |
| `Docs/QA/QA_BACKLOG.md` | The `QA-NET-*` items, human-runnable, pass/fail per step |
| `Docs/UNITY_VERIFICATION_CHECKLIST.md` | Everything that compiled but was never opened in the Editor |
| `Docs/PartySystem/ARCHITECTURE.md` | The locked design, and the current retry/classifier table |

A rebuilt flow diagram of the whole layer (two sessions, three doors, the request policy, the
refresh tick, the recovery paths) is at <https://claude.ai/artifact/28FsHDcaHpfKwEYFu4KqK1>.
**One point in it is now stale:** it shows the party session as **6 transport slots / 4 displayed**
and calls that split an invariant. B25's fix collapsed it to **one size, 4**, enforced by the
session itself. Fix that figure and its legend when you next touch the diagram.

## Where the work actually stands

**Landed and verified by a real test run:**

- **Block 3, by the multi-process route (2026-10-08).** `Tools/Build/prisma_party_scenarios/run.sh`
  runs five game processes on Prisma and **14/14** scenarios pass. Covered: T1, T2, T2b (B25),
  T3 (B21), T4 (B20 match + lobby), T5, T6, T7 (B10), kick, leave and the session record.
  - NGO's in-process harness cannot reach this layer. The brief's Block 3 status says why
    (161 `NetworkManager.Singleton` sites).
  - The runs found two real defects, both fixed: B20's lobby half had never worked, and B25's
    loser logged a red error.
  - **Prisma is not Unity:** the tickets read "passed on Prisma" and stay 🟡 until MPPM.
- **Block 1's code remainder.** The recorder's providers and the lifecycle marks are in. On the
  five-process run, `net` names host, client and spectator and the record holds the marks.

- **Block 1's code half.** `NetSessionRecord` (the JSON schema as a DTO), `NetSessionRecorder` (the
  lifecycle timeline + the writer), `NetSessionConsoleCommand` (`net` / `net dump` / `net reset` /
  `net mark <text>`), `NetSessionRecorderTests` — **11 pass / 0 fail** in the project's own headless
  harness. The recorder deliberately **counts nothing**: `UgsRequestTelemetry` already owns 14
  counters instrumented at the real call sites, and two owners of one piece of state is the exact
  shape of bug B24. `net reset` resets both owners.

**Landed, compiles, and has never been run with four players — this is the real backlog:**

- **Review Phases 0–1:** one failure classifier (`UgsRequestPolicy`), the request executor
  everywhere, the PENDING acceptance handshake deleted, `JoinTargetValidator`'s pre-flight before
  teardown, the corrected UTP timeout nest.
- **B24** — a rate-limited (429) session call matched neither retry filter, so the guest bounced to
  solo and the host fell back to an offline session. Fixed; 🟡.
- **B25** — nothing enforced the four-player size; two simultaneous Joins on a 3/4 party seated a
  fifth. Fixed by collapsing to **one** party size (`maxPartySlots = 4`, enforced by the session
  itself); 🟡.
- **B18–B23** — can't leave a match, lost scene transition = black screen, a leaver strands the
  ready gate, a leaver takes their score out, Scoreboard exit unwired, arcade lobby doesn't follow
  the host. All 🟡.

- **Block 4's code half (2026-10-08).** The seven offline cases are L1 tests (`OfflineSessionTests`,
  15/15), and B26–B28 are fixed with negative controls. Its real gate, a Windows IL2CPP player
  pass, is the owner's.

**Not started:** Blocks 2, 5, 6, and the `HostConnectionService` decomposition.

## What to do next

0. **Block 5 — push instead of poll** is next in the brief's order (Block 4's code half is done).
   Re-run `prisma_party_scenarios` after anything touching the party layer: it takes ~15 minutes
   and is the only multi-player check that runs without the owner.
1. *(Done 2026-10-08 by a different route - kept for the record.)* **Block 3 — the in-process
   multi-NetworkManager harness.** This is the highest-value thing you
   can do without the owner at a keyboard, and it converts the whole 🟡 pile into CI tests.
   `PartyAcceptFlowPlayModeTests.cs` says in its own comment that it needs two NetworkManagers in
   one process and cannot do it. **NGO 2.13.3 already ships that harness** in
   `Tests/Runtime/TestHelpers/`: `NetcodeIntegrationTestHelpers.Create(clientCount, out server, out
   clients[], …, useMockTransport)`, the `NetcodeIntegrationTest` fixture with `NumberOfClients` as
   a one-line override, `StartServerAndClientsWithTimeTravel` for deterministic frames, and
   `MockTransport`. That is ~3,900 lines you do not write.
   **Verify the wiring before relying on it:** the shipped package has no separate
   `Unity.Netcode.TestHelpers.Runtime.asmdef` — those files compile into
   `Unity.Netcode.Runtime.Tests.asmdef`, which is `autoReferenced: false`, gated on
   `UNITY_INCLUDE_TESTS`, and references an assembly name absent from the tree. The route in is
   `"testables": ["com.unity.netcode.gameobjects"]` in `Packages/manifest.json` (none declared
   today) plus an asmdef reference. If that does not compile, write the same shape against the
   public `NetworkManager` API (~150 lines for a 2–4 client fixture) and say which route you took.
   The eight tests to write, in order, are listed in the prompt doc's Block 3 — each one closes a
   🟡 ticket.
2. **Block 1's remainder needs the owner in the Editor**, because both pieces need authored assets
   and writing C# that expects unauthored assets is the placeholder trap: a
   `NetStatsMonitorConfiguration` + the `RuntimeNetStatsMonitor` attach, and a
   `NetworkSimulatorPreset` set. Also still open and cheap: assign the recorder's three providers
   (`RoleProvider`, `OfflineProvider`, `DeviceOnlineProvider`) where role and offline state are
   known, call `MarkOfflineFallback(deviceWasOnline)` from the one place that sets
   `IsOfflineSession`, and add `Mark(...)` at the lifecycle points. Until that last one, the record
   carries counters and a verdict but a thin timeline.
3. **Block 2 is the owner's time, not yours** — the 4-player MPPM run on Phases 0–1 plus B24/B25.
   You cannot run it. Ask for it when you need it; do not block on it.

## Tooling facts — do not rediscover these

- **`com.unity.multiplayer.tools` 2.2.12 is installed and has zero references in
  `Assets/_Scripts`.** It ships the Network Profiler, the Runtime Network Stats Monitor and a
  latency/jitter/loss simulator. `RuntimeNetStatsMonitor` is a `MonoBehaviour` in an
  `autoReferenced` assembly, so no asmdef edit is needed and it can be added at runtime.
- **Edit-mode tests run headlessly, with no Editor:**
  `bash Tools/Build/prisma_edit_mode_tests/run.sh [path/to/XTests.cs …]`, suites listed in that
  folder's `suites.txt`. Needs .NET 10 — install to `$HOME/.dotnet10` and pass
  `DOTNET10_ROOT=$HOME/.dotnet10`. **Register any new suite in `suites.txt` and actually run it.**
- **`bash Tools/Build/prisma_party_scenarios/run.sh`** runs five game processes through 14 party
  scenarios (~15 min, needs .NET 10). Each scenario is driven through the `party` console command.
  It writes `results.json`, and its README says what a pass does not prove. Add a scenario there
  before a party fix, not after.
- **`python3 Tools/Build/check_ugs_request_discipline.py`** fails on a UGS call that bypasses
  `UgsRequestPolicy.ExecuteAsync` and on any private failure classifier outside
  `UgsRequestPolicy.cs`. It has already caught a real escape. Run it with the other gates.
- **A real compiler is available here.** `dotnet` installs fine; the earlier sessions proved
  predicates and maths by compiling the shipped sources against small engine shims and **running**
  them. Prefer that over inspection — `/verify-unity` is unavailable in a cloud container, so this
  is how you get evidence.
- Package sources are worth reading directly rather than guessing from changelogs: download with
  `curl -sSL https://packages.unity.com/<pkg>/-/<pkg>-<version>.tgz`.

## Standing constraints (the prompt doc repeats these; they are not optional)

- **`/verify-unity` gates every C# commit.** No Unity in a cloud container, so say so explicitly in
  the commit and file the change in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. **Never claim a
  verification that did not run.**
- **No file in this layer over ~400 lines.** `HostConnectionService` is the standing violation at
  ~2,500 lines with eleven responsibilities; the four extractions are named in the prompt doc.
- **One cross-cutting decision, one owner.** B24 was four copies of one predicate and the three
  written sloppily were the three that worked. Grep before adding a rule.
- **Two LOCKED decisions, given and not up for discussion:** the two-level presence-lobby +
  party-session model, and **eager per-user Relay** ("Always-InParty").
- Preserve the invariants in `ROADMAP.md` § "Genuinely strong": single-writer SOAP,
  `.AsMainThread()` at every UGS/Netcode await, `PartyStateMachine` as the only lifecycle
  authority, `NetworkSceneObjectGuard.Sweep` before every join, every catch mapping to a named
  recovery.
- **Other sessions work this branch in parallel.** Fetch before you push; expect to merge. Resolve
  conflicts by reading **both** sides at hunk granularity — a `git checkout --ours` on a whole file
  silently drops the other side's unrelated fixes in that file, which has already happened once and
  had to be repaired. A Roslyn parse says nothing about a duplicate member, so a clean parse is not
  a clean compile.

## Decided with the owner — do not re-ask

| | |
|---|---|
| Crash reporter | Unity Cloud Diagnostics (chosen, **not yet relevant** — launch prep is deferred) |
| Week-one CCU | 10–40, which is why Block 5 matters and sharding does not |
| CI host | A Windows box with a Unity licence is available |
| Where to start | Block 1 — its code half is done |

## How to report back

Lead with what you ran, not what you wrote. State plainly what is unverified. If you correct
something in these documents, say what it was wrong about — the programme's own argument is that
this layer has never been short of fixes, only of runs.
