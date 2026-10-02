# Bestiary (Direction B): threat fauna on local rules

Eight species, one module each in `species/`. Each is a struct-of-arrays population whose every step is a
weighted sum of steering terms driven by continuous drives. No behaviour trees, no scripted sequences. The
shared helpers live in `core.py`. Results and the design picks are in `../DISCOVERIES.md`, section "Bestiary".

| file | what |
|---|---|
| `species/<name>.py` | the species, its rules in the module docstring, `EMOTION`, `COUNTER`, `ABLATIONS` (negative controls) |
| `run.py` | scores species with the shared `common/scorecard.py`, plus the bestiary extras -> `scorecards.json` |
| `emotion_check.py` | an outside emotion read with Direction C's frozen probe (fetched from `cece/eco-emotion`) -> `emotion.json` |
| `build_viewer.py` + `viewer.html.tpl` | builds `bestiary.html`, a 45 s showcase of every species with its scorecard |
| `diag.py`, `diag_tel.py`, `emotion_explain.py` | small diagnostics used while tuning |

```
python Tools/Ecology/bestiary/run.py                     # all species, 6 pilots x 3 seeds, 90 s (~2 min, 4 procs)
python Tools/Ecology/bestiary/run.py --ablations         # plus every ablation (~8 min)
python Tools/Ecology/bestiary/emotion_check.py pack locust@55
python Tools/Ecology/bestiary/build_viewer.py            # -> bestiary.html (~2.3 MB)
```

Pilot TRAILS are on in every bestiary run (`Arena.enable_trails`, added to `common/` off by default), because
three species eat, graze or steal the trail.

Scorecard additions on top of the shared `combine`:
- `telegraph_first_s`: lead time of the FIRST strike of each engagement (a hit with no hit on that pilot in
  the previous 3 s). The shared median also counts every follow-up bite of a swarm, which it scores at ~0.1 s.
  The gate uses `telegraph_first_s`; both are reported.
- `counterplay_same_speed`: the shared evader at the wanderer's 120 u/s (the shared one flies 140).
- `counterplay_aware`: a same-speed pilot that flees only threats it can SEE (inside 350 u). The shared
  evader reads every threat in the cell.
- `hits_per_min_skimmer`, `hits_by_kind`, `conservation_max_drift`, `ms_per_step`, `us_per_agent_step`, `phase`.
