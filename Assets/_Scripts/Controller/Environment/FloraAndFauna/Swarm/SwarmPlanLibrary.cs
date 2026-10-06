using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Loads the four baked body plans (Tools/Build/swarm_plans.py) once per TextAsset and shares
    /// them between every swarm - a plan is immutable data, so three swarms need one copy. Each is
    /// UPSAMPLED to the config's <see cref="SwarmFaunaConfigSO.PlanDensity"/> (cached per density): the
    /// rest of the swarm (the seed count, LayMax, the stomach, ThreatGain) is already scaled by that
    /// density, and a raw plan would cap a density-5 whale at 192 members (QA-SWARM-ROUND11-9).
    /// Indexed by RESEARCH element: 0 Charge (pufferfish), 1 Mass (whale), 2 Space (jellyfish),
    /// 3 Time (dragonfly).
    /// </summary>
    public static class SwarmPlanLibrary
    {
        static readonly Dictionary<TextAsset, SwarmPlanData> s_cache = new();
        static readonly Dictionary<(TextAsset, int), SwarmPlanData> s_dense = new();

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
                plan = Dense(asset, plan, config.PlanDensity);
                if (plan.MajorElement != e)
                {
                    CSDebug.LogError($"[Swarm] {config.name}: {asset.name} is the {plan.Kind} plan, assigned to the {SwarmFaunaConfigSO.ToElement(e)} slot.");
                    return null;
                }
                plans[e] = plan;
            }
            return plans;
        }

        /// <summary>
        /// Tandava: the config's SCRIPTED forms in order (<see cref="SwarmFaunaConfigSO.ScriptedPlans"/>), each upsampled
        /// to the config's density like the elemental plans. Any form may be of any major element, and several forms may
        /// share one (the serpent's three sizes), so there is no slot check - only that every entry parses into a plan
        /// with a real major element. Null (logged) when an entry is missing or broken: a half-loaded form list would
        /// let the director name a form the swarm cannot grow.
        /// </summary>
        public static SwarmPlanData[] LoadScripted(SwarmFaunaConfigSO config)
        {
            var assets = config.ScriptedPlans;
            var plans = new SwarmPlanData[assets.Length];
            for (int k = 0; k < assets.Length; k++)
            {
                var asset = assets[k];
                if (!asset)
                {
                    CSDebug.LogError($"[Swarm] {config.name}: scripted form {k} is not assigned - re-run Tools/Build/author_tandava_assets.py.");
                    return null;
                }
                if (!s_cache.TryGetValue(asset, out var plan))
                {
                    plan = JsonUtility.FromJson<SwarmPlanJson>(asset.text).ToPlanData();
                    s_cache[asset] = plan;
                }
                if (plan.MajorElement < 0 || plan.MajorElement > 3 || plan.N <= 0)
                {
                    CSDebug.LogError($"[Swarm] {config.name}: scripted form {k} ({asset.name}) is not a usable plan (major {plan.MajorElement}, {plan.N} units).");
                    return null;
                }
                plans[k] = Dense(asset, plan, config.PlanDensity);
            }
            return plans;
        }

        /// <summary>The plan at <paramref name="density"/> members per plan unit (SwarmPlanData.Upsample; 1 = as baked).</summary>
        static SwarmPlanData Dense(TextAsset asset, SwarmPlanData plan, int density)
        {
            int m = Mathf.Max(1, density);
            if (m == 1) return plan;
            if (!s_dense.TryGetValue((asset, m), out var dense))
            {
                dense = plan.Upsample(m);
                s_dense[(asset, m)] = dense;
            }
            return dense;
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
