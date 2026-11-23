using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A race's coach for an AI Honeyguide (Slip Line, <c>Controller/Arcade/SlipLine/SLIP_LINE.md</c>): where the AI is
    /// GOING, which is what its slip guard measures a slip against. The race steers the hull itself (onto the rail it
    /// means to ride, through <c>GateRaceController.TryOverrideAim</c>); without this the guard would measure the ride
    /// against that steering point on the rail, call every ride a step backwards and turn it down. The slip, its
    /// charge and its zip are the shipped kit either way.
    /// </summary>
    public interface IHoneyguideRaceCoach
    {
        /// <summary>Where the AI Honeyguide is going (the ring after the rail it rides, or its next ring). False: no
        /// goal (the shipped rule: the pilot's own objective).</summary>
        bool TryRaceGoal(HoneyguideCrew crew, out Vector3 goal);
    }
}
