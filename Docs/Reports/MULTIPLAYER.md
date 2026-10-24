# Multiplayer — where it stands (report for Yash)

**From:** the 11 multiplayer test logs sent 19-23 Oct (`bugledger/belac`, `gradi`, `owner`; read with
`python3 Tools/Build/net_logs.py collect`), the Bug Ledger, `Docs/PartySystem/BUGS.md` and the team board. Written
2026-10-24. **Branch:** everything below is on `bleeding-edge` (B31-B33 fixes since #1189, 17 Oct).

## 1. In one minute

- **Multiplayer logs now arrive by themselves.** Caleb, gradi and owner ran 11 multiplayer tests (2-3 players,
  19-23 Oct, all on code with the B31-B33 fixes) and every one reached a `bugledger/*` branch with no pasting.
- **B32 is fixed in practice.** Two tests (owner 20 Oct, gradi 23 Oct) lost the presence lobby; both rejoined at once
  ("Presence lobby is gone - rejoining at once") and went `Reconnecting -> InParty`. Nobody stayed stuck.
- **B31 is NOT fixed.** Rate limits fell little: about one every 10-15 s in a party (was ~10 s). Almost every hit is
  the PARTY session refresh, although the game now reads it only once per 6 s. So our poll is not what spends the
  budget; most likely the UGS SDK's own reads of the party lobby (its fallback polling / event resubscribes).
- **B33:** one possible recurrence (owner, 19 Oct: one "Relay session not ready"); otherwise clean.
- **New: joining another player's party fails often.** Direct-join and accept fail with `NetworkManagerStartFailed`,
  `SessionConflict`, "player is already a member", or "Netcode client never connected - bouncing to solo menu".
- **New: Relay drops.** "player timed out due to inactivity", "allocation is invalid", "Failed to establish
  connection with the Relay server" (16, 16 and 11 sightings in the ledger), each ending the host's session.

## 2. Open bugs, by priority

| # | What | Evidence | Status |
|---|---|---|---|
| 1 | Joining a party fails: `NetworkManagerStartFailed` / `SessionConflict` / "player is already a member" / client never connects | ledger E-d1d183ed78, E-96c9656884, E-5b3fdac4a4, E-f900f1f48d, E-9a3e719250, E-e2d305370d; in 5 of the 7 two-machine tests | new, not root-caused |
| 2 | Relay connection dies mid-session (timed out / allocation invalid / bind failed -> host shuts down) | E-0a098d80df, E-4d0d17e44b, E-fda866d511, E-e5ff2abb8c, E-e6054bc064 | new, not root-caused |
| 3 | B31: the party session refresh is still rate-limited every ~10-15 s | every test; e.g. gradi 23 Oct: 53 in 9 min | fix did not hold |
| 4 | Presence lobby create throws NullReference (5x); "INVITE-SEND ABORT - presence lobby is null" | E-6b79cd07f1, E-ca3bc5cbe1 | new |
| 5 | Recovery after a failed join throws (ArgumentNull / InvalidOperation) | E-0de9ec6c70, E-41b7610878 | new |

Closed in practice: **B32** (seen recover twice). Waiting on more evidence: **B33**.

## 3. What changed this week

- Crew stations (two people in one hull, seats then hulls, riders): `Docs/CREW_STATIONS.md`, on `bleeding-edge`.
- No party/lobby code changed since the B31-B33 fix (17 Oct).

## 4. What is untested by hand

- Team tasks still open: T-20261015-4354 (QA-NET MPPM hand tests), T-20261015-0426 (Amoebius on Windows),
  T-20261015-ac73 (log collection check; in practice it works), T-20261017-bb32 (B31-B33 retest; the logs above
  answer B32 and B31).
- Decisions waiting: T-20261017-0406 (PR #1045 relay: what is left), T-20261017-d3b8 (PR #1030 branch hygiene).

## 5. Next steps

1. Root-cause the failed joins (bug 1) from the 23 Oct logs (`net_logs.py show 1..3`).
2. B31 again: find who reads the party lobby (SDK polling vs Wire events) before tuning anything.
3. Relay drops (bug 2): check whether they follow long idle / alt-tab (timed out due to inactivity).
4. Close T-20261015-ac73 (log collection works) and mark B32 fixed in BUGS.md.
