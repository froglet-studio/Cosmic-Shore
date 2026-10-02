"""The four elements of each threat flora (Docs/ECOSYSTEM.md §51 + the /flora skill §5, applied to threats):

    TIME    the fastest clock: growth/absorption and the threat's own tempo x1.25 (the measured RISK: a faster
            telegraph may stop being readable - telegraph_p10 is the check)
    MASS    more volume, chunkier, and heavier: prism volume x1.5, bodies x1.2, slower moves (x1/1.15)
    SPACE   reach x1.35 at the same volume (longer jaws, wider alarm, farther thorns, farther sensing)
    CHARGE  armour: a plain prism SHEDS a shield on the first ram/cut and only breaks on the second (armour=1;
            in game a Charge plant's leaves are shielded - Flora.ResolveShieldPeriod - and shielded mass is inedible)

The anchor is the searched best (results/search_<species>_best.json). Each variant is a param transform, scored on
the same scorecard: python elements.py [species ...] -> results/elements.json
"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)


def _mul(p, d, keys, k):
    for key in keys:
        p[key] = p.get(key, d[key]) * k


def variant(name, base, element, defaults):
    p = dict(base); d = dict(defaults); d.update(base)
    T, M, S = 1.25, 1.5, 1.35
    if element == "charge":
        p["armour"] = 1.0
        return p
    if name == "snaptrap":
        if element == "time": _mul(p, d, ["t_prime", "t_close", "t_reset", "absorb_every"], 1 / T); _mul(p, d, ["turn_deg"], T)
        if element == "mass": _mul(p, d, ["prism_vol", "tooth_vol"], M); _mul(p, d, ["mouth_w"], 1.2); _mul(p, d, ["t_close", "t_prime"], 1.15)
        if element == "space": _mul(p, d, ["mouth_len", "sense"], S)
    if name == "spores":
        if element == "time": _mul(p, d, ["t_swell", "absorb_every"], 1 / T); _mul(p, d, ["alarm_speed", "relax"], T)
        if element == "mass": _mul(p, d, ["charge_cap", "spore_vol", "shell_vol"], M); _mul(p, d, ["launch"], 1 / 1.15)
        if element == "space": _mul(p, d, ["alarm_r", "launch"], S); _mul(p, d, ["wind"], 1.2)
    if name == "physarum":
        if element == "time": _mul(p, d, ["wave_speed", "ss", "digest"], T); _mul(p, d, ["period"], 1 / T)
        if element == "mass": _mul(p, d, ["prism_vol"], M); p["ex_ticks"] = int(d["ex_ticks"]) + 1; _mul(p, d, ["wave_speed"], 1 / 1.15)
        if element == "space": _mul(p, d, ["so"], S); _mul(p, d, ["ss"], 1.15)
    if name == "coral":
        if element == "time": _mul(p, d, ["rate", "eat_per_s"], T)
        if element == "mass": _mul(p, d, ["c0"], M); _mul(p, d, ["Dv"], 1 / 1.15)
        if element == "space": _mul(p, d, ["Du"], S); p["reach"] = int(d["reach"]) + 1
    if name == "walker":
        if element == "time": _mul(p, d, ["speed"], T); _mul(p, d, ["t_erupt", "t_prime", "absorb_every"], 1 / T)
        if element == "mass": _mul(p, d, ["prism_vol"], M); _mul(p, d, ["r_body"], 1.2); _mul(p, d, ["speed"], 1 / 1.15)
        if element == "space": _mul(p, d, ["thorn", "sense"], S); _mul(p, d, ["strike"], 1.2)
    return p


if __name__ == "__main__":
    import importlib
    from harness import scorecard
    from search import SPEC
    names = sys.argv[1:] or list(SPEC)
    out_p = os.path.join(HERE, "results", "elements.json")
    res = json.load(open(out_p)) if os.path.exists(out_p) else {}
    for name in names:
        mod = importlib.import_module(SPEC[name].split(":")[0])
        bp = os.path.join(HERE, "results", f"search_{name}_best.json")
        base = json.load(open(bp))["params"] if os.path.exists(bp) else {}
        res[name] = {}
        for el in ("anchor", "time", "mass", "space", "charge"):
            p = base if el == "anchor" else variant(name, base, el, mod.DEFAULTS)
            c, _ = scorecard(SPEC[name], p)
            res[name][el] = dict(params=p, R=c["R"], R_hard=c["R_hard"], card=c)
            print(name, el, "R", c["R"], "Rh", c["R_hard"], "hits", c["hits_per_min_wander"], "tel_p10", c["telegraph_p10"],
                  "cp", c["counterplay"], "cpb", c["crystals_per_burn"], "prisms", c["prisms_end"], flush=True)
            json.dump(res, open(out_p, "w"), indent=1)
