using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's base trail (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3.3): the fleet's spawner laying a
    /// single small block from the cockpit, with Speed Mode's one rule on top — "Speed mode increases the wavelength of
    /// the Brittle Star's volume production so as not to increase production output". The spawner already spaces prisms
    /// by DISTANCE (a delay of wavelength / speed), so a faster hull lays more per second; stretching the delay by the
    /// speed the mode adds keeps the count per second where it was.
    ///
    /// <para>Creation Mode's pentagon is not laid here: <see cref="BrittleStarExecutor"/> lays it from the ring's five
    /// points and holds this spawner's pen up while it does.</para>
    /// </summary>
    public class BrittleStarPrismController : VesselPrismController
    {
        /// <summary>Multiplier on the delay between base-trail prisms. Runtime only, written each frame by
        /// <see cref="BrittleStarExecutor"/> (its single writer); 1 = the authored wavelength.</summary>
        public float SpacingMultiplier { get; set; } = 1f;

        protected override float ApplyBoostSpawnDelay(float delay) => delay * Mathf.Max(0.1f, SpacingMultiplier);
    }
}
