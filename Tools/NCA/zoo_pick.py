"""Pick the zoo's named elites from the map by ARCHETYPE (one measurable criterion each, distinct cells),
writing results/zoo/elites.json for zoo_publish.py finalize and zoo_showcase.py. Also writes map.json / map.svg.
Farthest-point selection (zoo_publish select) kept returning near-duplicates of the 'hunt' creatures, so the
seven are chosen by what a player would call different."""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import zoo_search as zs, zoo_publish as zp

d = lambda r, k: r["desc"][k]
ARCH = [  # slug, name, wander, score
    ("glass_minnows", "Glass Minnows", 0.30, lambda r: -d(r, "stance") - 5 * d(r, "touched") - 0.5 * d(r, "loose")),
    ("phoenix", "Phoenix", 0.20, lambda r: 2 * d(r, "drama") - 0.3 * d(r, "loose")),
    ("hornets", "Hornet Cloud", 0.35, lambda r: d(r, "stance") / 3 + 2 * d(r, "live")),
    ("drifter", "Old Drifter", 0.10, lambda r: -d(r, "live") * 4 + d(r, "latency") / 40),
    ("hydra", "Hydra", 0.25, lambda r: -d(r, "heal") / 10 - r["div"] / 5),
    ("murmur", "Murmur", 0.20, lambda r: d(r, "loose")),
    ("sparkler", "Sparkler", 0.25, lambda r: -r["div"]),
]


def main():
    zp.select(1)            # map.json + map.svg (its own elites.json is overwritten below)
    el = zs.build_map(zs.load_log())
    names = json.load(open(os.path.join(zp.OUT, "names.json"))) if os.path.exists(os.path.join(zp.OUT, "names.json")) else {}
    used, out = set(), []
    for slug, name, wander, f in ARCH:
        r = next(r for r in sorted(el.values(), key=lambda r: -f(r)) if zs.cell(r["desc"]) not in used)
        c = zs.cell(r["desc"]); used.add(c)
        e = dict(slug=slug, name=name, blurb="", cell=list(c), div=r["div"], desc=r["desc"], g=r["g"], per=r["per"], wander=wander)
        e.update(names.get(slug, {}))
        out.append(e)
        print(slug, c, r["div"], r["desc"])
    json.dump(out, open(os.path.join(zp.OUT, "elites.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
