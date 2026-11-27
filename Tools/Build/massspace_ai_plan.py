"""Write each hull's learned Mass-Space AI into the game (plan steps mass-arcade, space-arcade).

Source: Docs/AI_SYSTEM/massspace_ai.json - per hull the genome massspace_ai_search.py found in the hull's OWN Mass and
Space games, with the evidence (loss, confirm). Writes:
  - Assets/Resources/AIMassSpacePlan.asset (AIMassSpacePlanSO rows: ability cycle, aggressiveness and throttle scales),
    which AIPilot flies only in a Mass-Space mode (its own games and Brood Rush);
  - for the Urchin, whose rail autopilot has its own config, Resources/UrchinAutopilotConfig_<mode>.asset for each
    Mass-Space mode it flies (Hijack, BroodRush), so Skein and Regatta keep theirs.
usage: massspace_ai_plan.py [--check]
"""
import json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Compute", "evaluators"))
import massspace_ai_search as M  # noqa: E402

SPEC = os.path.join(ROOT, "Docs", "AI_SYSTEM", "massspace_ai.json")
ASSET = os.path.join(ROOT, "Assets", "Resources", "AIMassSpacePlan.asset")
SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "AI", "AIMassSpacePlanSO.cs.meta")
URCHIN_BASE = os.path.join(ROOT, M.URCHIN_CFG)
URCHIN_MODES = ["Hijack", "BroodRush"]
# A fixed guid per written asset, so a rewrite never re-links anything.
GUIDS = {"AIMassSpacePlan": "4c1f0b9e7a3d4b62a8e5f0c2d9b7a611",
         "UrchinAutopilotConfig_Hijack": "8e2d6a1f4b9c4e07b3a5d8f1c6e2a940",
         "UrchinAutopilotConfig_BroodRush": "b71c5e3a9d2f4a86c0e4f7b2a8d3c155"}
HULL_ENUM = None


def guid_of(path):
    m = re.search(r"guid: ([0-9a-f]{32})", open(os.path.join(ROOT, path) + ".meta").read())
    return m.group(1)


def hull_value(name):
    global HULL_ENUM
    if HULL_ENUM is None:
        src = open(os.path.join(ROOT, "Assets", "_Scripts", "Data", "Enums", "VesselClassType.cs")).read()
        HULL_ENUM = {k: int(v) for k, v in re.findall(r"\b(\w+)\s*=\s*(-?\d+)", src)}
    return HULL_ENUM[name]


def meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def plan_asset(spec):
    script = re.search(r"guid: ([0-9a-f]{32})", open(SCRIPT_META).read()).group(1)
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % script, "  m_Name: AIMassSpacePlan",
           "  m_EditorClassIdentifier: "]
    rows = [(h, s) for h, s in sorted(spec["hulls"].items()) if s.get("ship")]
    if not rows:
        out.append("  rows: []")
    else:
        out.append("  rows:")
        for h, s in rows:
            g = s["genome"]
            out.append("  - Hull: %d" % hull_value(h))
            ab = []
            for b, assets in M.HULLS[h][3].items():
                if g[b]["on"]:
                    for p in assets:
                        ab.append((p, round(g[b]["dur"], 3), round(g[b]["cd"], 3)))
            if not ab:
                out.append("    Abilities: []")
            else:
                out.append("    Abilities:")
                for p, d, c in ab:
                    out.append("    - Ability: {fileID: 11400000, guid: %s, type: 2}" % guid_of(p))
                    out.append("      Duration: %s" % d)
                    out.append("      Cooldown: %s" % c)
            out.append("    AggressivenessScale: %s" % round(g["aggr"], 4))
            out.append("    ThrottleScale: %s" % round(g["throttle"], 4))
    return "\n".join(out) + "\n"


def urchin_asset(genome, mode):
    base = open(URCHIN_BASE).read()
    base = re.sub(r"m_Name: .*", "m_Name: UrchinAutopilotConfig_" + mode, base)
    for k, (_, lo, hi, _) in M.URCHIN_GENES.items():
        v = round(float(genome[k]), 4)
        base, n = re.subn(r"(\n  %s: ).*" % k, r"\g<1>%s" % v, base)
        assert n == 1, k
    return base


def main():
    check = "--check" in sys.argv
    spec = json.load(open(SPEC))
    files = {ASSET: plan_asset(spec), ASSET + ".meta": meta(GUIDS["AIMassSpacePlan"])}
    u = spec["hulls"].get("Urchin")
    if u and u.get("ship"):
        for mode in URCHIN_MODES:
            p = os.path.join(ROOT, "Assets", "Resources", "UrchinAutopilotConfig_%s.asset" % mode)
            files[p] = urchin_asset(u["genome"], mode)
            files[p + ".meta"] = meta(GUIDS["UrchinAutopilotConfig_" + mode])
    bad = [p for p, t in files.items() if not os.path.exists(p) or open(p).read() != t]
    if check:
        for p in bad:
            print("drift:", os.path.relpath(p, ROOT))
        print("massspace_ai_plan: %s" % ("ok" if not bad else "%d file(s) drift; run without --check" % len(bad)))
        sys.exit(1 if bad else 0)
    for p in bad:
        open(p, "w").write(files[p])
        print("wrote", os.path.relpath(p, ROOT))
    print("massspace_ai_plan: %d file(s) written" % len(bad))


if __name__ == "__main__":
    main()
