# Ratel Raid Lab

A browser lab (`/labmaker`) for the second game proposed for the Ratel's seat of the Honeyguide: **hive mode** (round 1
called it the raid). A person plays it on the real Dynamo, and a headless scorecard measures what it does to the seat's
rhythm. Page: `Docs/Studios/HoneyguideRaidLab.html`. Live artifact: https://claude.ai/artifact/5ZGDVoET4mreTbvQ1YHrMo
(private to Garrett until shared from its Share menu; its own decision log).

**The question** (Garrett, 2026-10-29): the Ratel "is a fun puzzle but it needs something to break up the repetition of
pure pattern building ... you can't escape the time pressure of fiddling with your next three moves." Then, on the
proposal (`Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §3): "Ratel: lets have the ap cap go to 6. lets do the 3 ability pick as
well. those will go in for sure, but show me the raid in an artifact."

**Who else works here:** the Honeyguide and Ratel are Garrett's crew vessel
(`Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md`); its lab is the Honeyguide Lab
(`Docs/Studios/HoneyguideLab.html`, https://claude.ai/artifact/4B7oZwb5aYxFJR7XKyWx7v). This lab changes neither. It
copies the lab's Dynamo (the `dynamo-core` block, byte for byte apart from its header comment) and builds hive mode
around it.

## Graduated to Unity (2026-10-31)

Garrett: "this is good lets push these ratel changes to unity including the way the timer subtract from both sides to the
center." The page's hive rules now live in one pure block, `<script id="hive-core">` (`window.HiveCore`), moved out of the
main script with every random draw in the same order: `selfTest()` and the full `runBatch()` scorecard are byte-for-byte
the same before and after. `Assets/_Scripts/Controller/Vessel/Crew/HiveCore.cs` is its port, held to it by
`Tools/Build/hive_lab/parity.cjs` (PASS: 18,625 results alike over 72 played hives in 8 rule sets and all three domains,
300 crystal boards and 220 solver cases; 11 planted controls caught). What reached the game, and what did not, is in
`Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md` "Hive mode": the strip (omnicrystals only), hive mode on an
omnicrystal, the hive as real super-shielded prisms, chains, stings, shields, colour rewards on the planar crystal, the
AI, a rider, the centred bars. Not ported: the bees, the Mass-power hive. The Honeyguide Lab's spin bar now shrinks from
both sides too.

## Round 2b (2026-10-31): the restart, shields and danger

**Decision log, read at the start of the round:** empty.

Garrett, 2026-10-31:

> "the restart doesn't work. the solvable puzzle should always include some danger prisms, prisms of all three domains, shielded prisms in all three domains."

### The restart: what was broken, and why

Reproduced in headless Chromium by clicking (desktop) and tapping (phone width) the header buttons in every state:
building, mid-pick, in hive mode, after a hive, as You, after switching rules, and while paused.

- **The cause: the page froze the moment a hive ended.** `leaveHive` cleared the omnicrystal (`R.omni = null`) but left
  `R.target = 'omni'`. When the frame was drawn before the strip's next step, `drawStrip` read `R.omni.d` and threw
  ("Cannot read properties of null (reading 'd')"). The frame loop re-armed `requestAnimationFrame` only at its end,
  so one throw stopped the page for good. Restart then built a new world that nothing ever stepped or drew, so it
  looked like it did nothing. Reproduced on the published round 2: hive mode at 1.2 s, the AI clears the hive, the
  clock stops at 5.3 s with that error, and after Restart the clock stays at 0.
- **Two smaller faults in the same button:**
  - Restart rebuilt the **same seed**, so at the opening it changed nothing visible: as You, the board after Restart
    was byte-identical to the board before.
  - Restarting **while paused** built a world that stayed frozen at 0 s, with the button still reading "Play".
- **The fix:**
  - The target is cleared with its omnicrystal or hive (`enterHive`).
  - The strip draws a target only if it still exists.
  - The frame loop re-arms first, so one bad frame can never stop the page again.
  - Restart starts the **next seed's** match, unpaused, with the toast, the drag, the chain in hand and the frame
    accumulator cleared. The seed shows in the header.
  - **Hive now** also unpauses, so the hive can open.
  - **Shipped** restarts the same way.
- **Checked by clicking**, desktop and phone: Restart from every state above gives the next seed with the clock running.
  Pause holds and Play resumes; Hive now opens hive mode, while paused too; Shipped puts the sliders back. No page errors.
  `selfTest()` clicks Restart itself: from hive mode, paused, with a hive behind and a toast up, it must give the next
  seed, 0 s, building, no hive, an empty strip, unpaused, no toast, the next seed's own opening board (not the old one).
  It also draws a frame right after a hive ends, which must not throw.

### Read as (correct any of these)

1. **Every hive always holds** prisms of all three domains, at least one shielded prism in each of the three, and at
   least `hiveDangerMin` (2) danger prisms. The generator asserts it (`hiveHolds`) and regenerates when it fails.
2. **Shielded**, other domains: takes two passes. A chain through it strips the shield (the ring bursts) and leaves the
   prism; a later chain through it breaks it. **Shielded, our own domain**: the hive's armour (a faint ring). Our own
   cells are never chained, so armour is only seen. At least one own cell is armoured (`hiveArmour` 15%).
3. **Danger**: a hazard in the comb, in any domain. It is drawn as a dark cell with a red rim and spikes and a fleck of
   its domain's colour; that is a stand-in for the game's `DangerousBlockMaterial`, not its look. A chain that touches
   one **stings**: the chain breaks with nothing in it destroyed, and the thumb is stunned for `stingSeconds` (1 s). There
   is no AP cost. Danger is never raided and does not count toward completing a colour. Solvable means a solution
   exists that never touches danger.
4. **Solvable**: every non-danger cell of the two other domains can be cleared by chains of 3 or more through cells of
   one domain. Each chain takes one hit from each of its cells (shielded 2, plain 1), and a cell is gone at 0. The
   solver works on that hit state (memo on it). A cell with h hits sits in exactly h chains of any solution, so the
   order of chains never matters and the lowest-cell search is complete.
5. **The generator builds it constructively.** Each other colour starts with a **crossing**: two snakes of that colour
   sharing one cell, its shield (the first snake's chain strips it, the second clears it). More snakes follow, and a
   later snake crosses a plain cell of its colour with chance `hiveCross` (35%), making another shield. Danger goes on
   cells no snake runs through; then holes; then our own body, some of it armoured. The solver then proves the hive, and
   it is regenerated on a failure.
6. **Colour completion**: every non-danger cell of that colour cleared, shields included.
7. **The AIs**: the skilled AI plays the solver's plan and never stings. The rookie picks among the longest chains, cuts
   some short (stranding cells, shielded ones included), and with chance 15% slides on into a danger cell next to its
   chain's end. The scorecard counts stings per hive.
8. Round 2's chain rule (any touching cells of one other colour, at least 3) stands; round 2's "no shields, no
   stings" is withdrawn.

### Measured

Same call as round 2 (6 seeds × 180 s, skilled and rookie, the same seeds in every row; headless Chromium, about 1.7 s).
Round 6 and Breath are unchanged from round 2.

| Rules | Ratel | Patterns /min | Charges /min | Omni hives /min | Mass hives /min | Colours done /hive | Full clears | Stranded /hive | Stings /hive | Regenerated /hive | Hostile broken /min | Rewards on crystal | Rest share | In hive mode | Arc (15 s CV) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 | skilled | 4.56 | 4.72 | 0 | 0 | – | – | – | – | – | 0 | – | 12% | 0% | 0.19 |
| Round 6 | rookie | 3.00 | 3.17 | 0 | 0 | – | – | – | – | – | 0 | – | 11% | 0% | 0.16 |
| Breath | skilled | 5.22 | 5.22 | 0 | 0 | – | – | – | – | – | 0 | – | 8% | 0% | 0.27 |
| Breath | rookie | 2.95 | 2.95 | 0 | 0 | – | – | – | – | – | 0 | – | 10% | 0% | 0.20 |
| **Breath + Hive** | skilled | **9.94** | **15.1** | 0.95 | 0.67 | **2.00** | **100%** | **0** | **0** | 0.33 | 27.2 | 93% | 11% | 16% | **0.83** |
| **Breath + Hive** | rookie | 3.22 | 3.39 | 0.84 | 0.61 | 0.12 | 0% | 4.48 | 0 | 0.20 | 19.4 | 42% | 7% | 24% | 0.62 |
| Idle Ratel (control) | either | 0 | 0 | 0.73 | 0.61 | 0 | 0% | 18.53 | 0 | 0.21 | 0 | – | 68% | 32% | 0 |

- **The noise band** (seeds 1-3 against 4-6): skilled patterns 9.22 / 10.67, broken 24.8 / 29.7, arc 0.86 / 0.79;
  rookie patterns 2.78 / 3.67, colours 0.08 / 0.15, stranded 4.83 / 4.13, on the crystal 33% / 50% (few placements).
- **The other spin reading** (only stop the clock): skilled patterns 8.89, arc 0.79; rookie patterns 2.67, colours 0.24,
  stranded 4.08, **stings 0.13 a hive**.
- **The regeneration rate**, measured over 2000 generated hives: 234 regenerations, **11.7 per 100 hives**. 89% of hives
  come first time, 9.9% need one retry, 0.9% two, one hive in 2000 needed three, and none failed. Every regeneration had
  the same cause: a crossing snake with no room to grow to 3 (`makeHive(rnd, P, why)` counts reasons). **The solver
  rejected none.** Shields by crossings and danger off the snakes make a hive solvable by construction; the solver is
  the guard.

### Checked

- `verify_lab.cjs`: **PASS** at desktop 1600×900 and an iPhone 13 (no errors, fits, no sideways scroll, hooks, `SPEC`
  in range, the manual clock advances, `runBatch()` deterministic within a page and across loads, the stage drawn).
- `selfTest()`: **22 of 22**. New in 2b:
  - **Restart, clicked**: a new match (next seed, 0 s, building, no hive, empty strip, unpaused, no toast), and the
    next seed's board rather than the old opening;
  - a frame drawn right after a hive ends does not throw (the freeze);
  - **300 hives**, each with 3 domains, a shield in each and at least 2 danger, all solvable (24 regenerations);
  - the solver catches a cell reachable only through danger: a planted line of 5 whose second cell is danger. The same
    line with the danger cell made plain clears, so the danger flag is what decides;
  - the solver clears a planted crossing in two chains, both through its shield;
  - **a chain through danger stings**: it breaks, nothing in it loses a hit, the thumb is stunned (a chain start is
    refused while it lasts);
  - **a shield takes two passes**: the first chain leaves it standing with one hit while its plain cells break; the
    second breaks it, and the colour completes though its danger cell still stands.

  Kept from round 2: the arc yardstick, the star, the 2-chain and own-domain refusals, the rewards for one colour
  against both and how they land, `largestCrystal` on planted boards, determinism.
- The `dynamo-core` block is byte-identical to `HoneyguideLab.html`'s after its header comment.
- Screenshots read, portrait first: hive mode shows the shield rings, the armoured body, and two danger cells (red rim,
  spikes, a fleck of their domain). The strip is still thin, and the seed shows in the header.

### Found

1. **The freeze was the restart bug.** One unguarded read in the strip (`R.omni.d` after the omnicrystal was gone) threw
   on the first frame after any hive ended, and the frame loop, re-armed only at its end, never ran again. Every round 2
   hive that ended in the live page froze it. The scorecard never saw it: it runs headless and never draws.
2. **Restart used the same seed**, so even when the page ran, Restart replayed the same opening.
3. **The constructive generator never needs the solver to say no.** Of 2000 hives, 11.7% were laid again, all for room
   (a crossing that could not grow to 3), and none for solvability.
4. **Shields widen the rookie's cliff.** Colours completed per hive fall from 0.38 (round 2) to 0.12 and stranded cells
   rise from 3.3 to 4.5, because a shield needs a second chain through it and the rookie rarely plans one.
5. **Danger rarely stings an AI.** Two danger cells sit off every snake, so the skilled AI never touches one, and the
   rookie stings 0 to 0.13 times a hive. Danger here mostly shapes where a person's thumb can go. A phone will show
   more stings than the AI does.
6. **The skilled AI's rewards stay rich** (patterns 9.94 a minute against 10.17 in round 2, inside the noise): shields
   cost it time, not completions.

### Decision needed (designer)

- Whether danger should be **near** the snakes (one step off a chain's natural route) to make it bite. Today it is
  anywhere no snake runs.
- **The sting**: break the chain and a 1 s stun with no AP cost, or something harsher?
- **How many shields** (one crossing per colour plus `hiveCross` 35% more) and **how much danger** (`hiveDangerMin` 2).
- The earlier round 2 questions stand: the cliff (now steeper), the reward size, the spin reading, the timeout, the bees,
  invulnerability, the Mass power's cooldown.

## Round 2 (2026-10-31): hive mode (partly superseded by round 2b)

Round 2b withdrew "no shields, no stings" (Read as 4) and replaced the scorecard (Measured, below, is round 2's). The
rest of round 2 stands.


**Decision log, read at the start of the round:** empty. Garrett's notes came through the session, not the page.

Garrett, 2026-10-31:

> "Ratel: seeing a symbolic view (low fidelity) of the honeyguide gameplay that can run on old devices in a thin strip is great, even if it only reported distance to an omnicrystal. the omnicrystal transforms into a hive. the ratel and the hive are invulnerable until the ratel leaves hive mode, the ratel enters hivemode after the honeyguide gets an omnicrystal, and the ratel ends their current timer. The shortest chain should be 3. every hive should be solvable, which means all of the prisms of the other domains are raided. after the hive is left it should spawn bee fauna in the player's domain that protect it pull mass out of opposing fauna to bring back mass to grow the hive. the honeyguide's mass power is to produce one hive in a random location within the cytoplasm that the honeyguide can find which also begin as all 3 domains and is similar to finding an omnicrystal. this is distinguished in the ratel's view only in the symbolic view when they see the honeyguide approaching a hive instead of an omnicrystal. after the hive mode times out players can take their rewards back to the main puzzle without needing to select a boon. the prisms should not need to be placed manually afterward. if you look at the patterns they are all cut from a planar crystal that represents the only way a hex grid can be tiled with 3 colors. if you detect the largest planar crystal in their dynamo then you can add the new prisms to that planar crystal, which helps teach the player about that planar crystal pattern. if they complete a whole color then they get 2 prisms of that color in their dynamo and 1 in their battery. if they complete both colors they get the same rewards in the other color and their own."

### Read as (correct any of these)

1. **The strip** stays thin (44 to 60 px) and symbolic: plain canvas shapes and two cached strings, so it allocates
   nothing per frame. It shows the Honeyguide, its target and the distance to it in u. An **omnicrystal is a diamond**,
   a **Mass-power hive a hexagon**: the icon is the only place the two differ. Omnicrystals appear on a seeded schedule
   (`omniEverySeconds` 45, ±25%, once the last is taken). The Mass power makes one hive every `massHiveEverySeconds`
   (70) at a random distance (`targetNearU` 400 to `targetFarU` 1600 u). The stand-in pilot flies to the nearer one at
   `plSpeed` (60 u/s); the other one's distance waits. Both kinds of hive begin with all three domains and use the same
   generator.
2. **Hive mode starts on its own** when the Honeyguide reaches its target. Round 1's Hive card and the AI's "when to
   raid" policy are gone. If the Ratel is in the middle of a pick of three, hive mode waits until it picks.
   "Ends their current timer" is read as **the spin completes at once** (the ports turn and the spin's AP arrive); the
   clock then stays stopped through hive mode, and a fresh full spin starts on return. A rule option
   (`hiveEndsSpin`) offers the other reading, **only stop the clock**: the old spin then resumes where it was.
3. **Invulnerable**: the Honeyguide and the hive cannot be harmed while the Ratel is in hive mode. The lab has no
   damage model, so the strip shows it (a shield ring on both) and it is listed under Not modelled as a Unity rule.
   Round 1's "the pilot leaves the hive" ending is gone. A **Leave** button ends hive mode and keeps what was earned;
   the AI never leaves early.
4. **Chains**: a path through touching cells of ONE other domain, at least 3 long (`minChain` 3), no maximum. Our own
   domain (Jade) is the hive's body and refuses a chain. Round 1's shields, stings, super-shields and per-chain honey and
   AP are dropped: they fight solvability (a super-shielded cell splits a run; a sting makes cells unreachable), and the
   new rewards replace them.
5. **Every hive is solvable**: the generator lays the two other domains as **snakes** (simple paths of 3 to 5 cells;
   snakes of the same colour may touch) up to `hiveOtherShare` (45%) of the cells, leaves `hiveHoles` (2) empty, and
   fills the rest with our domain. A **solver** then proves every other-domain cell can be cleared by chains of 3 or
   more. Per colour and per connected group it takes the group's lowest cell, tries every simple path of 3 to 8 cells
   through it, removes it and recurses, memoised on the cells left. A hive that fails is regenerated. The skilled AI
   plays the solver's solution. The rookie picks among the four longest chains and cuts some short, so it can strand
   cells; the scorecard counts them.
6. **Rewards, automatic, no boon**: clearing every cell of one other colour pays 2 prisms of it on the Dynamo
   (`rewardDynamo`) and 1 charge of it in the battery (`rewardBattery`); clearing both other colours also pays the same in
   our own colour. They are shown in the hive panel as they are earned and **go back when hive mode ends** (cleared, timed
   out after `raidSeconds` 15, Leave, or no chain of 3 left), with no pick: round 1's feast is gone. The prisms **place
   themselves**.
7. **The planar crystal**: all 39 patterns are cut from the one 3-colouring of the hex grid, colour class
   `k = ((q − r) mod 3 + 3) mod 3`, and neighbours never share a class. A crystal is a colour permutation (prism colour =
   `perm[k]`); there are six. The **largest planar crystal** is, over the six, the largest connected set of board prisms
   whose colour is `perm[class]` (ties: the earlier perm, then the lower cell). A reward prism lands on an empty cell of
   its colour's class next to the crystal (nearest the centre, then lowest index), else on any empty cell of that class,
   else on any empty cell. Each landed prism joins the crystal, so the next one can sit beside it. While they land, the
   crystal's cells light, the rewards ring, and the empty cells touching the crystal show a ghost prism in the colour the
   crystal puts there, for `crystalGlowSeconds` (1.6 s). `__lab.largestCrystal(cells)` exposes it.
8. **After the hive is left** it stays in the strip as our hive, with `beesPerHive` (5) bees and a mass that grows as each
   bee brings `beeMassPerMinute` (4) back a minute. Symbolic only (Not modelled, Decision needed).
9. **The round 7 Breath** (AP bank 6, a pick of three every 4 patterns) stays as it is on the main puzzle.

### What the page is now

- The strip, hive mode and the hive panel replace round 1's Hive card, raid and feast. The build, the pick of three
  and the AI's build planner are unchanged.
- **Hive mode's panel** (where the cards sit) counts each other colour (cleared / total, "done" when complete), lists the
  rewards earned, and holds **Leave** for a thumb.
- **Header:** You / AI skilled / AI rookie, the rules (Round 6, Breath, Breath + Hive), **Hive now** (puts the omnicrystal
  under the pilot).
- **Settings:** Rules (and the spin option), Strip, Hive. The decision log's topics follow the notes.
- **Test surface:** `window.__lab` adds `hive(source)`, `largestCrystal`, `crystalSlot`, `solveHive`, `makeHive`,
  `solution()`, `CLASS` and a 15-check `selfTest()`.

### Measured (superseded by round 2b)

`window.__lab.runBatch({seeds:[1,2,3,4,5,6], seconds:180, skills:['skilled','rookie']})`, candidate settings, the same
seeds in every row (headless Chromium, about 1.9 s):

| Rules | Ratel | Patterns /min | Charges /min | Omni hives /min | Mass hives /min | Colours done /hive | Full clears | Stranded /hive | Hostile broken /min | Rewards on crystal | Rest share | In hive mode | Arc (15 s CV) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 (bank 3, random draw) | skilled | 4.56 | 4.72 | 0 | 0 | – | – | – | 0 | – | 12% | 0% | 0.19 |
| Round 6 | rookie | 3.00 | 3.17 | 0 | 0 | – | – | – | 0 | – | 11% | 0% | 0.16 |
| Breath (bank 6, pick of three) | skilled | 5.22 | 5.22 | 0 | 0 | – | – | – | 0 | – | 8% | 0% | 0.27 |
| Breath | rookie | 2.95 | 2.95 | 0 | 0 | – | – | – | 0 | – | 10% | 0% | 0.20 |
| **Breath + Hive** | skilled | **10.17** | **16.0** | 1.00 | 0.67 | **2.00** | **100%** | **0** | 30.9 | 95% | 11% | 15% | **0.80** |
| **Breath + Hive** | rookie | 3.83 | 4.39 | 0.95 | 0.67 | 0.38 | 0% | 3.27 | 22.6 | 79% | 9% | 25% | 0.57 |
| Idle Ratel (control) | either | 0 | 0 | 0.89 | 0.50 | 0 | 0% | 18.72 | 0 | – | 68% | 32% | 0 |

- AP wasted a minute is 0 in every AI row but Breath + Hive (0.11 skilled, 0.22 rookie: a completed spin can overflow
  the bank) and the idle control (10.22).
- **The noise band** (seeds 1-3 against 4-6): skilled patterns 9.34 / 11.0, charges 15 / 17, broken 31.2 / 30.7, on
  the crystal 96% / 93%, arc 0.82 / 0.78; rookie patterns 3.22 / 4.45, colours 0.32 / 0.43, stranded 3.60 / 2.93, on the
  crystal 100% / 58% (few placements, so wide).
- **The other spin reading** (`hiveEndsSpin` = only stop the clock), same seeds: skilled patterns 9.17 (against 10.17),
  charges 15.3, arc 0.75; rookie patterns 2.73 (against 3.83). Completing the spin is worth about one pattern a minute,
  just outside the noise.
- **The idle control** reads 0 everywhere a Ratel acts. Its hives time out after 15 s with every other-domain cell left
  (18.7 a hive), so it sits in hive mode 32% of the match.

**What it says:**
- **Hive mode keeps the arc round 1 found** (0.80 skilled against round 6's 0.19), now without a card to tap.
- **The rewards are rich.** A full clear pays 6 prisms and 3 charges. The skilled Ratel full-clears every hive (1.67 a
  minute), so its patterns more than double (4.56 → 10.17) and its charges more than triple (4.72 → 16.0). The battery
  holds 3, so many of those charges replace older ones.
- **The reward is a cliff.** The rookie breaks 22.6 hostile prisms a minute but completes only 0.38 colours a hive and
  strands 3.3 cells. It never full-clears, so it earns almost nothing, and its patterns barely move (3.00 → 3.83).
- **The crystal placement works where the board allows it**: 95% of the skilled AI's rewards land on the crystal, 79% of
  the rookie's (its board is messier, so the crystal's empty cells of the right class run out sooner).

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideRaidLab.html`: **PASS** at desktop 1600×900 and an
  iPhone 13: no console errors, the desktop fits, the phone does not scroll sideways, hooks present and not throwing,
  `SPEC` inside `SHIPPED` and its ranges, the manual clock advances, `runBatch()` deterministic within a page and across
  two loads, the stage not blank.
- `window.__lab.selfTest()`: **15 of 15**:
  - the arc reads 0 for a flat seat and 1 for an on/off one;
  - 300 generated hives are all solvable (0 regenerated);
  - the solver rejects a planted star (a centre with three leaves: every chain of 3 strands a leaf) and clears a line of 3;
  - a 2-chain is refused at `minChain` 3;
  - our own domain refuses a chain;
  - one colour done pays only its reward; both pay theirs and ours;
  - a full clear lands 6 prisms and 3 charges (the battery capped);
  - `largestCrystal` reads 0 on an empty board, finds a planted 3-cell crystal beside a wrong-coloured blob, and prefers a
    7-cell crystal over a touching 4-cell one of a perm sharing no class colour;
  - a reward lands on the crystal, in its class;
  - `runBatch` is deterministic.
- The `dynamo-core` block is byte-identical to `HoneyguideLab.html`'s after its header comment (checked by script).
- Screenshots read, portrait first: the strip flying to an omnicrystal (226 u), hive mode with a chain held after one
  colour was done, and the crystal lighting as the rewards land. Fixed from them: the crystal's next slots were dashed
  outlines that read as ports (now ghost prisms), the toast covered the hive's top row and, on desktop, the side panel
  (now just above the note row, centred on the board), the header still said round 1, and the spin option's labels
  clipped in their select.

### Found

1. **A snake hive is solvable by construction.** Each snake is a chain of 3 or more on its own, so clearing snake by
   snake always works. The solver confirmed 300 of 300 hives with no regeneration. It stays as the guard and as the
   skilled AI's plan; the planted star is what proves it can say no.
2. **The rookie strands cells by taking the long chain through a junction**, which cuts a group into pieces smaller than
   3. With the all-or-nothing reward that is the difference between earning a colour and earning nothing.
3. **Rewards at this size dominate the build.** Two full clears a minute double the pattern rate; the main puzzle risks
   becoming the place where hive rewards are spent.
4. **Completing the spin on entry is a free spin per hive**: its 3 AP can overflow a full bank of 6 (the first AP wasted
   in any non-idle row).
5. **No bug in the copied Dynamo.** Hive mode writes the Dynamo only through `D.tick` (the completed spin),
   `D.boardEffect('charge')` (reward charges) and `st.cells` (reward prisms on empty cells).

### Decision needed (designer)

- **Keep hive mode as played**, change it, or drop it: tap **You**, then **Hive now**, on a phone.
- **The cliff**: should a colour pay something before it is complete (per cell, or per chain), or is all-or-nothing the
  point?
- **The size of the rewards**: 2 prisms + 1 charge per colour, and the same in ours for both. The skilled AI's pattern
  rate more than doubles.
- **"Ends their current timer"**: complete the spin (default) or only stop the clock?
- **The hive mode timeout** (15 s) and **Leave** (keeps what was earned).
- **The bees** (in Unity this is ecology, below): their count, how they find opposing fauna, and how fast a hive grows.
  It needs the ecology rules (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`; mass is conserved, so the bees' mass is a
  transfer from opposing fauna, never minted) and whoever owns ecology.
- **Invulnerability** in Unity: what exactly cannot be harmed (the hull, the hive's prisms, both) and for how long.
- **The Mass power's cooldown** (70 s) against the omnicrystal's (45 s ±25%).

### Not modelled

- **The Honeyguide.** The pilot is a stand-in that flies a straight line to the nearer target and spends a charge about
  every 7 s. Omnicrystals and Mass-power hives come on seeded clocks, not from a real cytoplasm.
- **Invulnerability** (a Unity rule): the lab has no damage, so the shield rings only show it.
- **The bees and the hive left behind** (ecology in Unity): bee fauna of our domain that guard the hive and pull mass out
  of opposing fauna to grow it. Here a bee count and a mass that grows at a stand-in rate.
- **The pilot-side card effects.** Only the cards that change the Dynamo fire (as round 1).
- **A hive's cells** are generated snakes on our domain's body, not a projection of a real structure.
- **Sound, haptics, network play**, and what the Honeyguide sees while the Ratel is in hive mode.
- **The AI is a heuristic**: a person plays between the skilled and rookie rows, and the cliff hits them in between.
- **Lab §1.5's four element games**: this lab asks one question (a Space-kind destruction game) and has no Time, Mass or
  Charge games.

### Next

Garrett plays round 2 on a phone and records the decisions in the page. Round 3 then answers the cliff and the reward
size from that log. After that, hive mode moves into the Honeyguide Lab as a round and, through `/artifact-to-unity`,
into `DynamoCore.cs` and `HoneyguideDynamoView.cs`. In Unity the omnicrystal and the Mass power's hive are real, and the
bees join ecology.

## Round 1 (2026-10-29): the raid (superseded by round 2)

Kept as it was measured. Round 2 dropped the Hive card, the Ratel's choice of when to raid, shields, stings,
super-shields, per-chain honey and AP, and the feast.

### What the page is

- **The build is the real Dynamo.** The `dynamo-core` script block is copied line for line from the Honeyguide Lab's
  round 7 working copy (2026-10-29), with the round 7 Breath that Garrett decided: AP bank 6 (3 a spin, what is not
  spent is kept) and a pick of three abilities every 4 patterns, the clock stopped while the Ratel picks. The pattern
  cards, abilities, port engines and palette are the lab's tables, also copied. The numbers are
  `HoneyguideConfig.asset`'s plus the Breath. Only the cards that change the Dynamo itself fire (below).
- **A hive is found.** A stand-in Honeyguide flies a strip at the top. About every 40 s (seeded) it finds a hive (in the
  game: a hostile surface or volume, `PrismscapeTopology.DimensionOf`, that a Peck marks). The **Hive** card lights
  beside the three pattern cards for 20 s. The Ratel taps it when it chooses.
- **The raid.** The spin clock stops. The 37 cells fill with the hive's comb: patches of Ruby and Gold (hostile), a
  little of our own Jade (cannot be broken), holes, shielded cells (white rim, two passes), danger cells (red, they
  sting) and super-shielded cells (steel, never chained, never broken). The thumb drags a chain through touching cells
  of one colour, and letting go breaks every plain prism in it.
  - A chain that breaks 3 or more pays one **honey**: a free prism of its domain for the build.
  - Every 4 broken bank +1 AP, up to the bank.
  - A chain that breaks 6 or more sends a charge of its domain to the battery.
  - Dragging into a danger cell stings: the chain breaks and costs 1 AP.
  - The raid ends when the comb has no chain left, after 12 s, or when the stand-in pilot leaves the hive (it holds
    85% of raids).
- **The feast.** The clock stays stopped (up to 10 s). The honey rides the cursor until placed, and the Ratel picks one
  of three: a new ability, a new port engine, or a card swapped. Then the spin restarts.
- **The AI Ratel** plays the build with the Honeyguide Lab's planner (`aiPlan`, hard or easy) and raids with a
  longest-chain search. The rookie picks among the four best chains, cuts them short and sometimes slides into a
  danger cell. Its policy for when to raid is a setting: finish the pattern in hand (default) or go at once.
- **Every number** of the hive, raid and feast is a proposal, on a slider, with the candidate underlined when changed.
- **The decision log** is shared (the artifact's `decisions` collection), with a browser fallback.
- **Test surface:** `window.__lab` (`manualClock`, `tick`, `reset`, `score`, `runBatch`, `state`, `SHIPPED`, `SPEC`,
  `P`, `PLATFORM`, plus `input`, `hive`, `bestChains`, `selfTest`).

### Measured

`window.__lab.runBatch({seeds:[1,2,3,4,5,6], seconds:180, skills:['skilled','rookie']})`, candidate settings, the same
seeds in every row (run headless in Chromium, about 2.5 s):

| Rules | Ratel | Patterns /min | AP wasted /min | Charges /min | Hostile broken /min | Raids /min | Honey /min | Stings /min | Rest share | Clock stopped | Raiding | Actions /s | Arc (15 s CV) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 (bank 3, random draw) | skilled | 4.56 | 0 | 4.72 | 0 | 0 | 0 | 0 | 12% | 9% | 0% | 0.21 | 0.19 |
| Round 6 | rookie | 3.00 | 0 | 3.17 | 0 | 0 | 0 | 0 | 11% | 6% | 0% | 0.21 | 0.16 |
| Breath (bank 6, pick of three) | skilled | 5.22 | 0 | 5.22 | 0 | 0 | 0 | 0 | 8% | 5% | 0% | 0.28 | 0.27 |
| Breath | rookie | 2.95 | 0 | 2.95 | 0 | 0 | 0 | 0 | 10% | 4% | 0% | 0.24 | 0.20 |
| **Breath + Raid** | skilled | **6.83** | 0.28 | **8.67** | **28.9** | 1.33 | 2.78 | 0 | 12% | 10% | 15% | 0.99 | **0.87** |
| **Breath + Raid** | rookie | 3.50 | 0 | 4.78 | 13.2 | 1.17 | 1.34 | 0.06 | 13% | 9% | 19% | 0.59 | 0.63 |
| Idle Ratel (control) | either | 0 | 11.67 | 0 | 0 | 0 | 0 | 0 | 100% | 0% | 0% | 0 | 0 |

- **Rest share**: seconds holding AP with no move for 2 s, plus the seconds the clock is stopped (picks, feasts).
- **Arc**: actions a second in 15 s windows (one spin each); the spread of the windows over their mean. 0 means every
  spin looks the same; it is the number for "repetitive".
- **The noise band** (seeds 1-3 against 4-6, skilled): patterns /min ±0.6, arc ±0.1, broken /min ±2. So Breath's
  patterns gain (+0.7) is at the edge of the noise, and the raid's (+2.3) and its arc (+0.7) are well outside it.
- **The idle control** reads 0 everywhere except AP wasted (11.67 /min) and rest (100%), which are those columns'
  ceilings.

**What it says:**
- **The raid turns the flat line into an arc.** Round 6's arc is 0.19: every spin is the same burst then wait. The raid
  takes it to 0.87 (skilled) and 0.63 (rookie). The charts on the Scorecard tab show it: round 6 is a comb of equal
  spikes, the candidate has a tall peak each raid.
- **The raid feeds the build instead of taking time from it.** Patterns a minute rise 4.56 → 6.83 (+50%) with the clock
  stopped for 25% of the match (raids and feasts), because the honey (2.8 free prisms a minute) and the banked AP finish
  patterns. Charges rise 4.72 → 8.67, so the Honeyguide has more slips.
- **The squad gets something it did not have**: about 29 hostile prisms broken a minute by the skilled Ratel, 13 by the
  rookie. Today the Ratel breaks none.
- **Breath alone is a small change for the AI** (patterns +14% skilled, none for the rookie, arc 0.19 → 0.27). The AI
  never wastes AP under either bank, so this scorecard cannot measure the pressure Garrett named. Only a person can.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideRaidLab.html`: **PASS** at desktop 1600×900 and
  an iPhone 13. No console errors; the desktop fits the window; the phone does not scroll sideways; the hooks are
  present and do not throw; every `SPEC` key is in `SHIPPED` and inside its range; the manual clock advances;
  `runBatch()` is deterministic within a page and across two loads; the stage is not blank.
- Screenshots read at both sizes for the build, the pick of three, a raid with a chain held, and the feast. Fixed from
  them: the AI note covered the board's bottom row, the pattern shapes overlapped their card names, the toast covered
  the pilot strip, and the feast named a port engine by its index.
- `window.__lab.selfTest()`: 7 of 7, each a planted case. A flat seat reads arc 0 and an on/off seat arc 1. A
  super-shielded cell and a cell of the other domain are refused. A danger cell stings, breaks the chain and takes the
  AP. A shield takes the first pass while the plain prism beside it breaks. `runBatch` is deterministic.

### Found

1. **The first arc measure could not see the problem.** Over 5 s bins, round 6 scored 1.34 and the raid 1.30, because
   one spin is itself a burst then a wait. Measured per spin (15 s windows), the burst repeats inside every window, so
   a board that only builds reads flat (0.19) and the raid's peaks show (0.87). The page uses the 15 s measure.
2. **The AI does not feel time pressure.** It spends every AP under a bank of 3 or 6, so AP wasted is 0 in every AI row.
   Whether the bank of 6 relieves "fiddling with your next three moves" is a question for a phone in Garrett's hand.
3. **The AI picks at once, so its valleys are short.** The clock is stopped 5-10% of a match for the AI. The old reveal
   held 3.5 s; a person reading three abilities, or a feast, will hold it longer.
4. **A skilled raid clears the comb in about 7 s** (15% of the match raiding over 1.33 raids a minute), well inside the
   12 s cap. The cap matters for a person and for combs with many shields or stings.
5. **No bug in the copied Dynamo** turned up. The copy runs the lab's core unchanged; the raid sits outside it and
   writes the Dynamo only through `D.boardEffect` ('charge'), `st.free` (honey) and `st.ap` (banked AP, capped at the
   bank).

### Decision needed (designer)

- **Keep the raid, change it, or drop it**, after playing it as the Ratel on a phone (tap **You**, then **Find a
  hive**). Record it in the page's Decisions tab.
- **Chains**: drag through one colour (this page), or another gesture?
- **The pay**: honey from a chain of 3, +1 AP every 4, a charge from 6. Too rich? The raid lifts patterns 50%.
- **Stings**: break the chain and cost 1 AP, or something else?
- **The feast's three**: a new ability, new ports or a card swap. Right three?
- **Who decides when to raid**: the Ratel (this page), or does the pilot's arrival start it?

### Not modelled

- The Honeyguide. The pilot is a stand-in: it spends a charge about every 7 s, finds a hive on a seeded clock and holds
  it 85% of the time. Whether a real pilot finds and holds hives, and how a hive is chosen in the world, is not tested.
- The pilot-side card effects. Only the cards that change the Dynamo fire: Amanita Furnace, Big Red Button, Dragon
  Saddle, Zero Point Amplifier (its drain only), Triplesynth, Universal Translator, Anode Ray, 5D Printer, Square-Peg
  Hammer, Reactor Cilia, Chrono-Compressor and Vorticity Module. The 13 trigger upgrades and the passives do nothing.
- The comb is a seeded stand-in for a projection of real prisms, not a capture of a real structure.
- What the broken prisms are worth to the squad: counted, not scored.
- Sound, haptics, network play, and what the Honeyguide sees while the Ratel raids.
- The AI is a heuristic. A person plays differently; the scorecard shows the shape, and a phone decides.
- Lab §1.5's four element games: this lab asks one question (the raid, a Space-kind destruction game) and has no
  Time, Mass or Charge games.

### Next

Garrett plays it on a phone and records the decisions. If the raid stays, round 2 tunes the pay and the stings from
that log. Then it moves into the Honeyguide Lab as a round, and through `/artifact-to-unity` into `DynamoCore.cs`
and `HoneyguideDynamoView.cs`. In Unity, the hive is a real prismscape the Peck marks.
