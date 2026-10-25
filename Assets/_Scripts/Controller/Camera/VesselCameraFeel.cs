using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where gameplay tells the camera feel what just happened to a vessel (<c>Docs/CAMERA_FEEL.md</c>): a skim, a
    /// hit, a blast, a round striking it, a shot it fired, a crystal it collected. Each call reaches only the
    /// cameras that are following THAT vessel, so a call site never needs to know whether the vessel is the
    /// local pilot's, an AI's, or a remote player's: the player's camera feels the player's ship, a spectator's
    /// camera feels the ship it watches, and an event on a vessel nobody is watching costs one empty loop.
    ///
    /// <para>Every number is on <see cref="CameraFeelConfigSO"/>; every call is allocation-free.</para>
    /// </summary>
    public static class VesselCameraFeel
    {
        static readonly List<CustomCameraController> s_cameras = new(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_cameras.Clear();
            AccessibilitySettings.OnCameraShakeChanged -= OnCameraShakeChanged;
            s_accessibilityRead = false;
        }

        internal static void Register(CustomCameraController camera)
        {
            if (camera && !s_cameras.Contains(camera)) s_cameras.Add(camera);
        }

        internal static void Unregister(CustomCameraController camera) => s_cameras.Remove(camera);

        // ---------------------------------------------------------------- the player's setting

        static bool s_accessibilityRead;
        static float s_accessibilityScale = 1f;

        /// <summary>
        /// The player's camera-shake / motion setting (<see cref="AccessibilitySettings.CameraShakeIntensity"/>,
        /// 0 = off, 1 = full), read once and then kept current by its change event rather than read from
        /// PlayerPrefs every frame.
        /// </summary>
        public static float AccessibilityScale
        {
            get
            {
                if (!s_accessibilityRead)
                {
                    s_accessibilityRead = true;
                    AccessibilitySettings.OnCameraShakeChanged -= OnCameraShakeChanged;
                    AccessibilitySettings.OnCameraShakeChanged += OnCameraShakeChanged;
                    s_accessibilityScale = Mathf.Clamp01(AccessibilitySettings.CameraShakeIntensity);
                }
                return s_accessibilityScale;
            }
        }

        static void OnCameraShakeChanged(float value) => s_accessibilityScale = Mathf.Clamp01(value);

        // ---------------------------------------------------------------- events

        /// <summary>A prism entered <paramref name="vessel"/>'s skimmer (centre and radius in world units).</summary>
        public static void Skim(IVesselStatus vessel, Vector3 prismPosition, Vector3 skimmerCentre, float skimmerRadius)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (!cam.FeelsFor(vessel)) continue;
                float closeness = skimmerRadius > 1e-4f
                    ? Mathf.Clamp01(1f - Vector3.Distance(prismPosition, skimmerCentre) / skimmerRadius)
                    : 1f;
                cam.Feel.Skim(CameraFeelConfigSO.Instance, cam.ToLocal(skimmerCentre - prismPosition), closeness,
                    cam.FeelWeight);
            }
        }

        /// <summary>
        /// <paramref name="vessel"/>'s hull hit something at <paramref name="contact"/>, landing with
        /// <paramref name="weight"/> (1 = a reference prism; <see cref="PrismWeight"/> weighs a prism).
        /// </summary>
        public static void Impact(IVesselStatus vessel, Vector3 contact, float weight)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            var config = CameraFeelConfigSO.Instance;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (!cam.FeelsFor(vessel)) continue;
                cam.Feel.Impact(config, cam.ToLocal(vessel.Transform.position - contact), weight * cam.FeelWeight);
            }
        }

        /// <summary><paramref name="vessel"/>'s hull passed through another vessel's skimmer, or rammed a
        /// creature's heart, at <paramref name="contact"/>.</summary>
        public static void Bump(IVesselStatus vessel, Vector3 contact) =>
            Impact(vessel, contact, CameraFeelConfigSO.Instance.VesselBumpWeight);

        /// <summary>A blast centred at <paramref name="centre"/> caught <paramref name="vessel"/>.</summary>
        public static void Blast(IVesselStatus vessel, Vector3 centre)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            var config = CameraFeelConfigSO.Instance;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (!cam.FeelsFor(vessel)) continue;
                cam.Feel.Blast(config, cam.ToLocal(vessel.Transform.position - centre), cam.FeelWeight);
            }
        }

        /// <summary>A projectile at <paramref name="roundPosition"/> struck <paramref name="vessel"/>.</summary>
        public static void Struck(IVesselStatus vessel, Vector3 roundPosition)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            var config = CameraFeelConfigSO.Instance;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (!cam.FeelsFor(vessel)) continue;
                cam.Feel.Struck(config, cam.ToLocal(vessel.Transform.position - roundPosition), cam.FeelWeight);
            }
        }

        /// <summary><paramref name="vessel"/> fired a shot (one call per trigger, however many rounds it threw).</summary>
        public static void Shot(IVesselStatus vessel)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            var config = CameraFeelConfigSO.Instance;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (cam.FeelsFor(vessel)) cam.Feel.Shot(config, cam.FeelWeight);
            }
        }

        /// <summary><paramref name="vessel"/> collected a crystal.</summary>
        public static void Pickup(IVesselStatus vessel)
        {
            if (vessel == null || s_cameras.Count == 0) return;
            var config = CameraFeelConfigSO.Instance;
            for (int i = 0; i < s_cameras.Count; i++)
            {
                var cam = s_cameras[i];
                if (cam.FeelsFor(vessel)) cam.Feel.Pickup(config, cam.FeelWeight);
            }
        }

        /// <summary>How heavy <paramref name="prism"/> lands: its size and its shield tier
        /// (<see cref="CameraFeelConfigSO.PrismWeight"/>). 1 for a missing prism.</summary>
        public static float PrismWeight(Prism prism)
        {
            if (!prism) return 1f;
            var props = prism.prismProperties;
            return CameraFeelConfigSO.Instance.PrismWeight(prism.Volume,
                props != null && props.IsShielded, props != null && props.IsSuperShielded);
        }
    }
}
