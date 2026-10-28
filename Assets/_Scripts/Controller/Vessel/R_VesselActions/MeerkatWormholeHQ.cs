using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The high-quality PC look of the Meerkat's wormholes (Yash, 2026-10-29: "make the shader good for a good quality PC ...
    /// a nice subtle wormhole and its networked holes"; <c>Docs/Shaders/WORMHOLE_SHADERS.md</c> §11). The SAME contour-field
    /// lens with the numbers chosen in the Tube Lens Lab (<c>MeerkatWormholeConfig.asset</c>, <c>field*</c>), drawn with the
    /// shader's <c>_MW_HQ</c> keyword: a visible outer layer of water-glass (the shell's optical depth along the bent ray),
    /// a Fresnel rim, a glint of the main light, chromatic dispersion, and for an omnicrystal network's channels an
    /// iridescent film and pulses of light running along them.
    /// <para>Two presets, chosen by the nearest tube's look (<see cref="MeerkatTubeLook.Formed"/>):
    /// <b>YASH'S LOOK</b> (<c>formedLook</c> Bridge): the layer and its rim are clearly visible, and the spindle bridge draws
    /// over it. <b>GARRETT'S LOOK</b> (ContourField): the layer is barely there and the lensing sells it.</para>
    /// Numbers live here (not on the config asset) so the Lavalamp lab can tune them live without touching the asset.
    /// </summary>
    public static class MeerkatWormholeHQ
    {
        [System.Serializable]
        public struct Look
        {
            [Tooltip("How much the outer layer glows (×1 - e^-optical depth).")] public float Outer;
            [Tooltip("The Fresnel rim where a ray grazes the layer.")] public float Rim;
            [Tooltip("The rim's power: higher keeps it to the very silhouette.")] public float RimPower;
            [Tooltip("How much the layer tints the world behind it toward Tint (the water's faint absorption).")] public float Absorb;
            [Tooltip("The water's tint (linear).")] public Color Tint;
            [Tooltip("The main light's glint off the layer.")] public float Glint;
            [Tooltip("Chromatic dispersion: red lands this share further, blue this share nearer.")] public float Dispersion;
            [Tooltip("Dispersion through a network channel.")] public float NetDispersion;
            [Tooltip("A network channel's thin-film iridescence (0 none, 1 full).")] public float NetIridescence;
            [Tooltip("The pulses of light running along a network channel.")] public float NetPulse;
            [Tooltip("Iridescent bands round a channel.")] public float NetIriScale;
            [Tooltip("Pulses along a channel's dug window.")] public float NetPulseCount;
            [Tooltip("Pulses per second.")] public float NetPulseSpeed;
            [Tooltip("A network channel's layer glow, × Outer.")] public float NetGlow;

            public void Pack(out Vector4 a, out Vector4 b, out Vector4 c, out Vector4 d)
            {
                a = new Vector4(Mathf.Max(0f, Outer), Mathf.Max(0f, Rim), Mathf.Max(0.5f, RimPower), Mathf.Clamp01(Absorb));
                b = new Vector4(Tint.r, Tint.g, Tint.b, Mathf.Max(0f, Glint));
                c = new Vector4(Mathf.Max(0f, Dispersion), Mathf.Max(0f, NetDispersion), Mathf.Clamp01(NetIridescence), Mathf.Max(0f, NetPulse));
                d = new Vector4(NetIriScale, Mathf.Max(0f, NetPulseCount), NetPulseSpeed, Mathf.Max(0f, NetGlow));
            }
        }

        /// <summary>YASH'S LOOK: the wormhole has a skin you can see - a thin, bright-edged layer of water-glass.</summary>
        public static readonly Look YashPreset = new()
        {
            Outer = 0.10f, Rim = 0.22f, RimPower = 3f, Absorb = 0.35f, Tint = new Color(0.82f, 0.94f, 1f), Glint = 0.5f,
            Dispersion = 0.035f, NetDispersion = 0.08f, NetIridescence = 0.65f, NetPulse = 0.18f,
            NetIriScale = 1.5f, NetPulseCount = 3f, NetPulseSpeed = 0.6f, NetGlow = 1.6f,
        };

        /// <summary>GARRETT'S LOOK: "lets the lensing alone sell the effect" - the layer is only a whisper.</summary>
        public static readonly Look GarrettPreset = new()
        {
            Outer = 0.02f, Rim = 0.05f, RimPower = 4f, Absorb = 0.12f, Tint = new Color(0.86f, 0.95f, 1f), Glint = 0.25f,
            Dispersion = 0.02f, NetDispersion = 0.06f, NetIridescence = 0.5f, NetPulse = 0.12f,
            NetIriScale = 1.5f, NetPulseCount = 3f, NetPulseSpeed = 0.6f, NetGlow = 2f,
        };

        /// <summary>The Lavalamp lab's live overrides: while <see cref="Active"/>, every tube draws with <see cref="Live"/>.</summary>
        public static class Tuning
        {
            public static bool Active;
            public static Look Live = YashPreset;
        }

        /// <summary>The numbers a tube of this look draws with now.</summary>
        public static Look For(ScriptableObjects.MeerkatFormedLook look) =>
            Tuning.Active ? Tuning.Live : look == ScriptableObjects.MeerkatFormedLook.Bridge ? YashPreset : GarrettPreset;

        /// <summary>Toward the main light (xyz) and its glint strength (w): the scene's sun, else a soft key from above.</summary>
        public static Vector4 LightDir()
        {
            var sun = RenderSettings.sun;
            if (sun && sun.isActiveAndEnabled)
            {
                var d = -sun.transform.forward;
                return new Vector4(d.x, d.y, d.z, Mathf.Clamp(sun.intensity, 0f, 2f));
            }
            var k = new Vector3(0.3f, 0.8f, 0.4f).normalized;
            return new Vector4(k.x, k.y, k.z, 0.6f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Tuning.Active = false;
            Tuning.Live = YashPreset;
        }
    }
}
