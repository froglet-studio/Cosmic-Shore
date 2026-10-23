# Seats, then hulls: changing seat and vessel in freestyle

Garrett, 2026-10-23: *"when i am flying in freestyle with a multi seat vessel any other player in freestyle should be
able to press left or right on the dpad, or use a two finger swipe on mobile, to change seats so we are both controlling
different turrets on the orca or one of us could play the honeyguide and the other the ratel in the same vessel leaving
the AI to play the abandoned vessel and a seat that any player can occupy. furthermore if ai vessels are added to the
spawner then any player on the same domain can switch to those as well. there should be no difference between a vessel
spawned in and a vessel abandoned by switching."*

Code: `Assets/_Scripts/Controller/Multiplayer/CrewStations.cs` (the ring, the rules, the local driver, the crew relay),
`Player.cs` (`NetStation` and its RPCs), `PilotSwapGesture.cs` (the gesture and the two-finger swipe).

## What a person can do

| Control | Does |
|---|---|
| D-pad left / right, the left / right arrow keys, a two-finger swipe left / right | Step to the previous / next seat in the **domain's list of seats**: the seats of the hull you are on, then the next hull's. Every person on the domain cycles the same list in the same order; a seat another person holds is passed over, and every seat no person holds is flown by its AI |

The ring is every hull on your domain, ordered by vessel NetworkObjectId (every machine agrees), each expanded into its
seats (helm first). A step skips every station that is not free for you. Where hulls change hands - **freestyle**
(Menu_Main) and a **running arena match** - the ring holds every hull of the domain; anywhere else it is your own hull's
seats only, so a crew vessel still changes seats in every mode.

The keyboard mirrors the pad (Garrett, 2026-10-23: "the keyboard controls should mirror the others and just have a
consistent cycle left and right"): no key picks a seat outright any more (the Orca's 1-4 and the Wrasse's 1 / 2 are
gone, and the Orca's turret turns on A / D only, the arrows being the cycle). A **double tap on the small view** (Wrasse,
Goby, Honeyguide, Jacks) still swaps a crew's two seats on a phone. The arena's swap tiles (`PilotSwapIndicator`) still
swap hulls by a tap.

## One rule for every hull without a person at its helm

A hull is **abandoned** when its pilot is an AI (a Spawn Matrix companion, an arena backfill) or a person who sits on some
OTHER hull. Both are the same kind of seat:

- **Taking any seat of an abandoned hull** is the arena's `PilotSwap`: the two pilots exchange hulls. The hull you leave
  goes to whoever flew the one you took, and is abandoned in its turn. Nothing is spawned or destroyed, so a hull you left
  is exactly as free as a companion the spawner released. In freestyle the AI that takes a hull flies it the way a
  companion does (`CrewStations.ConfigureFreestyleAI`, the Spawn Matrix's own settings).
- **A free seat other than the helm, on a hull a person flies** (or sits aboard), is taken as a **rider**: your own hull is
  parked on its AI on your machine, your camera, occlusion corridor, speed tunnel, rear view and HUD follow the ridden
  hull, and its crew runs your seat. Only crews that say `HostsRiders` offer this (below).
- **A free helm on a hull whose pilot sits at one of its seats** (the Orca's helm while its pilot mans a gunner seat)
  is taken by the exchange too: you fly it, and its pilot rides their seat on, their own hull now the one you left.
- **Another person's seat** is never offered; a person's own crew skips a seat a rider holds.

Your own hull, parked while you ride, runs its crew as an **AI crew**, exactly like a hull an AI flies
(`CrewStations.IsParkedHere`).

## Which crews take riders

| Crew | Seats | A person on another machine can ride | How |
|---|---|---|---|
| Orca (`OrcaCrew`) | helm + 3 gunners | **yes**, any free gunner seat | The rider's seat runs on the rider's machine (`Mode.Rider`): their turret, gun, missiles and bomb on the hull's copy there. The hull's machine leaves that seat alone (`RunsHere`). |
| Honeyguide (`HoneyguideCrew`) | Honeyguide + Ratel | **yes**, the Ratel | The Dynamo stays on the hull's machine. The rider's board is a copy: its moves go to the hull's machine (`ICrewRelay.OnRiderInput`), which applies them and sends the board back 5 times a second and after every move (`DynamoCore.WriteState` / `ReadState`). The Ratel AI stands down. |
| Wrasse (`KiteNavigator`) | Grouper + Wrasse | not yet: while a person is aboard, the other seat is passed over | Its kite decides pops, blasts, clusters and boosts on one machine (`KITE_NAVIGATOR.md`, "Two humans, two seats", step 4) |
| Goby (`GobyPistolDuo`) | Goby + Pistol | not yet | The Pistol rides the Goby's plane on the hull's machine |
| Jacks (`JacksExecutor`) | fly + herd | no | One person flies or herds |

A crew joins by implementing `ICrewStations` (`SeatCount`, `LocalSeat`, `HostsRiders`, `TakeSeat`) and, when it hosts
riders, by running the rider's seat (`CrewStations.LocalRiderSeat`) and leaving it alone on the hull's machine
(`CrewStations.HeldByRemoteHuman`). A crew that runs a rider's seat on the hull's machine relays through `ICrewRelay`
(`CrewRelay.SendInput` / `SendState`, checked by the server).

## Authority

- `Player.NetStation` (`CrewStation`: hull id + seat; the default means "my own helm") is **server-write**.
- A move (`RequestStation_ServerRpc`) is re-checked against the server's state by the same test the ring uses
  (`CrewStations.Classify`), rate-limited like the arena swap. A refused move is a warning naming why.
- A seat on your own hull is decided on your machine (the crew moves you at once) and reported
  (`ReportOwnSeat_ServerRpc`), so other people see it taken.
- A station on a hull that is gone, or a seat it no longer has, is reset to the own helm by the server (4 times a
  second while riding).
- A menu that parks your hull (`CrewSeat.ReturnHumansToHull`: the app shell, the mode preview, the end-of-match
  flourish) also ends a ride: the person is sent home without this machine taking the autopilot or the pause back.
- Wire change: protocol version **12** (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`).

## Known limits (stated, not hidden)

- **A rider's effects are seen on the rider's machine.** An Orca rider's rounds hit prisms on their machine, as an AI
  gunner's do on the hull's machine today; nobody else sees the turret turn. Networking a crew's effects is each crew's
  follow-up (`ORCA_CREW.md`; `KITE_NAVIGATOR.md` work order steps 1 and 4).
- **The Ratel copy does not show the Honeyguide's trigger upgrades** (they are the pilot side's, not the board's).
- **A hull exchange starts the crew afresh**: a Honeyguide handed to its rider starts a new Dynamo.
- **Score follows the pilot**, as in the arena: the AI flying your parked hull earns for you.
- **The two-finger swipe** steers the hull for the moment the fingers are down (the touch strategy reads them too).
- Nothing here has been run in the Editor or on a device yet: the hand tests are on the team board.

## Hand tests (MPPM with two players, then a phone and a PC)

1. Freestyle, one player, a Jade AI Orca released from the Spawn Matrix: D-pad right steps Squirrel -> Orca helm ->
   gunners 1-3 -> back to the Squirrel; the Squirrel you left flies like a companion while you are in the Orca.
2. Two players: A flies the Orca, B steps into a gunner seat: B's camera and HUD follow the Orca, B aims and fires; A's
   AI gunner in that seat stops; B's own hull flies on its AI; A can step past B's seat but not into it.
3. While B rides A's Orca, A steps on to B's parked hull: A flies it. The Orca is handed to B as B's own hull; B stays
   in the gunner seat, and the Orca's helm flies on its AI (a third player can now take that helm).
4. Two players on the Honeyguide: A flies, B plays the Ratel; B's moves land on A's board and both see the same board;
   patterns B makes fire their effects on A's hull.
5. On a phone: a two-finger swipe steps seats and hulls; flying with two thumbs never fires it.
6. Leave freestyle while riding: the ride ends, the menu parks your own hull.
7. A flies the Orca and steps to a gunner seat (the helm goes to its AI); B steps onto the Orca's helm: B flies it, A
   stays in the gunner seat (now riding), and A's own hull is the one B left, flown by its AI.
8. Keyboard: the left / right arrows cycle exactly as the D-pad does, for both players, in the same order; keys 1-4 do
   nothing; the Orca's turret turns on A / D.
