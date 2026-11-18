# Proposed

The plan with proposed changes. Every hole of the plan has a fill, and every fill is an invention until the team accepts it.

## Era (2 wk to 6 mo)

What changes without you: the living sea, sagas, the meta, new vessels.

| Loop | One pass | Passes before stepping up | State | Note |
|---|---|---|---|---|
| **The long game** | 2 wk to 6 mo |  | changed | Give the last phase a name and a clock: the Tide (see The Tide). |
| **The living sea** | 1 d to 3 mo | x1 |  |  |
| **The meta turns** | 4 wk to 4 mo | not set | changed | Tie each turn to a Tide so that players can see it coming. |
| **The Tide** | 7 d to 6 wk | x6 | NEW | Proposal P6. It gives the long game's empty last phase a rhythm built from systems that already tick. |

## Arc (1 d to 4 wk)

What changes between sittings: unlocks, the fleet, the squad, the crew, the hoard.

| Loop | One pass | Passes before stepping up | State | Note |
|---|---|---|---|---|
| **Activation** | 10 min to 2 h | x1 |  |  |
| **The quest chain** | 60 min to 2 wk | x4 |  |  |
| **The fleet** | 1 d to 4 wk | x20 | changed | Crystals get a second sink so the loop does not end with the last vessel. |
| **The fleet's kits** | 1 d to 4 wk | not set |  |  |
| **The vessel's flower** | 60 min to 7 d | x3 | NEW | Proposal P8. It gives every card an order, makes 'arcade trains for arena' visible to the player, and reuses the petal shape the HUD already teaches. |
| **The squad book** | 1 d to 4 wk | not set |  |  |
| **Crew and standing** | 3 d to 2 mo | x5 |  |  |
| **The hoard** | 1 d to 2 mo | not set |  |  |
| **A saga** | 3 d to 6 wk | x3 |  |  |
| **Party and friends** | 10 min to 7 d | not set | changed | A crew vessel's empty seat is the invite: 'fly the kite on my Wrasse'. |
| **Episodes** | 4 wk to 6 mo | not set |  |  |

## Session (10 min to 45 min)

A sitting in one place: a ladder run, an arena set, a voyage.

| Loop | One pass | Passes before stepping up | State | Note |
|---|---|---|---|---|
| **A sitting** | 10 min to 45 min | not set | changed | Add the breath between rounds so that crossing to another place is offered, never left to the menu. |
| **Toybox** | 5 min to 30 min | not set | changed | Each toy ends by offering its scored cousin: the Vessel Changer offers the hull's Time game, the Arkway offers a voyage. |
| **Wanderway** | 5 min to 20 min | not set |  |  |
| **Arkway run** | 5 min to 25 min | not set |  |  |
| **Arcade** | 5 min to 25 min | not set | changed | The result screen says what the round trained, and lights a petal on the vessel's flower. |
| **The rung ladder** | 10 min to 2 h | x4 |  |  |
| **Arena** | 10 min to 40 min | not set | changed | An arena match offers the voyage that squad is ready for. |
| **A voyage** | 20 min to 40 min | x1 | changed | Docking offers the Hangar when the pay covers a vessel, and the next saga chapter when one opened. |
| **Replays** | 60 s to 10 min | not set | changed | The breath offers 'watch the pass that beat you': a ten-second cut, not the whole match. |

## Round (60 s to 6 min)

One objective, won or lost: an arcade or arena match, or a cell on a voyage.

| Loop | One pass | Passes before stepping up | State | Note |
|---|---|---|---|---|
| **Free flight** | 30 s to 10 min | not set |  |  |
| **Painting** | 2 min to 10 min | not set |  |  |
| **Time game** | 60 s to 2 min | not set |  |  |
| **Charge game** | 60 s to 3 min | not set |  |  |
| **Mass-Space game** | 2 min to 5 min | x3 |  |  |
| **Party game** | 60 s to 5 min | not set |  |  |
| **Regatta** | 2 min to 5 min | not set |  |  |
| **Broadside** | 2 min to 5 min | not set |  |  |
| **Brood Rush** | 2 min to 6 min | x3 |  |  |
| **Astro League** | 2 min to 6 min | not set |  |  |
| **Port** | 60 s to 5 min | not set |  |  |
| **A leg** | 2 min to 5 min | x3-6 |  |  |
| **A cell** | 45 s to 3 min | not set |  |  |
| **A location** | 2 min to 8 min | x1 |  |  |
| **A meeting** | 2 min to 8 min | not set |  |  |

## Role (20 s to 3 min)

The kit pointed at the game's goals: what this vessel does for its team.

| Loop | One pass | Passes before stepping up | State | Note |
|---|---|---|---|---|
| **The breath** | 8 s to 30 s | x1 | NEW | Proposal P5. The scoreboard already knows the placing and the volume books; this adds the hand-off. |
| **The changers** | 5 s to 60 s | not set | changed | After a few minutes in a hull you do not own, offer its Time game at I1. |
| **Spawn Matrix and Cell Selector** | 10 s to 2 min | not set |  |  |
| **The way home** | 30 s to 5 min | x1 |  |  |

## Role, Combo, Verb and Pulse

These tiers are per vessel. Open the Loop Atlas card under Components, pick a vessel, and zoom into any Round. The same data as text: `assets/Model/loop-atlas.json`, key `fleet`.

## Holes

| Loop | What is missing | Proposed |
|---|---|---|
| The long game | How many sittings make one pass, and how often a person comes back, is not measured. Analytics events are declared but no funnel reads them yet. | Target 3 to 5 sittings a week and one Tide every 6 weeks. Measure it from game_started and game_completed per account before tuning anything above the Round. |
| A sitting | Nothing in the game points a player from one place to another. A crossing happens only if the player opens Home and thinks of it. | The breath (new loop): after each result, one offered next step chosen from what the round just trained. |
| A sitting | Rounds per sitting and places per sitting are not measured. | Aim for 5 to 8 rounds and 1 or 2 places in a sitting; read it from the analytics events. |
| Toybox | What a toy session is for, in loop terms, is not written: there is no stated reason to leave a toy or to come back to one. | A toy's exit is a crossing. Every toy names the scored loop it is the free version of, and offers it when the player has done the toy's verb a few times. |
| Arcade | Rounds per run are not measured, and nothing but the player's own wish moves them from a card to the next card. | 3 to 5 rounds a run. The breath offers the next rung after a win and the same rung after a loss. |
| Arena | Matches per set, and what makes a squad stay for another, are not written. | Best of three against the same opponents, hulls re-drafted between matches, so the set is a loop of its own. |
| Astro League | The plan names three arena games. Where Astro League sits (a fourth arena, or an arena-sized party game) is not said. | Call it the Arena's party game: it balances nothing and is kept for fellowship, the way party cards are in the Arcade. |
| A voyage | It is not said whether voyage pay is the same crystals that buy vessels, so the step from 'voyage ends' to 'spend it on a vessel' has no confirmed path. | One soft currency. Voyage pay is crystals, paid at dock, scaled so a clean voyage is worth about three arcade wins. |
| A cell | Cells per leg is not set. | 2 to 4 cells a leg, more on a rough lane, so a leg stays inside 2 to 5 minutes at 45 to 90 seconds a cell. |
| Activation | The Lesson teaches a ship's Time verb. Nothing yet teaches a combo or a role. | The Mentor's stream is ordered by the ladder of tiers: Pulse and Verb in the Lesson, the kit's first Combo after three races, the Role the first time the player enters a Mass-Space game. |
| The quest chain | The chain orders 4 cards. About 80 others have no order, so after the Hangar opens nothing says what to play next. | After the Hangar, the chain is per vessel: each hull's flower (see The vessel's flower) is its own short chain of three element games. |
| The fleet | Crystals buy only vessels. When the fleet is owned the reward has no use, and the payout stops driving anything. | Three more sinks: refitting the Ark (hold and plates), recruiting a pilot at port, and colour sets. |
| The fleet | Which of the 23 hulls ship, at what price and in what order, is not decided beyond the first five. | Price by how many loops a hull opens: a solo hull 4000, a crew vessel 6000 because it opens a second seat for a friend. |
| A saga | A saga does not run again, so four sagas are a fixed amount of story. 'A season could reseed it' is a default, not a decision. | Each Tide reseeds one saga and adds one, so there is always an unfinished thread. |
| Party and friends | Nothing a party does together lasts past the sitting, so a friend is a reason to play tonight and not a reason to return. | A party shares one voyage record: the Ark, its hoard and the saga thread belong to the party that sailed it. |
| Replays | A replay cannot leave the device, so it drives mastery and nothing else. | Share a ten-second cut as a link that opens the same card at the same rung for whoever taps it. |
| Episodes | What an episode gives the player is not defined in loop terms: it opens no loop on this map. | An episode is a Tide's content, sold once the Tide has run free for its season: its saga, its arena environments, its colour set. |
| The meta turns | The schedule is not set, and nothing tells a player that the meta turned or why their vessel flies differently. | One turn per Tide, six weeks. The Tide's news says in authored words which vessels changed and which arena environments are new. |

## The proposals

| # | Proposal | What changes |
|---|---|---|
| P1 | **One ladder of words** | Use the eight tier terms (Pulse, Verb, Combo, Role, Round, Session, Arc, Era) in every design doc, ticket and review, so a problem is placed before it is argued. |
| P2 | **Every verb names its feeling** | Each ability's doc gets its loop as steps and one sentence: the feeling, and the step where it lands. The fun card asks about that step. |
| P3 | **Every kit names two combos** | A feeder into a spender, and one that crosses two elements. Ability icons and the Mentor teach them; a game's course is checked against them. |
| P4 | **Show the role** | The two volume numbers the scoreboard already books become one live meter, and the vessel's measured lean goes on its Hangar card. |
| P5 | **The breath between rounds** | After every result, one offered next step: rematch, next rung, or a crossing to the place the round trained for. |
| P6 | **The Tide** | A daily job, a weekly card and a six-week season, built from the living sea's tick and the studio's rebalance. |
| P7 | **More to spend crystals on** | Ark refits, recruiting pilots and colour sets, so the payout still drives something after the last vessel. |
| P8 | **The vessel's flower** | Three petals per hull, lit by clearing its Time, Charge and Mass-Space games at I3. The player's own pass through the elemental loop. |
| P9 | **Set the counts** | Cells per leg, rounds per run, matches per set, passes per round: each gets a target number and an analytics event that measures it. |
| P10 | **One currency** | Voyage pay is the same crystals as arcade pay, so 'the voyage ended, buy a vessel' is a real path. |
| P11 | **A party owns something** | A party shares one voyage record, and a crew vessel's empty seat is an invite. |
| P12 | **A replay can travel** | A ten-second cut shared as a link that opens the same card at the same rung. |
