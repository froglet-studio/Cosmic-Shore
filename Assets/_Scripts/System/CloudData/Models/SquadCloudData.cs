using System;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists the player's squad (leader + two rogues) to UGS Cloud Save.
    /// Mirror of the local <c>Squad</c> struct (class + element per slot).
    /// Cloud key: "SQUAD_DATA".
    /// </summary>
    [Serializable]
    public class SquadCloudData
    {
        public VesselClassType SquadLeaderClass;
        public Element SquadLeaderElement;
        public VesselClassType RogueOneClass;
        public Element RogueOneElement;
        public VesselClassType RogueTwoClass;
        public Element RogueTwoElement;

        /// <summary>False until the player has configured a squad at least once
        /// (distinguishes a fresh cloud record from a real all-default squad).</summary>
        public bool Initialized;
    }
}
