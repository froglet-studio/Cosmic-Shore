"""Docs/Compute/jobs/unity-train.json's pairs and units, from the AI trainer's own catalog (README section 16).

    python3 Tools/Compute/make_unity_job.py            rewrite the job's command.pairs and command.units
    python3 Tools/Compute/make_unity_job.py --check    exit 1 when the job and the catalog disagree

One unit per vessel and mode at intensity 4: every row of TrainingModeCatalog.Live (read by
Tools/Build/author_training_scenarios.py's parser), with the row's hull when the card locks it, else every hull the card
seats (Docs/AI_SYSTEM/vessel_mode_chart.json). The pairs are keyed by the GameModes name, which is how the chart and the
console name a mode. Everything else in the job file is left as it is.
"""
import argparse
import json
import os
import sys

import compute_lib as L

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
JOB = os.path.join(ROOT, "Docs", "Compute", "jobs", "unity-train.json")
CHART = os.path.join(ROOT, "Docs", "AI_SYSTEM", "vessel_mode_chart.json")
EPISODES = 200


def build(job, root=ROOT):
    import author_training_scenarios as A
    m = A.model(root)
    with open(os.path.join(root, "Docs", "AI_SYSTEM", "vessel_mode_chart.json"), encoding="utf-8") as fh:
        cards = {c["mode"]: c for c in json.load(fh).get("cards") or []}
    pairs, units = {}, []
    skip = (L.read_json(os.path.join(root, "Docs", "Compute", "skip_modes.json"), {}) or {}).get("modes") or {}
    for row in m["rows"]:
        if row["mode"] in skip:                        # Docs/Compute/skip_modes.json: the game keeps it, Compute skips it
            continue
        card = cards.get(row["mode"]) or {}
        hulls = [row["vessel"]] if row["locked"] else list(card.get("hulls") or [row["vessel"]])
        if row["vessel"] not in hulls:
            hulls.insert(0, row["vessel"])
        pairs[row["mode"]] = {"token": row["token"], "name": card.get("name") or row["mode"], "vessels": hulls,
                              "locked": row["locked"]}
        for h in hulls:
            units.append({"id": f"{row['mode'].lower()}-{h.lower()}-i4",
                          "args": {"mode": row["token"], "vessel": h, "intensity": 4, "episodes": EPISODES}})
    out = json.loads(json.dumps(job))
    out["command"]["pairs"], out["command"]["units"] = pairs, units
    n = len(units)
    out["title"] = f"Train each vessel's AI in the real game: the Unity trainer, run headless ({n} vessel x mode pairs)"
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    with open(JOB, encoding="utf-8") as fh:
        job = json.load(fh)
    new = build(job)
    if a.check:
        same = new == job
        print(f"unity-train: {len(new['command']['units'])} units from the catalog: " + ("ok" if same else
              "DIFFERS from the job file - run python3 Tools/Compute/make_unity_job.py"))
        return 0 if same else 1
    with open(JOB, "w", encoding="utf-8") as fh:
        json.dump(new, fh, indent=1, sort_keys=True)
        fh.write("\n")
    print(f"unity-train: wrote {len(new['command']['units'])} units over {len(new['command']['pairs'])} modes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
