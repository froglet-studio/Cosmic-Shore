# Presence System — Manual Test Procedures

Presence-lobby-specific MPPM scenarios. For party (Relay) gameplay
tests see `../PartySystem/TESTS.md`. For NetDiag-specific tests see
`../NetworkDiagnostics/TESTS.md`.

> **Convention.** See `../README.md` § "MPPM test convention" for VP
> naming and NetDiag class references.

> **⚠ Prerequisite:** every MPPM virtual player must carry a **unique
> tag** (auth-profile isolation) — untagged clones share ONE UGS
> identity and break every presence scenario. Full rule + symptom
> table: `../PartySystem/TESTS.md` § "MPPM prerequisites".

## Smoke gate — run on every presence-side commit

### P1. Lobby join on sign-in

**Setup.** Start one VP. Sign in.

**Steps.** Wait for `[PresenceLobbyService] JoinOrCreateAsync complete
— lobby: <id>` log.

**Pass criterion.**
- Log shows lobby id (not NULL).
- No NetDiag log lines on the JoinOrCreate path.
- `[BenignLobbyLogFilter] Installed` line appears once at startup
  (confirms the B1 filter is active).

### P2. Two-VP discovery

**Setup.** Start VP1, sign in. Wait for P1 completion. Start VP2, sign
in.

**Steps.** Wait for both to settle (~10 s).

**Pass criterion.**
- Both VPs' `HostConnectionDataSO.OnlinePlayers` includes the other
  (visible via `FriendsListPanel`'s Online section if the UI is open, or via the
  refresh logs).
- Both VPs are in the **same** lobby (compare lobby IDs in the
  startup logs).

### P3. Lobby leave on sign-out

**Setup.** Run P1 to completion.

**Steps.** Sign out (or stop play mode and restart).

**Pass criterion.**
- `[PresenceLobbyService] LeaveAsync` log appears.
- No `NetDiag: class=…` line in the leave log (if it appears,
  classify and file under `BUGS.md`).

## Stress gate — run on every refactor commit

### Stress-P1. Three-VP join storm

Start VP1, VP2, VP3 in quick succession (within 2 s of each other).
Each signs in independently.

**Pass criterion.**
- All three end up in the **same** lobby.
- If one creates its own lobby, the
  `PresenceLobbyService.ConvergeToCanonicalAsync` flow detects the
  rival and merges it in within ~3 s.
- No NetDiag log lines beyond `class=Transient` (which is acceptable
  for the race path).
- The B1 `LobbyPatcher` exception does **not** appear (or appears once
  per client and is suppressed by `BenignLobbyLogFilter`).

### Stress-P2. Rapid sign-in / sign-out

VP1 signs in, signs out, signs in five times in a row.

**Pass criterion.**
- Each sign-in completes P1 cleanly.
- No leaked lobbies on the UGS side (check via dashboard if
  possible, or by counting `[PresenceLobbyService] JoinOrCreateAsync`
  vs `LeaveAsync` log pairs).

## Failure-mode gate — run when investigating a bug

### P4. Offline during JoinOrCreate

**Setup.** Toggle the client machine's WiFi off **before** signing in.

**Steps.** Sign in.

**Diagnostic pass.** Sign-in fails (auth catch), or
`JoinOrCreateAsync` fails with
`NetDiag: class=Offline | reach=NotReachable | …`. Sign-in's bounce
path is the existing behavior — not the test target.

### P5. Mid-game lobby leave

> **Rewritten 2026-08-04.** The old single "within 5 s" criterion was
> wrong, and it was wrong in a way that makes a *correct* build look
> broken. Removal latency depends entirely on **how** the player went
> away, and the three cases differ by more than an order of magnitude.
> There is no single number.

**Setup.** VP1 + VP2 partied (per `../PartySystem/TESTS.md` S1), or
merely both present in the presence lobby for the non-party cases.

**The three departure classes.**

| How VP2 goes away | Mechanism | Expected removal on VP1 |
|---|---|---|
| **Graceful quit** — in-game quit button, alt-F4, window close | `Application.wantsToQuit` → 1.5 s drain → **awaited** UGS leave → `PlayerHasLeft` push | **< 1 s** |
| **Editor play-mode stop** | `EditorApplication.playModeStateChanged / ExitingPlayMode` → leave **dispatched but not awaited** (the exit cannot be deferred) | **< 1 s if the request made it onto the wire; otherwise falls back to reap** |
| **Hard kill** — process termination, crash, OS kill, **MPPM virtual-player deactivation** | nothing runs | **service-side reap, ~30–50 s** |

**⚠ MPPM cannot test the graceful path by "turning off" a player.**
Deactivating a virtual player in the Multiplayer Play Mode window
**terminates the clone process**. That is the third row, not the second —
no `ExitingPlayMode`, no leave, nothing to push. A 30–50 s disappearance
there is the **expected** result and not a regression of B12. Stopping
play mode in the *main* editor does fire `ExitingPlayMode`, but it stops
every virtual player at once, so there is no surviving observer left to
watch the removal.

**To actually exercise the graceful path:** run a **standalone build**
alongside the editor (or on a second machine) and quit the build with its
in-game quit button or alt-F4. That is the only route in this project
today that reaches `Application.wantsToQuit` with a peer still watching.
An editor-only test hook that fires the departure on demand is tracked as
`TODOS.md` § TODO-P10.

**Pass criteria.**
- **Graceful quit:** VP2 logs `Departure leave complete (leaveParty=True)`
  and pauses ~1.5 s before closing. VP1 removes VP2 from `OnlinePlayers`
  in **< 1 s** — via the named-id eviction path
  (`TryConsumeDepartedPlayerIds`), *not* the two-strike absence rule.
- **Hard kill / MPPM deactivate:** VP1 removes VP2 within **~30–50 s**.
  Faster is impossible: there is no transport between non-party presence
  members, so only the UGS service can observe the disconnect. See
  `PRESENCE_SYNC_PLAN.md` § 6.
- **Party members only** additionally free their party slot via the
  Netcode-backstop `OnClientDisconnected` callback, which *does* have a
  transport and therefore fires promptly in every case including a hard
  kill. A killed peer disappearing from `PartyMembers` quickly while
  lingering in `OnlinePlayers` is correct, not a contradiction.

### P6. Refresh-error escalation

This is the test that targets the watchdog
(`MAX_REFRESH_ERRORS_BEFORE_RECONNECT`). Hard to provoke
deterministically without SDK injection, so use it as a **post-hoc
check** when a `Reconnecting` state transition appears in the log:

**Check criterion.** Every `[HostConnectionService] N consecutive
refresh errors — reconnecting to presence lobby` log line should be
preceded by N NetDiag log lines from
`PresenceLobbyService.JoinOrCreateAsync.catch` or
`HostConnectionService.RefreshAsync.catch`. The classes on those lines
should be **consistent** (e.g. all `Offline`, or all `SessionGone`) —
inconsistency suggests the watchdog is escalating across heterogenous
causes, which is the bug `PresenceSystem/REFACTOR.md` targets.

### P7. Rename propagation (identity sync)

**Setup.** 3+ tagged VPs signed in and settled (P2 pass). Optionally
VP1+VP2 partied (S1) to also exercise the party-slot path.

**Steps.** On VP2, open the profile UI and change the display name
(`PlayerDataService.SetDisplayName` path).

**Pass criterion.**
- VP2's console logs `RepublishLocalIdentity` (immediate push) — and if
  that write were to fail, the next `PublishPartyState` save carries the
  name anyway (per-tick reconciler; no user-visible difference).
- Every other VP's Online row for VP2 shows the new name within ~2 s
  (one remote refresh tick — `RefreshOnlinePlayersDiff` change-detects
  and re-fires the row).
- If partied: every peer's party slot for VP2 updates within ~2 s
  (`PartyMemberService` identity refresh from the session player record,
  log line `Member identity refreshed`), VP2's own slot updates
  instantly, and no `Member joined` / `Member left` log lines or
  invite-clear side effects appear — a rename is an identity refresh,
  not a membership change.
- In-game (persistent Player object): `Player.NetName` picks up the new
  name (owner write via `HandleProfileLoadedAfterSpawn`), and
  `RoundStats.Name` follows on every peer (live mirror in
  `OnNetNameValueChanged`) so the next scoreboard render uses it.

> **Reminder:** re-tagging a clone switches it to a NEW UGS account —
> the display name resets to a fresh `Pilot####` default (see
> `../PartySystem/TESTS.md` § "MPPM prerequisites"). That is account
> switching, not a sync failure.

## What success on these tests means

| Gate | Required for |
|---|---|
| P1, P2, P3 | Every presence-side commit |
| Stress-P1, Stress-P2 | Every refactor commit |
| P4, P5, P6 | Run when investigating a specific bug; not a per-commit gate |
| P7 | Run after any change to identity publish / profile pipeline |

`ARCHITECTURE.md` § "Single-writer pattern" describes the invariants
these tests are protecting.
