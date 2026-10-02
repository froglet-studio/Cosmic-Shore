"""Does defence compete with repair? The same ram cut test with the colony's alarm defence ON (default) vs OFF.
python Tools/Ecology/builders/run_defend_vs_mend.py"""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.run_fortress import cut_run, summarise

if __name__ == "__main__":
    out = {}
    for name, alarm in (("defend_on", 110.0), ("defend_off", 0.0)):
        rows = []
        for sd in (7, 23, 41):
            r, _ = cut_run(sd, mend="both", alarm=alarm)
            rows.append(r); print(name, sd, [c.get("t50") for c in r["cuts"]], r["hits"], flush=True)
        out[name] = dict(summary=summarise(rows), hits=[r["hits"] for r in rows])
        print(name, out[name], flush=True)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "fortress_defend_vs_mend.json"), "w"), indent=1)
