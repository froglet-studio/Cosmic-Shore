# The arcade restructure: Mass-Space games and Party (Garrett, 2026-11-17)

The full plan is the Claude Doc **"Arcade restructure: cell duels and party games"**
(https://claude.ai/artifact/Qr8FMCmj3rUA7RnxReUc4Y). This page is the repo's copy of what is decided and what has
landed, so a session can find it without the doc.

## Garrett's words (2026-11-17)

"lets combine all mass games and space games into one filtered category in the arcade. systematically work your way
through vessels that have one and turn it into a duel for the cell match where players try to get fauna to spawn in
their domain. thus every mass-space game becomes the perfect training for brood rush. every charge game is training
for broadside, and every time game is training for regatta. then the remaining arcade games are not fixed to an
element (or two) they are labeled "party". scurry is the first that can be moved over. [...] creation and destruction
simply form a pair that should be scored and trained together, so it is fine if a vessel is poor at one as long as
they are great at the other."

## Decided

| # | Ruling (Garrett, 2026-11-17) |
|---|---|
| 1 | Four arcade categories: **Time** (trains for Regatta), **Charge** (Broadside), **Mass-Space** (Brood Rush), **Party** (nothing downstream). `ArcadeCategory`, answered by `ModeGenre.CategoryFor`. |
| 2 | Each vessel's Mass and Space games become ONE Mass-Space game: Brood Rush's rule (fauna born under your nucleus claim score your team) in an environment built for that vessel, learning from its old games' I1-I4 environments. Creation and destruction score as one pair. |
| 3 | **Teams, not duels.** Brood Rush and the Mass-Space games play in teams like the other arena and party games. Every game opens to **9 vessels, at most 3 per team**; multi-seat vessels raise player counts later. |
| 4 | **Party games have no rules or boundaries** (Shombith agrees), but every exception is explicit and, where possible, told to the player in the card's description (`PARTY RULES: ...`). |
| 5 | Party now: **Scurry**, **Scarab Scramble**, **Maelstrom** (`ModeGenre.IsParty`). **Tollway stays** and is a big part of Mass-Space play. |
| 6 | Retired cards and code are **deleted**, not parked; anything of value is flagged first. |
| 7 | First vessels: Dolphin (pilot), Scarab, then the rest of the Regatta ten without Stoat (Sparrow, Urchin, Squirrel, Swordfish, Brittle Star, Serpent, Tiger Moth). Stoat later, after its rework. |

| 8 | I4 in a Mass-Space game: the brood hatches as fierce Mass and Space creatures that graze the exterior and hunt, in the controller's one colour (option A). |
| 9 | Standing rule: every vessel has a parameter and an L5 upgrade for all four elements, plus an omnicrystal ability. |

## Landed

| Date | What |
|---|---|
| 2026-11-17 | Categories: `ArcadeCategory`, `ModeGenre.IsParty` / `CategoryFor`, the arcade filter (All, Time, Charge, Mass-Space, Party), party cards draw no petal, party descriptions say their rules. Party games keep their genre for their intensity ladder and analytics. `elemental_loop.py` no longer counts a Party game in a vessel's kit. |
