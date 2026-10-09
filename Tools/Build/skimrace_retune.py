#!/usr/bin/env python3
"""
Retune ONE Skim Race intensity's AI for the map that is in the scene now - the command that
`skimrace_track_fingerprint.py --check` and the in-game warning name when a map has changed.

    python3 Tools/Build/skimrace_retune.py 2              # about half an hour to an hour (longer tracks longer)
    python3 Tools/Build/skimrace_retune.py 5              # a NEW intensity: tuned starting from the general policy
    python3 Tools/Build/skimrace_retune.py 2 --dry-run    # everything except writing the result
    python3 Tools/Build/skimrace_retune.py 2 --iters 4 --seeds 2 --final 6   # a quick, rough pass

What it does, in order (Docs/SKIM_RACE_AI.md section 11):

 1. Reads that intensity's map fingerprint from MinigameSkimRace.unity, and checks the game's own C#
    (compiled into the simulator) computes the same value - so the fingerprint it records is the one the
    game will compare against.
 2. Starts from the intensity's own tuning (SkimRaceAIConfig_I<n> in author_skimrace_ai_config.py), or from
    the general policy when it has none, and tunes it on THIS track in the offline simulator with the search
    that made the general policy (run.sh tuneall <n> 4 16 sigma=0.15), restricted to the numbers the policy
    already uses (only=stated): a retune re-fits numbers, it never switches a control on or off.
 3. Races the new tuning and the general policy on the same fresh races (seedbase 99000) and keeps the new
    one only if it does at least as well - more races finished, then a faster median winner. A tuning file
    that loses to the general policy would make the AI worse than having none.
 4. Writes the SkimRaceAIConfig_I<n> block - the new numbers, a bumped PolicyVersion, the new
    TrackFingerprint and a comment saying how it was made - into author_skimrace_ai_config.py, regenerates
    the assets, and runs both --checks.

Nothing is committed: review the diff (Tools/Build/author_skimrace_ai_config.py, Assets/Resources/), then
commit it. Exit codes: 0 written (or would be, with --dry-run), 2 the general policy did better (nothing
written - the game keeps flying the general policy on that track), 1 an error.

The simulator needs a dotnet 8+ SDK (Tools/Build/skimrace_sim_harness/run.sh).
"""
import argparse
import datetime
import importlib.util
import os
import re
import subprocess
import sys
import textwrap

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
AUTHOR = os.path.join(HERE, "author_skimrace_ai_config.py")
RUN = os.path.join(HERE, "skimrace_sim_harness", "run.sh")
GENERAL = "SkimRaceAIConfig"
# How every shipped policy was last tuned and checked: 2 AI seats, 28 ms frames +-50% (section 6.12).
PHYSICS = ["ph.Seats=2", "ph.Dt=0.028", "ph.DtJitter=0.5"]
FRESH_SEEDBASE = 99000  # the same fresh races tuneall's own final check uses

sys.path.insert(0, HERE)
import skimrace_track_fingerprint as track_fingerprint  # noqa: E402


def load_author():
    spec = importlib.util.spec_from_file_location("author_skimrace_ai_config", AUTHOR)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def policy_args(policy):
    """A POLICIES entry as simulator Field=value arguments (what policy_args.py prints)."""
    return [f"{k}={int(v) if isinstance(v, bool) else v}" for k, v in policy.items()
            if k not in ("PolicyVersion", "TrackFingerprint")]


def sim(args, echo=False):
    """Run the simulator and return its output (echoed live while it tunes)."""
    cmd = ["bash", RUN] + [str(a) for a in args]
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=ROOT)
    out = []
    for line in proc.stdout:
        out.append(line)
        if echo:
            print("    " + line, end="", flush=True)
    if proc.wait() != 0:
        sys.exit(f"skimrace_retune: the simulator failed ({' '.join(map(str, args[:2]))}):\n" + "".join(out[-25:]))
    return "".join(out)


def race(intensity, policy, races, limit):
    """Races `policy` on the fresh races; returns (races every seat finished, races, median winner seconds)."""
    out = sim(["eval", intensity, races, f"seedbase={FRESH_SEEDBASE}", f"limit={limit}"] + policy_args(policy) + PHYSICS)
    fin = re.search(r" finished (\d+)/(\d+) median=", out)
    win = re.search(r"first finisher \(editor-comparable\): <= \S+ s in \d+/\d+, median ([\d.]+)", out)
    if not fin or not win:
        sys.exit("skimrace_retune: could not read the simulator's eval summary:\n" + out[-2000:])
    return int(fin.group(1)), int(fin.group(2)), float(win.group(1))


def number(text):
    v = float(text)
    return int(v) if v == int(v) else v


def literal(v):
    if isinstance(v, bool):
        return "True" if v else "False"
    if isinstance(v, str):
        return '"' + v + '"'
    return repr(v)


def bumped_version(start_version, intensity, from_own):
    m = re.match(r"^(.*-v)(\d+)(-.*)?$", start_version) if from_own else None
    return f"{m.group(1)}{int(m.group(2)) + 1}{m.group(3) or ''}" if m else f"skimrace-v1-i{intensity}"


def render_block(name, policy, comment):
    lines = [f"    # {c}" for c in comment]
    lines.append(f'    "{name}": {{')
    lines += [f'        "{k}": {literal(v)},' for k, v in policy.items()]
    lines.append("    },")
    return "\n".join(lines) + "\n"


def write_block(name, intensity, block):
    """Replace the policy's block (with the comment above it) in author_skimrace_ai_config.py, or insert it
    in intensity order."""
    lines = open(AUTHOR, encoding="utf-8").read().splitlines(keepends=True)
    head = f'    "{name}": {{\n'
    if head in lines:
        i = lines.index(head)
        start = i
        while lines[start - 1].startswith("    #"):
            start -= 1
        end = next(j for j in range(i, len(lines)) if lines[j] == "    },\n")
        lines[start:end + 1] = [block]
    else:
        open_at = lines.index("POLICIES = {\n")
        close_at = next(j for j in range(open_at, len(lines)) if lines[j] == "}\n")
        at = close_at
        for j in range(open_at, close_at):
            m = re.match(r'    "SkimRaceAIConfig_I(\d+)": \{\n', lines[j])
            if m and int(m.group(1)) > intensity:
                at = j
                while lines[at - 1].startswith("    #"):
                    at -= 1
                break
        lines[at:at] = [block]
    open(AUTHOR, "w", encoding="utf-8").write("".join(lines))


def main():
    ap = argparse.ArgumentParser(description="Retune one Skim Race intensity's AI for the map in the scene.")
    ap.add_argument("intensity", type=int)
    ap.add_argument("--seeds", type=int, default=4, help="races per candidate per search step (default 4)")
    ap.add_argument("--iters", type=int, default=16, help="search steps (default 16)")
    ap.add_argument("--final", type=int, default=20, help="fresh races in the head-to-head check (default 20)")
    ap.add_argument("--dry-run", action="store_true", help="do everything except write the result")
    args = ap.parse_args()
    i = args.intensity
    name = f"SkimRaceAIConfig_I{i}"

    # 1. The map, as the script and as the game's C# both read it.
    live = track_fingerprint.fingerprints()
    if i not in live:
        sys.exit(f"skimrace_retune: MinigameSkimRace.unity has no track for intensity {i} "
                 f"(it has {', '.join(map(str, sorted(live)))}) - author its waypoints first.")
    fp = live[i]
    in_game = re.search(rf"^I{i}: ([0-9a-f]{{8}})$", sim(["fingerprint"]), re.M)
    if not in_game or in_game.group(1) != fp:
        sys.exit(f"skimrace_retune: the game's C# reads this map as {in_game and in_game.group(1)}, the script as {fp} - "
                 "SkimRaceTrackFingerprint.cs and skimrace_track_fingerprint.py disagree; fix that before retuning.")

    author = load_author()
    general = author.POLICIES[GENERAL]
    own = author.POLICIES.get(name)
    start = own or general
    print(f"Intensity {i}: map {fp}. Starting from {name if own else GENERAL + ' (no file of its own yet)'} "
          f"({start['PolicyVersion']}).")
    if own and own.get("TrackFingerprint") == fp:
        print("  (its map has not changed since it was tuned - retuning anyway)")

    # 2. Tune on this track.
    switches = ["set=winner"] if start.get("UseTrackMpc") else []
    tune_cmd = ["tuneall", i, args.seeds, args.iters, "sigma=0.15", "final=0", "only=stated"] + switches
    print(f"  Tuning in the simulator: run.sh {' '.join(map(str, tune_cmd))} + {name if own else GENERAL} + "
          f"{' '.join(PHYSICS)} ...", flush=True)
    out = sim(tune_cmd + policy_args(start) + PHYSICS, echo=True)
    limit = int(re.search(rf"I{i}: ideal [\d.]+ s, raced to (\d+) s", out).group(1))
    best = dict(kv.split("=", 1) for kv in re.findall(r"^BEST (.*)$", out, re.M)[-1].split())
    tuned = {k: v for k, v in start.items() if k not in ("PolicyVersion", "TrackFingerprint")}
    tuned.update({k: number(v) for k, v in best.items()})
    candidate = {"PolicyVersion": bumped_version(start["PolicyVersion"], i, own is not None), "TrackFingerprint": fp}
    candidate.update(tuned)

    # 3. Head to head with the general policy on the same fresh races.
    print(f"  Racing the new tuning and the general policy on the same {args.final} fresh races "
          f"(seedbase {FRESH_SEEDBASE}, cut at {limit} + 60 s) ...", flush=True)
    new_fin, races, new_win = race(i, candidate, args.final, limit)
    gen_fin, _, gen_win = race(i, general, args.final, limit)
    print(f"    new tuning ({candidate['PolicyVersion']}): finished {new_fin}/{races}, winner median {new_win:.1f} s")
    print(f"    general    ({general['PolicyVersion']}): finished {gen_fin}/{races}, winner median {gen_win:.1f} s")
    if (new_fin, -new_win) < (gen_fin, -gen_win):
        print(f"\nThe general policy did better on this track, so nothing was written: intensity {i} keeps flying "
              f"the general policy (the game already does while the map differs). Try more search steps "
              f"(--iters 32), or remove {name} so the general policy owns this track (Docs/SKIM_RACE_AI.md section 11).")
        return 2

    # 4. Write it.
    date = datetime.date.today().isoformat()
    comment = textwrap.wrap(
        f"Intensity {i}: retuned {date} by Tools/Build/skimrace_retune.py for map {fp} (tuneall {i} {args.seeds} "
        f"{args.iters} sigma=0.15 only=stated{' set=winner' if switches else ''} from {start['PolicyVersion']}, "
        f"{len(best)} numbers searched; 2 AI seats, 28 ms frames +-50%). On {races} fresh races (seedbase "
        f"{FRESH_SEEDBASE}, cut at {limit} + 60 s) it finished {new_fin}/{races}, winner median {new_win:.1f} s; "
        f"the general policy ({general['PolicyVersion']}) on the same races: {gen_fin}/{races}, {gen_win:.1f} s.",
        width=104)
    block = render_block(name, candidate, comment)
    if args.dry_run:
        print("\n--dry-run: would write this block into Tools/Build/author_skimrace_ai_config.py:\n\n" + block)
        return 0
    write_block(name, i, block)
    for script, extra in (("author_skimrace_ai_config.py", []), ("author_skimrace_ai_config.py", ["--check"]),
                          ("skimrace_track_fingerprint.py", ["--check"])):
        r = subprocess.run([sys.executable, os.path.join(HERE, script)] + extra, cwd=ROOT, text=True, capture_output=True)
        print(r.stdout, end="")
        if r.returncode != 0:
            sys.exit(f"skimrace_retune: {script} {' '.join(extra)} failed:\n{r.stdout}{r.stderr}")
    print(f"\nWrote {name} ({candidate['PolicyVersion']}, map {fp}). Review and commit "
          "Tools/Build/author_skimrace_ai_config.py and Assets/Resources/.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
