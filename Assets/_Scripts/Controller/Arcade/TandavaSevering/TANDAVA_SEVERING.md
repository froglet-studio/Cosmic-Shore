# Tandava: Severing (Voyage 2)

`GameModes.TandavaSevering = 70`. A **fork of Tandava** (`GameModes.Tandava = 69`, Voyage 1,
`Assets/_Scripts/Controller/Arcade/TANDAVA.md`) that carries the work of PR #1046 (`claude/tandava-severing`): **severing**,
**wound memory** and the **whale-jelly chimera**. Everything else is Tandava's hunt: the same cell, reef, four forms, halo,
end conditions and HUD. Read TANDAVA.md for those; this file holds only what the fork adds.

## Why a fork

Tandava landed on bleeding-edge without the severing work (#1546) and Shombith is still developing it. Garrett asked
(2026-10-26) for the severing work "contained on a second voyage card in the voyages" so it never collides with that
development. So the fork shares no file it would have to edit with Tandava:

| What | Tandava (Shombith's) | Tandava: Severing (this fork) |
|---|---|---|
| Code | `Controller/Arcade/Tandava/`, `TandavaScoringRuleSO`, `TandavaTurnMonitor` | `Controller/Arcade/TandavaSevering/`, every type prefixed `TandavaSevering` (a copy of #1046's code, renamed) |
| Assets | `author_tandava_assets.py` | `author_tandava_severing_assets.py`: every asset DERIVED from Tandava's committed one (guids swapped, the fork's additions appended) |
| Plans | Tandava's 51 (`tandava_plans.py`) | the same 51 by guid, plus four (`tandava_severing_plans.py`: `severed`, `severed_feed`, `chimera_whale`, `chimera_jelly`, indices 51-54) |
| Menu | Voyage 1 on the Voyage window | **on no screen since 2026-10-29** (Shombith: "remove both the tandava parts"): the mode, scene and card stay, and `author_menu_mission.py` no longer lays the Voyage 2 card. To bring it back, restore `open_voyage(..., SEVERING_CARD, ...)` there (git history of 2026-10-28) |
| Harness | `run.sh <plans> tandava <dir>` | `run.sh <plans> tandava-severing <tandava dir> <fork dir>` (T1-T32 against the fork's director) |

What the fork shares, and so follows: Tandava's halo ring and gold burst (`TandavaHaloRing`, `TandavaGoldBurst`), its
end-condition overrides (shatter percent, halo rings), its toast ids 137-147 (plus 148-152, the ids #1046 claimed), its
plans, reef and cell numbers. A change to any of those reaches the fork the next time
`python3 Tools/Build/author_tandava_severing_assets.py` runs; `--check` goes red the moment Tandava moves, which is the cue.

The shared swarm platform carries #1046's seams for the sever (`ISwarmSeedGraft`, `SwarmFauna.ReleaseMembers` /
`Graft` / `Dissolve` / `BankAll`, `SwarmSortCore.Release` / `Graft`, `SwarmTickJob.QueueRelease` / `QueueGraft` /
`QueueStomachScale` / `MarkGrown`). They are additive: no other swarm calls them.

**Armour.** Tandava's plans gained armour after #1046 (Shombith, 2026-10-26: Mass members are shield tier except in the
poses it eats in). The fork's ladder uses those plans, so it is armoured too, and its own four plans follow the same
rule (the Severed is bare in its strike pose; the chimera is armoured). Turning that off for the fork would mean baking
its own copy of the ladder: ask before doing it.

### 3.11 Severing: cut it in two and the piece crawls off

A boss you cannot kill the same way twice starts with a body you can cut. Fly cleanly through the body - kill every
member across a band of it - and for the moment before the wound buds shut, the members beyond the cut are a separate
cloud. `TandavaSeveringSever.FindPiece` finds that moment: live members linked to every neighbour within `SeverLink` (32.7 u since 2026-10-27, 8 plan voxels: the research 13 u tore the Ten-Headed Serpent's coils, `Docs/ECOSYSTEM.md` §64.5; was 13 u -
a whole body's loosest knot of a dozen members sits inside it in every pose but the dance and the lunges, harness T20)
fall into pieces; the largest is the body, and the second, if it holds 12-40% of the form (and at least 45 members), is
**the Severed**. `TandavaSeveringDirectorCore.MaySever` allows it only for an eater roaming or feeding, never mid-lunge, never in
the rise or the dance, never while a Severed lives, and not within 8 s of the last.

The piece's members **move**: nothing dies and nothing is born. `SwarmFauna.ReleaseMembers` hands them over (their
proxies retire quietly, their index entries and body entities go, the core drops them next tick through
`SwarmTickJob.QueueRelease` without counting a death or laying a replacement), and the Severed - a second Tandava swarm
spawned through `CellLifeSpawnerBase.SpawnFaunaWithDomain` from `TandavaSeveringSettings.SwarmSpawn`, so the cell counts it as
the same creature and never reseeds one - hatches as exactly those members, grown and in place (`ISwarmSeedGraft`; the
first frame marks them grown, so nothing blooms in). The stomach splits by members. The body bolts: being cut in two is
the worst wound there is, and a meal it was cut at is broken.

The Severed is its own creature on its own director (`TandavaSeveringSettings.SeveredForm`: role `Severed`, one small plan and
its strike twin, `tandava_severing_plans.SEVERED`). It eats where it likes; once it has regrown 75% of its plan and banked 1,000
volume, or after 40 s apart whatever it has managed (the HUD counts it down: "The Severed - home in 0:40"), it turns for
home (`TandavaSeveringPhase.Rejoin`, "crawling home - cut it off") and grafts back on when it is within 90 u: up to the room the
body's form has (`SwarmTickJob.QueueGraft`), the rest paid into the stomach as the eggs they were, its own stomach
poured in up to capacity. Kill it first and those members are gone for good. Cut the BODY away to nothing while the
Severed lives and the Severed takes its place (**succession**): the director runs that swarm now, in the same form, and
it regrows into it. The shatter counts the whole animal - body and piece.

**A fed creature cut in two becomes more** (T23: both halves regrow out of their stomach shares, +121); a starved one
stays cut (-27). Severing is the hydra's rule, and the pilots' answer is the same as ever: starve it first.

Every peer runs its own swarms. The server finds the piece exactly on its body and runs the Severed's director; a
client parts the same number of members nearest the server's piece centre from its own body (`Sever_ClientRpc`), and
its Severed takes the replicated plan, levers and goal and is nudged toward the server's, as the body is. Rejoin,
succession and the piece's end are the server's call, sent to every peer. A peer that joins while a Severed lives
(it missed the sever's RPC) catches up (`CatchUpSevered`): once its body has been nudged to within 4 x `NudgeThreshold`
of the server's, and after a grace second (so a peer merely waiting on the RPC is never parted twice), it parts the
replicated member count (`_pieceMembers`, at most half its body) nearest the server's piece.

### 3.12 Wound memory: it will not die the same way twice

The director remembers what it was doing when it lost members (`TandavaSeveringWound`) - eating, lunging, roaming, or being cut
in two - for the rest of the match, a succession included (the director is the creature's mind, and a Severed is born
knowing what its body knows, `RememberFrom`). Each memory is `1 - exp(-share lost that way / ScarRef)` (0.15 of a
form's body: one badly broken meal teaches it); hunger's own sheds teach it nothing. At half strength it has LEARNED
that wound and the narrator says so, once (toast 151):

| Wound | What it learns (at full memory) |
|---|---|
| Struck at the table | bolts after 8% of its body instead of 20% (`LearnMealBreak`); a pilot spoils a plant 2.5x as far off and it fears every table at least warily (`LearnSafeRadius`) |
| Punished in a lunge | lunges only above 95% of its body instead of 70% (`LearnLungeBody`), with 3x the cooldown (`LearnLungeCooldown`) |
| Run down while roaming | senses pilots 40% farther (`LearnSense`), and runs at 35% less threat (`LearnFlee`) |
| Cut in two | each Severed after the first gets 0.6x the time apart (`LearnRejoin`): 40 s, 24 s, 14 s... |

So the play that broke it once works less the next time, and the pilots have to change theirs: T11 shows the classic
denial (strike every meal) shattering a creature that cannot learn at 145 s, failing against one that can (it reaches
the Antlion), and winning again (96 s) when the pilots also run it down when it bolts.

### 3.13 The whale-jelly chimera: torn between two animals

Before the Many-Headed Serpent rises into the Dance it passes through a body nobody drew. The hybrid 3D NCA (branch
`claude/hybrid-creatures`: one rule grows the lizard, the whale and the jelly, the form chosen by genome channels) was
given a grown whale's front spliced onto a grown jelly's back and left to run. The body it grows never settles: jelly
spreads over the whale for 200 steps (a whale core in a jelly shell, a bell at the head), then the whale climbs back
(by step 1,200 a whale with a jelly stripe down its back and a jelly fringe at the tail), and it is still mixed at step
1,600. One swim cycle of each moment is the chimera's two shapes (`chimera_whale`, `chimera_jelly`).

`Tools/Build/tandava_chimera_extract.py` turns the probe's voxels into plans: the skin voxels (live, with a dead
neighbour), 200 units spread over frame 0 by farthest-point sampling and carried frame to frame to the nearest skin
voxel, Space for the most jelly-genome units (the bell and the threads) and Time for the next, Charge at the tail, Mass
for the rest. Both shapes share that census, so a turn is a pose change (`RequestPose`), never a molt; 200 units sits
between the biggest Many-Headed Serpent (195) and the Dance (204), so entering it and rising from it only ever grow the
body (`tandava_severing_plans.validate`). The committed `tandava_chimera.json` is what the bake reads: the bake needs no numpy
and no NCA.

In the director (`TandavaSeveringPhase.Chimera`, `ChimeraTurning`): when a banked Many-Headed Serpent would rise, it becomes the
chimera instead (toast 152) for `ChimeraSeconds` (24 s), drifting slowly to where it will rise. Every
`ChimeraFlipSeconds` (6 s) it turns from one shape to the other, and for `ChimeraTurnSeconds` (2.5 s) of each turn it
holds its laying: it cannot heal while it changes (the HUD reads "Turning - it cannot heal"). It is BRITTLE while it
turns, too: it shatters below `ChimeraShatterFraction` (50%) of its body instead of `ShatterFraction` (35%), so the
pilots' play is to wear it down between turns and finish it mid-turn. It does not feed. It can still be cut in two (§3.11); its whale end trails a thin fringe, so only a cut through
its middle takes a piece big enough to live. When its time is up it rises into the Dance where it drifted to.

The chimera is not a rung of the ladder (form 5 of `tandava_severing_plans`, like the Severed's form 4): a variant names it with
`ChimeraPlanIndex` / `ChimeraAltPlanIndex` (0 = none), and only the Many-Headed Serpent's variants do.


## What the harness proves (`bash Tools/Build/swarm_core_harness/run.sh <plans> tandava-severing <tandava plans> <fork plans>`)

T1-T19 are Tandava's rows as #1046 had them, run against the fork's director; these are the fork's own:

| Test | What it shows |
|---|---|
| T2 | every Great Serpent variant hatches whole (450-507 tadpoles) and holds 93-98% of its shape 15 s later |
| T3 | calm 61 u/s against the config's 60; x2.1 = 127 u/s; the turn carry keeps the body's shape round a hard fleeing turn |
| T20 | the sever finder: no false pieces in any pose but the dance and the lunges (link 32.7 u, 8 voxels); a slice parts the tail |
| T21 | the Severed: every member accounted for, the stomach conserved, the body bolts, the piece crawls off in its own plan and regrows |
| T22 | home: unopposed, the piece feeds, turns for home and rejoins within its clock and the swim |
| T23 | the hydra: a fed creature cut in two outgrows the body that was cut (+121); a starved one stays cut (-27) |
| T24 | succession: cut the body away and the piece takes the form and regrows (93 -> 228 in 30 s) |
| T25 | the shatter counts both bodies |
| T26 | no sever in an unopposed run, nor from nibbling the tail as it roams |
| T27 | the scarred lunger: punished at every lunge, it learns and lunges less (7 -> 5; a creature that cannot learn: 10 -> 10) |
| T28 | severed twice: the second piece's clock is 24 s, not 40, and it is home sooner (43 -> 24 s); the narrator says so |
| T29 | run down: it learns, and feels a pilot at 518 u that an unscarred creature (450 u) cannot |
| T30 | the chimera: it becomes the chimera as the Many-Headed Serpent (40 s), wears both shapes and nothing else, turns 3 times whale/jelly in turn, lays nothing mid-turn, loses no members turning, rises 24.1 s later where it drifted to, and still completes unopposed in under 200 s (154 s, 167 s) |
| T31 | cut while torn: a slice through its middle parts a piece (94 of 504) and the piece is the Severed; the chimera stays torn |
| T32 | brittle as it turns: whittled to 43% between turns (226 of 525), it survives its turn when it is not brittle and shatters mid-turn (47 s) when it is |

The times are a model, not a measurement (TANDAVA.md §4). QA-TANDAVA-21..23 in `Docs/UNITY_VERIFICATION_CHECKLIST.md`
measure them in the editor.
