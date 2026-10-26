# The 51 shipped Tandava body plans (Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_*.json), in the swarm
# config's ScriptedPlans order, cut down to what the lab draws: per-frame positions and facings (2 decimals), element,
# tier, slot, half-extents, frame order and period, the mouth (plan voxels) and the dance plans' halo.
import json, sys
SRC = sys.argv[1]
COILS = ["coil", "wrap", "eight"]
KEYS = [k for v in ("great_serpent_1", "great_serpent_2", "great_serpent_3", "many_headed_5", "many_headed_7", "many_headed_10")
        for k in [v, v + "_feed"] + [f"{v}_{c}" for c in COILS] + ([v + "_rear", v + "_strike"] if v.startswith("many") else [])]
KEYS += ["dancer_1", "dancer_2", "dancer_3", "antlion_1", "antlion_1_feed", "antlion_1_gape", "antlion_1_snap",
         "antlion_2", "antlion_2_feed", "antlion_2_gape", "antlion_2_snap", "antlion_3", "antlion_3_feed", "antlion_3_gape", "antlion_3_snap"]
out = {}
for key in KEYS:
    d = json.load(open(f"{SRC}/SwarmPlan_tandava_{key}.json"))
    n, F = d["n"], d["frames"]
    r2 = lambda v: round(v, 2)
    pos = [[r2(x) for x in d["pos"][3 * n * f: 3 * n * (f + 1)]] for f in range(F)]
    face = [[r2(x) for x in d["face"][3 * n * f: 3 * n * (f + 1)]] for f in range(F)]
    p = {"n": n, "fs": d["frameSteps"], "clip": d.get("wellClip", 1.0), "order": d["order"], "elem": d["elem"], "tier": d["tier"], "slot": d["slot"],
         "half": [r2(x) for x in d["half"]], "pos": pos, "face": face}
    if "mouth" in d: p["mouth"] = d["mouth"]
    if "ring" in d: p["ring"] = d["ring"]
    out[key] = p
json.dump(out, open(sys.argv[2], "w"), separators=(",", ":"))
print(len(out), "plans,", sum(len(json.dumps(v, separators=(',', ':'))) for v in out.values()) // 1024, "KB")
