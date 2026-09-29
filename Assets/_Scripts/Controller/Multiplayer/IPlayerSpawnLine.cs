using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A mode's own answer to "where does everyone start?", asked by
    /// <see cref="ServerPlayerVesselInitializer"/> at the moment it needs the poses.
    ///
    /// <para><b>Why an interface and not a call from the controller.</b> A mode controller could
    /// write <c>GameDataSO.SetSpawnPoses</c> itself, and that is a race it loses about as often
    /// as it wins: vessels spawn at <c>preSpawnDelayMs</c> (200 ms) and AI at
    /// <c>OnNetworkSpawn</c> (t=0), while the order of two scene NetworkBehaviours' own
    /// <c>OnNetworkSpawn</c> calls is not defined. Asking at the point of use has no ordering to
    /// get wrong.</para>
    ///
    /// <para><b>The answer must therefore not depend on the match having started.</b> An
    /// implementation is called during the spawn chain, before the cell has latched its config
    /// and long before any course has been generated - so it has to be derivable from authored
    /// data alone. Skein's is: its start line is the collar at spine arc 0, and the spine is a
    /// pure function of the knot's radii, with no seed and no intensity in it.</para>
    ///
    /// <para>Returning false is the ordinary answer for a mode with no opinion, and the
    /// initializer falls through to the cell-relative ring (or the authored transforms).</para>
    /// </summary>
    public interface IPlayerSpawnLine
    {
        /// <summary>
        /// Poses for <paramref name="count"/> pilots, or false to let the platform decide.
        /// Slot order is the spawn slot order, so an implementation owes the same fairness the
        /// sphere formations give: every pose the same distance from whatever the mode is lining
        /// everyone up on.
        /// </summary>
        bool TryBuildSpawnPoses(int count, out Pose[] poses);
    }
}
