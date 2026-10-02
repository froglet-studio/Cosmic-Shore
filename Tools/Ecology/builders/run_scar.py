"""Does a fortress learn where it is attacked? Three cuts along the SAME line, with and without scar tissue.
python Tools/Ecology/builders/run_scar.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.run_fortress import cut_run, summarise

if __name__ == "__main__":
    out = {}
    for scar in (0.0, 3.0):
        rows = []
        for sd in (7, 23, 41):
            r, _ = cut_run(sd, mend="both", same_line=True, scar=scar, tag=f"_scar{scar:g}", record=(sd == 7 and scar > 0))
            r.pop("recording", None); rows.append(r)
            print(scar, sd, r["line_mass"], [c.get("cut_sites") for c in r["cuts"]], r["built"], flush=True)
        out[f"scar{scar:g}"] = dict(summary=summarise(rows), line_mass=[r["line_mass"] for r in rows],
                                    cut_sites=[[c.get("cut_sites") for c in r["cuts"]] for r in rows])
        print(scar, out[f"scar{scar:g}"]["summary"], flush=True)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "fortress_scar.json"), "w"), indent=1)
