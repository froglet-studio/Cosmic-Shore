using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the HULL LIGHT: every vessel hull lit by the world it flies in (the galactic core as the key,
    /// the sky and the galactic band as reflections, the pilot's domain as the rim). The shader is
    /// <c>VesselHullLighting.hlsl</c>, spliced into <c>VesselGraph</c>; the publisher is
    /// <c>VesselHullLighting</c>. Docs/VesselModels/COLOR.md section 2a.
    ///
    /// There are no colours here, on purpose: every colour the hull light draws is the live skybox's own (its core,
    /// its band, its cell glow) or the pilot's domain signal colour, so the light follows the worn colour set
    /// (CLAUDE.md, every colour comes from the colour set). These are only amounts and shapes.
    ///
    /// One asset for the fleet, at <c>Resources/VesselHullLightingConfig</c>. With no asset the defaults apply.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselHullLightingConfig",
                     menuName = "ScriptableObjects/Rendering/Vessel Hull Lighting Config")]
    public class VesselHullLightingConfigSO : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("0 = the old unlit hull, exactly. 1 = fully lit by the world. The A/B switch for footage and look " +
                 "calls; there is no per-vessel or per-mode switch.")]
        [Range(0f, 1f)]
        [SerializeField] float strength = 1f;

        [Header("Key (the galactic core)")]
        [Tooltip("How far the lit side wraps round the hull. 0 = a hard terminator at 90 degrees, 1 = light reaches " +
                 "all the way round. A soft wrap keeps a low-poly hull from showing every facet.")]
        [Range(0f, 1f)]
        [SerializeField] float wrap = 0.35f;

        [Tooltip("Brightness of the side facing away from the core, as a fraction of the authored colour. Never " +
                 "0: a black flank is a hole in the ship.")]
        [Range(0.1f, 1f)]
        [SerializeField] float shadeFloor = 0.55f;

        [Tooltip("How much the core's own colour adds on the lit side, on top of the authored colour.")]
        [Range(0f, 4f)]
        [SerializeField] float keyGain = 1.2f;

        [Tooltip("How much of the sky's cell glow fills the hull, strongest on the shade side.")]
        [Range(0f, 8f)]
        [SerializeField] float skyFillGain = 1.5f;

        [Header("Highlight")]
        [Tooltip("Tightness of the core's highlight. Higher = a smaller, sharper hot spot (a harder surface).")]
        [Range(4f, 256f)]
        [SerializeField] float highlightExponent = 48f;

        [Tooltip("Brightness of the core's highlight (times the slot's _HullGloss).")]
        [Range(0f, 8f)]
        [SerializeField] float highlightGain = 1.6f;

        [Header("Reflections of the sky (Fresnel-weighted, times the slot's _HullGloss)")]
        [Tooltip("Reflectance looking straight at the surface. Grazing angles always reach 1 (Fresnel).")]
        [Range(0f, 1f)]
        [SerializeField] float fresnelF0 = 0.06f;

        [Tooltip("Tightness of the core's reflection, the hot point that slides over the hull as it turns.")]
        [Range(4f, 2048f)]
        [SerializeField] float coreReflectionExponent = 400f;

        [Tooltip("Brightness of the core's reflection.")]
        [Range(0f, 8f)]
        [SerializeField] float coreReflectionGain = 1.5f;

        [Tooltip("Half-width of the galactic band's reflection, as |dot(reflection, band normal)|. The sky draws " +
                 "the band about this wide; wider reads as a broad sheen, narrower as a sharp streak.")]
        [Range(0.01f, 1f)]
        [SerializeField] float bandWidth = 0.18f;

        [Tooltip("Brightness of the galactic band's reflection.")]
        [Range(0f, 8f)]
        [SerializeField] float bandReflectionGain = 1.4f;

        [Tooltip("Brightness of the sky's overall glow in the reflection.")]
        [Range(0f, 8f)]
        [SerializeField] float skyReflectionGain = 1f;

        [Header("Rim (the pilot's domain on the silhouette)")]
        [Tooltip("Where the rim starts, in 1 - N.V (0 = facing the camera, 1 = the silhouette).")]
        [Range(0f, 1f)]
        [SerializeField] float rimInner = 0.6f;

        [Tooltip("Where the rim reaches full strength.")]
        [Range(0f, 1f)]
        [SerializeField] float rimOuter = 0.98f;

        [Tooltip("Brightness of the rim.")]
        [Range(0f, 4f)]
        [SerializeField] float rimGain = 0.6f;

        [Tooltip("0 = the rim is the same all round. 1 = it is stronger where the core is behind the ship " +
                 "(a back-light), and softer where the core is in front.")]
        [Range(0f, 1f)]
        [SerializeField] float rimKeyBias = 0.6f;

        public float Strength => strength;

        /// <summary>x wrap, y shade floor, z key gain, w sky fill gain (<c>_VesselHullShape</c>).</summary>
        public Vector4 PackShape() => new(wrap, shadeFloor, keyGain, skyFillGain);

        /// <summary>x highlight exponent, y highlight gain, z core reflection exponent, w core reflection gain.</summary>
        public Vector4 PackSpecular() =>
            new(highlightExponent, highlightGain, coreReflectionExponent, coreReflectionGain);

        /// <summary>x Fresnel F0, y band reflection gain, z sky reflection gain, w rim gain.</summary>
        public Vector4 PackReflect() => new(fresnelF0, bandReflectionGain, skyReflectionGain, rimGain);

        /// <summary>x rim inner, y rim outer (never inside the inner edge), z rim key bias.</summary>
        public Vector4 PackRim() => new(rimInner, Mathf.Max(rimOuter, rimInner + 0.01f), rimKeyBias, 0f);

        public float BandWidth => bandWidth;
    }
}
