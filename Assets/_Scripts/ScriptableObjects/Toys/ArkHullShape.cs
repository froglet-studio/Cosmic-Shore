namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Which solid an Ark model is to vessels (<see cref="CosmicShore.Gameplay.ArkHullSolid"/>): the volume a vessel is held
    /// out of and the passages it may fly. One per Ark model the voyages have drawn.
    /// </summary>
    public enum ArkHullShape
    {
        /// <summary>ArkReverse, the clam valve (ark_reverse.py): the hangar and the ring tunnel are the passage.</summary>
        Reverse = 0,
        /// <summary>The round Ark (ark_round.py): the tube, its junction hall and the three stern exits are the passage.</summary>
        Round = 1,
    }
}
