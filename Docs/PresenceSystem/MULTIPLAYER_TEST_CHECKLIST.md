# Multiplayer bug-hunt checklist (friends panel / presence / arcade lobby)

The manual half of the hybrid bug hunt. The automated half is
`Assets/_Scripts/Tests/Editor/MultiplayerBugHuntTests.cs` plus `AbsenceStrikesTests`,
`PresencePushTests` and `ArcadeLobbySnapshotTests`. Run those in the Test Runner (EditMode) first.

## Setup

- Use 2 or 3 instances: Editor + MPPM virtual players, or ParrelSync clones, or a dev build. Each one needs a different UGS account.
- In every instance, press **Ctrl+Shift+M** to open the **multiplayer debug overlay**. It is only in the Editor and Development builds.
- Turn on the `Party` and `ArcadeMatch` log channels in the Log Control window.
- When something looks wrong, press **Copy** on the overlay and paste the text into the bug report, along with the console log from every instance.

## What the overlay shows

| Line | Healthy | Suspicious |
|---|---|---|
| `Presence lobby:` | The same id on every instance | Different ids, which means a split lobby, so players can't see each other |
| `Converge:` | `on canonical`, and `lobbies visible=1` | `lobbies visible>=2` for more than ~10 s, or `query failed` / `join failed` repeating |
| `Safety poll:` | Ticks up to ~20 s, or ~8 s while the friends panel is open (`watchers>=1`) | Stuck at 0, or never fires |
| Lobby players `ready=` | `1` once their menu lava lamp shows | `-` for a long time on a player who is already in the menu |
| `listed=` / `strikes=` | Y / 0 for anyone present | `strikes=1` that never clears, or listed=Y for someone who has left |
| `Pending departures` | Usually `none` | Ids stuck here |
| Friends | ONLINE for a running friend, offline about 15 s after they quit | ONLINE long after they quit |
| Arcade lobby snapshot | Host and guest show the same `gen`, `pc`, `ai=N[...]` | Guest shows a different `ai=` list than the host |

## 1. Ghost row: a friend who quit still shows as online

1. A, B and C are all in the menu and can see each other in the friends/online panel.
2. **B stops Play in the Editor.** Do it once by quitting the build normally, and once by killing the process.
3. On A and C, keep the friends panel **open**.
   - **Expected after Editor Stop / normal quit:** B's row disappears within ~2 s. The log line `[HostConnectionService] ExitingPlayMode: leaving presence lobby ...` should appear on B.
   - **Expected after a killed process:** B's row disappears within ~30 s of UGS reaping B (8 s watched poll × 2 strikes). The overlay shows `strikes=1` for B first.
   - **Expected in Friends:** B shows offline within ~15 s. The panel re-reads Friends every 15 s while it's open.
4. Repeat with the panel **closed**, then open it. The open itself triggers a Friends re-read and the 8 s cadence.

## 2. Slow join: a player who just logged in isn't visible

1. A is in the menu. Start B.
2. **Expected:** once B's menu lava lamp shows (`ready=1` in B's overlay), A lists B within ~2–7 s.
   - B's log shows `In presence lobby <id> ... converge at +2s and +7s`.
3. Start A and B **at the same time**, which forces both to create a lobby.
   - **Expected:** `lobbies visible=2` may appear briefly. Within ~7 s both show the same `Presence lobby` id, with `Converge: migrated` on one of them.
4. Watch for a player who sits at `ready=-` while in the menu. That is a ready-flag problem, not a list problem: report it.

## 3. Stale AI on a guest's game config

1. A hosts and B joins the party. A opens an arcade card (the game config).
2. A adds 2 AI, then kicks one with ✕.
   - **Expected on B:** the kicked AI disappears immediately.
   - The overlay snapshot on both shows the same `ai=1[...]`.
   - The host logs `host roster ... -> ...` and the guest logs `client lobby ... -> ...`.
3. A kicks the last AI (if the card allows it).
   - **Expected:** B shows no AI, and the snapshot shows `ai=0[]`.
4. If B ever disagrees, look for `Guest roster drifted ... redrawing from the snapshot` in B's log. It should self-heal within 1 s. Report it if that line keeps repeating.
5. Repeat with B's config modal closed and reopened mid-edit, and with B joining after A already placed AI.

## 4. Regression sweep

- Invite → accept → party forms → start a match → return to the menu. The online list should be correct afterwards.
- Leave the party, then rejoin.
- Leave the friends panel open for 5 minutes. No `query failed` / rate-limit warnings should appear (8 s polls are about 0.13 GET/s).

## 5. Netcode roadmap checks (RTT, transform tuning, score guard, rejoin)

See Docs/Multiplayer/MULTIPLAYER_ARCHITECTURE.md §5 for what changed. Run with two MPPM players or two builds.

**RTT**
1. A hosts, B joins, start any match. Open the overlay (Ctrl+Shift+M) on both.
   - **Expected:** a `── Network (RTT) ──` section lists every Player. The host's own row shows `-- ms`, AI rows show `-`, B's row shows a real value (a few ms locally) and refreshes about once a second. On B, `me -> host:` shows its RTT. `time resync: on`.
2. Add latency on B: Unity Transport Network Simulator / MPPM network profile, or `clumsy` on Windows, with 300 ms.
   - **Expected:** after ~3 s B shows an orange **Poor connection - N ms** badge top-right. The host overlay shows B as `(Poor)`. Remove the latency → the badge goes within ~1 s.
   - The host never shows the badge.

**Ship transform tuning** (all 17 vessel prefabs)
3. Fly every hull class on the guest, watching from the host, including barrel rolls, loops, Scarab juke, Brittle Star and Tiger Moth.
   - **Expected:** smooth remote motion with no rotation flips or snapping at ±180°, and no visible position stepping.
   - Tiger Moth no longer syncs scale. Its size must look the same on both screens; report it if a grow/shrink effect only shows on one peer.
4. Repeat at 150 ms / 3 % loss. Some extra smoothing is fine; teleport-like jumps are not.
5. Fly far out to the arena edge. With half-float positions, the remote ship must not jitter there.

**Score guard**
6. Play a normal Rampage / wall-shredding match as the guest.
   - **Expected:** the guest's prism score matches what it was before this change. The host log has **no** `Prism reports from … throttled` lines.
   - If that line appears in honest play, report the mode and hull: the limits (600 burst, 300/s) need raising.

**Mid-match rejoin** (pre-existing feature, re-verify)
7. In a match, kill B's process (or pull its network).
   - **Expected on A:** a "handed to AI" toast. B's ship keeps flying under AI, and B's score keeps counting.
8. Restart B and log in.
   - **Expected:** the rejoin notice appears. Accept → B is back in the same match, flying the same ship, with its score intact. The host log has `seatReclaimed`.
9. Decline (NOT NOW). The AI keeps the seat to the end, and the offer is not shown again.
