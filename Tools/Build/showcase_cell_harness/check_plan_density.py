#!/usr/bin/env python3
"""Gate: the swarm's plan loader upsamples every plan to the config's PlanDensity (QA-SWARM-ROUND11-9).

SwarmFauna scales the seed count (SeedMembers x Density), LayMax, the stomach and ThreatGain by
SwarmFaunaConfigSO.PlanDensity, and the Sort config ships PlanDensity 5 - but until round 11-9
SwarmPlanLibrary.Load handed the swarm the RAW baked plans, so the core's Cap (max plan N) was 192 for a whale the
docs and the tooltip call 960, and 240 seeds went into a 192-slot body. The game's loader cannot run headless
(TextAsset, JsonUtility), so this is a textual gate on the shipped file: Load must route every plan through an
upsample keyed by the config's PlanDensity. The showcase harness then runs the cores on the plans this produces.

    python3 check_plan_density.py <SwarmPlanLibrary.cs>      # exit 1 on failure
    python3 check_plan_density.py --self-test                # the pre-fix loader must FAIL (negative control)
"""
import re
import sys

PRE_FIX = '''
        public static SwarmPlanData[] Load(SwarmFaunaConfigSO config)
        {
            for (int e = 0; e < 4; e++)
            {
                if (!s_cache.TryGetValue(asset, out var plan))
                {
                    plan = JsonUtility.FromJson<SwarmPlanJson>(asset.text).ToPlanData();
                    s_cache[asset] = plan;
                }
                plans[e] = plan;
            }
            return plans;
        }
'''


def problems(text):
    out = []
    m = re.search(r"public static SwarmPlanData\[\] Load\(SwarmFaunaConfigSO config\)\s*\{(.*?)\n        \}", text, re.S)
    if not m:
        return ["SwarmPlanLibrary.Load(SwarmFaunaConfigSO) not found"]
    body = m.group(1)
    dense = re.search(r"plan\s*=\s*(\w+)\(\s*asset\s*,\s*plan\s*,\s*config\.PlanDensity\s*\)", body)
    direct = re.search(r"\.Upsample\(\s*[^)]*config\.PlanDensity", body)
    if not dense and not direct:
        out.append("Load never upsamples a plan to config.PlanDensity (the swarm's Cap would be the raw plan N)")
    if dense:
        helper = dense.group(1)
        h = re.search(r"static SwarmPlanData %s\([^)]*\)\s*\{(.*?)\n        \}" % helper, text, re.S)
        if not h or ".Upsample(" not in h.group(1):
            out.append(f"{helper}(...) does not call SwarmPlanData.Upsample")
    assign = body.find("plans[e] = plan")
    ups = dense.start() if dense else (direct.start() if direct else -1)
    if assign >= 0 and ups > assign:
        out.append("the upsample happens after the plan is stored")
    return out


def main():
    if sys.argv[1:] == ["--self-test"]:
        p = problems(PRE_FIX)
        if not p:
            print("SELF-TEST FAIL: the pre-fix loader passed the gate")
            return 1
        print(f"self-test OK: the pre-fix loader fails ({p[0]})")
        return 0
    p = problems(open(sys.argv[1], encoding="utf-8").read())
    for x in p:
        print("FAIL:", x)
    if not p:
        print("plan density OK: SwarmPlanLibrary.Load upsamples every plan to config.PlanDensity")
    return 1 if p else 0


if __name__ == "__main__":
    sys.exit(main())
