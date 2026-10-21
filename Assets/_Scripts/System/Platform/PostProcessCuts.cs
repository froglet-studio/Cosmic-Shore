using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Core
{
    /// <summary>
    /// The quality preset's post-processing cuts (<see cref="PresetCuts.CutsBloom"/>), as ONE global
    /// volume above every scene's own. It overrides only the parameters the preset cuts (bloom's
    /// downscale and pyramid depth), so each scene's threshold, intensity and tint still come from
    /// its own profile, and a scene whose bloom is off stays off. The shared profile assets are never
    /// written, so nothing leaks into the Editor's assets after Play.
    /// </summary>
    public static class PostProcessCuts
    {
        /// <summary>Above any scene volume's priority, so the cut parameters always win the blend.</summary>
        const float Priority = 1000f;

        static Volume s_volume;
        static Bloom s_bloom;

        /// <summary>Enter Play Mode runs without a domain reload; the volume object did not survive.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_volume = null;
            s_bloom = null;
        }

        /// <summary>Put <paramref name="cuts"/>' post-processing cuts in force, or lift them (null or a
        /// row that cuts nothing).</summary>
        public static void Apply(PresetCuts cuts)
        {
            if (!Application.isPlaying) return;

            if (cuts == null || !cuts.CutsBloom)
            {
                if (s_volume) s_volume.enabled = false;
                return;
            }

            if (!s_volume) Create();

            s_bloom.downscale.overrideState = cuts.BloomAtQuarterResolution;
            s_bloom.downscale.value = BloomDownscaleMode.Quarter;
            s_bloom.maxIterations.overrideState = cuts.BloomMaxIterations > 0;
            if (cuts.BloomMaxIterations > 0) s_bloom.maxIterations.value = cuts.BloomMaxIterations;
            s_volume.enabled = true;
        }

        static void Create()
        {
            var go = new GameObject("[PostProcessCuts]");
            Object.DontDestroyOnLoad(go);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PostProcessCuts (runtime)";
            s_bloom = profile.Add<Bloom>(false);

            s_volume = go.AddComponent<Volume>();
            s_volume.isGlobal = true;
            s_volume.priority = Priority;
            s_volume.profile = profile;
        }
    }
}
