# The shipped Tandava serpent plans Shiva's swarms use (Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_*.json),
# cut down to what the lab moves and draws: per-frame positions (1 decimal), tier, element, frame steps, mouth.
import json, sys
SRC, OUT = sys.argv[1], sys.argv[2]
KEYS = [f"many_headed_{h}{pose}" for h in (5, 7, 10) for pose in ("", "_rear", "_strike")]
out = {}
for key in KEYS:
    d = json.load(open(f"{SRC}/SwarmPlan_tandava_{key}.json"))
    n, F = d["n"], d["frames"]
    pos = [[round(x, 1) for x in d["pos"][3 * n * f: 3 * n * (f + 1)]] for f in range(F)]
    p = {"n": n, "frames": F, "fs": d["frameSteps"], "tier": d["tier"], "elem": d["elem"], "pos": pos, "clip": d.get("wellClip", 1.0)}
    if "mouth" in d: p["mouth"] = d["mouth"]
    out[key] = p
json.dump(out, open(OUT, "w"), separators=(",", ":"))
print(len(out), "plans,", len(json.dumps(out, separators=(",", ":"))) // 1024, "KB")
