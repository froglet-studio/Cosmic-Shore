using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's cruising trail (design: <c>R_VesselActions/SHRIKE.md</c> §3.6): the fleet's spawner laying thin long
    /// blocks, with Mass's one rule on top — "shorter trail wavelength". The spawner spaces prisms by DISTANCE (a delay
    /// of wavelength / speed), so scaling the delay by the elemental wavelength over the authored one is exactly the
    /// shorter wavelength.
    ///
    /// <para>The sling's thick trail is not laid here: <see cref="ShrikeExecutor"/> lays it by distance and holds this
    /// spawner's pen up while it slings or sits docked.</para>
    /// </summary>
    public class ShrikePrismController : VesselPrismController
    {
        /// <summary>Multiplier on the delay between trail prisms. Runtime only, written each frame by
        /// <see cref="ShrikeExecutor"/> (its single writer); 1 = the authored wavelength.</summary>
        public float SpacingMultiplier { get; set; } = 1f;

        protected override float ApplyBoostSpawnDelay(float delay) => delay * Mathf.Max(0.1f, SpacingMultiplier);
    }
}
