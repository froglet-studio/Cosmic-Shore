---
name: compute-job
description: Turn a problem into a Froglet Compute search the whole team's idle hardware can work on - define a sensible parametric SOLUTION SPACE and a LOSS FUNCTION aligned with an ambitious goal, write (or wrap) the evaluator that scores a point, pick the searcher (sep-CMA-ES for one best answer, MAP-Elites for many different good ones, a command job for a long training), prove it with a self-test and a negative control, and queue it. Use on "/compute-job", "make this a compute job", "search for the best X", "optimise / tune / balance X automatically", "learn the values for", "let the machines find", "turn this sweep / grid / tuner into a job", "write an evaluator", "what loss function", "define the search space", or when a lab, sim or harness already scores something and a person tunes its dials by hand.
---

# /compute-job: a space, a loss, and an evaluator

Read `Docs/Compute/README.md` sections 4-6 (the job file, how search is shared, the evaluator contract) first. A job is
worth the team's hardware when three things are true: **the goal is ambitious and clear, the space is the real set of
choices someone would otherwise make by hand, and the loss is cheap to compute and honest about what it cannot see.**

## 1. The goal first (one sentence, someone's)

Write the `goal` as what the GAME gains, in the words of whoever wants it: "an AI that races the Manta's own game well
at every intensity", "the course on which the fastest and slowest trained AIs finish closest together". If you cannot
name who wants it and who owns the area its answer changes (`area_owner`, from `who_works_where.py query`), it is not
a job yet: write it as a `proposed` job and say whose call it is.

## 2. The space: the real dials, bounded by what the game allows

- **Search the shipped fields by their shipped names.** A drive job searches `cornerSpeed`, not `corner`: the answer
  is then written into the asset with no rename (`race_models.DRIVE_FIELDS` maps names to a sim's attributes).
- **`init` is today's shipped value**, read from the asset (`race_models.read_asset_fields`), never retyped. The
  worker scores it as the baseline, so the board always says "this much better than today".
- **Bounds are the game's limits, not a guess at the answer**: what the code clamps to, what the generator's floors
  assume (a mouth the rails still pass through), the band a resource level lives in (`regatta_balance.MIN_LEVEL`).
  A bound the best keeps hitting is a finding: widen it only if the game allows it, and say so.
- **Fewer, meaningful dimensions beat many.** Normalise what must sum (corner angles to 360), tie what must move
  together, and leave out what the loss cannot see. Up to a few dozen is comfortable for sep-CMA-ES.
- **Types**: `float` (`log: true` for scales spanning decades), `int` (resource levels, counts, seeds), `choice`
  (a swim style, an element). A course or any procedural thing: put its SEED in the space (an int) only when the
  game ships that seed; when the game deals a new one each match, score several fixed seeds instead.

## 3. The loss: lower is better, cheap, honest, hard to game

- **Make it the goal's own number**: ln(slowest/fastest) for a spread, mean ln(seconds) so every intensity counts
  equally, a quality score per cell for diversity. ln() keeps a 10% gain worth the same everywhere.
- **Every way to "win" without serving the goal becomes a penalty**, stated in the docstring: a course that levels
  the field by making the race trivially short (keep the length), by removing the hairpins (keep the character), by
  breaking the rails (keep them legal). Think adversarially: the searcher WILL find the loophole.
- **Prefer the shipped model to a new one.** Import the sim from `<ctx.root>/Tools/Build` (so a job pinned to
  `Ys-bleeding-edge` scores that branch's code); never copy it. If the sim reads a fixed input (a course file),
  swap the reader for the call (`race_models.use_course`) rather than editing it.
- **Deterministic**: the same params give the same loss (fixed seeds inside). Noise makes CMA-ES chase luck.
- **A model's limits travel with its numbers**: put them in `metrics.limits` (copy `race_models.MODEL_LIMITS` or
  write your own) and list what is NOT modelled in the docstring, the labmaker way.
- **Cost**: aim for 0.05-10 s per evaluation. Slower is fine for a few hundred dimensions of value; a training that
  takes hours is a `command` job, not an evaluator.

## 4. The searcher

| You want | `algorithm.name` | Notes |
|---|---|---|
| one best answer (a drive, a course, a tuning) | `sep-cmaes` | `sigma0` 0.15-0.3 of the unit cube; `popsize` optional; restarts are automatic and half start from the team's best |
| many different good answers (bodies, play styles, course families) | `map-elites` | `descriptors` = 2-3 measured features with bins (`{name, lo, hi, bins}`), `worst` = the loss an acceptable answer beats; the evaluator returns `descriptors` |
| a baseline or a smoke test | `random` | |
| a long training that checkpoints (torch, a C# tuner) | `kind: command` | `argv` with `{python} {out} {root} {tools} {threads} {unit.<arg>}`, `units`, `resume` + `resume_if`, `progress` (`{file, key, of}`), `score`, `keep` (what relays to the next machine), `lease_hours` |

`convergence`: `min_evals` (do not call it early), `window_evals` and `min_improvement` (relative). MAP-Elites
convergence reads its QD score.

## 5. The evaluator (Tools/Compute/evaluators/<name>.py)

Copy the shape of `drive_fit.py` (a sim wrapped) or `bioform_forms.py` (a generator scored). Required: `VERSION`,
`PATHS`, `evaluate(params, ctx) -> {loss, metrics, valid?, descriptors?}`, `self_test() -> [failures]`; recommended
`inputs(args)` (files hashed into the epoch), `setup(ctx)`, `baseline(args)`. **The self-test reproduces something
known** (the shipped values give the sim's own published number; a known-good input is valid) **and holds a negative
control** (a broken input scores worse; a changed input is seen). Add the module to `selftest.EVALUATORS`.

Bump `VERSION` whenever the loss changes meaning: it starts a new epoch, so old and new numbers never mix.

## 6. Queue it

```
python3 Tools/Compute/compute.py new <id> --evaluator <module>     # scaffold (status: proposed)
python3 Tools/Compute/compute.py eval <id>                          # score the shipped values here, see the metrics
python3 Tools/Compute/compute.py run --local --no-push --always --once --job <id> --max-evals 200   # a short real session
python3 Tools/Compute/compute.py validate && python3 Tools/Compute/compute.py self-test
```

Set `status: ready` when the evaluator is on the job's queue ref and the short session improved on the baseline (or
proved the baseline is already the optimum: that is a finding too). Set `ref` to the branch whose game code it must
score (`origin/Ys-bleeding-edge` for AI, `origin/bleeding-edge` otherwise), `requires` to what it imports, `paths` to
what it reads, `value` honestly (0-10), and write `promote`: what changes, with which tool, and who must agree.
The job file merges like any change (README section 7); an AI job rides `Ys-bleeding-edge`.

## 7. Patterns worth stealing

- **A lab as a loss.** A `/labmaker` lab with a headless scorecard (`runBatch`) is a loss already: wrap it with
  `node` in the evaluator (`requires: ["node"]`), map the lab's dials to the space, its scorecard to the loss.
- **A hand sweep as a job.** A `--sweep` grid in `Tools/Build` (the scout's `sweeps` lens lists them) already names
  its dials and its loss: lift them into a space and an evaluator; delete nothing of the author's.
- **A port with a parity gate.** When the game's generator is C#, port it once to Python with float32 per op and gate
  the port against the C# harness's own output (`course_port.py`): every contributor can then run it without .NET.
- **Imitation and self-play are partners.** Where people play, the learners imitate them; a compute job fills the
  cells nobody plays and checks the ones they do (`/regatta-balance` stage 1). Never let one overwrite the other
  without the AI owner.
