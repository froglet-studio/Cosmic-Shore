"""Build out/sandbox.html - the flyable threat-flora sandbox - with each species' DEFAULTS overlaid by its searched
best parameters (results/search_<species>_best.json) so the sandbox flies the tuned plants.

    python sandbox.py
"""
import importlib, json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
MODS = dict(snaptrap="snaptrap", spores="spores", physarum="physarum", coral="coral", walker="walker")

if __name__ == "__main__":
    params = {}
    for name, mod in MODS.items():
        p = dict(importlib.import_module(mod).DEFAULTS)
        bp = os.path.join(HERE, "results", f"search_{name}_best.json")
        if os.path.exists(bp): p.update(json.load(open(bp))["params"])
        params[name] = p
    src = open(os.path.join(HERE, "sandbox_src.html")).read().replace("__PARAMS__", json.dumps(params))
    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    out = os.path.join(HERE, "out", "sandbox.html"); open(out, "w").write(src)
    print(out, len(src), "bytes")
