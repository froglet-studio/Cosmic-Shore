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

            [System.NonSerialized]
            [Tooltip("The hull a teammate picked for each placed AI, parallel to AIDomains " +
                     "(entry i is bot i). Random = no pick: the spawner draws from the card. " +
                     "Only an ALLY seat (a domain a human flies) reads it. Kept the same length " +
                     "as AIDomains by SyncAIVesselSlots; a kick removes the matching entry.")]
            public List<VesselClassType> AIVessels = new();

            /// <summary>The pick for AI seat <paramref name="ordinal"/>, Random when none.</summary>
            public VesselClassType AIVesselAt(int ordinal) =>
                ordinal >= 0 && ordinal < AIVessels.Count ? AIVessels[ordinal] : VesselClassType.Random;

            /// <summary>Pad (with Random) or truncate <see cref="AIVessels"/> to one entry per
            /// placed AI. Placements are added and dropped at the END everywhere but a kick, which
            /// removes its own entry, so this keeps pick i on bot i.</summary>
            public void SyncAIVesselSlots()
            {
                while (AIVessels.Count > AIDomains.Count) AIVessels.RemoveAt(AIVessels.Count - 1);
                while (AIVessels.Count < AIDomains.Count) AIVessels.Add(VesselClassType.Random);
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
                AIVessels.Clear();
            }
        }
}
