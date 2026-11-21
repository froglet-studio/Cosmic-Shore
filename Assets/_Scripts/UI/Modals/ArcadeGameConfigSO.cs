using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
            fileName = "ArcadeGameConfig",
            menuName = "ScriptableObjects/Arcade/ArcadeGameConfig")]
        public class ArcadeGameConfigSO : ScriptableObject
        {
            [Header("Runtime State")]
            public SO_ArcadeGame SelectedGame;
            public int           Intensity;
            public int           PlayerCount;
            [FormerlySerializedAs("TeamCount")]
            public int           DomainCount;
            public SO_Vessel     SelectedShip;
            public Domains       SelectedDomain;

            [Tooltip("The AIs the host PLACED, one entry per bot, in placement order - the Add AI " +
                     "button arms placement and a domain tile tap appends here. PlayerCount " +
                     "follows humans + this list; a launch below the card's minimum tops the " +
                     "difference up with domain-balanced AI, so an empty list is always legal.")]
            public List<Domains> AIDomains = new();

            [Tooltip("How well the AI flies, the host's pick on the launch panel. Offered on " +
                     "every mode (AIDifficultyRules.IsOfferedFor) and read by every AI.")]
            public AIDifficulty AIDifficulty = AIDifficultyRules.Default;

            [System.NonSerialized]
            [Tooltip("The hull a teammate picked for each placed AI, parallel to AIDomains " +
                     "(entry i is bot i). Random = no pick: the spawner draws from the card. " +
                     "Only an ALLY seat (a domain a human flies) reads it. Kept the same length " +
                     "as AIDomains by SyncAIVesselSlots; a kick removes the matching entry.")]
            public List<VesselClassType> AIVessels = new();

            [System.NonSerialized]
            [Tooltip("Each placed AI's own difficulty, parallel to AIDomains (entry i is bot i). 0 = none of " +
                     "its own: the seat flies AIDifficulty, the card's. Set from the AI row while that AI's " +
                     "pedestal is selected (Shombith, 2026-10-27: each AI slot its own difficulty); a press " +
                     "with no AI selected sets every AI and clears these.")]
            public List<AIDifficulty> AIDifficulties = new();

            /// <summary>The difficulty AI seat <paramref name="ordinal"/> flies: its own, else the card's.</summary>
            public AIDifficulty AIDifficultyAt(int ordinal) => AIDifficultyRules.ForSeat(
                ordinal >= 0 && ordinal < AIDifficulties.Count ? AIDifficulties[ordinal] : 0, AIDifficulty);

            /// <summary>Whether any AI seat has a difficulty of its own.</summary>
            public bool AnySeatDifficulty()
            {
                foreach (var d in AIDifficulties)
                    if (AIDifficultyRules.IsSet(d)) return true;
                return false;
            }

            /// <summary>The pick for AI seat <paramref name="ordinal"/>, Random when none.</summary>
            public VesselClassType AIVesselAt(int ordinal) =>
                ordinal >= 0 && ordinal < AIVessels.Count ? AIVessels[ordinal] : VesselClassType.Random;

            /// <summary>Pad (with Random / no level of its own) or truncate <see cref="AIVessels"/> and
            /// <see cref="AIDifficulties"/> to one entry per placed AI. Placements are added and dropped at the
            /// END everywhere but a kick, which removes its own entries, so this keeps pick i on bot i.</summary>
            public void SyncAIVesselSlots()
            {
                while (AIVessels.Count > AIDomains.Count) AIVessels.RemoveAt(AIVessels.Count - 1);
                while (AIVessels.Count < AIDomains.Count) AIVessels.Add(VesselClassType.Random);
                while (AIDifficulties.Count > AIDomains.Count) AIDifficulties.RemoveAt(AIDifficulties.Count - 1);
                while (AIDifficulties.Count < AIDomains.Count) AIDifficulties.Add(0);
            }

            /// <summary>Drop bot <paramref name="ordinal"/>'s seat picks (a kick): the bots after it keep theirs.</summary>
            public void RemoveAISeat(int ordinal)
            {
                SyncAIVesselSlots();
                if (ordinal < 0 || ordinal >= AIDomains.Count) return;
                AIDomains.RemoveAt(ordinal);
                AIVessels.RemoveAt(ordinal);
                AIDifficulties.RemoveAt(ordinal);
            }

            public void ResetState()
            {
                SelectedGame   = null;
                Intensity      = 0;
                PlayerCount    = 0;
                DomainCount    = 1;
                SelectedShip   = null;
                SelectedDomain = Domains.Jade;
                AIDomains.Clear();
                AIDifficulty   = AIDifficultyRules.Default;
                AIVessels.Clear();
                AIDifficulties.Clear();
            }
        }
}
