# Swarm cell play guide (the demo cell)

**For Garrett, morning of the first editor session.** Nothing below has run in Unity: every behaviour is from the
headless harnesses and the docs they back. Each fact is quoted from `Docs/SWARM_FAUNA.md` §18-§28,
`SUBSTRATE_FAUNA.md`, `THREAT_FLORA.md`, `BUILDERS_AND_THIEVES.md`, `ECOLOGY_LOD.md`, `ELEMENTAL_ECONOMY.md` §4.1,
the `QA-SWARM-ROUND11-*` entries or the `author_*.py` models. **Map:** `/mnt/project-files/overnight/cell-map.png`
(redraw with `Tools/Build/showcase_cell_harness/cell_map.py`).

Numbers are the shipped values after the round-11-14 balance pass (SWARM_FAUNA.md §26.6).

---

## 1. Before you play: the editor checks, in the order things would break

Each row is one check. If it fails, use the fallback switch and keep playing. Every switch is an inspector field
on a committed asset.

| # | What could break first | Check (Console / inspector) | Fallback switch if it misbehaves |
|---|---|---|---|
| 1 | **The compile.** The real-reference compile caught two errors from tonight and fixed both: `Random` was ambiguous in SwarmFauna/ThreatGrove/BuilderColonyFauna, and `EntityManager.GetComponentLookup` is internal (it now goes through `PrismRenderLookupSystem`). | No CS errors on open. In Play, the PrismRenderService status line reads `ON · ents=…`. Prisms appear where they are created and follow their owners across a Play restart (QA-COMPILE-ROUND11H-1). | `Assets/Resources/PrismRenderConfig.asset` ▸ **Use Instanced Rendering** off. Every prism returns to the legacy MeshRenderer path, and swarm bodies drop to the round-7 instanced draw. |
| 2 | **Unified prism bodies** (round 11a). Swarm members are prism entities plus spatial-index entries. | There must be no `[Swarm] … member bodies fall back to the instanced draw` warning. Members look like prisms of their tier. They can be hit beyond 160 u, and the ladder does not jump when proxies appear (QA-SWARM-ROUND11-1). | `Assets/_SO_Assets/Swarm Fauna/SwarmSortFaunaConfig.asset` ▸ **Unified Prism Bodies** off. The round-7 instanced draw returns, and weapons and predators still work. |
| 3 | **Burst jobs.** Burst has never compiled any of them: `SubstrateAgentJob` (all seven substrate species), `SwarmPoseJob`, `ResolveHandlesJob`, `UpdatePositionsJob`. | Jobs ▸ Burst ▸ Open Inspector: compile each one. Enter Play with no "managed code" fallback warning and no safety exception. On exit there is no "Native Collection has not been disposed" (QA-SWARM-ROUND11-8). | There is no per-system switch. **Jobs ▸ Burst ▸ Enable Compilation** off runs every job as managed code (slower, same behaviour). To keep a substrate species on the main thread: its `Substrate * Species.asset` ▸ **Simulate Off Main Thread** off. |
| 4 | **Ecology LOD** (round 11f). Far, unseen populations collapse and must come back without a pop. | A swarm 600 u away, out of view, stops its `SwarmFauna.Tick.*` markers. On approach at boost it is already swimming before 300 u (QA-SWARM-ROUND11-6). | **Macro Lod** off, per system: `SwarmSortFaunaConfig.asset`; each `Substrate * Species.asset`; `Builder Colonies/{FortressColonyConfig,ThiefNestConfig,WearerConfig}.asset`. |
| 5 | **Stakes size** (round 11g). | `Swarm Cell Config.asset` ▸ Stakes ▸ **Petal Burn Rule = Tuned**. The effect asset reads -0.5 and Tuned -0.1 (QA-SWARM-ROUND11-7). | Set **Petal Burn Rule** to **Shipped** (5 petals per element). Make it permanent with `PETAL_BURN_RULE` in `author_swarm_fauna.py`. |
| 6 | **Round-10 swarm strikes** (lurker, locust and pack plates on the Mass, Space and Time members). | Plates show on all four elements, not only the pufferfish (QA-SWARM-ROUND10-1). | `SwarmSortFaunaConfig.asset` ▸ **Bestiary** off. Only the Charge pufferfish then plates. |
| 7 | **The pack's held ring** (round 11-12, a designed beat). | The ring holds for 6 s, then all six strike within about 1 s of each other (QA-SWARM-ROUND11-2). | `Substrate Pack Hunter Species.asset` ▸ Species ▸ **Ring Hold Seconds** 0. This is the research's quorum: the pack strikes about 1 s after the ring closes. |

Turn on **FrogletTools ▸ Toolbox ▸ Logging ▸ Ecology** first. That channel prints:
- the `[Substrate] … seeded` lines;
- the `[Builders] … founded` lines;
- the `[Swarm] … hatched as …` and `[Swarm] … morphs X -> Y` lines.

## 2. How to get in, and how to turn the stakes on

1. Open `Assets/_Scenes/Bootstrap.unity`, press Play, and let it reach **Menu_Main**.
2. Click the centre of the screen (gamepad **Y**). You are now in freestyle.
3. Fly through the **Cell Selector** toy, a ring near the membrane in the World group. Fly into the **Swarm**
   station; it is the bare one, with no miniature. The world suctions out and the Swarm cell grows in behind the
   veil.
4. Wait about 6 s (`InitialFaunaSpawnWaitTime: 6`) while populations seed.

**Hostile from the start; claim the nucleus to take it.** Every creature wears the cell's **controlling domain** at
spawn. `Cell.ControllingDomain` (`CellControlRules.ControllingDomain`) resolves in this order:
1. the live prism-count leader. For this cell that is whoever has laid the most mass **inside the nucleus** (r 392);
2. then the config's **authored starting controller**. The Swarm cell authors
   `initialControllingDomain: OpposingLocalPilot` (`author_swarm_fauna.py`, `INITIAL_CONTROLLING_DOMAIN`). That is
   the next domain after yours: Jade → **Ruby**, Ruby → **Gold**, Gold → **Jade**;
3. then gameData's controlling team, then the local pilot's own domain, then Jade. These are the legacy fallbacks,
   and every cell that authors nothing still uses them.

So in solo freestyle the Swarm cell seeds in a colour that is **not yours**, and it stays that way while you lay trail
outside the nucleus.
- **Hostile** (any other domain): every hostile contact burns petals for good. They do not come back as crystals.
- **Friendly** (own domain): a hit only *stings*. The flower dips by the same size and recovers in about 4 s, and
  nothing is lost.
- **To make the cell yours**, lay trail inside the nucleus until you lead it. The next waves hatch in your colour.
  - Live creatures keep the colour they hatched in.
  - Flipping your domain with the Domain Changer does not help: the start is not latched, so the unclaimed cell
    opposes your new colour too.
- **Networked:** the server resolves the start against the **host's** pilot and `CellNetworkSync` replicates it.
  Every peer then spawns one colour, even if that colour is a client's own.
- **Collect crystals first.** Get every element on your HUD flower to 5 or more petals so there is something to
  lose (QA-SWARM-ROUND11-7 step 0; the hostile start is QA-SWARM-ROUND11-13).

**What a burn costs here (Tuned):**
- **1 petal per element per landed hostile contact**, so 4 per contact.
- There is a 1 s cooldown per vessel.
- The loss is settled in whole petals and clamped at 0.
- **Pecks and sips are drains of weight 0.25** (a quarter-bite). A leech's own plate weighs 0, so it never burns.
- Every other cell plays Shipped, at 5 petals per element.

## 3. The guided tour, from the nucleus outward

The map uses top-down axes: **+X** is "east", **+Z** is "north", about the cell's Y axis. All radii are from the
cell centre. Every creature drops **one crystal of its heart's element** when it dies (ram, gun, missile, blast,
predator or starvation). That crystal is what it pays.

### 3.1 The inner cell (0-685 u)

| Where | Creature | Looks like | Telegraph | Beat it | It costs you | It pays |
|---|---|---|---|---|---|---|
| r < 392 | **Nucleus** | the cell's core | — | — | — | — |
| 400-465, all round; then roams the whole cell | **Wearers** (Charge hearts) | small white hearts pulling prisms out of YOUR trail into lumpy bodies, head-and-tail, that fuse when they touch | at ~60 worn prisms a body turns, then **rears**: it contracts for 1 s | dodge sideways during the rear. Fly through a body to strip your prisms back in your colour; strip fast and it **moults** and flees. A bare or small heart dies to a ram | the lunge, with the body's prisms in danger | your prisms back, and the heart's crystal. At 150 prisms it drops a static **lair** and a new heart |
| 470-620, whole shell | **Whale swarm** (Mass-majority start, up to 960 members) | a whale of tadpoles, nose-first. With **Multi Domain**, back and belly show different domains' prisms (round 9 lineage regions) | its Mass members are **lurkers**: they plate while half-startled (`LurkCalm` 0.05 < startle < `DangerEnter` 0.45) | **rush it** so it bolts; a bolted lurker stays safe until calm. Never creep up on it | a plate contact = a burn | crystal per member. Kill its majority quickly to **morph** it |
| 470-620, at the Mass flora | **Lurkers** (substrate, Mass; 8 seeded, cap 16) | indistinguishable from the Mass flora crystals: the calm body is drawn at 15% behind its heart. It creeps while unwatched and freezes when looked at | a **gape** (the body swells), then a 0.6 s snap at 230 u/s | keep it in your forward 50° cone and it freezes. After a snap it is spent for 3 s | the snap = a bite | crystal |
| 625-685, 5 roosts | **Mobbers** (substrate, Time; 40 on 5 roosts, cap 50) | a colony that orbits your hull at 30 u | provoked when you are slower than 100 u/s or near a roost. A bird pulls up for 0.8 s before its dive (at most 3 diving at once) | go faster than about 140 u/s and they fall behind. Point your nose at one inside 60 u and it **jinks** aside | a **peck** = 0.25 of a burn, at most 1/s | crystal |

### 3.2 The middle shell (690-905 u): three sectors, and the creatures that roam

The middle shell has three substrate pens, each a 55° half-angle cone about the Y axis (from
`author_substrate_fauna.py`):
- **stampede** along **+X**;
- **leeches** about 120° round, along (-0.5, 0, 0.866);
- **leviathan** 120° the other way, along (-0.5, 0, -0.866).

| Where | Creature | Looks like | Telegraph | Beat it | It costs you | It pays |
|---|---|---|---|---|---|---|
| 690-840, whole shell | **Pufferfish swarm** (Charge-majority start, 895 members at full size) | a pufferfish of tadpoles. It grazes the **Time** flora | Charge members **puff**: plates rise above startle `DangerEnter` 0.45 and stay until `DangerExit` 0.15 | stay calm near it, or hit it before it puffs. *Before the balance pass most harness burns came from these plates (SWARM_FAUNA.md §26, findings)* | a plate = a burn | crystal per member |
| 690-1080, roams | **Pack hunters** (substrate, Time; 6 seeded, cap 7) | six long, low 12 u bodies stalking at about 98 u/s, slower than you | they fan onto a **~110 u ring around your line and HOLD it for 6 s**: slow, circling, tightening by up to 45%. Then **all of them** light up within about 1 s of each other (a 0.5 s telegraph) and dive at about 168 u/s | **break out through a gap**: the hold resets. After 3 s of strike they are **winded for 3 s**: slow, harmless, falling back. That is the window to ram them | each dive contact = a bite | crystal. They eat locusts by scent, and breed only when fed |
| 690-840, **+X** | **Stampede** (substrate, Mass; 48 in 4 herds, cap 72) | herds that spook as one. The cows run away; every 4th animal is a **bull** | the bull lowers its head for 0.9 s (slows to 0.2×, faces you) | dodge sideways during the head-down | a landed charge (170 u/s, 1.5 s) = a bite. It then rests 4 s | crystal |
| 690-840, **+Z side** (120°) | **Leeches** (substrate, Charge; 48 at flora, cap 64) | puddles drifting at 12 u/s | within 140 u they **pounce** at 150 u/s and latch onto the hull (at most 6) | **turn hard** (more than 1 rad/s) and they are flung off and dazed for 2.5 s. Ram a free one | a **sip** every 1.5 s per rider = 0.25 of a burn. Its plate never burns | crystal (its proxy exists to be rammed) |
| 690-840, **-Z side** (240°) | **Leviathan** (substrate, Space; 96-member school, cap 128) | sated, the school assembles into one **120 u manta**. Hungry again (mean hunger above 0.55), it dissolves into a harmless shoal | it turns toward you within 700 u. With you inside 220 u ahead of its mouth, the **jaws open for 1.2 s** | get out of the cone in front of the mouth during the jaws. A loose shoal is harmless | touching the assembled body burns. The **gulp** surges at 115 u/s for 1.6 s, then it rests 4 s | crystal per member |
| 845-905 | **Fortress colony** (Mass; 48 founders) | workers carry YOUR trail prisms into a hollow shell about 40 u across | hover within about 110 u: a **screen of workers forms** between you and the core, then strikers plate | **cut the wall.** It knits shut (half the sites in about 5-10 s) mostly from your own trail. Cut the same line again and the scar grows thicker there. Kill workers to slow it | a striking worker = a burn | worker crystals. A rammed worker drops its prism loose |

### 3.3 The outer shell and the rim (910-1200 u)

| Where | Creature | Looks like | Telegraph | Beat it | It costs you | It pays |
|---|---|---|---|---|---|---|
| 910-1080, whole shell | **Jellyfish swarm** (Space-majority start, 440 members at full size). Its Charge bell members wear shields | a jellyfish of tadpoles | its Space members are **locusts**: a quarter of the body plates at a time, and the lit quarter moves every 2 s (`LocustPhaseSeconds`) | read the shimmer and **thread the gaps**. Shielded members are never food | a lit member = a burn | crystal per member |
| 910-1080, near the Space flora | **Locusts** (substrate, Space; 40 seeded, cap 360) | sparse and fed, they are **cute**: slow, curious, bobbing | **phase flip**: dense and hungry, they turn gregarious (aligned, 95 u/s, biting). The probe reads cute .72, then terrifying .85 | keep them fed and sparse, or leave. The cloud's size is the food it finds | bites | crystals. *They persist, but turn over heavily: the outer food holds 28-88 of the 360 (SWARM_FAUNA.md §26.6)* |
| 1085-1140, on an outer Space plant | **Thief nest** (Space; 6 founders, cap 18) | a few thieves fall in behind you within about 400 u and snatch your wake (trail at most 1.5 s old). They fly it home at half speed to a visible **hoard** at the plant | they tail you | **turn back**: a laden thief flies at 75 u/s and you always catch it. Knocking it down returns its prism to you. **Weave**, or **raid the hoard**: touching a hoarded prism takes it | your trail | your prisms back, plus a thief crystal |
| 1095-1192, 18° cone along (1, 0.3, 0): **+X, tilted up**, past the stampede side | **Snap traps** (Time; 3 clumps, 9 seeded, cap 15) | Venus flytraps of 27 prisms with a crystal in the jaws. Their mouths turn toward traffic (7°/s, within a 60° cone) | within 146 u the lobes **glow and gape** for 0.73 s, then hold open (armed). A path across the mouth fires them | back off before it arms and it never fires. Dodge sideways at the glow. Break 13 or more of its 24 lobe and tooth prisms and it can never fire | the closing lobes (0.52 s) and the always-dangerous teeth burn | joust the crystal while it is shut (about 7-9 s) for the crystal and a skeleton |
| same grove | **Physarum** (Space; 5 sclerotia, cables of tube prisms) | a slime-mould network cabling the sector between food and trails. Your wake **repels** it | a danger **pulse** runs along the cables at about 65 u/s (lit 0.5 s, then dark for 1 s). A sclerotium glows 0.8 s, then beats 0.6 s, about every 3 s | cross a cable just **behind** a pulse. Dive for a sclerotium's crystal right after a beat. A cut cable re-forms within about a minute | pulse or beat contact = a burn | the sclerotium crystal |
| 1200 | **Membrane** | — | — | — | — | — |

### 3.4 The swarms change shape

- **Morphs.** A swarm's creature is its **majority element**: Mass whale, Charge pufferfish, Space jellyfish, Time
  **dragonfly**. Kill enough of the majority and another element leads. After a short hesitation the body swirls
  into the new creature, in motion. Mismatched members visibly molt.
- **Kill fast.** A swarm sitting on food re-lays its majority, so slow kills never convert it. The Ecology log
  prints each morph.
- **The dragonfly appears only by morph.** Its Time members strike as **pack hunters**: they turn on you early,
  above startle `HuntEnter` 0.2. Keep your distance.
- **Healing.** Since round 11d the body regrows **from the wound** and laying eases back in, so there is no jolt
  after a cull.
- **Lineage colour regions** (round 9, **Multi Domain** on). A member's domain is its lineage and never changes.
  Two regions of one body (back vs belly) can wear different domains. Diet no longer colours anything.
- **Grazing.** Each band's Borromean flora feeds its swarm:
  - inner: Mass, 2-3 plants;
  - middle: Time, 9-12 (about a quarter stand in the 625-690 mobber gap);
  - outer: Space, 3-5.

  A swarm grazes a plant, then moves on (round 9), riding with its whole body inside its band. It sheds
  starving members only while hungry (stomach below `ForageBelow` 0.5), never while sated.

## 4. What is new tonight (one line each)

- **11a, one prism system.** Swarm members are platform prisms: index entries plus prism entities, with Burst pose
  and index jobs.
- **11b, the Living Ecology substrate.** It adds the pack, locusts and lurkers. The agent pass is a Burst job
  (11b-2).
- **11c, threat flora in the rim.** Snap traps and a physarum grove.
- **11d, smoother bodies.** No post-cull jolt. The dragonfly can now morph into a jellyfish. Lurkers never bite a
  pilot who rushed them, and plates no longer flicker.
- **11e, builders.** The fortress colony (wound-knit walls) and the thief nest (wake snatching, a raidable hoard).
- **11f, LOD and stomachs.** A far swarm costs nothing (macro LOD); substrate and builders followed in 11f-2. Every
  creature now starves on a conserved **stomach**, not a clock.
- **11g, the Tuned burn.** The demo cell burns 1 petal per element; every other cell still burns 5.
- **11-9, the whole cell at once.** Whales hold up to 960 members (not 192). Wearers steal rim trail. A subnormal
  frame-time cliff is gone.
- **11-10, the wearer.** A creature made of your stolen trail.
- **11-11, the rest of the bestiary.** Stampede bulls, mobbers, leeches and the leviathan manta.
- **11-12, the pack's 6 s held ring** (menace before terror). A puffed pufferfish shield member now stays shielded.
- **11h, a real compile.** Real Unity references caught the `Random` and `GetComponentLookup` errors.
- **11i, a generated-asset audit.** Two prefabs shed dead `TargetScale` overrides.

## 5. Known limits (honest)

- **Nothing here has run in Unity.** Rendering, physics colliders, Burst, the main-thread glue, `CellEcologyLod` and
  the spawners are only type-checked and compiled. Burst has never compiled any job, and the Jobs safety system has
  never run.
- **The hostile start is proven headless, not observed** (§2). `Tools/Build/cell_control_harness/run.sh` proves
  the resolver on 1,152 inputs, and its negative control fails on the old ordering. Nobody has watched a wave hatch
  in the opposing colour in the editor yet (QA-SWARM-ROUND11-13).
- **The compile is against Unity 6000.0.75 references, not 6000.3.** The Services.Multiplayer, Friends and
  Leaderboards packages could not be fetched. Editor code was checked only against UnityEditor 2021.1
  (`Tools/Build/unity_refcompile/README.md`).
- **Over 30 min every class persists except the lurker** (whole-cell harness, SWARM_FAUNA.md §26.6). Pilots ram
  it out (minute 23.8); the seeder has it back in about 19 s. One 30-min seed is a single sample.
  - Locusts still turn over heavily (hundreds starve per 30 min).
  - Fortress workers dip to about 15 of 48 under a raider cutting the walls, then recover by births.
- **Burns/min: careless 1.71, skilled 0.24.** 97% of burns were telegraphed. Is that true for a human?
- **LOD engages more now.** Thieves roost 11-31% of the time and wearers 0-13%, after their sight was cut to 400 u.
  Swarms were collapsed ≤ 1% of the time (measured before the pass).
- **The held ring makes the pack far less lethal to a moving pilot.** It landed 6 bites over 5 × 90 s, against 20
  for the research port (SUBSTRATE_FAUNA.md §7.7).
- **Not proved:**
  - The stampede's head-down is a 1.9 s median before a trample (the bestiary's telegraph is 0.7 s).
  - The grove and the substrate were built in parallel and never tested together near the 1,080-1,095 u seam.
  - The emotion reads (§27) see motion and size only; colour, glow and sound are invisible to the probe.
- **The collider worst case is 1,194 of 1,200.** Any extra always-on collider breaks the ceiling.
