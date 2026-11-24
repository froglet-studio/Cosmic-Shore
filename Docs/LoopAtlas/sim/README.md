# Loop timescale simulation

How players of different cultures and demographics spend their time and crystals in the loops the game provides,
and where the two do not fit. Garrett, 2026-11-21: "economic simulations based on different cultural preferences for
preferred gameplay loop timescales and how they map to the ones we provide ... anticipate how different demographics
will engage differently and model their different paths."

Read [`REPORT.md`](REPORT.md) for the results. It is generated; do not edit it.

## Files

| File | What it holds | Who edits it |
| --- | --- | --- |
| `economy.yaml` | The game's side: payouts, prices, the ladder, the quest chain, gates, unit lengths, squad sizes, the Tide. Every value names its source. | Anyone, citing the source |
| `segments.yaml` | The players' side: eight cultures and demographics as timescale preferences (sitting length and count, unit length, reset rhythm, patience, habit, social mode, drivers wanted). **Priors**, each marked `sure: low` or `medium`. | Anyone; replace a prior with telemetry as soon as it exists |
| `personas.yaml` | Eight particular people from those segments (Mira, Jonah, the Night Crew, the Commuter, the Painter, the Returner, the Party Host, the Completionist), with skill, place weights, a puzzle seat, group size and triggers. The ids are shared with the player-journeys work (`Docs/LoopAtlas/personas/journeys.yaml`). | Anyone |
| `expectations.yaml` | Activation expectations (E1-E10): what each kind of player should meet and by when, judged on the runs. Proposed targets except the D1/D7 gate. | Anyone (brainstorming); Shombith leads activation |
| `out/journeys.json` | One representative run per persona and scenario as steps `{day, minute, loop, dur_s, parent, crystals, event, with, layer}`, plus per-day state and milestones. The journey animation plays it as its "simulated" layer. | Generated |
| `out/summary.json` | The Monte Carlo (200 seeded runs per persona and scenario), the fit table, the flags and the expectation verdicts. | Generated |
| `REPORT.md` | All of it as tables and per-persona notes. | Generated |

## Run it

```
python3 Tools/Build/loop_timescale_sim.py report      # rebuild out/ and REPORT.md (about 25 s)
python3 Tools/Build/loop_timescale_sim.py --check     # the drift gate CI runs
python3 Tools/Build/loop_timescale_sim.py self-test   # negative controls
python3 Tools/Build/loop_timescale_sim.py fit         # just the mismatch flags, as JSON lines
```

Rebuild after any change to the loop model (`model.json`), `economy.yaml`, `segments.yaml`, `personas.yaml` or
`expectations.yaml`, and commit the outputs with the change. The run is seeded, so the same inputs give the same files.

## The three scenarios

They follow the atlas layers (`Docs/LoopAtlas/PROCESS.md`):

- **today** (Now): `DeveloperUnlockGate.DefaultAllUnlocked` is on, so every place and vessel is open, no guide runs,
  and crystals buy nothing. Voyages are single-device (the networked Ark is not built), the voyage is the Crystal Run
  (about 11 minutes).
- **planned** (Planned): the gate flipped. The quest chain as the loop states it (I4 on Scurry, Hex Race, Joust and
  Maelstrom) opens the Hangar, the Arena and Voyages; a voyage is the planned 20 to 40 minutes for a party of up to
  three; seven vessels at 4,000; only placing pays (200 / 50 / 0).
- **proposed** (Proposed): planned plus the fixes the simulation tests, each a proposal or a hole's fill: The Tide (P6),
  one currency (P10), more sinks (P7), voyage chapters (P13), a puzzle-first door (P14), a crew vessel in every squad
  (P15), making pays (P16), a quest clearing at I2, and the breath (P5).

## How a run works

Each day, a persona has a number of sittings from its segment's schedule. It comes back with a chance that starts at
the segment's habit and moves with how the last sittings felt: crystals earned, something unlocked, a unit finished,
the places matching the drivers the segment wants, playing with a friend; and against: a lost voyage haul, a goal more
wins away than the segment's patience, no seat for the person who asked, being made to fly when they came to solve,
nothing left to buy. Novelty fades a little every day. Below a floor the persona lapses; the Tide's news can bring a
lapsed persona back. A sitting is filled with real loops from the model (rounds from their `t` spans, toys, voyage
phases), paid from `economy.yaml`.

What it is not: a forecast. The segment numbers are priors and the satisfaction weights are a model. Read the
differences between scenarios and between personas, and the holes they point at. When the analytics funnel exists
(`Docs/ACTIVATION.md` section 6 step 3), fit `segments.yaml` to it and the same report becomes a measurement.

## Where the findings went

Into the atlas's Proposed layer (brainstorming, `Docs/LoopAtlas/PROCESS.md`): proposals P13 to P16 in
`model/proposals.yaml`, and holes with fills on `arc.activation`, `arc.unlock`, `sitting`, `arc.fleet`, `arc.social`,
`arc.saga` and `toybox`, each citing `REPORT.md`. Nothing was planned or built: that is the owner's call.
