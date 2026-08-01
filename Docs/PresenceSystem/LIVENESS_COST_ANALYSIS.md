# Liveness cost analysis — should we add a presence heartbeat?

**Question.** A hard-killed client lingers in every peer's online list until the
UGS lobby reap (~30 s). Should we add a `lastSeen` heartbeat property plus
peer-side staleness eviction to bound that?

**Answer: no. Don't build it.** It is not cheap even at N=4 for the latency that
would make it worthwhile, it consumes the last slot in a hard platform cap, and
the private-friend-list direction removes the need entirely.

Two optimisations that fall out of the analysis **are** worth doing, and one of
them fixes something I got wrong in Commit 4.

---

## 1. The hard caps

| Cap | Value | Source |
|---|---|---|
| Players per lobby | **100** | Unity Support: "What are the limitations of the Lobby service?" |
| **Player data values per player** | **10** | same |
| Lobby data values | 20 | same |
| GetLobby / QueryLobbies | ~1 req/s per player | in-repo comments — **unconfirmed**, the docs rate-limit page is a JS SPA and would not render |
| UpdatePlayer | ~5 req/5 s per player | same, unconfirmed |

### The decisive one: we are at 9 of 10 player data values

| # | Key | Written by |
|---|---|---|
| 1 | `displayName` | `BuildLocalPlayerProperties` |
| 2 | `avatarId` | ″ |
| 3 | `partyCount` | ″ |
| 4 | `partyMax` | ″ |
| 5 | `matchName` | ″ |
| 6 | `invite_payloads` | ″ |
| 7 | `joined_party` | ″ |
| 8 | `accepted_invite` | ″ |
| 9 | `presenceState` | `PublishPartyStateIfChangedAsync` (added this branch) |
| — | **1 slot free** | |

A `lastSeen` key takes the last slot. Every future presence field — region, ping
band, platform, rich-presence text, party lock state — would then have to
displace something. **Spending the final slot on a keepalive is the worst
possible use of it.**

*Mitigation if it were ever built:* pack the timestamp into `presenceState`
(`"3|1738…"`). Costs no slot, but couples two unrelated fields into one
parse — and the value then changes every beat, which is what drives §3.

---

## 2. The latency math says the interval must be small

Let **T** = heartbeat interval, **D** = staleness threshold before eviction.
D must exceed T with margin for a missed beat, so D ≈ 2.5 T. Detection latency ≈ D.

To beat the ~30 s reap at all, D < 30 → T < 12 s. To be *worth building* — say
5 s detection — **T ≈ 2 s.**

That is the crux: a comfortable 10 s heartbeat gives D ≈ 25 s, which is
indistinguishable from the reap it was meant to replace. The heartbeat is only
useful at a cadence that is expensive.

---

## 3. Cost per client at T = 2 s

Writes scale with 1/T. Inbound deltas scale with **(N−1)/T** — every member's
beat is fanned to every other member, so aggregate traffic is **O(N²)**.

### As the code stands today

- `LobbyPropertyWriter.WriteAsync` costs **3 UGS calls** per write (pre-write
  `GetLobby`, `UpdatePlayer`, post-save `GetLobby`).
- Every inbound push marks the roster dirty, and the drain calls
  `RefreshAsync()` → `ISession.RefreshAsync()` → **another `GetLobby`**.

| N | writes/s | inbound deltas/s | GetLobby/s (3/T writes + deltas + 1.5 s safety poll) | verdict |
|---|---|---|---|---|
| 4 | 0.5 | 1.5 | ≈ 3.7 | **over both caps** |
| 20 | 0.5 | 9.5 | ≈ 11.7 | far over |
| 100 | 0.5 | 49.5 | ≈ 51.7 | absurd |

**Even at your scale of 4 it breaches the read cap.** So "it's cheap at my scale"
is not true as the code stands.

### With both optimisations from §5 applied

| N | writes/s (1 call) | GetLobby/s | inbound deltas/s | verdict |
|---|---|---|---|---|
| 4 | 0.5 | 0.67 (safety poll only) | 1.5 | fits, but spends **half** the write budget on a keepalive |
| 20 | 0.5 | 0.67 | 9.5 | SDK patch churn climbing |
| 100 | 0.5 | 0.67 | 49.5 | this **is** the `LobbyPatcher` delta churn behind `BUGS.md` B1/B6 |

So even optimally implemented it is affordable only in the small-N regime, and it
puts sustained pressure on exactly the SDK path that already misbehaves.

---

## 4. The private friend list removes the need entirely

This is the real answer.

UGS **Friends** already provides **server-tracked, push-based presence**:
`IFriendsService.PresenceUpdated` fires with an `Availability`, and going offline
is detected by the *service* from the client's own connection — not by an
application heartbeat, and not by a lobby reap.

It is already wired in this project and simply unread:

- `FriendsServiceFacade.WireEvents` subscribes `PresenceUpdated` →
  `SyncAllRelationships()` → rebuilds `FriendsDataSO.Friends`.
- **No UI reads `FriendsDataSO.Friends`.** `FriendsListPanel.PopulateOnlineSection`
  iterates `HostConnectionDataSO.OnlinePlayers` — the presence-*lobby* roster.
  That is RC-12 in `PRESENCE_SYNC_PLAN.md`: two disjoint presence systems, and the
  UI consumes the wrong one.

Switching the panel to the friend list means:

| | Presence lobby (today) | UGS Friends (planned) |
|---|---|---|
| Liveness owner | us, via reap or heartbeat | **the service** |
| Property budget cost | 10-value cap | none |
| Fanout | O(N²) over *all* 100 members | events for **your friends only** |
| Scale ceiling | 100 hard | friend-list sized |
| Heartbeat needed | yes, to beat the reap | **no** |

**Unverified:** UGS Friends' actual offline-detection latency. It is the
service's connection tracking rather than a 30 s inactivity sweep, so it should
be materially better — but confirm with a two-account test before relying on a
number.

The presence lobby stays useful for what it is actually for: *discovery* of
players you are not yet friends with, and the invite property exchange. Liveness
for people you care about moves to Friends.

---

## 5. Two things worth doing regardless

### 5a. Stop issuing a GetLobby on every push delta — **this is a Commit 4 defect**

`HostConnectionService.Update` currently does:

```
ConsumeRosterDirty() → RefreshAsync() → ISession.RefreshAsync()   // a GetLobby
```

That round-trip is redundant. **The SDK has already applied the delta to the
local session object before the callback fires.** Evidence, in order of strength:

1. Unity documents `PlayerJoined` as *"called right after the session gets
   updated"* and `PlayerLeaving` as *"called right before"*.
2. This repo's own `IsBenignLobbyPatcherError` exists because
   `LobbyPatcher.ApplyPatchesToLobby` throws while **patching the local lobby
   from a WebSocket delta** — the patcher is the thing keeping local state
   current.

So a push should re-diff from the in-memory `ActiveLobby.Players`, not fetch.
This removes one `GetLobby` **per inbound delta per client** — the dominant read
cost in every row of §3, and pure profit even with no heartbeat. It is also what
makes the safety poll safe to relax from 1.5 s to 10 s.

I introduced this in `8a146795` by routing the push through the existing
poll-shaped `RefreshAsync()` because it was the smallest diff. It made push
*correct* but not *cheap*.

### 5b. Single-call writes for pure-presence updates

`WriteAsync`'s pre/post refreshes are load-bearing for the *stateful* keys
(`2452a392` documents why). A presence-only write has no such constraint and
could use `SaveCurrentPlayerDataAsync` alone: 3 calls → 1.

---

## 6. Recommendation

1. **Do not add a heartbeat.** Costs the last of 10 player-data slots, breaches
   the read cap at N=4 as the code stands, and sustains the delta churn behind
   B1/B6.
2. **Do 5a now** — it is a defect fix, it is the largest single read saving
   available, and it unblocks relaxing the safety poll.
3. **Do the friend-list migration when you get to it** (RC-12). It makes the
   whole question moot: liveness becomes the service's problem, with no property
   cost and no O(N²).
4. **Accept the ~30 s floor for hard kills in the interim.** Graceful quit, MPPM
   stop and backgrounding are already sub-second (B12). A hard kill is the one
   case nothing client-side can beat, and it is rare in production relative to
   how often it happens while testing.

Revisit only if hard-kill ghosts turn out to matter in a *shipped* build, with
429 counts from a real session to argue against (`TODOS.md` TODO-P5).
