using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How far a racing AI whose ramp runs on FUEL (the Rhino ploughs it, the Swordfish slices it; Garrett, 2026-11-18,
    /// RHINO_RAMP_BOOST.md § Fuel) will go out of its way for a prism on the way to its next gate, and how low its tank
    /// must be before it does. Two genes of that hull's Time genome, LEARNED, never typed - the crystal gene's twin
    /// (<see cref="RaceAICrystalGenesSO"/>).
    ///
    /// <para><b>Read by</b> <c>GateRaceController</c> for every gate race: a hull with no row, or a 0 slack, never
    /// detours for a prism (the behaviour before the genes). Written by the hull's Time ladder search (its own Time game,
    /// never Regatta; Docs/AI_SYSTEM/AI_PLAN.md step time-arcade).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RaceAIRefuelGenes", menuName = "ScriptableObjects/AI/Race AI Refuel Genes")]
    public class RaceAIRefuelGenesSO : ScriptableObject
    {
        public const string ResourcePath = "RaceAIRefuelGenes";

        [Serializable]
        public struct Row
        {
            [Tooltip("The hull whose racing AI these genes belong to.")]
            public VesselClassType Hull;

            [Tooltip("Extra distance (world units) this hull's racing AI accepts flying to plough or slice a prism on " +
                     "the way to its next gate. 0 = never detour for a prism.")]
            [Min(0f)] public float PrismDetourSlack;

            [Tooltip("Seek prisms only while the ramp's tank is below this fraction (0..1). 1 = always when a prism is " +
                     "on the way; 0 = never.")]
            [Range(0f, 1f)] public float RefuelBelow;
        }

        [Tooltip("One row per hull. A hull with no row never detours for a prism.")]
        public List<Row> Rows = new();

        /// <summary>The row for <paramref name="hull"/>, or false when it has none (or a 0 slack).</summary>
        public bool TryGet(VesselClassType hull, out Row row)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].Hull == hull)
                {
                    row = Rows[i];
                    return row.PrismDetourSlack > 0f && row.RefuelBelow > 0f;
                }
            row = default;
            return false;
        }

        static RaceAIRefuelGenesSO _cached;
        static bool _looked;

        /// <summary>The project's genes from Resources, or null when there are none.</summary>
        public static RaceAIRefuelGenesSO Load()
        {
            if (_looked) return _cached;
            _looked = true;
            _cached = Resources.Load<RaceAIRefuelGenesSO>(ResourcePath);
            return _cached;
        }

        /// <summary>The learned refuel genes for <paramref name="hull"/>; false when it never detours for a prism.</summary>
        public static bool Learned(VesselClassType hull, out Row row)
        {
            var genes = Load();
            if (genes) return genes.TryGet(hull, out row);
            row = default;
            return false;
        }

        /// <summary>True when a racer with these genes and this tank level should look for a prism. Pure, for tests.</summary>
        public static bool WantsFuel(Row row, float fuel01) =>
            row.PrismDetourSlack > 0f && fuel01 < row.RefuelBelow;
    }
}
