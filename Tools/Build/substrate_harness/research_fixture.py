#!/usr/bin/env python3
"""Dump the research substrate's species PARAMETER SETS to research_params.json - the fixture the C# harness
(Tools/Build/substrate_harness, test F) asserts SubstrateResearch.cs against, number for number.

    python3 Tools/Build/substrate_harness/research_fixture.py [<research Tools/Ecology dir>]

The research lives on the eco branches (Tools/Ecology/substrate/species.py, Docs/SUBSTRATE_FAUNA.md §1). When it
is not reachable the committed JSON is kept and the harness still checks the C# against it. Needs numpy (the
research module imports it); nothing is simulated here.
"""
import dataclasses
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "research_params.json")
SRC = sys.argv[1] if len(sys.argv) > 1 else "/home/claude/research/Tools/Ecology"


def main():
    if not os.path.isdir(os.path.join(SRC, "substrate")):
        print(f"research not reachable at {SRC} - committed {os.path.relpath(OUT)} kept")
        return 0
    sys.path.insert(0, SRC)
    from substrate import species as S   # noqa: E402

    out = {}
    for name in ("locust", "pack", "lurker"):
        P = getattr(S, name)()
        d = {}
        for f in dataclasses.fields(P):
            v = getattr(P, f.name)
            if f.name in ("solitary", "gregarious"):
                d[f.name] = {k: (list(x) if isinstance(x, tuple) else x) for k, x in dataclasses.asdict(v).items()}
            elif f.name == "body":
                d[f.name] = None if v is None else "plan"
            else:
                d[f.name] = v
        out[name] = d
    with open(OUT, "w") as fh:
        json.dump(out, fh, indent=1, sort_keys=True)
        fh.write("\n")
    print(f"wrote {os.path.relpath(OUT)} ({', '.join(out)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
