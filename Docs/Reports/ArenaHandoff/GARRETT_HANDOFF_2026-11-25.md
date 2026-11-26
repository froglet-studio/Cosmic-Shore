# Arena balancing hand-off for Caleb

Nov 25, 2026 · Garrett Milliron. Copied into the repo by Caleb's session on 2026-11-25 from Garrett's Claude Doc
(https://claude.ai/artifact/JQE329AGuXDqizHka8BCrp, which cloud sessions cannot read). The text below is Garrett's,
unchanged except for Markdown formatting. The rest of this folder (the reports, wild courses and patches it names) is on branch `claude/project-thread-nhk78v`.

## Start here

Caleb, you hold the three arena swim lanes while Garrett is out (about 2026-11-25 to 2026-11-30): Regatta (the Time
arena), Broadside (Charge) and Brood Rush (Mass-Space). This is step 5 of the elemental loop
(`Docs/ElementalLoop/PLAYBOOK.md`): measure each vessel in the arena game against a fixed AI pool, then tune the
imbalance with that vessel's own levers, or flag it for redesign.

The arena work so far ran in Garrett's private Claude project, so you can't see its threads, decision cards or shared
files. Everything you need is meant to be in this doc. Numbers are as of 2026-11-23 to 2026-11-25 on bleeding-edge.

Where things stand in one line each:

- **Regatta:** 11 vessels balanced within the 1.10x bar at both starts. One small Rhino lever waits on a yes.
- **Broadside:** 8 vessels in 0.87-1.16 of a fair share, except the Orca at 1.33. The Orca and Thresher fixes are
  measured but sit in other people's lanes.
- **Brood Rush:** the big rework merged 2026-11-25 (15-hull card, fair scoreboard, Serpent wall sync). Three balance
  decisions are open, then one refit.

The first three things worth doing:

1. **Play the Brood Rush build on bleeding-edge.** Garrett's own playtest on 2026-11-21 showed wild score imbalance the
   judge didn't. Land #2332 first (learned Mass-Space AI rows, still unmerged), or you'll be playing a tree the numbers
   don't describe. The fixes merged a few hours ago and nobody has played them in Unity yet.
2. **Answer or park the open decisions** in the decisions section below. Each has a recommendation; most need a lane
   owner's yes too.
3. **Play the eight wild Regatta courses** on the playtest branch. Garrett judges course fun by playing, and the search
   is done.

## Garrett's rules for balancing

These are rules Garrett set during this work, with dates. They decide most of the calls you'll face.

- **Don't cheat (2026-11-18).** Balance with each vessel's own levers. No per-mode handicaps, no per-hull starting
  rows. If the levers can't do it, flag the vessel for redesign, whether it's too fast or too slow. Flags go in
  `Docs/ElementalLoop/REDESIGN.md`.
- **Exaggerate and rein in, don't homogenize (2026-11-19).** Lean into what makes each vessel unique instead of pulling
  it toward the average.
- **A game mode never changes the vessel** (Shombith's locked rule, 2026-10-31). A lever is a vessel change made on the
  vessel's own assets, never a mode-only rescale.
- **One radius per ability (2026-11-19).** Don't give one ability different radii for its different effects. The
  Sparrow is the one exception.
- **Keep each vessel's own game premise.** When a lever only seats a hull by rewriting its own Charge or Time game
  (Rhino's Bullrush wind-up, Swordfish's Riposte rule), that's a design call for a person, not a tweak.
- **Both starts count.** Regatta must hold at the card start (rest) and at the L5 start. If any hull starts at L5,
  every hull does.
- **The bar.** Regatta: slowest over fastest at or under 1.10x at both starts. Broadside and Brood Rush: each hull
  within about ±10% of a fair share (index 0.9-1.1).
- **AIs train only in each vessel's own games**, never inside an arena game. Commits that train or tune an AI say
  `Plan step: <id>` (`Docs/AI_SYSTEM/AI_PLAN.md`).
- **Playtests beat simulation.** When a real game disagrees with the judge, the real game wins and the judge gets fixed.
- **Regatta courses: search far and wide (2026-11-21).** No more bland triple-domain tracks of super-shielded prisms.
  Everything is a gene (rings, prisms, cell, nucleus, flora, fauna). The only constraint is that each ring is visible
  from the previous one. Garrett picks courses by fun.
- **Move fast, flag the risky stuff.** Risky merges get the `risky-merge` label and a sign-off entry
  (`Tools/Build/risky_merge.py`, `Docs/Approvals/risky.json`). Lane collisions (`approvals.py check` exit 2) still need
  the occupant's yes.
- Stoat is out of Regatta for now; the Butterfly isn't on the roster (it's the TigerMoth); the Serpent's redesign
  happens in Garrett's other project, so here it's measured only.
- **Compute.** Use cloud sessions. Don't run heavy local jobs on Garrett's PC unless he offers it (it has crashed).
  Froglet Compute is paused (Shombith).

## Where each game stands

### Regatta (Time)

Eleven hulls hold the 1.10x bar at both starts with shipped values: 1.091x at the card start, 1.099x at L5 (judged
2026-11-23 on trunk 910c2778a, shipped 8-course shuffle, solo races, 5-15 seeds). Real Unity matched the judge within
0.92-1.09 for all 11 on the same day. A race seats 2-9, so any 9 of the 11 can share a start line.

| Hull | Card s | L5 s | Note |
|---|---|---|---|
| Rhino | 70.3 | 68.6 | Card fast edge, L5 slow edge (on the bar) |
| Sparrow | 71.1 | 64.8 | |
| Urchin | 71.4 | 63.5 | |
| BrittleStar | 71.5 | 63.3 | |
| Squirrel | 72.0 | 66.3 | |
| Swordfish | 72.3 | 65.0 | |
| Thresher | 72.3 | 67.4 | |
| Scarab | 73.5 | 63.7 | |
| Shrike | 74.5 | 63.4 | |
| Goby | 75.7 | 62.4 | L5 fast edge |
| Dolphin | 76.8 | 67.1 | Card slow edge |

Off the field:

- Serpent 91.3 / 70.9 s. Pellet duration at rest Min 1 to 1.5 seats it (73.3 / 67.9 s). That's Garrett's other
  project, measured only.
- Flagged for redesign: Manta (L5 Soar), TigerMoth (belly-first drop; its feel must not change), Orca 2.35x, Grizzly
  about 3.5x, Wrasse 2.0x, Jacks 2.4x, Meerkat 3.95x, Angler 4.5x, Honeyguide 4.9x. Prototypes for Jacks (three dogs,
  1.14x card / 1.33x L5) and Meerkat (tunnel carry with an AI brake, 1.66x) didn't reach the bar; patches in the files
  below, unmerged.
- Grizzly got 50% slower in Regatta after its 2026-11-20 Time retrain, because the bomb distances are shared with
  Grizzly Time. The grizzly-v3 kit (#2358, #2385, merged 2026-11-25) changed it again and hasn't been judged since.
- Not raced: Stoat (out for now), Jewel Wasp and Starling (no Time game or racing AI yet).

### Broadside (Charge)

Eight hulls sit at 0.87-1.16 of a fair share with shipped values, the Orca excepted (judged 2026-11-23, 21 real-game
runs, 4v4 timed 240 s, pooled over rest and L5). Share is a hull's points over the match's mean per seat. One run
swings a hull by up to 0.25, so pool 3 or more runs before you believe a number.

| Hull | Pooled share | Note |
|---|---|---|
| Orca | 1.33 | Fast gun farms the field; rein measured (see decisions) |
| Brittle Star | 1.16 | |
| Scarab | 1.02 | Plate cooldown 4.25 s shipped in #2263 |
| Stoat | 1.01 | |
| Angler | 0.98 | |
| Jacks | 0.95 | |
| Meerkat | 0.93 | |
| Grizzly | 0.87 | Measured before the grizzly-v3 kit merged |
| Swordfish | 0.77 | Sword latch 0.5 s merged (#2347); low edge at L5, not seated |

The Broadside card ships 6 of these today (Orca and Grizzly are judged in band but not on the card). The card seats up
to 9.

Flagged, with levers that stop helping below the band: Squirrel about 0.58 (the shared joust asset's
faster-than-victim rule caps it; it would need its own joust asset), Sparrow 0.55-0.77 (its AI only fires rockets when
someone is ahead), Manta about 0.52. Too strong with no lever tried: Wrasse 3.27, Jewel Wasp 1.87. Real Unity agreed at
the ends but 6 matches was too few to call per hull.

### Brood Rush (Mass-Space)

The end-to-end rework merged into bleeding-edge on 2026-11-25 (#2322 and the stack on it). It followed Garrett's
2026-11-21 playtest, where scores were wildly imbalanced even among AIs and the Serpent's wall didn't sync. The game is
3 teams of 3, a hull appears once per match, scored per pilot as VC + VD (volume created in your own domain plus volume
removed from opposing domains).

At Medium AI (the game default), the judge's provisional refit has 16 of 17 card hulls within 0.9-1.1 on the mean of 60
and 30 fps, and 12 of 17 holding at both rates separately. Real Unity agreed with the judge for 15 of 17 on the same
tree. That refit lives on a scratch branch and depends on the open decisions; it was never merged.

What merged, so you know what you're playing:

- The card seats 17 balanced hulls instead of the 7 it had (4 of them flagged).
- The scoreboard credited every brood to one pilot; it now shows each pilot's VC + VD.
- Shield churn counted every raise as new volume; it's netted now.
- The Serpent wall and six other AI abilities ran on the host only; now every peer sees them. On a second machine the
  client saw every wall, carrying about 87-90% of the host's prisms.
- Most hulls shipped with an empty AI ability list, so their AI pressed nothing in Mass-Space games. The learned rows
  that fix it (`AIMassSpacePlanSO`, #2332) are not on bleeding-edge yet; see the next section.
- The Dolphin AI's drift was frame-rate dependent (2.16x stronger at 30 fps); fixed down to 1.31x.

Known gaps: the Dolphin still reads faster in Unity (1.45) than in the judge (1.02 at 60 fps), so its lever is
unverified. The Sparrow is the highest-variance hull and needs bigger samples. Only 9 of 17 hold at Legendary
difficulty.

## What merged recently

All into bleeding-edge. These were checked with the reference compile and the judge, but none was played in the Unity
editor, so a hand playtest is the first real check.

| Merged (UTC) | PR | What |
|---|---|---|
| 2026-11-25 01:05 | #2331 | Manta: Mass-scaled trail brings it back into Brood Rush |
| 2026-11-25 01:00 | #2329 | Rhino: bigger plough rails so it counts in Brood Rush; reseated on the card |
| 2026-11-25 00:51 | #2322 | Brood Rush: 15-hull card, per-pilot VC+VD scoreboard, Serpent wall on every peer, AI ability presses, Medium refit levers |
| 2026-11-24 23:31 | #2333 | Shrike, Dolphin and TigerMoth Mass-Space AIs; Dolphin drift frame-rate fix; judge frame-rate knob |
| 2026-11-24 23:27 | #2330 | Swordfish: afterimage volume x1.7 at rest (Brood Rush Mass lever, judged 1.03) |
| 2026-11-24 23:12 | #2347 | Swordfish: sword hit latch 1.4 to 0.5 s (Broadside) |
| 2026-11-21 | #2263, #2281, #2301 | Broadside judge, Orca 20 s bomb re-arm, Grizzly 20-80 blast; Brood Rush trails Orca 0.8 / Grizzly 0.4; Grizzly Time AI rams kegs open |

Two things to check:

- **#2332 is not merged, and Brood Rush on bleeding-edge is missing it.** It carries the learned Mass-Space AI rows
  (`AIMassSpacePlanSO`, `Resources/AIMassSpacePlan`, the `AIPilot` hook) for Meerkat, Sparrow, Stoat and Urchin, and
  the Dolphin and TigerMoth throttle rows #2333 relies on. I checked on 2026-11-25: none of those files is on
  bleeding-edge. The judge's 16-of-17 refit and the Unity runs were on a tree that included it, so trunk won't match
  those numbers until it lands. It's an open draft touching `AIPilot` (Yash's AI platform, queued for his `/approve`).
  Merging it is the first technical step.
- **Other people merged changes to arena hulls on 2026-11-25 that no arena judge has seen:** the grizzly-v3 kit
  (#2358, #2385) and Yash's Time-games pass (#2381). Re-judge the Grizzly before you trust any Grizzly number in this
  doc.

## Open decisions

Eleven calls are waiting, all posted to Garrett and none answered. Garrett didn't hand his calls to you, so treat these
as yours to raise with him, or decide where he tells you to. Several also sit in another person's lane, and those need
that person's yes whoever decides (`python3 Tools/Build/approvals.py check --me Caleb` exits 2 on a collision).

### Brood Rush

1. **Write the four held levers?** Refit trail levers that sit in occupied lanes: Orca 0.978, Dolphin 1.647 to 0.977,
   Stoat 2.29 to 2.33 (Yash's lane), Thresher 0.914 to 1.057 (branch `claude/fervent-pascal-o28ftt`). Options: write
   all, write only the unoccupied ones, hold all. Recommended: write all, with the lane owners told. Orca and Dolphin
   are in the patch `orca-dolphin-levers.patch`.
2. **Which difficulty to balance for?** At Medium, 16 of 17 hold; at Legendary, only 9 of 17. Options: Medium only, or
   every difficulty. Recommended: every difficulty. The Legendary gap is AI work (Mass-Space AI rows), not levers.
3. **Lift the TigerMoth and Urchin lever caps?** Trail levers are capped at 0.4x-2.5x. At the caps the TigerMoth reads
   1.21 and the Urchin 0.61. The provisional refit lifted them to TigerMoth 0.259 and Urchin 3.66. Recommended: lift
   the caps.
4. **Replicate the AI drift commit to every peer?** Like the Serpent wall, the AI's drift commit runs on the host only.
   The patch `aipilot-commit-drift-replicated.patch` sends it to every peer. It touches `AIPilot`, which is Yash's
   lane. Recommended: apply, with Yash's yes.

After 1-3 are answered: rebuild the 30/60 fps refit on current bleeding-edge (the scratch branch
`claude/brood-night-refit` is provisional), then open its PR.

### Broadside

5. **Should the Swordfish sword still need to be faster than its victim?** Keep the rule: it sits near 0.66-0.77 and
   stays off the card. Drop it in Broadside's kit: with latch 0.5 s it overshoots to 1.07-1.34 (a latch near 0.8 s
   would likely seat it), but Riposte's premise goes. Recommended: keep the rule.
6. **Lower the Rhino's gore speed so it gores at cruise?** Keep the wind-up: about 0.3-0.4, off the card. Gore at
   cruise (50 to 0 u/s, plus gore latch 1.4 to 0.6 s): about 0.87, but Bullrush no longer needs its Ramp Spool
   wind-up. Halfway (100 to 33 u/s): 0.59, still below. Recommended: keep the wind-up.
7. **Rein the Orca's fast gun?** Heat 6 to 3 s, cool-down 5 to 10 s brings the Orca from 1.33 to 1.17 and the field to
   0.82-1.17. Not shipped because `claude/ms-ai-orca` (Orca AI training) occupies the lane. Needs a go from Garrett or
   that branch's owner.
8. **Raise the Thresher's reach?** `vesselHitRadius` 4 to 20-22 lands it at 1.0-1.13, with no speed or prism change.
   Yash's lane, so his yes.

### Mass-Space AIs

9. **Ship the Thresher's Mass-Space AI?** Branch `claude/ms-ai-thresher` (e9e0901033): sling off in Haul, Haul orbit
   radius 320, a Harvest boost-policy variant. Haul +59-80%, Harvest about 2x. Held for the lane
   `claude/fervent-pascal-o28ftt` (Yash). Needs his answer.

### Regatta

10. **Raise the Rhino's ramp Max 2.5 to 3.1?** Its own Time lever (`RhinoRampBoostAction.timeAccelerationMultiplier`
    Max) acts only above rest: L5 68.6 to 67.5 s, L5 spread 1.099x to 1.082x, card start unchanged. Not applied
    because Garrett designed this kit with low acceleration and the field already passes. No recommendation was made;
    it's a margin question.
11. **Merge the Grizzly Time retrain, #2346?** Faster on every Grizzly Time rung (I4 92 to 84 s), the ladder runs
    forwards, Regatta 246 to 242 s (stays flagged). Lane collision with Yash's Grizzly work. The grizzly-v3 kit merged
    after it was trained, so it likely needs a re-run on the new kit before it means anything.

For information, not a decision here: the Serpent seats in Regatta with pellet duration Min 1 to 1.5. That belongs to
Garrett's other project.

## Regatta course search: courses to play

The wide course search is done searching; the next step is a person playing what it found. Eight very different
courses sit on the playtest branch `claude/project-thread-vlkieg` (commit eb0d4f1a2). **Don't merge that branch:** it
replaces the shipped course pool. Restore `Tools/Build/regatta_shuffle_pool.json` from bleeding-edge before anything
from it merges.

Why this search exists: every earlier search kept three braided super-shielded rails on the same eight-ring circuit, so
all its courses looked alike. The wide search (genome v2, `Tools/Compute/evaluators/regatta_wide.py`) frees the rings,
draws whole worlds from other cells, sets per-prism genes, the nucleus and the lifeforms, and treats how much of the
lap has rail as a gene. Garrett, 2026-11-22: "there is an optimum amount of railing and part of learning the course is
to find out what that is."

| Course | World | Rail on lap | Super-shielded | Nucleus | Lifeforms from |
|---|---|---|---|---|---|
| Wild1 | Orrery | none | 0% | yes | Serpentine Cell |
| Wild2 | Corona | none | 9% | yes | Orrery Cell |
| Wild3 | Spherene | 19% | 48% | no | Wildlife Liberation Cell |
| Wild4 | LinkedRings | 28% | 0% | no | Breakwater Cell |
| Wild5 | Ourobor | 14% | 39% | no | Boneyard Cell |
| Wild6 | Boneyard | none | 17% | no | Scurry Cell |
| Wild7 | Helicoid | 12% | 4% | yes | Lattice Cell |
| Wild8 | Switchyard | 9% | 0% | no | Switchyard Cell |

The best of them keep nine hulls within 1.67x of each other (Ourobor, Wild5), against 1.09x for the shipped courses.
That gap is expected: railless and part-rail courses expose real vessel differences. Urchin and Shrike are slowest on
most of them, and the Urchin often fails to finish without rails. Those are step-5 vessel findings, not course bugs.

Search status: shard 1 finished its 3,000 courses on 2026-11-25; shards 2-4 were at 2,540-2,750 of 3,000 and stop on
their own. They run in Garrett's project, so you don't need to manage them. The course pool JSON is in the repo (link
in the next section).

What Garrett wanted next: play them, say which are fun, and feed that back into the search's score. To build a
playtest pool yourself: `regatta_wide.py confirm`, then `playtest`, put the result in
`Tools/Build/regatta_shuffle_pool.json` on a playtest branch, then run `Tools/Build/author_regatta_assets.py`.

## Where things live

In the repo (readable by you):

- `Docs/Reports/ArenaHandoff/` on branch `claude/project-thread-nhk78v`: the latest Regatta, Broadside and Brood Rush
  reports, the Unity-vs-judge comparisons, the eight wild courses as JSON and the held patches (Orca/Dolphin levers, AI
  drift replication, Meerkat prototype). Copied out of Garrett's private project storage for you; paths inside them
  starting `/mnt/project-files/` point at that storage and won't resolve.
- The loop: `Docs/ElementalLoop/PLAYBOOK.md`, board `python3 Tools/Build/elemental_loop.py next`, flags in
  `Docs/ElementalLoop/REDESIGN.md`. AI rules: `Docs/AI_SYSTEM/AI_PLAN.md`.
- Regatta judge background: `Docs/Reports/REGATTA_JUDGE.md`.
- Garrett's running arena report (a Claude Doc, may be private to him): https://claude.ai/artifact/9iga6uSYMhH6r9PUpGGU5G

Branches:

| Branch | What | Merge? |
|---|---|---|
| `claude/project-thread-d1elkw` | #2332, learned Mass-Space AI rows | Yes, first (Yash's AIPilot area) |
| `claude/project-thread-5psmk3` | #2346, Grizzly Time retrain | Decision 11; likely re-run on the grizzly-v3 kit |
| `claude/brood-night-refit` | Provisional Brood Rush 30/60 fps refit | No; rebuild after decisions 1-3 |
| `claude/brood-night-integration` | The tree the refit and Unity runs used | No, reference only |
| `claude/brood-unity-audit` | Real-Unity Brood Rush harness (`Assets/_Scripts/Editor/Launch/BroodAudit/`) and results | No, tooling |
| `claude/ms-ai-thresher` | Thresher Mass-Space AI | Decision 9 |
| `claude/project-thread-vlkieg` | Wild Regatta courses, `regatta_wide.py` | Never as is (replaces the course pool) |

**The judges.** Each runs the real game's C# headless in Amoebius (the port under `Port/`), every seat flown by its
shipped AI. Setup: .NET 10 (`apt-get install -y dotnet-sdk-10.0`); the player builds from your checkout. A game process
takes 2.5-3 GB, so run at most 3-4 at once on a 16 GB box or the OOM killer fakes DNFs.

```bash
# Regatta: shipped shuffle, both starts
python3 Tools/Compute/evaluators/regatta_judge.py --course shuffle --hulls Rhino,Dolphin,Goby --seeds 1,2,3,4,5 --start card
python3 Tools/Compute/evaluators/regatta_judge.py --course shuffle --hulls Rhino,Dolphin,Goby --seeds 1,2,3,4,5 --start L5
# Broadside: 4v4 timed brawls, share of points
python3 Tools/Compute/evaluators/broadside_judge.py --hulls Orca,Stoat,BrittleStar,Meerkat,Angler,Jacks,Scarab,Grizzly --per-hull 15 --start rest --out bs.json
python3 Tools/Compute/evaluators/broadside_judge.py --report bs.json
# Brood Rush: Medium, 3 teams of 3
python3 Tools/Compute/evaluators/brood_judge.py --hulls <card hulls> --rounds 15 --difficulty Medium --n-teams 3 --team-size 3 --out br.json
python3 Tools/Compute/evaluators/brood_judge.py --report br.json
```

All three take `--overrides` (a JSON of in-memory lever values) to try a lever without editing assets. Set
`JUDGE_DT=0.0333` to judge at 30 fps; 1/60 is the default and the frame-independent number. Brood Rush trail levers
are written by `Tools/Build/brood_trail_levers.py` (`--check`), Broadside's by `Tools/Build/broadside_balance.py`.

Traps the earlier sessions hit:

- Trunk 910c2778a didn't build in Amoebius: `UIThemeLive.cs(272)` CS8121. The one-line local fix is
  `Port/src/CosmicShore.Engine/UI/PackageComponents.cs`, `class Shadow : UIBehaviour`. Check whether trunk still needs
  it.
- One Broadside run swings a hull by up to 0.25: pool 3+ runs. Brood Rush shares depend on the field, so re-judge the
  subset you mean to seat.
- For contact hulls in Broadside the speed gate binds first, then the per-victim hit latch. For hulls whose hits spread
  across victims, latches don't bind; rates and sizes do.
- Only one Unity editor at a time on a machine (UDP 7777 collides). Garrett's PC isn't available while he's out.
