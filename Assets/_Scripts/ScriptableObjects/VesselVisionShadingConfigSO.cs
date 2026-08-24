using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the VESSEL VISION BAND — the fleet-wide aid that re-shades every vessel into a
    /// flat, cel-banded silhouette in its own domain colour as a function of its distance from
    /// the camera drawing it (<c>VesselVisionShading</c>, Docs/VESSEL_VISION.md).
    ///
    /// The law is ABSOLUTE, exactly as the speed tunnel's is: the mark is one global function of
    /// distance, so THE SAME DISTANCE ON ANY VESSEL LOOKS THE SAME. A big hull is marked at the
    /// same range as a small one because the question the aid answers — "is there a pilot over
    /// there, and whose?" — does not depend on how big the pilot's ship is. Do NOT add per-vessel
    /// windows, per-vessel scalars, or a normalization by apparent size: that is a different
    /// design, and it would destroy the one property the law exists to guarantee, which is that a
    /// player learns the distance cue ONCE and it is then true of every ship in the game.
    ///
    /// There is consequently nothing to author per vessel and nothing to author per mode: this
    /// asset is the ONLY tuning surface for the entire fleet. Place it at
    /// <c>Resources/VesselVisionShadingConfig</c>; with no asset the defaults below apply, so the
    /// law holds with zero authoring (the <c>PrismOcclusionConfigSO</c> / <c>SpeedTunnelConfigSO</c>
    /// precedent).
    /// </summary>
    [CreateAssetMenu(fileName = "VesselVisionShadingConfig",
                     menuName = "ScriptableObjects/Rendering/Vessel Vision Shading Config")]
    public class VesselVisionShadingConfigSO : ScriptableObject
    {
        [Header("Law")]
        [Tooltip("Master switch, for A/B-ing the whole law in one place. This is a global debug " +
                 "switch, NOT an authoring surface — there is deliberately no way to turn the " +
                 "vision band off for one vessel, one scene, or one game mode.")]
        [SerializeField] bool enabled = true;

        [Header("Distance Band (absolute world units — fleet-wide)")]
        [Tooltip("Below this the mark is exactly zero and costs nothing. Close up the hull's own " +
                 "art is the better read. This floor is also what excludes the pilot's OWN vessel " +
                 "— it rides 10-40 units from its camera — so the law needs no 'is this me' test.")]
        [Min(0f)]
        [SerializeField] float nearFadeStart = 150f;

        [Tooltip("Distance at which the mark reaches full strength on the way OUT. The span from " +
                 "nearFadeStart to here is the rising grade — long enough that the mark arrives " +
                 "rather than popping on.")]
        [Min(0f)]
        [SerializeField] float nearFullStart = 350f;

        [Tooltip("Last distance at which the mark is still at full strength. Keep this beyond a " +
                 "full arena crossing (the cell membrane is 1200 units of radius) so a pilot on " +
                 "the far side of the world is still marked at full strength.")]
        [Min(0f)]
        [SerializeField] float farFullEnd = 2000f;

        [Tooltip("Distance at which the mark has faded back to nothing. Past here a ship subtends " +
                 "a few pixels and a saturated dot reads as one more crystal rather than as a " +
                 "pilot, so the aid stops rather than lying.")]
        [Min(0f)]
        [SerializeField] float farFadeEnd = 3500f;

        [Header("Cel Shading")]
        [Tooltip("How far a fully-marked hull is driven to its flat domain silhouette (0 = the " +
                 "hull's own art, 1 = nothing but the mark). Below 1 the ship keeps a trace of " +
                 "its own shading, so a marked vessel still reads as that vessel.")]
        [Range(0f, 1f)]
        [SerializeField] float strength = 0.85f;

        [Tooltip("Number of flat tones the facing term is quantized into. This is what makes it " +
                 "read as CEL shading rather than as a tint: 2-4 flat tones with hard borders is " +
                 "a SHAPE, and a shape survives being thirty pixels tall.")]
        [Range(2, 6)]
        [SerializeField] int celSteps = 3;

        [Tooltip("Brightness of the darkest cel tone, as a fraction of the domain colour. Never " +
                 "0: a black band on a marked ship punches a hole in the silhouette the aid " +
                 "exists to draw.")]
        [Range(0.05f, 1f)]
        [SerializeField] float shadeFloor = 0.35f;

        [Tooltip("Overall brightness multiplier on the mark. Gameplay bloom is clamped at 0.5 " +
                 "(Docs/PALETTE.md §3), so the domain signal colour already blooms at 1.0 and " +
                 "this buys presence rather than glow.")]
        [Min(0f)]
        [SerializeField] float gain = 1.15f;

        [Header("Silhouette Rim")]
        [Tooltip("Where the rim begins, measured on 1 - dot(normal, view): 0 is head-on, 1 is the " +
                 "silhouette. Raise it for a thinner rim.")]
        [Range(0f, 1f)]
        [SerializeField] float rimInner = 0.55f;

        [Tooltip("Where the rim reaches full brightness. Keep it close to rimInner for a hard " +
                 "outline and far from it for a glow.")]
        [Range(0f, 1f)]
        [SerializeField] float rimOuter = 0.95f;

        [Tooltip("How much the rim ADDS on top of the cel tone. The rim is the part that survives " +
                 "at extreme range — by then the whole ship is rim — and it is what separates a " +
                 "marked hull from lit mass tangled up with it.")]
        [Min(0f)]
        [SerializeField] float rimGain = 1.1f;

        public bool Enabled => enabled;

        public float NearFadeStart => Mathf.Max(0f, nearFadeStart);
        /// <summary>Held above the floor so a mis-authored asset can never invert the rising edge.</summary>
        public float NearFullStart => Mathf.Max(nearFullStart, NearFadeStart + MinEdgeWidth);
        /// <summary>Held above the rising edge so the plateau can never be negative.</summary>
        public float FarFullEnd => Mathf.Max(farFullEnd, NearFullStart);
        /// <summary>Held above the plateau so <see cref="Effect01"/> can never divide by zero.</summary>
        public float FarFadeEnd => Mathf.Max(farFadeEnd, FarFullEnd + MinEdgeWidth);

        public float Strength => Mathf.Clamp01(strength);
        public int CelSteps => Mathf.Clamp(celSteps, 2, 6);
        public float ShadeFloor => Mathf.Clamp(shadeFloor, 0.05f, 1f);
        public float Gain => Mathf.Max(0f, gain);

        public float RimInner => Mathf.Clamp01(rimInner);
        /// <summary>Held above the inner edge so the rim smoothstep can never invert.</summary>
        public float RimOuter => Mathf.Max(rimOuter, RimInner + MinEdgeWidth);
        public float RimGain => Mathf.Max(0f, rimGain);

        /// <summary>Narrowest legal grade, so no edge in the law is ever a step function.</summary>
        public const float MinEdgeWidth = 0.01f;

        /// <summary>
        /// The law itself: distance from the drawing camera → mark strength in [0, 1].
        ///
        /// This is the C# transcription of <c>VesselVisionBand01</c> in VesselVisionShading.hlsl,
        /// and it is written the same way ON PURPOSE — the same Hermite smoothstep, the same
        /// min() of a rising and a falling edge — because three gates ask about the band (the
        /// shader, the edit-mode test, the FrogletTools validator) and a paraphrase is how they
        /// drift apart. <c>VesselVisionLawTests</c> proves the shape; the validator proves the
        /// HLSL still says what this says.
        /// </summary>
        public float Effect01(float distanceToCamera)
        {
            if (!enabled) return 0f;
            float rise = Smooth(NearFadeStart, NearFullStart, distanceToCamera);
            float fall = 1f - Smooth(FarFullEnd, FarFadeEnd, distanceToCamera);
            return Mathf.Clamp01(Mathf.Min(rise, fall));
        }

        /// <summary>Hermite smoothstep, written out to match the shader instruction for instruction.</summary>
        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
            return t * t * (3f - 2f * t);
        }

        /// <summary>(nearStart, nearFull, farFull, farEnd). w &lt;= 0 is the shader's OFF sentinel.</summary>
        public Vector4 PackBand() =>
            enabled ? new Vector4(NearFadeStart, NearFullStart, FarFullEnd, FarFadeEnd)
                    : Vector4.zero;

        /// <summary>(strength, celSteps, shadeFloor, gain).</summary>
        public Vector4 PackShape() => new(Strength, CelSteps, ShadeFloor, Gain);

        /// <summary>(rimInner, rimOuter, rimGain, unused).</summary>
        public Vector4 PackRim() => new(RimInner, RimOuter, RimGain, 0f);

        /// <summary>
        /// One rule, three gates: the runtime, the FrogletTools validator and the edit-mode test
        /// all ask THIS method whether an authored asset is usable, so an asset that passes the
        /// audit cannot fail at runtime (the <c>SpeedTunnelConfigSO.IsSane</c> pattern).
        /// </summary>
        public bool IsSane(out string reason)
        {
            if (nearFullStart <= nearFadeStart)
            {
                reason = $"nearFullStart ({nearFullStart}) must exceed nearFadeStart ({nearFadeStart}) — " +
                         "an inverted rising edge makes the mark pop on instead of arriving.";
                return false;
            }

            if (farFullEnd < nearFullStart)
            {
                reason = $"farFullEnd ({farFullEnd}) is below nearFullStart ({nearFullStart}) — " +
                         "the band has no plateau, so the mark never reaches full strength.";
                return false;
            }

            if (farFadeEnd <= farFullEnd)
            {
                reason = $"farFadeEnd ({farFadeEnd}) must exceed farFullEnd ({farFullEnd}) — " +
                         "an inverted falling edge makes the mark pop off at range.";
                return false;
            }

            // The cell membrane is 1200 units of radius, so a full arena crossing is ~2400. A band
            // that dies inside that is a band that cannot answer the question it exists for.
            if (farFadeEnd < MinUsefulReach)
            {
                reason = $"farFadeEnd ({farFadeEnd}) is inside a single arena crossing " +
                         $"({MinUsefulReach} units) — pilots on opposite sides of a cell would be " +
                         "unmarked, which is the case the aid exists for.";
                return false;
            }

            // The gameplay camera rides 10-40 units behind its own ship (CameraSettingsSO). A floor
            // at or below that marks the local pilot's own hull, which is the one ship nobody has
            // ever had trouble finding.
            if (nearFadeStart < MinLocalHullClearance)
            {
                reason = $"nearFadeStart ({nearFadeStart}) is inside the gameplay camera's own " +
                         $"follow distance (up to {MinLocalHullClearance} units) — the local " +
                         "pilot's own vessel would be marked.";
                return false;
            }

            if (strength <= 0f)
            {
                reason = "strength is zero — the law is authored to do nothing.";
                return false;
            }

            if (rimOuter <= rimInner)
            {
                reason = $"rimOuter ({rimOuter}) must exceed rimInner ({rimInner}) — an inverted " +
                         "rim window collapses the silhouette outline to a hard step.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>A full crossing of a standard cell (membrane radius 1200), doubled for the diameter.</summary>
        public const float MinUsefulReach = 2400f;

        /// <summary>
        /// <c>CameraSettingsSO.dynamicMaxDistance</c> across the shipped fleet, with headroom.
        /// The near floor must clear it or the law marks the ship the player is flying.
        /// </summary>
        public const float MinLocalHullClearance = 60f;
    }
}
