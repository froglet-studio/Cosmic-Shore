#!/usr/bin/env python3
"""Dump the research substrate's species PARAMETER SETS to research_params.json - the fixture the C# harness
(Tools/Build/substrate_harness, test F) asserts SubstrateResearch.cs against, number for number.

    python3 Tools/Build/substrate_harness/research_fixture.py [<research Tools/Ecology dir>] [<burn-rules.md>]

The research lives on the eco branches (Tools/Ecology/substrate/species.py, Docs/SUBSTRATE_FAUNA.md §1). When it
is not reachable the committed JSON is kept and the harness still checks the C# against it. Needs numpy (the
research module imports it); nothing is simulated here.

Round 11-11 adds:
  * stampede and leviathan (species.py), the leviathan's BodyPlan (scale, well, speed) and research
    manta_slots(96) - the body plan at the game's member count (the research binds K to n0);
  * "bestiary": the numbers the ports take from the BESTIARY (bestiary/species/{stampede,mobber,leech,leviathan,pack}.py)
    read straight out of the source - named constants and the inline literals each rule is written with - plus the
    burn rules' drain weight (burn-rules.md). A rule rewritten so a pattern no longer matches FAILS here rather
    than silently keeping an old number.
"""
import dataclasses
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "research_params.json")
SRC = sys.argv[1] if len(sys.argv) > 1 else "/home/claude/research/Tools/Ecology"
BURN = sys.argv[2] if len(sys.argv) > 2 else "/mnt/project-files/overnight/burn-rules.md"
SUBSTRATE = ("locust", "pack", "lurker", "stampede", "leviathan")

# (key, file, regex with ONE number group) - each must match exactly once
BESTIARY = [
    ("stampede.BULL_R", "stampede", r"SENSE, BULL_R, HEAD_DOWN, SCENT = [\d.]+, ([\d.]+),"),
    ("stampede.HEAD_DOWN", "stampede", r"SENSE, BULL_R, HEAD_DOWN, SCENT = [\d.]+, [\d.]+, ([\d.]+),"),
    ("stampede.charge_speed", "stampede", r"desired\[ch\] = unit\(lead\[ch\]\) \* ([\d.]+)"),
    ("stampede.charge_s", "stampede", r"self\.charge = np\.where\(go, ([\d.]+),"),
    ("stampede.charge_accel", "stampede", r"acc = np\.where\(ch, ([\d.]+),"),
    ("stampede.rest_s", "stampede", r"self\.rest = np\.where\(was & \(self\.charge <= 0\), ([\d.]+),"),
    ("stampede.bull_every", "stampede", r"self\.bull = \(np\.arange\(n\) % (\d+)\) == 0"),
    ("stampede.lead_clip", "stampede", r"np\.clip\(dist / [\d.]+, 0, ([\d.]+)\)"),
    ("stampede.closing", "stampede", r"axis=1\) > ([\d.]+) \* np\.maximum\(sp"),
    ("stampede.n", "stampede", r"def __init__\(self, arena, n=(\d+)"),
    ("pack.WINDUP", "pack", r"\nWINDUP = ([\d.]+)"),
    ("mobber.MAXV", "mobber", r"MAXV, DIVE_V, PULL = ([\d.]+),"),
    ("mobber.DIVE_V", "mobber", r"MAXV, DIVE_V, PULL = [\d.]+, ([\d.]+),"),
    ("mobber.PULL", "mobber", r"MAXV, DIVE_V, PULL = [\d.]+, [\d.]+, ([\d.]+)"),
    ("mobber.dive_s", "mobber", r"self\.dive = np\.where\(godive, ([\d.]+),"),
    ("mobber.clock", "mobber", r"self\.clock = np\.where\(startpull, ([\d.]+) \+ PULL"),
    ("mobber.provoke_r", "mobber", r"provoke = \(dist < ([\d.]+)\)"),
    ("mobber.slow", "mobber", r"pspeed\[k\] < ([\d.]+)\)"),
    ("mobber.roost_r", "mobber", r"self\.roost\[self\.home\], axis=1\) < ([\d.]+)"),
    ("mobber.orbit_r", "mobber", r"np\.clip\(r - ([\d.]+), -40, 80\)"),
    ("mobber.pull_r", "mobber", r"\(self\.dive <= 0\) & \(dist < ([\d.]+)\)"),
    ("mobber.jink_cos", "mobber", r"unit\(-off\), axis=1\) > ([\d.]+)\) & \(dist < [\d.]+\)"),
    ("mobber.jink_r", "mobber", r"unit\(-off\), axis=1\) > [\d.]+\) & \(dist < ([\d.]+)\)"),
    ("mobber.peck_r", "mobber", r"\(dist < arena\.pilots\[0\]\.radius \+ ([\d.]+)\)"),
    ("mobber.n", "mobber", r"def __init__\(self, arena, n=(\d+)"),
    ("mobber.mob_m", "mobber", r"mob = al & \(self\.m > ([\d.]+)\)"),
    ("mobber.loud", "mobber", r"tgt = np\.where\(provoke, 1\.0, ([\d.]+) \* loud"),
    ("mobber.hear_r", "mobber", r"loud = np\.where\(d < ([\d.]+), self\.m"),
    ("mobber.m_rate", "mobber", r"min\(1, ([\d.]+) \* dt\), self\.m - "),
    ("mobber.steer", "mobber", r"self\.vel = steer\(self\.vel, des, ([\d.]+), dt\)"),
    ("leech.MAX_PER_HULL", "leech", r"MAX_PER_HULL = (\d+)"),
    ("leech.SENSE", "leech", r"SENSE = ([\d.]+)"),
    ("leech.pounce_v", "leech", r"unit\(aim\[pounce\] - self\.pos\[pounce\]\) \* ([\d.]+)"),
    ("leech.lead_clip", "leech", r"tau = np\.clip\(dist / [\d.]+, 0, ([\d.]+)\)"),
    ("leech.hop", "leech", r"self\.hop = np\.full\(n, ([\d.]+)\)"),
    ("leech.drift", "leech", r"desired = unit\(cen \* 0\.05 \+ wander\) \* ([\d.]+)"),
    ("leech.grip_turn", "leech", r"if omega\[j\] > ([\d.]+) else"),
    ("leech.grip_loss", "leech", r"-\(omega\[j\] - [\d.]+\) \* ([\d.]+)"),
    ("leech.grip_gain", "leech", r"else ([\d.]+)\) \* dt"),
    ("leech.sip", "leech", r"if self\.sip\[i\] >= ([\d.]+):"),
    ("leech.daze", "leech", r"self\.daze\[i\] = ([\d.]+);"),
    ("leech.fling", "leech", r"self\.rng\.choice\(\[-1, 1\]\) \* ([\d.]+)"),
    ("leech.rel", "leech", r"if pounce\[i\] or rel < ([\d.]+):"),
    ("leech.n", "leech", r"def __init__\(self, arena, n=(\d+)"),
    ("leech.pounce_steer", "leech", r"np\.where\(pounce\[free\], ([\d.]+), [\d.]+\)"),
    ("leviathan.gulp_r", "leviathan", r"ahead = \(np\.linalg\.norm\(dm, axis=1\) < ([\d.]+)\)"),
    ("leviathan.gulp_cos", "leviathan", r"@ self\.head\) > ([\d.]+)\)"),
    ("leviathan.gulp_prep", "leviathan", r"if self\.gulp_prep >= ([\d.]+):"),
    ("leviathan.gulp_s", "leviathan", r"self\.gulp, self\.gulp_prep = ([\d.]+), 0\.0"),
    ("leviathan.gulp_v", "leviathan", r"spd = ([\d.]+) if self\.gulp > 0"),
    ("leviathan.gulp_rest", "leviathan", r"if self\.gulp <= 0: self\.gulp_rest = ([\d.]+)"),
    ("leviathan.curious_r", "leviathan", r"if dp\.min\(\) < ([\d.]+):"),
    ("leviathan.curious_w", "leviathan", r"want = unit\(want \+ ([\d.]+) \* unit\(PP"),
]


def bestiary(src):
    out, missing = {}, []
    text = {}
    for key, f, pat in BESTIARY:
        if f not in text:
            text[f] = open(os.path.join(src, "bestiary", "species", f + ".py")).read()
        m = re.findall(pat, text[f])
        if len(m) != 1:
            missing.append(f"{key} ({len(m)} matches)")
            continue
        out[key] = float(m[0])
    if missing:
        raise SystemExit("bestiary rules changed shape - update the patterns: " + ", ".join(missing))
    # a mobber's dive rhythm: the clock restarts at clock + PULL when the pull-up starts
    out["mobber.period"] = out["mobber.clock"] + out["mobber.PULL"]
    return out


def burn_rules(path):
    t = open(path).read()
    m = re.search(r"drain ([\d.]+)", t)
    if not m:
        raise SystemExit(f"{path}: no 'drain <weight>' in the danger kinds")
    return {"burn.drain": float(m.group(1))}


def main():
    if not os.path.isdir(os.path.join(SRC, "substrate")):
        print(f"research not reachable at {SRC} - committed {os.path.relpath(OUT)} kept")
        return 0
    sys.path.insert(0, SRC)
    from substrate import species as S   # noqa: E402

    out = {}
    for name in SUBSTRATE:
        P = getattr(S, name)()
        d = {}
        for f in dataclasses.fields(P):
            v = getattr(P, f.name)
            if f.name in ("solitary", "gregarious"):
                d[f.name] = {k: (list(x) if isinstance(x, tuple) else x) for k, x in dataclasses.asdict(v).items()}
            elif f.name == "body":
                d[f.name] = None if v is None else {"scale": v.scale, "well": v.well, "speed": v.speed,
                                                    "k": int(len(v.slots))}
            else:
                d[f.name] = v
        out[name] = d
    out["manta_slots_96"] = [float(x) for x in S.manta_slots(96).reshape(-1)]
    out["bestiary"] = bestiary(SRC)
    if os.path.exists(BURN):
        out["bestiary"].update(burn_rules(BURN))
    else:
        prev = json.load(open(OUT)) if os.path.exists(OUT) else {}
        keep = prev.get("bestiary", {}).get("burn.drain")
        if keep is None:
            raise SystemExit(f"burn rules not reachable at {BURN} and no committed drain weight to keep")
        out["bestiary"]["burn.drain"] = keep
    with open(OUT, "w") as fh:
        json.dump(out, fh, indent=1, sort_keys=True)
        fh.write("\n")
    print(f"wrote {os.path.relpath(OUT)} ({', '.join(SUBSTRATE)}, manta_slots(96), {len(out['bestiary'])} bestiary numbers)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
