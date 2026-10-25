# Multiplayer sweep log

One row per run of the multiplayer sweep (`Docs/MULTIPLAYER_START_HERE.md` §3.1), newest first. The sweep reads
the tests that arrived since the row above it.

| Date | Tests read (newest `net_logs.py` id) | Found | Fixed (commits on `Ys-bleeding-edge`) | Suggested tickets |
|---|---|---|---|---|
| 2026-10-25 | `shombith+gradi-20261025_175453` (newest), `gradi-20261025_003200` | **B35 holds**: Shombith joined Gradi's party, same config hash on both (`a437284610b6e8f1`), no refused join. **B31 much better**: 1 and 3 rate-limit failures in 310 and 200 reads (was 1,061 in 21-23 Oct), all in the first seconds of a party. No `stall`, no `relayDrop`, no Load Time Insights report yet (both builds predate the auto-record). The test ended with Gradi stopping Play (Shombith: "Host disconnected", a normal bounce). "No Player found to get domain!" x3 on Shombith's join: already fixed by the bug-hunt check (`fb2466b822`). Gradi's solo log is from `cece/optimistic-rubin-t46tzs`, before the pacer (25 429s, expected) | The identity republish's read before its save was not retried on a 429 (`LobbyPropertyWriter`, B31 addendum) | None (the B31 / B35 retest tickets stay open until a longer party with a match is logged) |
| 2026-10-25 | Started. Last test read by hand: the owner's and Caleb's / Gradi's 20-23 Oct tests (B31-B38) | - | - | - |
