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
        [Tooltip("Camera shake when a silence BEGINS. Between the Thresher's lightest crack (0.35) " +
                 "and the Serpent's shot (0.6): felt, not a blow. 0.18 was tried first and read as " +
                 "nothing at all in a Dog Fight playtest (Garrett, 2026-10-21). 0 = none.")]
        [Min(0f)]
        [SerializeField] float onsetShakeIntensity = 0.45f;

        [Tooltip("How long the onset shake decays over.")]
        [Min(0f)]
        [SerializeField] float onsetShakeSeconds = 0.35f;

        [Tooltip("A lighter shake on every further hit that EXTENDS a live silence, so each hit " +
                 "you take is felt, not only the first. A stronger shake already playing is kept " +
                 "(CustomCameraController.Shake). 0 = only the onset shakes.")]
        [Min(0f)]
        [SerializeField] float hitShakeIntensity = 0.2f;

        [Tooltip("How long each per-hit shake decays over.")]
        [Min(0f)]
        [SerializeField] float hitShakeSeconds = 0.15f;

        [Header("Vignette - a danger-coloured edge drawn OVER the frame")]
        [Tooltip("Peak opacity of the danger-coloured edge while silenced. It is an overlay that ADDS " +
                 "the colour (URP's Vignette multiplies, so on near-black space a red one is invisible - " +
                 "the first cut was). Overtuned on purpose until the Silence Vignette Lab picks a look. " +
                 "0 = no edge.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteIntensity = 0.75f;

        [Tooltip("0 = Soft (a smooth ellipse), 1 = Faceted (a polygonal window, the prisms' language).")]
        [Range(0, 1)]
        [SerializeField] int vignetteStyle = 0;

        [Tooltip("Sides of the Faceted window.")]
        [Range(3, 16)]
        [SerializeField] int vignetteFacets = 10;

        [Tooltip("Where the edge starts: 0 = the centre of the screen, 1 = the top and bottom edges.")]
        [Range(0f, 1.5f)]
        [SerializeField] float vignetteInner = 0.55f;

        [Tooltip("How far the edge takes to rise from nothing to full, in the same units.")]
        [Range(0f, 1.5f)]
        [SerializeField] float vignetteFeather = 0.5f;

        [Tooltip("0 = the edge follows the screen's own shape (an ellipse), 1 = a circle in pixels " +
                 "(reaches the top and bottom before the sides).")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteRoundness = 0.25f;

        [Tooltip("Opacity of the outer fill, relative to the peak. 0 leaves only the rim line.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteFill = 1f;

        [Tooltip("Opacity of a bright line traced along the edge's inner boundary. 0 = no line.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteRimStrength = 0.35f;

        [Tooltip("Half-width of that line, in the same distance units.")]
        [Range(0f, 0.3f)]
        [SerializeField] float vignetteRimWidth = 0.04f;

        [Tooltip("How much of the palette's danger colour the edge carries: 1 is the full danger " +
                 "signal colour (the rim every danger prism wears), 0 is black.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteColorStrength = 1f;

        [Tooltip("Used only when the active palette authors no danger colour.")]
        [SerializeField] Color fallbackDangerColor = new Color(1f, 0.12f, 0.08f, 1f);

        [Tooltip("How much the edge breathes while silenced: 0 = steady, 0.3 = it dips to 70% and back.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteBreatheDepth = 0.25f;

        [Tooltip("Breaths per second.")]
        [Range(0f, 4f)]
        [SerializeField] float vignetteBreatheHz = 0.8f;

        [Tooltip("Extra opacity every hit that extends a live silence adds to the edge, decaying - the " +
                 "edge flares with each hit you take.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteHitPulse = 0.35f;

        [Tooltip("Seconds for a hit's flare to decay to a third.")]
        [Range(0.05f, 2f)]
        [SerializeField] float vignetteHitPulseSeconds = 0.35f;

        [Tooltip("A darkening at the screen's edge under the colour (URP Vignette, black), so the edge " +
                 "still reads over a bright scene. 0 = none.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteDarken = 0.3f;

        [Tooltip("URP Vignette smoothness of that darkening.")]
        [Range(0.01f, 1f)]
        [SerializeField] float vignetteSmoothness = 0.55f;

        [Tooltip("Seconds for the edge to rise when a silence lands.")]
        [Min(0f)]
        [SerializeField] float vignetteFadeInSeconds = 0.15f;

        [Tooltip("Seconds for the edge to drain once the silence runs out.")]
        [Min(0f)]
        [SerializeField] float vignetteFadeOutSeconds = 0.6f;

        [Tooltip("Sorting order of the overlay canvas. Below the HUD's canvases, so the edge sits " +
                 "over the world and under the HUD.")]
        [SerializeField] int vignetteSortingOrder = -50;

        public float SecondsPerPetal => secondsPerPetal;
        public float MaxSeconds => maxSeconds;
        public ElementalDebuffSources SilencingSources => silencingSources;
        public bool SuppressDangerProduction => suppressDangerProduction;
        public float OnsetShakeIntensity => onsetShakeIntensity;
        public float OnsetShakeSeconds => onsetShakeSeconds;
        public float HitShakeIntensity => hitShakeIntensity;
        public float HitShakeSeconds => hitShakeSeconds;
        public float VignetteIntensity => vignetteIntensity;
        public float VignetteColorStrength => vignetteColorStrength;
        public Color FallbackDangerColor => fallbackDangerColor;
        public float VignetteBreatheDepth => vignetteBreatheDepth;
        public float VignetteBreatheHz => vignetteBreatheHz;
        public float VignetteHitPulse => vignetteHitPulse;
        public float VignetteHitPulseSeconds => vignetteHitPulseSeconds;
        public float VignetteDarken => vignetteDarken;
        public float VignetteSmoothness => vignetteSmoothness;
        public float VignetteFadeInSeconds => vignetteFadeInSeconds;
        public float VignetteFadeOutSeconds => vignetteFadeOutSeconds;
        public int VignetteSortingOrder => vignetteSortingOrder;

        /// <summary>The mask's shape, as <see cref="CosmicShore.Gameplay.SilenceVignetteMask"/> takes it.</summary>
        public CosmicShore.Gameplay.SilenceVignetteMask.Shape VignetteShape => new()
        {
            Style = vignetteStyle,
            Facets = vignetteFacets,
            Inner = vignetteInner,
            Feather = vignetteFeather,
            Roundness = vignetteRoundness,
            Fill = vignetteFill,
            RimStrength = vignetteRimStrength,
            RimWidth = vignetteRimWidth,
        };
    }
}
