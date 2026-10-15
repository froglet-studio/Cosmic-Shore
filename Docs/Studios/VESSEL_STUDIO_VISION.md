# The Vessel Studio vision: anyone can make a vessel

**The owner's goal (2026-10-14), in their words:** a Vessel Studio so elegant that someone who has never programmed
(a storyteller, a musician, "my mom, my dad, my girlfriend") can be shown it once, press a few buttons, pick options
(at random, from presets, or from what was made before) and get a working vessel, then tweak it by voice. With it,
anyone can make **hundreds of vessels**. They can also **train each vessel's AI from their own play**, make the vessel's
**mini-games**, and **assign it to arena games** (arena games may get their own studio). In the end **Amoebius becomes
a game engine for people who are not programmers**.

This file is the north star every studio session reads first. `VESSEL_STUDIO_PLAN.md` is how the studio works today,
`/vessel-studio` holds its rules (D1-D38), and this file is where it is going and in what order. **Update §4 (the
status) in every change that moves a step**, and add what a step taught to §6.

## 1. What "done" looks like: one person, one afternoon

1. **Open the Vessel Studio**: the claude.ai artifact, Amoebius's VESSEL STUDIO page, or Unity's studio home. All three
   are the same studio (D37).
2. **Press +**. Choose how to start:
   - **Like one we have**: "a racer like the Squirrel";
   - **From a preset**: a named archetype;
   - **Roll the dice**: random, but seeded so the same roll can be made again;
   - **From a document**: a design note.
   
   Every choice shows a picture or a moving preview, never a code word.
3. **Name it** and press Create. A playable studio for the new vessel appears in **minutes**, not a coding session
   later: its hull, its four abilities over the four elements, its flight, and an AI that flies it.
4. **Fly it** on a phone or a PC. **Say what to change** ("turn faster", "the boost should feel heavier"). The studio
   shows the change as before → after, the person accepts it, and it is recorded as a decision.
5. **Teach its AI by playing.** Press Record, race a few laps, press Learn. The AI flies more like that person, at
   Easy, Medium and Hard. The scorecard shows how close it got.
6. **Give it games.** Pick one of the game's mini-games or make a new one from a template. The studio **recommends**
   arena games to assign it to; assigning is optional.
7. **Send it to the game.** The decided vessel is cloned into the real game (`/artifact-to-unity`), and **PLAY IN
   ENGINE** flies it in Amoebius. Nobody opens Unity unless they want to.

## 2. Rules that keep it simple and safe

- **Choose, never type code.** Every input is a picture, a dropdown, a slider, a dice or a voice line (D21, §1.5). Names
  are the only free text.
- **See it at once.** Every option has a preview. A change shows on the stage within a second.
- **Nothing breaks the game.** Every creation lives on its own branch and its own studio page until it is decided. The
  shipped game changes only through `/artifact-to-unity`, with its gates.
- **Undo is always there.** Every accepted change is a recorded decision that can be rolled back.
- **The artifact is the source; three surfaces show it** (D33, D37). The game is its clone.
- **Plain words.** No jargon on any screen. A number says what it does ("turn rate" → "how fast it turns").
- **Docs get better with every task** (§6). A future session must find the next step here without asking.

## 3. The building blocks

| # | Block | What it is | Builds on |
|---|---|---|---|
| B1 | **+ New studio** | The front door: vessel (fleet or new), what to do, mini-game (game's or new), voice, Create → Claude Code / an Amoebius AGENT chat | **Built** (D36, D37) |
| B2 | **Start from: like / preset / dice / document** | Options in the + panel. An archetype library (the fleet's own facts, plus presets), a seeded random roll, and a design-doc picker. Each one fills the form, which the person can still change | `studio-game.js` (the game's vessels and modes), `element_ability_table.py`, the fleet facts in `/arenagame` |
| B3 | **The vessel recipe and the instant studio** | A vessel as DATA: `vessels/<name>.json` holds the hull, four abilities over four elements, flight numbers, look and AI style. A generic studio page reads any recipe, so a new vessel is playable at once with no code. Claude then refines what the template cannot express. This is what makes hundreds possible | `squirrel.html` / the Stoat page as templates (§2 of the skill), `ai_race_panel.js`, `studio-look.js` |
| B4 | **Say it: voice tweaks in every studio** | A "Tell it" box on every stage: speech → Claude (`sample`) → a proposed change to the recipe's numbers, shown before → after → Accept records a decision | The + panel's Speak and "Fill the form from this"; the decision log (D33) |
| B5 | **Learn from me: the AI training harness** | Record the person's inputs plus what the AI sees, then fit the vessel's AI to them (imitation), per difficulty and play style. The existing evolution kit (`Assets/_Scripts/Utility/AITraining`: genomes, `FitnessProfileSO`, `TrainingEditorWindow`) tunes it further. The scorecard measures the result. The AI stays input-only (D4) | `/vessel-ai`, `Docs/AI_SYSTEM/`, the studio's `raceHeadless` / scorecard (D5), the game's `DecisionContext` |
| B6 | **Mini-game studio** | Pick a mode template (gate race, crystal race, ball, survival…) and its four-step intensity ladder, then name and tune it. The real mode is built by the `/arcadegame` recipe | `/arcadegame`, the course generators, `/cardart` |
| B7 | **Arena game studio and assignment** | Arena games get their own studio (mixed fleets). Each vessel gets a **recommended** assignment, with the reason (its speed, its abilities against shielded mass). Assigning is optional | `/arenagame`, `ArenaGames.asset` |
| B8 | **Amoebius, the home** | All of the above works inside Amoebius with no Unity: the served studio (D34), AGENT chats for the coding, PLAY IN ENGINE for the real vessel, and a phone build. Unity stays for the people who want it | `Port/`, `/amoebius` |
| B9 | **At scale** | A gallery of every vessel made, batch creation ("ten racers from these presets"), and automatic quality checks: a vessel whose AI cannot finish, or whose numbers break a rule, is flagged before anyone sees it | `parity_gate.py`, `check_studio.cjs`, the scorecards |

## 4. Status and order (update in every change that moves a step)

| Step | Status (date) | Done when |
|---|---|---|
| B1 + New studio on all three surfaces | **Done** 2026-10-14 | the card on the hub, Amoebius and Unity; Create reaches Claude Code or an AGENT chat (`parity_gate.py`, `StudioServerTests`) |
| B2 Start from like / preset / dice / doc | next | the + panel offers the four; a dice roll is reproducible from its seed; the form stays editable |
| B3 Vessel recipe + instant studio | after B2 (the big one) | a recipe JSON opens as a playable studio with an AI on Easy/Medium/Hard, with no code written; `check_studio.cjs` passes it |
| B4 Voice tweaks in every studio | after B3 | a spoken line becomes a before → after on the recipe; Accept records a decision; `/artifact-to-unity` can clone it |
| B5 Learn from me | after B3 (can run beside B4) | Record + Learn in a studio makes the AI's line measurably closer to the person's (scorecard column), still input-only |
| B6 Mini-game studio | after B3 | a templated mode is playable in the studio, then built for real by `/arcadegame` |
| B7 Arena studio + assignment | after B6 | every vessel shows recommended arenas with a reason; an assignment lands in the arena roster |
| B8 Amoebius-only workflow | throughout | each step above works with Unity closed |
| B9 Scale | last | ten vessels from presets in one go, each passing the quality checks |

## 5. Who does what

- **The person**: chooses, plays, speaks, decides.
- **The studio page**: shows, previews, records decisions. It never writes code.
- **Claude Code / Amoebius AGENT chats**: everything that needs code. They start from the + panel or a request, follow
  `/vessel-studio`, `/vessel`, `/vessel-ai`, `/arcadegame` and `/arenagame`, and work on their own branch.
- **The gates**: catch what nobody should have to look for (`parity_gate.py`, `check_studio.cjs`,
  `studio_to_unity.py --check`, the repo's C# checks).

## 6. What each step taught (append; newest last)

- 2026-10-14, B1: a feature on one surface reads as missing on the others. The + card reached the artifact and Unity
  first, and the owner looked for it in Amoebius. Hence D37 and a gate that fails a native home without a hub card. The
  first version of Amoebius's stand-in for Claude Code ignored the request's prompt and still answered "sent", so a
  backend stand-in must do the real thing or say it cannot.
- 2026-10-15, toward B2 (start from a document): the Shrike studio was built from its Notion class document in one
  session. What a document gives is WORDS ("flies incredibly slowly", "a moderate cooldown"), so every number is a
  proposal the page must label, and two kinds of rule sit above any document: the LOCKED project rules (PvP is petals
  only retired half of the Shrike's on-hit effects) and readings the document leaves open (one salvo per paint, a cup
  nest), each listed for the designer. A document start needs those three lists on screen: quoted words, proposals,
  overridden or open readings (`Docs/Studios/SHRIKE.md` §2, §3, §6).
