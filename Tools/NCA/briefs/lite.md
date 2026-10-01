# lite: HOLD the front runner, then make it the lightest, most scalable implementation

DIRECTION (from the game's lead, 2026-10-01 ~22:00 UTC, verbatim): "Once a front runner starts scoring well
enough we will want to hold its scores including a score for smoothness of changes. Then optimize for the
lightest weight or most computationally performant and scalable method. One thing we did before is only
update a fraction of a larger swarm each frame."

Front runners (Tools/NCA/DISCOVERIES.md "Update 21:40 UTC" + the seed-101 rescore under it):
- combo (combo_model.py, results/combo/params.json, the G8 grid): 16/16, 15/16, 15/16, 15/16 at seeds
  7/23/41/101, lossless, organic, 12.2 ms/step @134 (Python, 1 thread). The ACCURACY front runner.
- sortfeel (sortfeel_model.py, results/sortfeel/params.json): 12/13, 11/13, 13/13, 13/13 feasible, lossless,
  organic (planar 0.05), 6.3 ms/step. The LIGHT front runner. (Its 3 n/a switches are bodies that hold < 2 of
  the target element - not failures.)
Your TARGET (below) names which one is yours.

STEP 1 - HOLD (before changing anything). The hold is two new shared files; use them unchanged:
- `swarm_smooth.py` - SMOOTHNESS OF CHANGE: over the 4 standard switches and a vessel strike on each plan it
  records every step and scores loss-path monotonicity, settle time, lurch (worst-step p95 speed over the
  body's steady speed), jerk during the morph, molt bursts and birth bursts (a share of the body changing
  at once = a pop), and self-inflicted deaths; `smoothness` = 1/(1 + mean rough) in (0,1]. Read its
  docstring.
- `hold.py` - `python Tools/NCA/hold.py --model <spec> --record results/hold/<name>.json` freezes ACCURATE
  (swarm_eval at seeds 7,23,41,101, 3 samples, loss-8 bar), LOSSLESS, ORGANIC (swarm_feel band + planar),
  SMOOTH and reports PERFORMANT. `--baseline results/hold/<name>.json` checks a candidate and exits 1 on any
  regression (rules in its docstring). PERFORMANT is reported, never gated: it is what you optimise.
Record the official baseline FIRST, commit it, and never re-record it. Every candidate you publish must PASS
`hold.py --baseline` against it - report the full PASS/FAIL table in your NOTE. If you find hold.py or
swarm_smooth.py wrong (a metric that rewards a visibly worse body, a tolerance that is noise), do not edit
them: write your evidence in your NOTE and tell me in the commit message; I own those files.
Note: swarm_nca's LOSS now ranges the slot->domain assignment over every domain id (LOSS_PERMS; it was
restricted to ids {0,1} for a 2-region plan - a perfect jellyfish coloured with teams 0 and 2 scored 14.5).
It only ever lowers a loss; models that read sn.PERMS for their own wells are unchanged.

SMOOTHNESS CALIBRATION (results/hold/calibration/*.json, swarm_smooth at seed 7): smoothness evo 0.58 (the
lead's "beautifully organic") > combo 0.51 = field 0.51 > sortfeel 0.44. Both front runners JOLT: right after a
cull or a strike their per-step speed spikes to 4-7x the change's own median pace (lurch 6.3 / 6.7 worst) where
evo eases in at ~1.7. Neither backtracks, neither teleports, and molt/birth bursts are modest. So a cheap,
specific way to RAISE the held smoothness while you lighten the model: ramp the response (an acceleration
limit, or the corrector's gain easing in over ~16-32 steps after a composition change). You may publish a
config that beats the baseline on smoothness - the hold only forbids getting worse.

STEP 2 - PROFILE. Where does the time go per step (ProfilerMarker-style: time each phase of the step with
time.perf_counter over 200 steps of a grown body)? Separate the per-step O(N) / O(N^2) / O(grid) work from
the bookkeeping (composition census, molt quota, plan cache) that need not run every step.

STEP 3 - MAKE IT LIGHT. Every idea is a flag on a SUBCLASS (new file lite_<target>_model.py), default off, so
each one is measured alone and in combination against the hold:
 a. FRACTIONAL UPDATE (the lead's idea, what the game did before): each step only a 1/k share of tadpoles
    (round-robin by slot, or a per-tadpole phase) recomputes its steering/sensing; the rest coast on their
    last velocity (or a cheap extrapolation). Rates that are per-update (laying, molting, fate choice) must
    be scaled by k so the dynamics PER UNIT TIME are unchanged; the hold is measured at the same step
    counts. Report k = 1, 2, 4, 8, 16: ms/step, and the hold table - smoothness is the axis most at risk.
 b. AMORTISED BOOKKEEPING: run the composition controller / census / plan cache every m steps (m = 4, 8,
    16), or incrementally (update counts on birth/molt/death instead of recounting).
 c. VECTORISE: remove per-tadpole / per-batch Python loops (the `for b in range(B)` and `.tolist()` loops in
    the step). Batch-friendly code is also what makes many swarms cheap.
 d. CHEAPER SENSING: neighbour queries by a uniform spatial hash instead of all-pairs; a coarser fine grid;
    fewer collision iterations; float16 where it does not move a score.
 e. Your own ideas. Anything that removes work without losing a held score.
SCALABILITY is part of the score: report ms per SWARM-step for B = 1, 4, 16, 64 swarms batched (the game runs
several populations per cell), and ms per TADPOLE-step, before and after. If a fractional update makes cost
independent of swarm size at fixed per-frame budget, say so with numbers.

STEP 4 - DELIVER: results/lite_<target>/ with NOTE.md (profile table before/after, the Pareto front of
ms/step against smoothness and accuracy, the hold PASS/FAIL table for every published config, and the ONE
config you recommend for the game with its estimated C# cost: the in-game C# cores on cece/swarm-fauna-game
run 5-15x faster than these Python prototypes, so give a per-operation count, not just Python ms),
params.json for the recommended config, summary.json + rollout.json (as swarm_gpu.py publishes them, so the
gallery shows it), smooth.json, and the hold check JSON. Add a `lite_<target>:` branch to
swarm_eval.load_model if you need one (append only). Commit and push at least every ~60 minutes.
