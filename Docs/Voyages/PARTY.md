# Voyages in a party

How a voyage sails with friends: a party of up to three players, the host leading and AI filling the seats nobody holds.
This covers both voyage kinds, the job voyage (Crystal Run, `VoyageRun`) and Escort the Ark (`ArkEscortRun`).

**Status (2026-11-16): built, never tested with two machines.** Every section below is code that compiles. None of it
has been flown by two people. The hand test is at the end.

The model is SLICE.md §5's "parallel worlds". Every machine sails its own Ark through its own copy of the worlds and
fauna. No Ark, cell or creature is a networked object. What makes it one voyage is what the host sends over three named
messages (`VoyageCourseLink`, NGO custom messaging):

| Message | Direction | Delivery | Carries |
|---|---|---|---|
| `cosmicshore.voyage.course` | host to all | reliable, fragmented, sequenced | `VoyageCourse` JSON: embark, each leg, the Crystal Run count, the end, the escort's sail and end |
| `cosmicshore.voyage.arkpose` | host to all | unreliable, sequenced | `VoyageArkPose` binary: leg, server time, position, rotation, velocity |
| `cosmicshore.voyage.report` | friend to host | reliable | `VoyagePartyReport` JSON: the friend's lobby seat, a crystal taken, a creature felled |

Protocol version 32 carries these (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`). A build on an older protocol cannot join.

## 1. Seats

The host's lobby seats the party. The host sits in the squad's `MySeat`, friends take the seats after it in client-id
order (`VoyageRules.FriendAt`), and AI fills the rest. `VoyageRules.SeatClients` turns that into a list of client ids, one
per seat, with -1 for an AI seat. It rides every course, so each friend knows their seat whenever they join.

A friend's own squad record is not changed. `VoyageRules.WithSeat` makes a copy with the friend's own slot (vessel,
element, pilot, artifact) moved into the seat they were given, and that copy is what the friend sails.

## 2. The same worlds, the same Ark

The host picks the voyage's seed. It decides the worlds, their modifiers, each world's hostile roll and the corridor's
headings. The host also sends its home pose (where its Ark set off). A friend seeds their corridor with the same seed
and places it at the host's home (`ArkwayRun.HomeOverride`), so every world stands in the same place on every screen.

Each machine starts its Ark when its own load veil drops, which is seconds apart. So the host sends its Ark's pose five
times a second (`VoyageArkSync`), stamped with the network clock. A friend's Ark is pulled toward that pose, predicted
forward by the message's age (`Ark.FollowPose`). Under 2 units nothing moves, so nothing jitters. Over 2500 units the
worlds disagree and the friend's Ark sails on its own. Poses older than 1.5 s, or from another leg, are ignored.

Satellite cells (the corridor's worlds) never network-spawn fauna: `CellLifeSpawnerBase` and `FaunaNetworkSync` leave
them local on every machine, and pacifying a world runs on every machine, not only the server.

## 3. The lobby seat

A friend's lobby choices (vessel, element, pilot, artifact) are sent to the host as a `Seat` report every 3 s while the
Ark page is open, and at once when they change (`VoyagePartySeats.Publish`). The host's lobby draws the friend's card
from it: pilot portrait and name, element petals and element name. The voyage's intro uses the same record to name each
friend's pilot.

## 4. One count

On a Crystal Run each friend takes crystals and fells creatures in their own world. Each take is counted on the friend's
machine at once (so it feels immediate) and reported to the host. The host adds it to the party's count
(`VoyageRaidTally`) and sends the whole count back four times a second while it changes. The host's count replaces the
friend's, so every objective box and scoreboard shows the same number, and the goal is met by the party, not by each
pilot alone.

An escort has no crystal goal. Friends report kills there too, so the host's scoreboard has a row per seat, and the host
sends those rows with the end.

## 5. One fate, and the boards

The host's end is everyone's.

- **Job voyage.** A fallen host Ark shows every friend the lost board. A docked host Ark docks every friend who is
  sailing home, and each banks what they carry. Any other end ends the friend's voyage, unless they are sailing home,
  in which case they dock on their own. A friend whose own Ark falls first is out of that leg and rejoins on the host's
  next leg with a fresh Ark. They sail their own corridor then, because the host's has moved on.
- **Escort.** A friend whose own Ark falls, is overrun or comes through first does not end. They hold, with a banner
  saying so, until the host's end arrives, then end with the host's outcome and see the host's rows. If the session
  drops, or the host says nothing for 90 s, the friend's own outcome stands.

A friend's boards offer no choice. The host decides Venture further, Finish or Try again, so a friend's board has one
button that closes it and keeps them flying, and a caption saying the host chooses. All of these words are authored
lines in `voyage_slice.json` (`ScoreHostChooses`, `ScoreKeepFlying`, `FollowerArkLost`, `EscortHoldFallen`,
`EscortHoldThrough`, `GuestStays`).

## 6. Leaving

The host leaving the voyage scene takes the party back to the menu, as before. A friend pressing leave gets a toast
(`GuestStays`) instead. Their controls are never locked by a menu return that only the host makes.

## Not done

- A job-voyage friend who rejoins on a later leg sails an unsynced corridor (section 5).
- The Ark pose sync has no tuning pass. The catch-up time and deadband were picked, not measured.
- Nothing here has run on two machines.

## Hand test (two machines)

1. Host and friend in one party. The friend picks a different vessel, element and pilot on the Ark page. The host's
   lobby shows the friend's pilot, element and vessel.
2. Start a Crystal Run. Both see the intro with both pilots named. The first world stands in the same place on both
   screens, and the two Arks stay together after the load veils drop.
3. Each takes crystals. Both objective boxes show the same total, and it reaches the goal together.
4. At the stop, the friend's board has only the keep-flying button. The host picks Venture further. The friend's board
   closes and both sail the next leg.
5. Let the host's Ark fall. The friend sees the lost board.
6. Start Escort the Ark. Both sail the same difficulty and worlds. Let the friend's Ark fall first: the friend holds.
   Then the host's end reaches the friend, with every seat's row on the board.
7. Drop the friend's connection mid-escort. The friend's own outcome stands within a frame.
