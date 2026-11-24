using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How far each hull's racing AI will go out of its way for a crystal on the way to its next gate - one gene of
    /// that vessel's Time genome, LEARNED, never typed (Garrett, 2026-11-16: "let the genome do this and let the
    /// process find if it is good").
    ///
    /// <para><b>Why.</b> Yash's 2026-10-26 rule (a racing AI flies the waypoints, never a crystal) was a constraint to
    /// seed effective racers fast; the AI Dolphins of the time took crystals instead of gates. With seeds in hand the
    /// search widens again: taking some crystals can make a race faster (a Time crystal is speed), so the detour budget
    /// is a gene the training sets, starting at 0 (the seed's behaviour) and kept only when it wins on I1-I4. The
    /// standard element ladder lays team crystals of the game's element at I3-I4 (<see cref="ElementLadder"/>), which
    /// is where the gene starts to matter.</para>
    ///
    /// <para><b>Read by</b> <c>GateRaceController.AICrystalDetourSlackFor</c> for every gate race: a hull with no row
    /// (and a project with no asset, the state until a search writes one) gets 0, exactly the old rule. A mode whose
    /// race NEEDS crystals (<c>AIDetoursForCrystals</c>) keeps its own budget; the larger of the two applies. Written by
    /// the training's writer, never by hand (Docs/AI_SYSTEM/AI_PLAN.md).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RaceAICrystalGenes", menuName = "ScriptableObjects/AI/Race AI Crystal Genes")]
    public class RaceAICrystalGenesSO : ScriptableObject
    {
        public const string ResourcePath = "RaceAICrystalGenes";

        [Serializable]
        public struct Row
        {
            [Tooltip("The hull whose racing AI this gene belongs to.")]
            public VesselClassType Hull;

            [Tooltip("Extra distance (world units) this hull's racing AI accepts flying to take a crystal on the way " +
                     "to its next gate. 0 = never detour. Learned by the hull's Time genome search.")]
            [Min(0f)] public float DetourSlack;
        }

        [Tooltip("One row per hull. A hull with no row never detours (0).")]
        public List<Row> Rows = new();

        /// <summary>The learned detour budget for <paramref name="hull"/>, 0 when it has no row.</summary>
        public float DetourSlackFor(VesselClassType hull)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].Hull == hull) return Mathf.Max(0f, Rows[i].DetourSlack);
            return 0f;
        }

        static RaceAICrystalGenesSO _cached;
        static bool _looked;

        /// <summary>The project's genes from Resources, or null when no search has written any yet.</summary>
        public static RaceAICrystalGenesSO Load()
        {
            if (_looked) return _cached;
            _looked = true;
            _cached = Resources.Load<RaceAICrystalGenesSO>(ResourcePath);
            return _cached;
        }

        /// <summary>The learned detour budget for <paramref name="hull"/> from the project's genes (0 when none).</summary>
        public static float LearnedDetourSlack(VesselClassType hull)
        {
            var genes = Load();
            return genes ? genes.DetourSlackFor(hull) : 0f;
        }
    }
}
