using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Everything else is suppressed" (Docs/Pilots/CALLSIGNS.md §3): a global post-processing
    /// volume of its own, desaturating and dimming the frame, whose WEIGHT is the LB fade. Its own
    /// volume and its own profile, so it never touches the gameplay profile that
    /// <c>PostProcessingManager</c> drives (the speed tunnel's Panini, silence's vignette): URP blends
    /// the two by priority and weight, and at weight 0 this one contributes nothing.
    ///
    /// Local, like the sight: it exists only on the machine of the pilot who is looking.
    /// </summary>
    public static class PilotSightSuppression
    {
        static Volume s_volume;
        static VolumeProfile s_profile;
        static ColorAdjustments s_adjust;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_volume = null;
            s_profile = null;
            s_adjust = null;
        }

        /// <summary>Weight the suppression by <paramref name="strength01"/> (the LB fade).</summary>
        public static void Apply(float strength01, CallsignSetSO set)
        {
            if (!Application.isPlaying) return;
            if (strength01 <= 0f)
            {
                if (s_volume) s_volume.weight = 0f;
                return;
            }
            if (!s_volume) Build(set);
            if (!s_volume) return;

            s_adjust.saturation.Override(set ? set.SuppressSaturation : -70f);
            s_adjust.postExposure.Override(set ? set.SuppressExposure : -1f);
            s_volume.priority = set ? set.VolumePriority : 50f;
            s_volume.weight = Mathf.Clamp01(strength01);
        }

        static void Build(CallsignSetSO set)
        {
            var go = new GameObject("PilotSightSuppression");
            Object.DontDestroyOnLoad(go);
            s_volume = go.AddComponent<Volume>();
            s_volume.isGlobal = true;
            s_volume.weight = 0f;
            s_profile = ScriptableObject.CreateInstance<VolumeProfile>();
            s_profile.name = "PilotSightSuppression (runtime)";
            s_adjust = s_profile.Add<ColorAdjustments>(true);
            s_volume.sharedProfile = s_profile;
        }
    }
}
