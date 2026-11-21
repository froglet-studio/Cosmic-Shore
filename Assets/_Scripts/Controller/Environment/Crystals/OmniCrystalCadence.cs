using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni crystal's clock for its Space and Time beats (<see cref="OmniCrystalCadenceSO"/>): the Space squares
    /// (<see cref="CrystalSpaceSquares"/>, on the body) and the Time rhombi (<see cref="CrystalFlipWave"/>, on this root)
    /// both ask it what to play, so they take turns and never move at once. Without one, each plays on its own clock
    /// (the Time crystal's flip wave, and the art file's squares loop).
    ///
    /// Each crystal starts at its own phase, so a field of omnis does not beat in lockstep. A pure function of
    /// <see cref="Time.time"/>: nothing is stepped, and a crystal out of view is right again the frame it comes back.
    /// </summary>
    public class OmniCrystalCadence : MonoBehaviour
    {
        [Tooltip("The beats, the square turns and the plate thickness. OmniCrystalCadence.asset is generated from the Omni Motion Lab's round-5 pick (Tools/Build/author_omni_crystal_triangles.py).")]
        [SerializeField] OmniCrystalCadenceSO settings;

        OmniCadence cadence;
        float phase;

        public OmniCrystalCadenceSO Settings => settings;

        void Awake()
        {
            if (!settings)
            {
                CSDebug.LogError($"OmniCrystalCadence on '{name}': no settings assigned. The squares and the rhombi fall back to their own clocks.", this);
                enabled = false;
                return;
            }
            cadence = settings.Cadence;
            // Its own stream: the global UnityEngine.Random is seeded for deterministic content.
            phase = (float)new System.Random(GetInstanceID()).NextDouble() * cadence.CycleSeconds;
        }

        float Now => Time.time + phase;

        /// <summary>The square turn under way now, and how far through it (0..1, not eased).</summary>
        public SquareTurn SquaresNow(out float progress)
        {
            if (!enabled) { progress = 0f; return SquareTurn.None; }
            return cadence.SquaresAt(Now, out progress);
        }

        /// <summary>The Time wave under way now: seconds into it AT THE PROFILE'S SPEED, and its index.</summary>
        public bool WaveNow(out float profileSeconds, out long index)
        {
            profileSeconds = 0f;
            index = -1;
            if (!enabled || !cadence.WaveAt(Now, out float secondsIn, out index)) return false;
            profileSeconds = secondsIn * settings.WaveSpeed;
            return true;
        }
    }
}
