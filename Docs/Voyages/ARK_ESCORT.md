# The Ark escort

**Status: BUILT, tuned by sim** (Garrett, 2026-10-29). Branch `claude/ark-escort-voyage`.

> "a voyage that ... is attractive for 3 vessel squad to protect their ark and escort it through the hypersea. the
> voyage should go through 5 cells and one of them should be very challenging to survive ... a squad of any three
> vessels, set a difficulty from 1-9 and have an easy time with 1 but an impossible challenge with 9 ... 3 should be
> fun, 4 should be challenging" (Garrett, 2026-10-29)

## 1. What it is

A voyage card of its own in the Voyage window (beside the pilots' jobs): **Escort the Ark**, with a difficulty
stepper (1 to 9, remembered per device) and Set sail. Setting sail enters freestyle, deploys the active squad (you
fly your seat, AI wingmates fly the others, each in its slot's vessel, pilot, artifact and element) and the Ark sails
five worlds in order. The escort ends when the Ark reaches the last world's core (made it through) or is overrun.

| # | World | Cell config | Its creatures |
|---|---|---|---|
| 1 | Garland Shallows | Garland | brittlestars, quadfish, sharks |
| 2 | Scarab Reef | Scarab Scramble | tadpoles, brittlestars, piranhas |
| 3 | The Boneyard | Boneyard 3 | scavengers |
| 4 | The Swarm | Swarm | three swarms, the substrate packs (hunters, locusts, leeches, a leviathan...), builders, NCA lizards |
| 5 | **The Maw** (the gauntlet) | Wildlife Liberation 4 | quadfish, brittlestars, sharks, worm colonies, clawfish |

Nothing is scripted. A world that starts **hostile** is controlled by a domain opposing the squad's, so its fauna wear
that colour and graze the Ark's hull (`Cell.SetReplicatedStartingController`); taking the world back is laying more
of your own mass in it than they have. Every AI seat escorts (`ArkEscortPilot`): seat 0 hunts the hostile creature
nearest the Ark, seat 1 the next, seat 2 the next, each holds a station off the bow when there is nothing in reach.

## 2. Difficulty

`Assets/_SO_Assets/Toys/ArkEscort.asset` (`ArkEscortSO`) holds the five worlds and nine tiers. A tier is only numbers
the corridor already had:

| Field | What it does |
|---|---|
| Population | multiplies each world's fauna and flora seeds and caps (`Cell.RuntimePopulationScale`), times the world's own Threat |
| RegrowSeconds | the Ark's reactor: seconds per regrown hull plate (`ArkReactorSO.WithRegrowSeconds`) |
| HostileShare | chance each world (from its HostileFrom difficulty up) starts hostile |
| ArkSpeed | a slower Ark spends longer in each world |
| BreachHull | the Ark is **overrun** (escort lost) below this share of its hull |

Why a breach line: the Ark's shielded plates (about a fifth of the hull) are never grazed, so measured in the sim the
hull bottoms out near 20% and the Ark never truly falls to fauna. The breach line is the escort's loss condition; a
true fall (the last plate) still ends it too.

## 3. Tuning (the escort sim)

`ArkEscortSim` plays escorts with every seat on AI and writes one JSON report per run (each world's hull at entry,
lowest and exit, peak threats, how it ended). Unattended, from the command line:

```
Unity.exe -projectPath . -executeMethod CosmicShore.Editor.ArkEscortSimLauncher.Run \
  -arkEscortSim "1:Sparrow,Rhino,Manta;5:Squirrel,Dolphin,Serpent" -arkEscortOut Logs/escort.jsonl \
  -arkEscortTimeScale 3 -arkEscortQuit
python3 Tools/Build/ark_escort_report.py Logs/escort.jsonl
```

The measured runs and what each tuning pass changed are in §4.

## 4. Tuning log

(filled in by the tuning passes; see the PR)
