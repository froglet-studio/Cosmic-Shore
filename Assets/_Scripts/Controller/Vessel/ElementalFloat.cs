using CosmicShore.Data;
using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE element scaling channel: one authored parameter that interpolates on one element's
    /// live level between its value AT REST (<see cref="Min"/>) and its value at integer level 10
    /// (<see cref="Max"/>), extrapolated across the element system's full [-5, 15] band and
    /// optionally floored.
    ///
    /// <para><b>It is PARAMETER-addressed, and that is the whole point.</b> An ElementalFloat is
    /// serialized on the asset or component that owns the number it scales, so it can only ever
    /// reach that number. The retired alternative — <c>ElementalAbilityMapSO</c>'s
    /// <c>MultiplierAtFullLevel</c>, read through a generic <c>handler.Multiplier(element)</c> —
    /// addressed only an ELEMENT, so it had no way to name which parameter it scaled and every
    /// reader of that element got it. Measured before removal: of eight vessels, two used the
    /// fleet-wide boost-speed read legitimately, FOUR pinned their map entry to 1.0 purely to
    /// defend against it, and TWO (Rhino ramp ceiling, Serpent boost speed) were silently
    /// double-applying one element to one ability. See ELEMENT_SCALING_UNIFICATION.md.</para>
    ///
    /// <para><b>A multiplier is just an ElementalFloat whose Min is 1.</b> There is deliberately no
    /// Absolute/Multiplier mode: the arithmetic is identical and whether the result is a value or a
    /// factor is the consumer's business, not the data's. <see cref="Multiplier"/> is a factory for
    /// readability, not a second code path.</para>
    ///
    /// <para><b>The FLOOR is load-bearing, not defensive polish.</b> In the deficit band
    /// (<c>t &lt; 0</c>) an unfloored multiplier with a large <see cref="Max"/> goes negative — a
    /// debuffed Sparrow's gun-range factor reaches <c>1 + (9-1)(-0.5) = -3</c> — which inverts the
    /// parameter instead of weakening it. Every migrated multiplier authors one.</para>
    /// </summary>
    [Serializable]
    public class ElementalFloat
    {
        [Tooltip("Off = this parameter does not scale with any element; Value is used verbatim.")]
        [SerializeField] public bool Enabled;

        [Tooltip("The authored base. Returned verbatim when disabled or off-vessel, and kept in " +
                 "step with the live level by the legacy bound path.")]
        [SerializeField] public float Value;

        [Tooltip("Value at the RESTING element level (normalized 0). For a multiplier this is " +
                 "almost always 1 — an element that only ever adds to the vessel's baseline.")]
        [SerializeField] float Min;

        [Tooltip("Value at integer element level 10 (normalized 1). Extrapolated, not clamped, " +
                 "across the deficit and overcharge bands.")]
        [SerializeField] float Max;

        [SerializeField] Element element;

        [Tooltip("Clamp the result from below. Required on any multiplier whose Max is far from " +
                 "1: the deficit band extrapolates below Min and can cross zero, which inverts " +
                 "the parameter rather than weakening it.")]
        [SerializeField] bool UseFloor;

        [Tooltip("The lower clamp applied when UseFloor is set.")]
        [SerializeField] float Floor;

        IVessel vessel;
        string name;

        public ElementalFloat(float value)
        {
            Value = value;
        }

        /// <summary>
        /// A floored multiplier around an element's level: <paramref name="atRest"/> at the
        /// resting level, <paramref name="atFull"/> at integer level 10.
        ///
        /// <para>Used as a FIELD INITIALIZER at every migrated call site, and that is deliberate
        /// rather than belt-and-braces: Unity applies only the keys an asset's YAML actually
        /// carries, so a field added to the C# after an asset was last written is absent from that
        /// asset and <b>the initializer is the shipped value</b> (the vessel contract's rule 4-i).
        /// The asset YAML is authored to match, so the number is visible in the inspector and
        /// becomes authoritative the moment anyone re-saves.</para>
        /// </summary>
        public static ElementalFloat Multiplier(float atRest, float atFull, Element element,
            float floor) => new ElementalFloat(atRest)
            {
                Enabled = true,
                Min = atRest,
                Max = atFull,
                element = element,
                UseFloor = true,
                Floor = floor,
            };

        /// <summary>
        /// Live evaluation against the vessel's CURRENT element level. Returns the authored
        /// <see cref="Value"/> when disabled or when there is no ResourceSystem to read, so an
        /// off-vessel or pre-initialization call falls back to the authored base rather than to 0.
        /// </summary>
        public float EvaluateLive(IVesselStatus status)
        {
            if (!Enabled) return Value;
            var resources = status?.ResourceSystem;
            if (!resources) return Value;
            return Evaluate(resources);
        }

        /// <summary>
        /// The ONE formula. Both read paths route through it so they cannot disagree — they did
        /// before this existed, and only outside exact tenths, which is the hardest kind of
        /// disagreement to notice.
        ///
        /// <para><b>t is the CONTINUOUS normalized level</b>
        /// (<see cref="ResourceSystem.GetNormalizedLevel"/>), matching
        /// <see cref="ElementalScaling"/>. Both read paths previously used
        /// <c>GetLevel(element) / 10f</c>, which is <c>FloorToInt(normalized * 10) / 10</c> and so
        /// quantized to tenths. Crystal progression moves the level in exact tenths
        /// (<c>AdjustLevel(±0.1)</c>) and the two agree there; they diverge only while a temporary
        /// effect, fauna buff or comeback bonus is decaying — all continuous — where the quantized
        /// form steps and this one glides. No authored endpoint changes: both forms return
        /// <see cref="Min"/> at rest and <see cref="Max"/> at level 10.</para>
        ///
        /// <para>LerpUnclamped so the deficit (−5) and overcharge (+15) bands extrapolate beyond
        /// the authored endpoints instead of flattening against them.</para>
        /// </summary>
        float Evaluate(ResourceSystem resources)
            => EvaluateAtNormalizedLevel(resources.GetNormalizedLevel(element));

        /// <summary>
        /// The formula itself, against a normalized level supplied by the caller: 0 at rest, 1 at
        /// integer level 10, and the [-0.5, 1.5] band at the extremes.
        ///
        /// <para>Public and pure so the migrated multipliers can be asserted in edit mode without
        /// standing up a vessel, a ResourceSystem and a GameObject —
        /// <c>ElementalScalingUnificationTests</c> locks each one's rest / full / floored-deficit
        /// values, which is exactly what an asset edit could silently move.</para>
        /// </summary>
        public float EvaluateAtNormalizedLevel(float normalizedLevel)
        {
            float value = Mathf.LerpUnclamped(Min, Max, normalizedLevel);
            return UseFloor ? Mathf.Max(Floor, value) : value;
        }

        public string Name
        {
            set { name = value; }
        }

        public IVessel Vessel
        {
            set
            {
                vessel = value;

                if (Enabled)
                {
                    vessel.BindElementalFloat(name, element);
                    vessel.VesselStatus.ResourceSystem.OnElementLevelChange += ScaleValueWithLevel;
                }
            }
        }

        /// <summary>
        /// The legacy BOUND path: keeps <see cref="Value"/> in step so a consumer that reads the
        /// field directly sees the scaled number without asking.
        ///
        /// <para>The event's <c>level</c> argument is deliberately unused — it is the quantized
        /// integer, and this path reads the continuous level through <see cref="Evaluate"/> so the
        /// bound and live paths return the same number for the same state.</para>
        ///
        /// <para>KNOWN ISSUE, not fixed here: this mutates a serialized field, and several
        /// ElementalFloats live on SHARED ScriptableObject assets — which is the vessel contract's
        /// rule 1 ("never bind state to an SO asset") and dirties the asset in the editor.
        /// Retiring this path in favour of <see cref="EvaluateLive"/> at every consumer is its own
        /// change with its own blast radius (<c>Skimmer</c>, <c>VesselAction</c>); logged in
        /// Docs/ElementalAbilitySystem/BACKLOG.md.</para>
        /// </summary>
        void ScaleValueWithLevel(Element changed, int level)
        {
            if (changed != element) return;

            var resources = vessel?.VesselStatus?.ResourceSystem;
            // Unreachable: this handler is subscribed to that very ResourceSystem's event.
            if (!resources) return;

            Value = Evaluate(resources);
        }
    }
}
