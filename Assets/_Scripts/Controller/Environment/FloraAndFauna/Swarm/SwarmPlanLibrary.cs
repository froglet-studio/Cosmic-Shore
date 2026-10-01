using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Loads the four baked body plans (Tools/Build/swarm_plans.py) once per TextAsset and shares
    /// them between every swarm - a plan is immutable data, so three swarms need one copy.
    /// Indexed by RESEARCH element: 0 Charge (pufferfish), 1 Mass (whale), 2 Space (jellyfish),
    /// 3 Time (dragonfly).
    /// </summary>
    public static class SwarmPlanLibrary
    {
        static readonly Dictionary<TextAsset, SwarmPlanData> s_cache = new();

        public static SwarmPlanData[] Load(SwarmFaunaConfigSO config)
        {
            var plans = new SwarmPlanData[4];
            var assets = new[] { config.ChargePlan, config.MassPlan, config.SpacePlan, config.TimePlan };
            for (int e = 0; e < 4; e++)
            {
                var asset = assets[e];
                if (!asset)
                {
                    CSDebug.LogError($"[Swarm] {config.name}: the {SwarmFaunaConfigSO.ToElement(e)} plan is not assigned - re-run Tools/Build/author_swarm_fauna.py.");
                    return null;
                }
                if (!s_cache.TryGetValue(asset, out var plan))
                {
                    plan = JsonUtility.FromJson<SwarmPlanJson>(asset.text).ToPlanData();
                    s_cache[asset] = plan;
                }
                if (plan.MajorElement != e)
                {
                    CSDebug.LogError($"[Swarm] {config.name}: {asset.name} is the {plan.Kind} plan, assigned to the {SwarmFaunaConfigSO.ToElement(e)} slot.");
                    return null;
                }
                plans[e] = plan;
            }
            return plans;
        }

        /// <summary>A typical prism (median half-extents) of each element across all four plans -
        /// the shape a member wears when its home slot belongs to another element.</summary>
        public static Vector3[] TypicalHalfExtents(SwarmPlanData[] plans)
        {
            var result = new Vector3[4];
            for (int e = 0; e < 4; e++)
            {
                var xs = new List<float>(); var ys = new List<float>(); var zs = new List<float>();
                foreach (var p in plans)
                    for (int k = 0; k < p.N; k++)
                        if (p.Elem[k] == e) { xs.Add(p.Half[k].X); ys.Add(p.Half[k].Y); zs.Add(p.Half[k].Z); }
                result[e] = xs.Count == 0 ? new Vector3(1f, 0.6f, 0.6f) : new Vector3(Median(xs), Median(ys), Median(zs));
            }
            return result;
        }

        static float Median(List<float> v) { v.Sort(); return v[v.Count / 2]; }
    }

}
