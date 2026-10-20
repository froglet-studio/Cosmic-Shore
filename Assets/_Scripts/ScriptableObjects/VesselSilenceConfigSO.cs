using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for SILENCE (Docs/ElementalAbilitySystem/SILENCE.md): a pilot who has petals stripped
    /// or stolen is silenced for a time proportional to the petals they lost. While silenced, every
    /// ability they have still works and still flies, but nothing it does can harm another pilot's
    /// vessel, fauna or prisms, and it lays no new danger prisms.
    ///
    /// <para>The numbers here are STARTING values. They are meant to be set by the balancing plan
    /// in the doc (each hull's AI plateau in its Charge arcade game, then in the Time arena game),
    /// not by feel.</para>
    ///
    /// Place the asset at <c>Resources/VesselSilenceConfig</c>. With no asset the defaults below
    /// apply and a warning names the missing asset once.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselSilenceConfig",
                     menuName = "ScriptableObjects/Vessel/Vessel Silence Config")]
    public class VesselSilenceConfigSO : ScriptableObject
    {
        [Header("Duration")]
        [Tooltip("Seconds of silence per petal the victim lost to a strip or a steal. One petal is " +
                 "one element level; a hit that takes one petal on each of four elements silences " +
                 "for four times this.")]
        [Min(0f)]
        [SerializeField] float secondsPerPetal = 1f;

        [Tooltip("The most silence one pilot can carry at once. A new take EXTENDS the remaining " +
                 "silence by its own seconds, up to this ceiling. 0 disables silence entirely.")]
        [Min(0f)]
        [SerializeField] float maxSeconds = 8f;

        [Header("What silences")]
        [Tooltip("The take classes that silence their victim. Danger prisms BURN rather than strip " +
                 "or steal, so they never silence. The Butterfly's wormhole toll is a price the " +
                 "rival chose to pay by threading the mouth, so it is left out by default.")]
        [SerializeField] ElementalDebuffSources silencingSources =
            ElementalDebuffSources.Explosion | ElementalDebuffSources.VesselContact | ElementalDebuffSources.Other;

        [Header("What silence blocks")]
        [Tooltip("A silenced pilot lays no NEW danger prisms: a prism they lay or turn dangerous is " +
                 "laid plain instead. Danger prisms already standing keep their danger.")]
        [SerializeField] bool suppressDangerProduction = true;

        [Header("Feedback - the silenced pilot's own screen")]
        [Tooltip("Camera shake at the ONSET of a silence (not on every extension). Kept well under " +
                 "the fleet's hit shakes (the Serpent's shot is 0.6, the Thresher's lightest crack " +
                 "0.35) - a nudge that says something happened to you, not a blow. 0 = none.")]
        [Min(0f)]
        [SerializeField] float onsetShakeIntensity = 0.18f;

        [Tooltip("How long the onset shake decays over.")]
        [Min(0f)]
        [SerializeField] float onsetShakeSeconds = 0.3f;

        [Tooltip("Peak strength of the danger-coloured vignette while silenced (URP Vignette " +
                 "intensity). Subtle on purpose: it sits at the screen's edge for the whole " +
                 "window. 0 = no vignette.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteIntensity = 0.26f;

        [Tooltip("URP Vignette smoothness: higher spreads the edge softer and further in.")]
        [Range(0.01f, 1f)]
        [SerializeField] float vignetteSmoothness = 0.55f;

        [Tooltip("How much of the palette's danger colour the vignette carries: 1 is the full " +
                 "danger signal colour, 0 darkens to black. Below 1 reads as a deep, lit-from-" +
                 "inside red rather than a flat UI red.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteColorStrength = 0.75f;

        [Tooltip("Used only when the active palette authors no danger colour.")]
        [SerializeField] Color fallbackDangerColor = new Color(1f, 0.12f, 0.08f, 1f);

        [Tooltip("Seconds for the vignette to rise when a silence lands.")]
        [Min(0f)]
        [SerializeField] float vignetteFadeInSeconds = 0.2f;

        [Tooltip("Seconds for the vignette to drain once the silence runs out.")]
        [Min(0f)]
        [SerializeField] float vignetteFadeOutSeconds = 0.6f;

        public float SecondsPerPetal => secondsPerPetal;
        public float MaxSeconds => maxSeconds;
        public ElementalDebuffSources SilencingSources => silencingSources;
        public bool SuppressDangerProduction => suppressDangerProduction;
        public float OnsetShakeIntensity => onsetShakeIntensity;
        public float OnsetShakeSeconds => onsetShakeSeconds;
        public float VignetteIntensity => vignetteIntensity;
        public float VignetteSmoothness => vignetteSmoothness;
        public float VignetteColorStrength => vignetteColorStrength;
        public Color FallbackDangerColor => fallbackDangerColor;
        public float VignetteFadeInSeconds => vignetteFadeInSeconds;
        public float VignetteFadeOutSeconds => vignetteFadeOutSeconds;
    }
}
