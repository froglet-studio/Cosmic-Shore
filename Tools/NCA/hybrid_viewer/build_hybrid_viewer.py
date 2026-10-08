"""Build the lab's Hybrid Creatures page: hybrid3d model.pt -> weights.json -> nca_hybrid.js (window.NcaHybrid,
the same runtime as the lab's NCA lizard) -> inlined into hybrid_src.html -> hybrid.html.

    python3 Tools/NCA/hybrid_viewer/build_hybrid_viewer.py --model /mnt/project-files/hybrid3d/h1/model.pt --note "h1 step 3000"
"""
import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
NCA = os.path.dirname(HERE)
CREATURES = os.path.join(NCA, "..", "Ecology", "flight", "creatures")

ap = argparse.ArgumentParser()
ap.add_argument("--model", required=True)
ap.add_argument("--note", default="")
a = ap.parse_args()
sys.path.insert(0, NCA)
from hybrid3d import export, GENOME  # noqa: E402

export(a.model, os.path.join(HERE, "weights.json"), note=a.note)
subprocess.run([sys.executable, os.path.join(CREATURES, "build_nca_creature.py"), "--weights", os.path.join(HERE, "weights.json"),
                "--out", os.path.join(HERE, "nca_hybrid.js"), "--global", "NcaHybrid"], check=True)
src = open(os.path.join(HERE, "hybrid_src.html"), encoding="utf-8").read()
mods = [os.path.join(HERE, "nca_hybrid.js")] + [os.path.join(CREATURES, n) for n in ("nca_creature.js", "nca_whale.js", "nca_jelly.js")]
js = "\n".join(open(m, encoding="utf-8").read() for m in mods).replace("</script", "<\\/script")
for k in ("/*__NCA__*/", "/*__GENOME__*/", "/*__NOTE__*/"):
    assert src.count(k) == 1, k
out = src.replace("/*__NCA__*/", js).replace("/*__GENOME__*/[13, 14, 15]", json.dumps(list(GENOME))).replace("/*__NOTE__*/''", json.dumps(a.note))
open(os.path.join(HERE, "hybrid.html"), "w", encoding="utf-8").write(out)
print(f"hybrid.html {len(out) // 1024} KB ({a.note})")
