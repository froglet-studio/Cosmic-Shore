# Honeyguide Lab

A `/labmaker` browser studio for the **Honeyguide and Ratel**, the two-seat crew vessel that carries *Slipstream* (the
tabletop game Garrett and friends designed and played from 2019 to 2022). The question it puts to the designer:

> Is the handoff fun? The **Ratel** (the Engineer seat) plays a hex pattern-building puzzle, the **Dynamo**, whose
> injection ports rotate every tick. Activating a pattern pulls one muton of the Engineer's choice into the battery.
> That muton's domain becomes a slip charge for the **Honeyguide** (the Pilot seat), and when the hull touches a trail
> of that domain it teleports to the end of the trail it is pointing at.

**The acceptance gate for phase 1:** Garrett plays the Engineer on a phone and records *Sign-off* ("this is fun") on
the Decisions tab. The Unity port (phase 2) starts only after that.

- **Source (the page is published from it):** `Docs/Studios/HoneyguideLab.html`. Open it in Chrome to use a gamepad
  (the claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/4B7oZwb5aYxFJR7XKyWx7v (private to the owner until shared; decisions recorded
  there land in the shared log Claude reads at the start of each round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideLab.html` (PASS, round 1).
- **Parity:** `node Tools/Build/dynamo_lab/parity.cjs` (PASS, round 1; see Checked).

Why a lab of its own and not a fourth round of the Crew Vessel Lab: that page is the Wrasse's signed-off and graduated
record with an open catch-up task (`T-20261018-3247`), and one decision log per vessel keeps each sign-off readable.
The Orca took the same route (`CrewOfFourLab.html`, `cece/happy-noether-m54hz6`). The shell (seats, swap, AI squad
mate, decision log, platform detection) is the Crew Vessel Lab's, rebuilt around a 2D arena.

## How to use it

- **Your seat** is in the header: **Engineer** (you play the Ratel; an AI flies), **Fly** (you fly; an AI plays the
  Dynamo), **Watch** (two AIs) or **Couch** (two people: the keyboard flies, the mouse or a finger plays the Dynamo).
  Swap with the D-pad left/right, 1 / 2, the Swap button, or a double-tap on the small view on a phone. It is the
  arena's gesture (`PilotSwapGesture`, `PilotSwap.CooldownSeconds` 0.75 s).
- **The Ratel (Engineer).** Drag a muton to move it (Tinker: one AP per cell). Tap an empty coloured port to Refuel
  (a muton of the port's colour). A slot card lights when its pattern is on the board: tap it to Activate, then tap one
  of the outlined mutons. That muton goes to the battery, and its colour is the domain the pilot can slip through. Tap
  the left of the AP row to Research (2 AP: draw 3 cards of one family, keep 1 in a slot).
- **The Honeyguide (Pilot).** A/D or ←/→ steer, W/Space boost (only while an ability has given boost), Shift or E fires
  a dash or swap token, F arms the slip. On touch, hold a finger on the flight view and the hull turns toward it; two
  fingers boost; the pads lower left are the button and arm. Fly into a trail of a domain you hold and you slip.
- **Rules tab:** the brief's nine open questions as dropdowns (each change restarts the match). **Cards tab:** all 39
  Slipstream upgrade cards with their printed shapes and what each does here.

## Round 1 (2026-10-20): the Dynamo, the Slip and the starter twelve

### The model

| Part | What the lab does | Source |
|---|---|---|
| Dynamo grid | 37 pointy-top cells, axial (q, r), r down, centre (0,0) | brief §3.2 |
| Injection ports | 2020-22 board: ring 2, clockwise from top-left M C C C S M M M C S S S; Heimdall-13, Claw and the 2021 co-op engine as variants | Appendix B |
| Rotation | every tick (8 s) each port steps one cell clockwise round its own ring; mutons stay. Variant: the whole engine turns 60° | brief §3.2 |
| Actions | 3 AP a tick, bank 3. Tinker, Refuel, Activate cost 1; Research 2 | the board; Research cost is a proposal |
| Matching | a translated copy of the card's printed shape (orientation fixed; variant: 6 rotations). First copy in a fixed scan order | Appendix C |
| Battery | 3 charges; a new one replaces the oldest | "LIMIT 3 MUTONS" |
| Pick | after Activate, 4 s to pick the battery muton, then the Dynamo picks one of the family colour | proposal |
| Domains | Mass→Ruby, Charge→Jade, Space-Time→Gold (a table; variant: own domain is always M). Mutons are drawn in their domain's colour | brief §3.2, palette from `studio-domains.js` |
| Pilot hull | the Sparrow's numbers: cruise 35 u/s, boost ×5 on the throttle term, yaw 80°/s, a prism every 7 u | `Sparrow.prefab`, as the Crew Vessel Lab |
| Own-trail grace | own prisms younger than 1 s are ignored | `SelfTrailContactConfig.hullGraceSeconds` |
| The Slip | contact with a prism of a held domain on a trail of at least 6 prisms; land 8 u past the end the nose points at more (larger dot product), aligned to the trail; reach cap 400 u (else the farthest block within reach); burn 1 charge; the trail just ridden is ignored for 1 s | brief §3.3 |
| Arena | a 420 u disc, three AI hulls laying Ruby, Gold and Jade trails (110 prisms each), and the Honeyguide racing from one destination ring to the next | lab |
| AIs | input-only. Engineer: picks the cheapest placement to complete (a breadth-first path for each missing cell), activates as soon as a slot lights, and picks the battery muton by scoring each domain's best slip toward the destination within reach. Pilot: flies for the destination, steers for a slip when one of a held domain gains enough, avoids hostile prisms (and, under always-armed, held-domain trails it does not want) | `/vessel-ai` |

**The starter twelve** (proposals, all in "Slipstream points": one point of boost = 0.5 s):

| Card | Here |
|---|---|
| Amanita Furnace (M) | burns one more muton of the pattern; 6 points of boost |
| Big Red Button (M) | destroys every other muton of the picked colour; 2 points of boost per muton |
| Dragon Saddle (M) | drains the battery into boost, 3 points a charge |
| Thruster Swarm (M) | four 24 u dashes on the pilot's button |
| Reality Detangler (C) | the next 3 prisms the hull touches turn the picked domain (new slip paths) |
| Epitaxial Condenser (C) | lays a chain of the picked domain ahead (6 prisms, the slip minimum, not the card's 5) |
| Anode Ray (C) | +1 charge of each colour |
| 5D Printer (C) | two free mutons, any colour, on any empty cell |
| Blink Drive (S) | a 60 u forward jump |
| Rosen Bridge (S) | teleport to the farthest prism of the picked domain in a ±35° cone within 300 u |
| Monte Cannon (S) | three swaps with a prism ahead within 120 u, on the pilot's button |
| Chrono-Compressor (S) | banks one extra Engineer action |

Every card holds one muton of each other colour, so every activation offers all three domains: the domain choice is
always real. The other 27 cards fire with no effect yet; the seven about sector tiles, planets or initiative are
parked (brief §3.4).

### Scorecard

`runBatch()` defaults: 6 seeds × 120 s at 30 Hz, AI Hard pilot. **Legs/min** is the race: destinations reached a
minute plus the share of the last leg flown. Every other column is about the handoff. Re-run with **Score every
variant** (about 4 s).

| Variant | Legs/min | Lift % | Slips/min | Mean slip (u) | Slip gain (u) | Bad slips % | Patterns/min | Waiting % | Hit rate % | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|
| Solo (no Engineer) | 3.97 | 0 | 0 | | | | 0 | 100 | | 0.16 |
| **Candidate · Hard Engineer** | **4.97** | **+25.2** | 4.33 | 253 | 151 | 5 | 6.17 | 9.6 | 70.8 | 0.15 |
| Candidate · Easy Engineer | 4.49 | +13.1 | 1.92 | 252 | 141 | 12 | 2.75 | 43.2 | 69.7 | 0.34 |
| Candidate · idle Engineer (control) | 3.97 | 0 | 0 | | | | 0 | 100 | | 0.16 |
| Slip off (abilities only) | 4.25 | +7.1 | 0 | | | | 6.58 | 7.4 | | 0.13 |
| Always armed (the brief's default) | 4.05 | +2.0 | 5.92 | 217 | 61 | 32 | 6.33 | 33.1 | 93.8 | 0.25 |
| Aim-gated arming | 4.61 | +16.1 | 3.67 | 227 | 159 | 10 | 6.83 | 9.4 | 54.0 | 0.37 |
| Whole engine turns | 5.15 | +29.7 | 2.83 | 229 | 158 | 6 | 3.33 | 52.8 | 84.5 | 0.27 |
| Rotatable patterns | 5.37 | +35.3 | 4.92 | 216 | 142 | 11 | 7.08 | 13.4 | 69.3 | 0.13 |
| Exit keeps facing | 4.73 | +19.1 | 4.75 | 238 | 112 | 20 | 6.67 | 17.2 | 72.5 | 0.16 |
| Tick 6 s | 5.70 | +43.6 | 5.42 | 249 | 133 | 13 | 9.00 | 6.1 | 59.9 | 0.31 |
| Tick 12 s | 4.67 | +17.6 | 3.50 | 267 | 167 | 16 | 4.50 | 18.7 | 78.8 | 0.30 |
| Battery 6 | 5.02 | +26.4 | 4.67 | 258 | 139 | 8 | 6.42 | 9.6 | 73.5 | 0.15 |
| Own domain is M | 5.43 | +36.8 | 4.50 | 264 | 190 | 5 | 6.50 | 11.4 | 69.2 | 0.36 |
| Heimdall-13 engine | 5.59 | +40.8 | 5.33 | 219 | 136 | 15 | 7.08 | 15.0 | 75.3 | 0.20 |
| Claw engine | 5.71 | +43.8 | 5.42 | 226 | 144 | 11 | 6.42 | 23.2 | 85.3 | 0.22 |
| Co-op engine | 5.56 | +40.1 | 5.08 | 238 | 145 | 9 | 6.83 | 14.1 | 75.0 | 0.23 |
| Loadout: Mass (boost) | 5.36 | +35.0 | 1.00 | 292 | 197 | 4 | 6.00 | 83.5 | 17.1 | 0.36 |
| Loadout: Space-Time | 5.73 | +44.3 | 4.75 | 217 | 112 | 21 | 7.25 | 11.6 | 65.3 | 0.17 |
| Loadout: Engineer | 5.24 | +32.0 | 5.42 | 221 | 130 | 18 | 8.75 | 17.1 | 48.0 | 0.17 |
| Loadout: Charge (trails) | 4.92 | +23.9 | 4.42 | 225 | 97 | 21 | 6.92 | 9.8 | 63.8 | 0.34 |

Columns not in the table are on the page: Aimed % (100 under hold- and aim-gated arming), Aimed gain, Clipped %,
Charges/min, Eng idle % (0 for every AI Engineer: it never lets a tick's AP go to waste) and Hostile hits/min.

**Noise.** The standard error of Legs/min is 0.13–0.37 on about 5, so read differences under about 15% as a plateau.

### Found

1. **Always-armed slips are worth almost nothing to the race (+2%), and hold-to-arm makes them worth +25%.** With
   every touch of a held domain's trail armed, about a third of the slips were brushes the pilot did not steer for, and
   a third of all slips carried the hull away from its destination (32% bad slips; mean gain 61 u against 151 u). The
   Hard pilot was taught to avoid unwanted held-domain trails the way it avoids hostile prisms, and that only brought
   the bad share down from 43% to 32%. Under hold-to-arm, every slip is one the pilot asked for (5% still land worse,
   when the pilot's read of which end it points at is off). Aim-gated arming, a lab proposal that needs no button (a
   slip fires only when one of the trail's ends lies within 45° of the nose), sits between them (+16%). The playtests of
   2020 called Slip "powerful" and easy to visualise; a slip that fires by accident would make the Engineer's choice
   look random. **The lab's candidate is hold-to-arm**, changed from the brief's default because of this.
   **Decision needed (Garrett).**
2. **The Slip carries the handoff.** The abilities alone (Slip off) lift the race 7%; the candidate lifts it 25%. An
   idle Engineer lifts it 0, exactly the solo hull (the control). An Easy Engineer, who completes a third as many
   patterns, still gets +13%: the handoff degrades gracefully with skill.
3. **The Engineer's domain choice lands about 70% of the time** (hit rate: charges slipped on, of charges made). The
   rest are replaced in a full battery or still held at the end. Under always-armed it reads 94%, but those charges were
   spent on brushes, not on choices.
4. **A faster tick is the strongest single lever** (6 s: +44%, 9 patterns a minute; 12 s: +18%). Time scales the tick
   in the four-ability proposal, so this is the Time element's reach. A tick of 8 s leaves the battery empty 10% of the
   time; 12 s, 19%.
5. **The other engines beat the 2020-22 board** (Heimdall-13 +41%, Claw +44%, co-op +40%, against +25%), at about the
   same patterns a minute, with SEs of about 0.2. The lab does not yet show why; one candidate is where each engine
   puts its starting mutons relative to the starter cards' shapes. **Open thread**, not a result.
6. **Rotatable patterns (+35%) and own-domain-is-M (+37%) both read above the candidate** by more than the noise. The
   first makes patterns easier; the second makes Ruby (most hostile trails here are not Jade) cheaper to reach. Both
   are design choices for Garrett, not tuning.
7. **The Mass loadout boosts instead of slipping**: Dragon Saddle drains the battery into boost (1 slip a minute,
   battery empty 84% of the time) and still reads +35%. Boost is a strong substitute for slips in a race; worth
   remembering when sizing boost-for-points.
8. **Exit keeps facing is worse than aligning to the trail** (+19%, and 20% bad slips): the hull lands pointing where
   it came from rather than along the trail it just rode.

**Fixed in the lab before any number was taken:**
- a whole-leg yardstick (destinations a minute) moved in steps of one destination, so the first table could not see a
  slip; the race now counts the share of the last leg flown, and every slip records its signed gain (L-STU-114);
- the AI pilot armed every contact; it now arms only when it is steering for a slip.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideLab.html`: PASS (no console errors, desktop
  1600×900 fits, the phone does not scroll sideways, `__lab` hooks, `SPEC` ⊂ `SHIPPED` within range, the manual clock,
  `runBatch` deterministic within a page and across loads, stage not blank).
- `node Tools/Build/dynamo_lab/parity.cjs`: PASS. 21,624 steps over 24 scenarios (four engines × ports or whole engine ×
  three rule sets, one with synthetic one- and two-cell patterns so several copies are on the board at once): 1,302
  activations, 385 picks, 1,134 ticks, 47 research offers and 4,278 illegal inputs refused alike, every digest and event
  equal. 400 slip destinations equal to 1e-9. Four planted controls were each caught: ports turning counter-clockwise,
  the placement scan reversed (only once the synthetic patterns were added: L-STU-115), a tick of 8.25 s, the slip
  taking the other end.
- Mid-game screenshots at 40 s (desktop 1600 × 900 and an iPhone 13) were read. On the phone the Dynamo takes the lower
  68% of the stage in the Engineer seat, and cells are 43 × 50 px; the first layout had 36 px cells and slot cards
  below the fold, both fixed.
- On the iPhone 13 viewport, a drag through the real pointer path moved a muton one cell (1 AP) and a tap on the
  emptied port refuelled it (1 AP), with no page errors.

### What round 1 does not model

- The world is a flat 2D disc, not the 3D cell; trails are lines of dots. No crystals, elements, petals or rivals to
  fight; a hostile prism only slows the hull for a second.
- The race between destination rings is the lab's yardstick for "a slip shortened the route"; no game mode.
- Trails never loop and have no holes, so `IsLoop` and `IsRidable` are not exercised.
- Time, Space, Mass and Charge scaling and the L5 upgrades are fixed numbers here (brief §3.5).
- 27 of the 39 cards have no effect yet; the twelve effects are proposals.
- Two players on one device only; no networking.
- The Engineer AI never researches. A real thumb tires and misses; the AIs only have speed, noise and reaction time.

### Decisions needed (Garrett), on the Decisions tab

1. **Sign-off:** is the Engineer seat fun on your phone? (the acceptance gate)
2. **Slip arming** (question 3): hold-to-arm (the lab's candidate), always armed (the brief) or aim-gated (Found 1).
3. **What rotates** (1): ports only, or the whole engine.
4. **Pattern orientation** (2): fixed as printed, or rotatable (Found 6).
5. **Exit facing** (4): align to the trail (Found 8).
6. **Domain map** (5): M→Ruby, C→Jade, S→Gold, or own domain is M (Found 6).
7. **Tick, AP, battery** (6): 8 s / 3 AP / 3 (Found 4).
8. **Research** (7): 2 AP mid-match, plus the loadout of 3 at launch.
9. **Starter set and loadout** (8): which three cards fill the slots at launch.
10. **Engine at launch** (9): the 2020-22 board, Heimdall-13, Claw or co-op (Found 5).

## The port, when signed off (phase 2)

- **`VesselClassType.Honeyguide = 21`.** 18 is the Goby's (`cece/sharp-tesla-j0kd7z`), 19 the Wrasse's and **20 the
  Orca's** (`cece/happy-noether-m54hz6`, in flight on 2026-10-20). The enum carries a comment reserving 21.
- **`DynamoCore.cs` is already in the game** (`Assets/_Scripts/Controller/Vessel/Crew/`), engine-free, and held to the
  page by the parity gate. `DynamoAI` (the page's `aiPlan`) is ported next, with the same gate extended.
- The generic duo-vessel system (`CrewSeat`, `/duo-vessel`, `cece/sharp-tesla-j0kd7z`) had not landed on 2026-10-20.
  If it has by phase 2, the Ratel seat builds on it instead of copying the Wrasse's seat plumbing.
- Everything else follows the brief's phase 2 and `/vessel` `CONTRACT.md` §1.

## Parity toolchain note

`trace.sh` needs a per-user .NET 8 under `$HOME/.dotnet`. Where `builds.dotnet.microsoft.com` is blocked (it was in this
session), the same layout can be assembled from `api.nuget.org/v3-flatcontainer`: `runtime.linux-x64.Microsoft.NETCore.DotNetHost`
(the `dotnet` host), `runtime.linux-x64.Microsoft.NETCore.DotNetHostResolver` (`host/fxr/<v>/libhostfxr.so`),
`Microsoft.NETCore.App.Runtime.linux-x64` (`shared/Microsoft.NETCore.App/<v>/`), `Microsoft.NETCore.App.Ref`
(`packs/Microsoft.NETCore.App.Ref/<v>/ref/net8.0`) and `Microsoft.Net.Compilers.Toolset`
(`sdk/<v>/Roslyn/bincore/csc.dll`). Both this gate and the Kite's pass on that layout.

## Publishing

The repo copy is the source. It is published as its own artifact with `capabilities: {db: {}, user: {}}` for the shared
decision log. Republish to the same URL so the log stays one log.
