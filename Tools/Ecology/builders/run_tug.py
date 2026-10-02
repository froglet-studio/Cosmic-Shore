"""Raid by RAMMING (the wall is destroyed) vs raid by STEALING (the wall changes hands back to the pilot).
python Tools/Ecology/builders/run_tug.py"""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.run_fortress import cut_run, summarise

if __name__ == "__main__":
    out = {}
    for raid in ("ram", "steal"):
        rows = []
        for sd in (7, 23, 41):
            r, _ = cut_run(sd, mend="both", raid=raid)
            rows.append(r)
            print(raid, sd, {k: r[k] for k in ("built", "audit", "pilot_stole", "destroyed", "pilot_dom_vol")},
                  [c.get("cut_sites") for c in r["cuts"]], flush=True)
        out[raid] = dict(summary=summarise(rows), runs=[{k: r[k] for k in ("built", "audit", "pilot_stole", "destroyed",
                                                                           "pilot_dom_vol", "cuts")} for r in rows])
        print(raid, out[raid]["summary"], flush=True)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "fortress_tug.json"), "w"), indent=1)
