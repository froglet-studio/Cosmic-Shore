# Multiplayer branch audit (2026-10-26)

Yash asked: find the party and lobby work done on other branches (August especially, and Garrett's),
compare it with `bleeding-edge`, decide what to take and what to delete, and remove the multiplayer branches
nobody needs.

**How it was measured.** Many old branches sit on a rewritten history, so they look thousands of commits
ahead and commit counts mean nothing. Every remote branch (546) was compared by **content**:
1. Which multiplayer file versions (`Controller/Party`, `Controller/Multiplayer`, `Docs/PartySystem`,
   `Docs/PresenceSystem`, `Docs/MultiplayerArchitecture`, `NetworkManager.prefab`) never existed on
   `bleeding-edge`. 288 branches had at least one.
2. For each candidate commit, how many of its added lines exist on `bleeding-edge` today.
3. A hand read of the code on both sides, with file:line, for every branch that still mattered.

Nothing below ran in Unity.

## 1. What to take, in order

| # | From | What | Size | Owner to check with |
|---|---|---|---|---|
| 1 | `Ys-rescue-2026-08-24` | **The push design for Block 5** (§2). Block 5 ("push instead of poll") is marked *not started* and a launch blocker in `MULTIPLAYER_START_HERE.md`, yet August built it, measured it and retested it in MPPM. Port the design behind `IPresenceTransport`, not the files. | ~1,000 lines over 9 steps | Yash (party layer) |
| 2 | `cece/nice-brahmagupta-ojclij`, part J | The bounce notice that never shows (`PartyInviteController.cs:688` raises `bounceToastChannel`, which is unassigned in `PartyServices.prefab`), plus the toast manager keeping destroyed items (`ToastNotificationManager.cs:126-147`) | ~100 lines + test | Garrett (his test plan, `Docs/BRANCH_PARTS_STATUS.md` row J) |
| 3 | same, part J | Dropping out of a match (6be10752d0): the host-loss path neither covers the screen nor unpauses (`PartyInviteController.cs:642`); "Host disconnected" is shown when your own link timed out (`MultiplayerSetup.cs:559`); a UGS id is parsed as a ulong and never matches (`MultiplayerDomainGamesController.cs:193`) | ~50 lines | Garrett |
| 4 | `claude/host-connection-refresh-error-85c6a2` | The presence converge can release the lobby it just joined (03f6ad7543). `PresenceLobbyService.cs:265-271` deletes a fresh read of `_activeLobby` after the join await, and `Update` (`HostConnectionService.cs:459`) does not check `_joining`, so a tick can race a first join or a re-init. Its doc calls it "B13"; that number is taken, so give it a new one. | ~80 lines | Yash |
| 5 | same, part J | Ready lights show who is ready, not a count (107c04c8ae; `LobbySlotRow.cs:113`, `ArcadeConfigSyncManager.cs:867`). Conflicts with today's shared vessel panel (7d14a6d1c2). | ~180 lines | Garrett |
| 6 | same, part J | Add Friend by name, with the `#1234` resolver (d70061b424, 4e8d7789cf). `FriendsServiceFacade.cs:100,123` `SendFriendRequest*Async` has no caller, so nobody can send a friend request today. | medium | Garrett |
| 7 | same, part J | Recently played with (5 commits). No `RecentPlayersStore` exists. | medium to large | Garrett |
| 8 | same, part J | The invite inbox (caf4e2dd80, e52d069877). Today `HostConnectionService` keeps one `_lastFiredInvite`, so two invites at once ping-pong, and `DeclineInviteAsync` declines every sender. HCS has changed ~530 lines since, and the popup is now `PartyInvitePopup.prefab`. | large | Garrett |
| 9 | `claude/unified-systems-refactor-apze59` | cfc9c4951f retires `GameDataSO.IsMultiplayerMode` (still used at `GameDataSO.cs:89`, `MultiplayerSetup.cs:225-239`). It changes behaviour; it is a decision, not a port. | ~11 call sites, 12 assets | Yash |
| 10 | `prototype-level` | The Friction mode and its AI hooks, only if Friction is wanted. Its `Friction = 48` collides with `Tollway = 48`. | mode-sized | dbrutus |

Every part-J port redoes its scene work: the friends panel moved out of Menu_Main into `PartyPanel.prefab`.

## 2. Block 5 from the August design (`Ys-rescue-2026-08-24`)

`Ys-rescue-2026-08-24` is a saved copy of the old `Ys-bleeding-edge` from 24 Aug. Its presence and party work
(2026-07-16 to 08-21) is not an ancestor of `bleeding-edge`, and no doc on `bleeding-edge` mentioned it until
this one.

**Port order.** Bring the design, not the files. `UgsRequestPolicy`, `LobbyReadPacer` and the 400-line rule stay.

**Progress (2026-10-26, `Ys-bleeding-edge`):** steps 1-8 are done (below, each marked); step 9 is next.

1. **Done 2026-10-26.** **Count voided ticks** (44587a2fa4). The benign branch at `HostConnectionService.cs:1552` and `:2089` is
   empty, and `UgsRequestCounter.Benign` is never counted, so a voided tick looks healthy. About 5 lines;
   this is the baseline the Block 5 gate needs.
2. **Split done 2026-10-26; the extraction is not** (as in August: the publish trackers are tied to four
   dependencies, more surface than it buys yet). **Extract `PresenceRefreshLoop`, then split the read from the publish** (791c6d0456; hardening seams 1-2).
   Today they share one `try` (`HostConnectionService.cs:1484-1541`), so a benign read failure skips the
   publish. About 150 lines.
3. **Done 2026-10-26.** **Fix the roster wipe on a background rejoin** (RC-8 from 8a14679585). `SeedLocalPlayer(clearFirst:true)`
   (`HostConnectionService.cs:2326`) runs on the rejoins at `:1631,:1724` and clears a live party roster.
   Clear only with no live session, and make `SeedLocalPlayer` idempotent. About 20 lines; it must land
   before step 5.
4. **Done 2026-10-26** (the engine's `ISession` gained the three events in `6757fc61d9`). **Party session push** (090f61a6ba). Today `PartySessionService` subscribes only to `PlayerLeaving`
   (`:202,:231`). Add flag-only handlers for `PlayerJoined`, `PlayerHasLeft` and `PlayerPropertiesChanged`,
   drained in `Update` into the cache sync B31 already added. About 120 lines.
5. **Done 2026-10-26** (`PresencePushChannel`; the engine's `ISession` gained `RemovedFromSession` in `023d5c55db`).
   One rule the port added: the channel detaches BEFORE our own leave or delete, since the SDK raises `Deleted` on
   the lobby we leave and a frame can run while the leave awaits. **Presence push** (b0adfa7284). `PresenceLobbyService` subscribes to no `ISession` event. Route its six
   `_activeLobby` assignments (`:164/271/307/366/505/583`) through one `SetActiveLobby`. Drain events
   **without** a fetch (4022675284's shape, not 8a14679585's `RefreshAsync` per push). Evict by departed id at
   once (a510bd5128). Send `RemovedFromSession`/`Deleted` into the existing
   `PresenceErrorForcesRejoin`/`SchedulePresenceRejoin`. About 300 lines, in a new class.
6. **Done 2026-10-26** (hooked to `PartyMembers`' own `OnItemCountChanged` / `OnCleared`, since `OnPartyRosterChanged`
   is not on this line; the optional coalesced channel was not ported). **Republish the moment the roster moves** (111676ba22): a flag plus a drain. Today the immediate publish
   happens only on launch and scene load (`HostConnectionService.cs:2697`), so partyCount lags a poll. About 40
   lines. Optionally the coalesced `OnPartyRosterChanged` channel (f6aecb6a5b + 4aece9257f + 9ee67e5f2a as one
   unit; today one change repaints `FriendsListPanel` three times).
7. **Done 2026-10-26** (one `AbsenceStrikes` counter used by both lists; a strike needs a SERVER read, so a push tick
   or a party tick between GETs re-reading the SDK's copy adds none; a departure the service states (`PlayerLeaving` /
   `PlayerHasLeft`, collected by `DepartureReports`) or a member turning spectator removes at once, which covers
   August's "graceful leave is not slowed down" by id instead of by a re-read). **Two absent reads before eviction** for poll-only absences (c9c6db1772). Today one absence evicts
   (`HostConnectionService.cs:1913-1917`, `PartyMemberService.cs:162-171`). About 110 lines.
8. **Done 2026-10-26** (August never took this step: its push was unproven in the editor, so it kept the 1.5 s poll;
   ours fired in Shombith's 26 Oct Unity test, `presence pushes 3`). One `LobbyRefreshScheduler.SAFETY_POLL_SECONDS`
   tick (20 s, +-10%), menu and match alike, reads the presence lobby, runs the converge and reads the party session
   (`LobbyReadPacer` holds the two reads a second apart): about 0.1 GETs/s where the old cadence spent 0.83 (0.98
   boosted). Gone: the boost window and its six `Boost()` calls, the 6 s / 15 s party-read timer and
   `PartyReadDueThisTick`, the 4 s converge timer, the 10 s in-match tick; a push sync no longer GETs. The first
   poll comes 2 s after a presence join (`FIRST_POLL_AFTER_JOIN_SECONDS`), since a join raises no push for the
   players already there. Trade-offs: a missed push, a lobby split or a stale local copy now waits up to ~20 s for
   the net, and the watchdog's three failed reads take ~60 s (a pushed removal or deletion still rejoins at once).
   The re-measure is the next multiplayer test log (`reads`, `429s`, `voided`, pushes). **Then relax:** delete the boost window, the 4 s converge and the per-tick reads, and keep one 20 s ±
   jitter safety poll. Re-measure with step 1's counter.
9. **Explicit leave on quit and background** (52b8f5f656). There is no `wantsToQuit` handler
   (`ApplicationLifecycleManager.cs:69-87`); the only leave is an async-void `OnDestroy`
   (`HostConnectionService.cs:485-515`), and its own message admits about 30 s. A push only fans out a leave
   if the leaver actually leaves. About 230 lines.

Independent of Block 5:
- **Own party size from the local roster** (3e8885c2d4; `FriendsListPanel.cs:416` shows the peer's claimed
  count even for "In Your Party"). About 60 lines.
- **One owner for the wire keys** (4129b9321d: `PartyLobbyKeys`). Key literals live in four files
  (`HostConnectionService.cs:78-95`, `PartyMemberService.cs:58`, `PartySessionService.cs:70-76`,
  `PresenceLobbyService.cs:67-74`). The party session still writes six presence-only keys
  (`PartySessionService.cs:348-355`).
- **Cadence:** `AppManager.cs:520` hardcodes `new LobbyRefreshScheduler(1.5f)` while `PartyServices.prefab`
  says 3, and the backoff is `refreshIntervalSeconds*2` = 6 s where B31's text says 3 s (09381defe5, 084dce0ba4).
- **UI:** unknown avatars resolve to `ProfileIcons[0]` (3b9a30fa53; `ArcadeLobbyList.cs:740`,
  `FriendsListPanel.cs:943`, `ConnectingPlayerRoster.cs:464`); panels re-read only in `OnEnable` (24a9b42093);
  PARTY FULL is still shown (5b36156e9c, an owner decision).

**Do not bring back:**
- `UgsErrorClassifier` and the `IsRateLimit` copies: `UgsRequestPolicy` is the one classifier.
- The scheduler's read-cap pacing: `LobbyReadPacer` covers it.
- `PresenceStateMachine`, `PresenceState` and the `presenceState` key. It used a 9th of 10 player-data slots
  and caused regressions B11 and B14; in-match is solved by matchName (party B15).
- A fetch per push.
- `AcceptanceSignalService` (deleted by D1).
- The heartbeat (costed and rejected).
- `CSDebug.LogNoStack` lines: use counters and `LogVerbose`.

## 3. Measured facts from August worth keeping

- **Stale-index void rate (MPPM, SDK 1.1.8):** presence read 20% → 12%, party read 43% → 32% over 95.8 s and
  206.5 s runs. Mostly at startup (a 2.2× longer window had 1.2× the skips). Every fault was Transient /
  SdkStaleIndex.
  - A poll's real cadence is nominal ÷ (1 − void rate).
  - The startup write burst is the lever, not the read rate.
  - "No stale-index lines in 30 min" (the Block 5 gate) may not be reachable by push alone.
- **UGS caps:** 10 player-data values per player, 20 per lobby, 100 players. Presence writes 8 keys, so only
  **2 slots are free**.
- **SDK semantics, re-checked in 2.3.3:**
  - Changes are applied to the local lobby **before** `PlayerJoined` and the data events fire, so a push
    needs no GET.
  - `PlayerLeaving` fires before the update, `PlayerHasLeft` after.
  - `Player/SessionPropertiesChanged` do **not** fire for your own write.
  - **`PlayerLeft` is `[Obsolete]`**; the hardening prompt names it, so use `PlayerLeaving`/`PlayerHasLeft`.
- **The writer's pre/post refreshes carry load:** a stale index makes `SaveCurrentPlayerDataAsync` fail
  silently. Measure before dropping them (REVIEW §5.1 proposes to).
- **MPPM pitfall:** deactivating a virtual player kills its process, which gives a 30-50 s reap, not a leave.
  A graceful leave is testable only in a standalone build or with a "simulate departure" hook.
- **Own party size is a source-of-truth bug, not latency:** N published scalars over N×(N−1) lossy edges never
  agree on any cadence. B29's "Block 5 cures it" is half true.
- **Rule from August B16:** any SOAP raise reachable from a UGS continuation must be an Interlocked flag drained
  in `Update`.
- **Bug numbers:** August's Presence B11-B16 are not Party B11-B38. Renumber when porting.

## 4. Branches kept, and why

| Branch | Why it stays |
|---|---|
| `Ys-rescue-2026-08-24` | The source of §2 until Block 5 is ported |
| `cece/nice-brahmagupta-ojclij` | Garrett's test plan; part J and parts B, D-L have not landed |
| `claude/host-connection-refresh-error-85c6a2` | Until the converge fix (§1 row 4) is ported |
| `claude/unified-systems-refactor-apze59` | Yash's decision on retiring `IsMultiplayerMode`; it also holds the `Docs/UnifiedSystems/` programme docs |
| `prototype-level` | dbrutus's Friction mode (and aradia1's 09-07 FMOD/Dolphin audio, not checked) |
| `claude/android-performance-stripped-dap5z2` | Garrett's port plan tracks its non-multiplayer steps |
| `claude/vigilant-archimedes-euoz8z` | In flight (fair-play work Shombith asked for) |
| `master` | Its multiplayer work is fully on `bleeding-edge`, but it is a historical default name; delete it only on purpose |

## 5. Branches to delete (nothing is lost)

Each one's multiplayer work is on `bleeding-edge` (in another form), inside `Ys-rescue-2026-08-24`, or on code
that no longer exists. The tip commit is recorded so any of them can be restored.

| Branch | Tip | Last | Why it can go |
|---|---|---|---|
| `claude/fix-netcode-namespace-Ln7J2` | `324829790d` | 02-25 | Merged as #133; every commit is in `Ys-rescue-2026-08-24` |
| `claude/add-party-game-mode-Go0YH` | `1881410aa4` | 02-25 | Shombith's February party game mode; the mode no longer exists |
| `claude/add-party-game-mode-gJ8AE` | `6f3368fecd` | 02-25 | Same |
| `claude/fix-party-game-mode-aALL3` | `6fe53546ff` | 02-27 | Same mode |
| `claude/test-multiplayer-freestyle-sync-U5XgI` | `164a0e69dd` | 02-26 | Multiplayer Freestyle was retired 10-08 (8e701f692f) |
| `claude/party-cards-arena` | `9eb28a82de` | 10-08 | Put Multiplayer Freestyle on the Arena roster; the mode was retired the same day |
| `claude/add-party-invite-panel-K5pah` | `b39fd8af9b` | 02-28 | February invite flow, replaced by the eager-Relay `PartyInviteController` |
| `claude/review-friend-system-nVgua` | `2ae6505a51` | 02-28 | Same period; rewritten since |
| `claude/setup-friends-panel-ui-j3ihi` | `a4a6d4de5a` | 02-28 | Friends panel editor setup; the panel is now `PartyPanel.prefab` |
| `claude/sync-game-start-lobby-G9Aec` | `87484ed6d8` | 02-28 | Game config sync and AI backfill, rebuilt since |
| `claude/test-multiplayer-play-mode-9cnAw` | `b047c88f6c` | 03-01 | Merged as #293; its MPPM helper tests target helpers removed by MPPM 2.0 |
| `claude/test-friend-system-Zm5wJ` | `db03e781f1` | 03-02 | Yash's 74 friend tests, mostly data types and redesigned views; the live friend system is covered by `Port/tests/CosmicShore.Tests/FriendsSystemTests.cs` |
| `claude/fix-hex-race-multiplayer-6mQs3` | `332c093f4a` | 03-03 | HexRace scene-transition fixes on code since rewritten (`HexRaceHUD` is gone) |
| `claude/fix-networkmanager-host-mMOmz` | `4b293928ed` | 03-03 | Fully inside `Ys-rescue-2026-08-24` |
| `claude/lobby-player-team-selection-9Bzzy` | `c929e31a30` | 03-03 | Replaced by the domain picker |
| `claude/sync-spawning-algorithm-nLaH5` | `6da9eb23f8` | 03-03 | Replaced by today's vessel spawner pipeline |
| `claude/fix-menu-main-relay-host-e1JYI` | `986fe9d461` | 03-04 | Relay-at-invite approach, replaced by the eager Relay host |
| `claude/fix-volume-scoring-multiplayer-5hBSs` | `929c781f2b` | 03-19 | Scoring rebuilt since |
| `claude/fix-multiplayer-joust-crash-agbs9` | `1504d370fe` | 03-19 | Fully inside `Ys-rescue-2026-08-24` |
| `claude/fix-lobby-rate-limit-W7HA4` | `af14b441ef` | 03-28 | Replaced by `UgsRequestPolicy` and `LobbyReadPacer` |
| `claude/fix-multiplayer-host-issues-C88Sm` | `2e87fe6176` | 04-03 | Fully inside `Ys-rescue-2026-08-24` |
| `claude/fix-friend-party-invites-1NgTo` | `da58630770` | 04-22 | April invite fixes, replaced by the October hardening (B1-B38) |
| `claude/fix-party-lobby-connection-voFVM` | `b0eb1024bb` | 04-23 | An April audit doc, replaced by the same |
| `claude/fix-multiplayer-scoring-Jsl98` | `a2d689f1b0` | 05-01 | Scoring rebuilt since; `StatsManager` already gates on the server |
| `claude/optimistic-maxwell-uet05g` | `f6467fbdc5` | 07-17 | Yash's; PR #597 was closed unmerged; every doc exists, longer, on `bleeding-edge` |
| `fix/networkmonitor-prefab-dead-component` | `6507f167a1` | 08-13 | jtgjones's; `NetworkMonitor.prefab` no longer exists |
| `multiplayer/sdk-upgrade` | `99910a6506` | 09-26 | Yash's; fully merged into `bleeding-edge` |

A cloud Claude session can push only to its own branch, so the
deletion is run from a person's machine:

## 6. The command (archive as a tag, then delete)

Run from a clone with push rights. It keeps every tip as `archive/<branch>`; restore one with
`git push origin refs/tags/archive/<branch>:refs/heads/<branch>`.

```bash
git fetch origin
while read b sha; do
  git push origin "$sha:refs/tags/archive/$b" && git push origin --delete "$b"
done <<'EOF'
claude/fix-netcode-namespace-Ln7J2 324829790dd53cc7e0830cbdb736b9d9bf4ee4d9
claude/add-party-game-mode-Go0YH 1881410aa49a4a7a3c32c60f5dac8221d8d01aef
claude/add-party-game-mode-gJ8AE 6f3368fecdacb37953bcc2e6b4d59ff28085a98e
claude/test-multiplayer-freestyle-sync-U5XgI 164a0e69dd2949914b5ecd37d44f093f3e13f5cb
claude/fix-party-game-mode-aALL3 6fe53546ff623b01b3d53c14c8c2b8c0b34788c6
claude/add-party-invite-panel-K5pah b39fd8af9b744257b6039780b08421d73b9a29a2
claude/review-friend-system-nVgua 2ae6505a51993a7df8a31539578fbe980c84a1b8
claude/setup-friends-panel-ui-j3ihi a4a6d4de5a72c0bb77421e73afc6a410b977442f
claude/sync-game-start-lobby-G9Aec 87484ed6d86a097f1c2dbe416003d62f6615fafe
claude/test-multiplayer-play-mode-9cnAw b047c88f6c34d532594d54e6764b603295550f7b
claude/test-friend-system-Zm5wJ db03e781f17749d8690dc7c24bd3221cf281f54c
claude/fix-hex-race-multiplayer-6mQs3 332c093f4affcbbd37a3ab6c731ab8c13d21eac6
claude/fix-networkmanager-host-mMOmz 4b293928edf1b733df32a5b2ccab31b54cd054c1
claude/lobby-player-team-selection-9Bzzy c929e31a30e6919ed592a8d0d9c1bddf8d57dd90
claude/sync-spawning-algorithm-nLaH5 6da9eb23f8df83a5ba9ce17c64f27813f2203fc8
claude/fix-menu-main-relay-host-e1JYI 986fe9d461bcf285a534f5753b99084077ff4a09
claude/fix-volume-scoring-multiplayer-5hBSs 929c781f2b32ebeed58cb5af0a3c32870c9ae698
claude/fix-multiplayer-joust-crash-agbs9 1504d370fe68f418fc399abb2afd004347b48e3b
claude/fix-lobby-rate-limit-W7HA4 af14b441ef92c4ace92b733d704cbc2621efada5
claude/fix-multiplayer-host-issues-C88Sm 2e87fe6176ce57462af8b0c92b1a8bc605f590c4
claude/fix-friend-party-invites-1NgTo da58630770c45042ea0ffa332144ce66b841a3f5
claude/fix-party-lobby-connection-voFVM b0eb1024bb0a6800fa6931e8e5f97fd28e288463
claude/fix-multiplayer-scoring-Jsl98 a2d689f1b0382ad24a7f8b0027ce265dce993a90
fix/networkmonitor-prefab-dead-component 6507f167a1d2ddb52d630366f006bcda18dcf6d0
multiplayer/sdk-upgrade 99910a650600cea55cd17407e7e9d37aa5321adb
claude/party-cards-arena 9eb28a82de7d5506957582abb98b594b8168d1db
claude/optimistic-maxwell-uet05g f6467fbdc5aaeb1737387d37a40e201ae45a7804
EOF
```
