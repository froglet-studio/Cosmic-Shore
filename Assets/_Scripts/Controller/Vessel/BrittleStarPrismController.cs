using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's base trail (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3.3): the fleet's spawner laying a
    /// single small block from the cockpit, with Speed Mode's two rules on top. "Speed mode increases the wavelength of
    /// the Brittle Star's volume production so as not to increase production output": the spawner spaces prisms by
    /// DISTANCE (a delay of wavelength / speed), so a faster hull lays more per second; stretching the delay by the speed
    /// the mode adds keeps the count per second where it was. And each prism is STRETCHED along the path by the same
    /// factor (Garrett, 2026-10-16: "stretch its prisms so its trail connects better"), so the wider-spaced blocks still
    /// join up.
    ///
    /// <para>Creation Mode's five corner trails are not laid here: <see cref="BrittleStarExecutor"/> lays one behind each
    /// ring corner — with this spawner's own block size — and holds this spawner's pen up while it does.</para>
    /// </summary>
    public class BrittleStarPrismController : VesselPrismController
    {
        /// <summary>Multiplier on the delay between base-trail prisms. Runtime only, written each frame by
        /// <see cref="BrittleStarExecutor"/> (its single writer); 1 = the authored wavelength.</summary>
        public float SpacingMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on each base-trail prism's length along the path. Runtime only, written each frame by
        /// <see cref="BrittleStarExecutor"/> (its single writer); 1 = the authored block.</summary>
        public float LengthMultiplier { get; set; } = 1f;

        // -= first: a re-enabled spawner must not subscribe twice. (No OnDisable here: the base has its own private one,
        // which a derived OnDisable would silently replace.)
        void OnEnable()
        {
            OnBlockSpawned -= AdmitStretched;
            OnBlockSpawned += AdmitStretched;
        }

        void OnDestroy() => OnBlockSpawned -= AdmitStretched;

        protected override float ApplyBoostSpawnDelay(float delay) => delay * Mathf.Max(0.1f, SpacingMultiplier);

        protected override Vector3 ApplyBoostScale(Vector3 scale)
        {
            float stretch = Mathf.Max(1f, LengthMultiplier);
            return stretch > 1.0001f ? new Vector3(scale.x, scale.y, scale.z * stretch) : scale;
        }

        /// <summary>A stretched prism is a stated size: admit it past the pool's scale window AFTER Initialize (whose
        /// reset re-clamps it), or a long one is trimmed with no error (the base spawner does the same for a widened lay).
        /// OnBlockSpawned is raised after Initialize.</summary>
        void AdmitStretched(Prism prism)
        {
            if (!prism || LengthMultiplier <= 1.0001f) return;
            var target = prism.TargetScale;
            prism.AdmitTargetScale(target);
            prism.TargetScale = target;
        }
    }
}
