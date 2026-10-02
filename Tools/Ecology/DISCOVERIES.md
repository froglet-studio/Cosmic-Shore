# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

## Bestiary (Direction B, `cece/eco-bestiary`) — 2026-10-02

Eight threat species in `bestiary/species/`, each a struct-of-arrays population steered by a weighted sum of
local terms whose weights are continuous drives. No behaviour trees, no scripted sequences. Each one has
ablations (negative controls) that remove its key rule. Scored by the shared scorecard against wanderer,
evader, hunter and skimmer, plus two extra pilots (below), 3 seeds x 90 s, pilot trails on.
Files: `bestiary/scorecards.json` (with ablations), `bestiary/emotion.json`, `bestiary/bestiary.html` (2.3 MB,
`build_viewer.py`), `bestiary/README.md`. Mass conservation: max ledger drift 0.0 in every run.

### The table

| species | emotion (built for / outside probe read: hover · cruise · evade, threat 0-1) | counterplay | telegraph first strike (s) / unwarned | hits/min wander · skim | cp evader · aware | payoff kills/min | variety | agents · numpy us/agent-step |
|---|---|---|---|---|---|---|---|---|
| **pack** (encircle) | dread / terrifying · eerie · menacing, threat 0.50-0.63 | break the gap before the ring closes; change heading | **0.9** / 16% | 6.0 · 6.5 | 0.0 · 0.0 | 4.7 | 1.10 | 5.5 · 56 |
| **leech** (hitchhiker) | cute / cute · cute · cute (0.79-0.87), threat 0.05 | hard turn shakes them off; skirt the puddle | **2.0** / 14% | 49 · 24 (drains) | 0.0 · 0.11 | 2.4 | 1.76 | 63 · 17 |
| **locust** (phase change) | cute -> terrifying / sparse: cute 0.87 all viewers; after 55 s: terrifying · terrifying · cute, threat 0.03 -> 0.62 | leave before it tips; outrun the march; thin it | **0.9** / 9% | 39 · 116 | 0.0 · 0.07 | 21.3 | 1.00 | 200 · 64 |
| **stampede** (herd) | majestic / cute · cute · majestic, threat 0.16-0.25 | approach from outside alarm range; sidestep the bull | **0.7** / 33% | 10.9 · 5.3 | 0.0 · 0.10 | 3.8 | 1.06 | 71 · 44 |
| **lurker** (ambush) | eerie / cute · cute · cute, threat 0.29-0.44 | read the gape and swerve; strike first or while spent | **0.85** / 9% | 0.9 · 4.9 | 0.0 · 1.25 (noise, see below) | 6.2 | 2.0 | 15 · 104 |
| **leviathan** (assembled) | majestic / cute · majestic · majestic, threat 0.15-0.34 | keep clear; never linger at the tail; pick off loose units | **1.0** / 0% | 0.45 · 3.1 | 0.0 · 0.0 | 37.1 | 2.0 | 133 · 6 |
| **thief** (steals trail) | mischief / cute · cute · cute, threat 0.08-0.14 | turn back on laden thieves (recapture); weave | **1.5** / 22% | 39 (steals) · 89 | 0.27 · 0.76 | 5.6 | 1.46 | 17 · 32 |
| **mobber** (mobbing flock) | playful / cute · cute · cute, threat 0.00-0.08 | keep speed > 110 u/s; don't loiter at the roost | **1.15** / 0% | 6.2 · 70 (pecks) | 0.0 · 0.0 | 25.1 | 2.0 | 45 · 16 |

All eight pass all four gates (telegraph >= 0.7 s on the first strike, counterplay < 1, variety > 0,
payoff > 0). The feel profiles differ: z-scored over the six shared feel axes, the closest pair (locust vs
thief) is 1.14 apart, mean pair 3.46. On Direction C's threat axis the eight span 0.0 (mobber, sparse
locust) to 0.63 (pack), and the locust alone crosses most of it (0.03 -> 0.62) with no scripted switch.

### What the ablations say (each removes one rule; same 6 pilots x 3 seeds)

| ablation | result vs the full species |
|---|---|
| pack `chase` (chase the current position, no spread, no quorum, same stamina) | wanderer hits **0.22 vs 6.0 per min (27x fewer)**, first-strike telegraph 0.0, unwarned 86%. Geometry, not speed, is how a 95 u/s pack catches a 120 u/s pilot. |
| pack `noquorum` (spread + prediction, each hunter strikes alone) | same hit rate (6.0) but telegraph 0.4 s, unwarned 45%, aware counterplay 0.19 vs 0.0. The quorum adds no damage. It is what makes the attack READABLE and dodgeable. |
| leech `nogrip` (turning never shakes) | drains 164 vs 49 per min on the wanderer (3.4x): the shake-off is the whole counterplay. `nopounce`: wanderer hits 0. The hop is how a 12 u/s puddle reaches anything. |
| locust `solitary` (phase pinned 0) | **zero hits** from every pilot. `gregarious` (pinned 1): 53 vs 39 per min, telegraph 3.85 s. All the threat lives in the phase; the sparse swarm is genuinely harmless. |
| stampede `nobulls` | **zero hits**: the bull charge is the entire direct threat. `selfish` (flee straight away, no contagion, no herd-centre flee): the SAME hit rate (10.9) and a better telegraph (1.25 s). **Negative:** the herd-centre flee and alarm contagion added nothing measurable. They change how a stampede looks and where it tramples (env mass trampled 1266 vs less), not how often it hurts you. |
| lurker `nogape` | telegraph 0.2 s, unwarned 100%, and wanderer/skimmer hits 0 (the instant snap fires from too far and misses). The gape costs nothing; it gives the lurker its reach. `nocreep` (never moves while unwatched): wanderer hits 0. Creep is how an ambush predator finds a pilot that does not come to it. |
| leviathan `noassemble` | zero hits, payoff 93/min (a shoal is free crystals). `nogulp`: wanderer hits 0, skimmer 2.4. Assembly makes the threat; the gulp is what reaches a passing pilot. |
| thief `cold` (any trail, never tails a ship) | steals **10.9 vs 39 per min**, aware counterplay 0.55. Wanting only the warm wake (< 1.5 s) made thieves both more effective and more legible: they fly where you can see them. `bold` (never veers off): steals barely change (40), but payoff doubles (12/min). Timidity buys survival, not theft. |
| mobber `speedblind` | wanderer pecks 39 vs 6.2. The speed gate is what turns a swarm into a nuisance you outfly. `nodive`: zero hits and zero telegraph. |

### Findings

1. **Encirclement works, and its value is fairness rather than damage.** With the same stamina, the naive chase
   lands 27x fewer hits than the encircling pack. Prediction plus angular spread puts hunters ahead of your
   line. The quorum ("strike when the ring closes") adds no hits over solo strikes. It moves the telegraph
   from 0.4 to 0.9 s and lets an aware pilot escape every time (0.19 -> 0.0). Both halves are kept.
2. **One continuous drive gives the cute-to-terrifying range.** The locust is green, shy and harmless sparse,
   and the probe reads it cute at 0.87 from all three viewers. It is the same code after 55 s of breeding on
   food: the cloud tips at ~28 s (median), 82% gregarious at peak, and the probe reads terrifying (0.58 hover,
   0.40 cruise, threat 0.62). Ablating the drive removes the entire threat.
3. **A swarm breaks the shared telegraph metric.** The shared median credits every follow-up bite with the
   0.1 s since the previous one, so locust, stampede, thief and mobber all score 0.1-0.2 s. Scoring only the
   FIRST strike of each engagement (no hit in the previous 3 s) gives 0.7-1.5 s. `telegraph_first_s` is
   reported alongside the shared one, and the gate uses it.
4. **The shared evader is clairvoyant and fast,** so most species score counterplay exactly 0. Two extra
   pilots separate "fled well" from "saw everything": `evader120` (same speed as the wanderer) and `aware`
   (same speed, flees only threats within 350 u). Under `aware`, fleeing still fully counters pack, leviathan
   and mobber, mostly counters leech/locust/stampede (0.07-0.11), only partly counters thieves (0.76), and
   is no help against the lurker (1.25 over 3 seeds; 6 seeds give w 0.45/min, too rare to separate from
   noise). That is by design: a thief's counter is turning on it, and a lurker's is reading the gape. Fleeing
   is the wrong verb for both.
5. **A threat a non-engaging pilot never meets is no threat, and most first cuts were that.** Grazers, leech
   puddles, lurkers and the leviathan all scored 0 against the wanderer at first. What fixed each was a LOCAL
   reason to be where pilots go, never a director: herds follow trail scent upstream, leeches creep to
   fresh trail, lurkers creep toward your path only while you are not looking, the leviathan follows your
   lanes, thieves tail ships. Sparse encounters still leave variety saturated at 2.0 (disjoint hit
   histograms) for lurker, leviathan and mobber.
6. **The outside emotion probe is mostly a size detector on these species.** Small agents read cute almost
   regardless of behaviour (the mobber 0.77-0.99, thieves 0.61-0.75, the lurker 0.43-0.50 against an
   eerie target). It does separate the locust's two phases, the pack (threat 0.5-0.63) and the two giants
   (majestic from cruise and evade). Its threat coordinate orders the eight sensibly; its top label misses
   eerie and playful. I used it as feedback and did not tune to it. One round that did (pack: fewer, bigger,
   elongated hunters that stare at the predicted pilot) moved the pack from playful/majestic to the threat
   cluster without losing score. A per-hunter speed jitter tried in the same round cost the telegraph (0.9 -> 0.4
   s) and was reverted.
7. **Negatives kept:** herd-centre flee and contagion added no measurable threat (above). The
   `MAX_PER_HULL = 6` leech cap was needed: uncapped, a hunter pilot took 490 drains/min. Two state-machine
   bugs of one shape (`x <= 0 and x + dt > 0` is true at every frame x == 0) silently disabled the bull charge
   and the mobber's pull-up until found; transitions now latch on `was`. The lurker's feel is degenerate on the
   shared probe: its median speed is 0, so `burstiness` reads 3.5e7. That is its signature, not a bug, but
   the shared `feel` needs a floor for still species.

### Cost and the substrate (Direction A, `cece/eco-substrate`)

numpy timings are per step, single thread. They are Python-bound and only rank species. Every species is
O(neighbours) per agent with n <= 360 here; the all-pairs blocks become the substrate's spatial hash.

| species | per agent per step | on A's substrate today? | needs from A |
|---|---|---|---|
| pack | bearing push over packmates + ring slot + closure | **yes**: A's `pack` is the same design (ring ahead + quorum strike), arrived at independently | nothing |
| locust | density count + Vicsek + 1-in-4 food search | **yes**: A's `locust` regimes + quorum are this rule; its fused numba step is the port | nothing |
| leviathan | spring to a body slot + one composition controller per body | **yes**: A's `BodyPlan` (fixed slots, flat-bottom well) | a **posture clock** for the gulp |
| stampede | alarm max over neighbours + flee inheritance | mostly: alarm = A's alarm FIELD (deposit + gradient) | posture clock (bull head-down -> charge -> rest) |
| mobber | orbit spring + staggered dive | mostly: A's `w_ring`/comfort ring | posture clock (pull-up -> dive) |
| lurker | O(1) + seat search on relocation | partly | posture clock (gape), a **gaze sensor** (am I in a pilot's view cone) |
| leech | O(1) free; attached = a frame transform | no | **attach** to a moving frame (host hull) with a grip drive |
| thief | claim book + carry | no | **ownership**: claim (TryReserve) -> steal (owner switch) -> carry -> drop/recapture |

Four primitives would put the whole bestiary on A: (1) a **posture clock** (ramp -> act -> rest, the
intent channel *is* the ramp; it is the telegraph engine for five species); (2) **attach** (agent rides a
frame: a host hull or a carried prism); (3) **ownership** of prisms (claim, steal, carry, recapture; the
serpent wall `Steal` already exists in game); (4) a **gaze** sensor (the pilot's forward cone). None needs
per-agent branching beyond a clock compare.

Collider budget for a game port: zero per agent if members are GPU-instanced and pilot contact is a
`PrismSpatialIndex`-style query (the swarm round-7 shape). Exceptions: the leviathan's units are danger
prisms (in game: up to 140 body prisms with colliders while assembled), and every lifeform's heart crystal
(one always-on collider per individual; for the locust's cap of 360 that is the dominant cost, so locust
hearts should drop only on death, carried as data until then).

### Top 3 to port first

1. **Thieves.** Nothing in the game steals today, and the verb already exists: `Prism.Steal` from the
   serpent walls. A cell's thieves nest on a plant. When a ship comes within ~700 u they fall in behind it
   like gulls behind a trawler, snatch prisms from the last 1.5 s of its trail, and fly them home at half speed
   to a visible hoard. Nothing is destroyed, so the conserved-mass law holds trivially. The counterplay is
   the interesting part: fleeing barely helps (aware 0.76); turning back does, because a laden thief is slow
   and knocking it down returns the prism to you. The hoard grows into the fattest target in the cell (all
   your stolen mass in one place), which gives a natural objective for a raid. The port is cheap
   (17 agents, ~0.5 ms numpy, O(1) each), and the new platform piece is a prism claim book (`TryReserve`
   already exists) plus a carry transform.
2. **Pack hunters.** A pack of 5-7 long, staring hunters for dread. They are slower than you, and they
   predict your line and fan out ahead of it on a ring. When the ring closes (the quorum, visible as the
   pack tightening) they strike together, then back off winded. The measured value is legibility: the same
   pack without the quorum hits as often but telegraphs half as long and can't be escaped as cleanly. Its
   design converged independently with Direction A's `pack` parameter set, so it runs on the substrate with
   no new primitive. Bites map to Strike-class combat hits with an elemental steal; a winded hunter is the
   payoff window.
3. **Locust.** The program's emotion thesis in one species: shy and harmless sparse, a storm dense and
   hungry, with the probe confirming the flip (cute 0.87 -> terrifying 0.58). It is also the only species
   whose threat is created by the food web: the swarm's size is the food it found, so a cell with
   untended mass breeds its own storm. Players manage it the way the brief wants: leave before it tips, or
   thin it early while it is still cute. It needs A's substrate for scale (200-360 agents), which A has
   already built as its locust parameter set. Gate it on the heart-collider decision above.

Runner-up: the **lurker** (cheap, 16 agents; reuses crystal visuals as mimicry; the creep-while-unwatched
rule is the most distinctive mechanic here). It is the fourth port, after the posture clock lands.

### Where iteration stopped

Each species stopped once a further round moved no gate. The last rounds changed only the probe reading
(pack) or improved every axis at once (thief wake 2.0 -> 1.5 s: telegraph 1.1 -> 1.5, unwarned 42 -> 22%,
aware 1.19 -> 0.76). What would still move numbers: (a) the stampede's 33% unwarned first strikes are all
hunter-pilot engagements, where the nearest agent is a calf with no posture, not the charging bull. A
herd-wide posture would fix the metric, not the creature. (b) Lurker and leviathan meet a wanderer
< 1/min, so their variety and aware-counterplay need 6+ seeds or longer runs to mean anything.
