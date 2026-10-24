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

        [Header("Depth - the more seconds of silence, the deeper it goes")]
        [Tooltip("Seconds of silence left that reach FULL depth (Garrett, 2026-10-21: \"the more seconds of silence " +
                 "the deeper it goes in and bleeds back out as the seconds drain\"). With fewer seconds left the " +
                 "perimeter and the edge sit shallower, down to their thin band.")]
        [Min(0.1f)]
        [SerializeField] float depthFullSeconds = 8f;

        [Tooltip("How fast depth catches up with the seconds left, 0 to full, in seconds. Small, so a hit's " +
                 "seconds push it in at once; the drain is followed as it happens.")]
        [Min(0f)]
        [SerializeField] float depthFollowSeconds = 0.25f;

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

        [Header("Perimeter - 3D shards on the screen's edge, where a vignette would sit")]
        [Tooltip("The silence perimeter (Garrett, 2026-10-21: \"instead of a 2d effect we just have a client " +
                 "side perimeter of 3d craziness\"): faceted shards ring the silenced pilot's screen, each facet " +
                 "showing the frame behind it bent. 0 = off, 1 = full.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterAmount = 1f;

        [Tooltip("0 = crystal shards (pointed), 1 = prism slabs (broken pieces of a trail).")]
        [Range(0, 1)]
        [SerializeField] int perimeterShape = 0;

        [Tooltip("How many shards ring the screen: many, so they overlap in layers and no one of them reads as a " +
                 "crystal (Garrett, 2026-10-21: \"there should be so much overlap, and distortions\").")]
        [Range(4, 160)]
        [SerializeField] int perimeterCount = 160;

        [Tooltip("The arrangement: reshuffles where the shards sit and how each is cut.")]
        [Range(1, 99)]
        [SerializeField] int perimeterSeed = 7;

        [Tooltip("Shard length with little silence left (the thin band), as a share of the screen's height.")]
        [Range(0.02f, 0.6f)]
        [SerializeField] float perimeterSize = 0.1f;

        [Tooltip("Shard length at full depth: the more seconds of silence, the longer they grow.")]
        [Range(0.02f, 0.8f)]
        [SerializeField] float perimeterDeepSize = 0.38f;

        [Tooltip("Each shard's centre sits ON the screen's edge, half of it beyond (Garrett: \"show only half the " +
                 "faceted object that is poking into the screen\"); this is how far in a centre may sink, as a share " +
                 "of half the height. The pull's side sinks further.")]
        [Range(0f, 0.6f)]
        [SerializeField] float perimeterReach = 0.03f;

        [Tooltip("How far in a shard's centre may sink at full depth.")]
        [Range(0f, 0.6f)]
        [SerializeField] float perimeterDeepReach = 0.22f;

        [Tooltip("How fast each shard turns about its long axis, radians a second.")]
        [Range(0f, 3f)]
        [SerializeField] float perimeterSpin = 0.35f;

        [Tooltip("How far each facet bends the frame it shows, along the facet's normal.")]
        [Range(0f, 0.3f)]
        [SerializeField] float perimeterRefraction = 0.14f;

        [Tooltip("How much each shard turns and scales the reality it shows.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterTwist = 0.9f;

        [Tooltip("Red and blue pulled apart inside the facets.")]
        [Range(0f, 0.04f)]
        [SerializeField] float perimeterAberration = 0.02f;

        [Tooltip("How much the bent frame inside a facet takes the danger colour.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterTint = 0.25f;

        [Tooltip("Danger light where a facet turns away from you (fresnel), as every prism's rim does.")]
        [Range(0f, 3f)]
        [SerializeField] float perimeterRimGlow = 0.6f;

        [Tooltip("Danger lines along the shards' facet edges.")]
        [Range(0f, 3f)]
        [SerializeField] float perimeterEdgeGlow = 0.05f;

        [Tooltip("A lit danger glow inside every facet, brighter where it faces the light, so a shard reads " +
                 "as a solid volume even over black space.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterFill = 0.1f;

        [Tooltip("Turns the reality inside a facet into its negative.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterInvert = 0f;

        [Tooltip("How solid the shards are.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterOpacity = 0.95f;

        [Tooltip("The frame swirls and ripples near the edges while silenced; each hit sends a ring through it.")]
        [Range(0f, 0.15f)]
        [SerializeField] float perimeterWarp = 0.08f;

        [Tooltip("Each hit tears the facets' picture into sliding bands, decaying with the hit flare.")]
        [Range(0f, 0.2f)]
        [SerializeField] float perimeterGlitch = 0.06f;

        [Tooltip("The pull: near the edges, the frame and every facet's picture are dragged, in slow waves, toward " +
                 "a point that wanders around the screen, and the shards on that side grow longer and sink deeper - " +
                 "reality trying to pull you somewhere you don't want to be. The middle of the screen stays still.")]
        [Range(0f, 0.3f)]
        [SerializeField] float perimeterPull = 0.08f;

        [Tooltip("Each facet's picture is smeared toward the pull, so light streams into it.")]
        [Range(0f, 0.05f)]
        [SerializeField] float perimeterStreak = 0.012f;

        [Tooltip("Each shard fades toward its inward tip, so none ends in a point you could name: it dissolves " +
                 "into the distortion.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterTipFade = 0.85f;

        [Tooltip("How fast the pull wanders around the screen, radians a second.")]
        [Range(0f, 1f)]
        [SerializeField] float perimeterPullDrift = 0.12f;

        [Tooltip("CosmicShore/SilencePerimeter. Referenced here so a build includes it. Empty = no perimeter " +
                 "(a warning names this field once).")]
        [SerializeField] Shader perimeterShader;

        [Header("Edge - a danger-coloured 2D edge under the perimeter's shards")]
        [Tooltip("Peak opacity of the danger-coloured 2D edge while silenced. It is an overlay that ADDS " +
                 "the colour (URP's Vignette multiplies, so on near-black space a red one is invisible - " +
                 "the first cut was). With the perimeter on, it is drawn into the frame BEFORE the shards copy it, " +
                 "so the pull drags it and the facets refract it (Garrett liked \"the pull plus the 2d edge\"). " +
                 "0 = no edge at all, not even the hit flare.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteIntensity = 0.6f;

        [Tooltip("0 = Soft (a smooth ellipse), 1 = Faceted (a polygonal window, the prisms' language). Faceted with " +
                 "4 sides is a frame that hugs the screen's border.")]
        [Range(0, 1)]
        [SerializeField] int vignetteStyle = 1;

        [Tooltip("Sides of the Faceted window.")]
        [Range(3, 16)]
        [SerializeField] int vignetteFacets = 4;

        [Tooltip("Where the edge starts with little silence left (the thin band): 0 = the centre of the screen, " +
                 "1 = the top and bottom edges.")]
        [Range(0f, 1.5f)]
        [SerializeField] float vignetteInner = 0.93f;

        [Tooltip("Where the edge starts at full depth: it moves in with the seconds of silence and bleeds back " +
                 "out as they drain.")]
        [Range(0f, 1.5f)]
        [SerializeField] float vignetteDeepInner = 0.6f;

        [Tooltip("How far the edge takes to rise from nothing to full, in the same units.")]
        [Range(0f, 1.5f)]
        [SerializeField] float vignetteFeather = 0.07f;

        [Tooltip("0 = the edge follows the screen's own shape (an ellipse), 1 = a circle in pixels " +
                 "(reaches the top and bottom before the sides).")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteRoundness = 0f;

        [Tooltip("Opacity of the outer fill, relative to the peak. 0 leaves only the rim line.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteFill = 1f;

        [Tooltip("Opacity of a bright line traced along the edge's inner boundary. 0 = no line.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteRimStrength = 0.3f;

        [Tooltip("Half-width of that line, in the same distance units.")]
        [Range(0f, 0.3f)]
        [SerializeField] float vignetteRimWidth = 0.015f;

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

        [Tooltip("The hit flare: every hit that extends a live silence adds this, decaying. It flares the " +
                 "2D edge (when on) and jolts the perimeter's shards inward, tears their picture and sends a " +
                 "ring through the warp.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteHitPulse = 0.35f;

        [Tooltip("Seconds for a hit's flare to decay to a third.")]
        [Range(0.05f, 2f)]
        [SerializeField] float vignetteHitPulseSeconds = 0.35f;

        [Tooltip("A darkening at the screen's edge under the colour (URP Vignette, black), so the edge " +
                 "still reads over a bright scene. 0 = none.")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteDarken = 0f;

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
        public float DepthFullSeconds => depthFullSeconds;
        public float DepthFollowSeconds => depthFollowSeconds;
        public float PerimeterAmount => perimeterAmount;
        public float PerimeterRefraction => perimeterRefraction;
        public float PerimeterTwist => perimeterTwist;
        public float PerimeterAberration => perimeterAberration;
        public float PerimeterTint => perimeterTint;
        public float PerimeterRimGlow => perimeterRimGlow;
        public float PerimeterEdgeGlow => perimeterEdgeGlow;
        public float PerimeterFill => perimeterFill;
        public float PerimeterInvert => perimeterInvert;
        public float PerimeterOpacity => perimeterOpacity;
        public float PerimeterWarp => perimeterWarp;
        public float PerimeterGlitch => perimeterGlitch;
        public float PerimeterPull => perimeterPull;
        public float PerimeterStreak => perimeterStreak;
        public float PerimeterTipFade => perimeterTipFade;
        public Shader PerimeterShader => perimeterShader;
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

        /// <summary>The perimeter's layout, as <see cref="CosmicShore.Gameplay.SilencePerimeter"/> takes it.</summary>
        public CosmicShore.Gameplay.SilencePerimeter.Settings PerimeterSettings => new()
        {
            Shape = perimeterShape,
            Count = Mathf.Clamp(perimeterCount, 1, CosmicShore.Gameplay.SilencePerimeter.MaxShards),
            Seed = perimeterSeed,
            Size = perimeterSize,
            Reach = perimeterReach,
            Spin = perimeterSpin,
            PullDrift = perimeterPullDrift,
            DeepSize = perimeterDeepSize,
            DeepReach = perimeterDeepReach,
        };

        /// <summary>The mask's shape at the thin band (no depth).</summary>
        public CosmicShore.Gameplay.SilenceVignetteMask.Shape VignetteShape => VignetteShapeAt(0f);

        /// <summary>The mask's shape at a depth 0..1: the inner boundary moves from the thin band
        /// (<c>vignetteInner</c>) to <c>vignetteDeepInner</c>.</summary>
        public CosmicShore.Gameplay.SilenceVignetteMask.Shape VignetteShapeAt(float depth) => new()
        {
            Style = vignetteStyle,
            Facets = vignetteFacets,
            Inner = Mathf.Lerp(vignetteInner, vignetteDeepInner, depth),
            Feather = vignetteFeather,
            Roundness = vignetteRoundness,
            Fill = vignetteFill,
            RimStrength = vignetteRimStrength,
            RimWidth = vignetteRimWidth,
        };
    }
}
