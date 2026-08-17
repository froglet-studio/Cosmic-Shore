using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Draws the Dolphin's blast <b>profile</b> — the shape you would see looking straight down the
    /// barrel of the next crystal detonation, which is exactly what the Echo Sight paints onto the
    /// world. It is the CHARGE slot's ability icon.
    ///
    /// <para><b>The shape is a stadium</b> (a disc dragged along a segment), because that is what
    /// the blast's cross-section literally is: <c>AOEConicSweepQueryJob</c>, the capsule trigger and
    /// <c>PrismDestructionSight.hlsl</c> all clamp onto a segment and then measure distance to that
    /// point. Drawing anything else here — a cone, a wedge, a circle — would be an illustration of
    /// the ability rather than a readout of it.</para>
    ///
    /// <para><b>The two readings never fight.</b> <c>halfLength + radius</c> is always half the
    /// blast's base diameter, so banked energy sets the profile's total EXTENT while Charge only
    /// redistributes that extent between the round part and the straight part. Bank energy and the
    /// shape grows; raise Charge and it rounds out into a fatter, shorter capsule. One glyph, two
    /// independent axes, no shared pixels.</para>
    ///
    /// <para><b>Procedural rather than authored sprites.</b> The profile is a continuous function of
    /// two live meters; a sprite ladder would quantize it and would silently stop matching the blast
    /// the first time anyone retuned a scale. This generates the mesh from the same numbers
    /// <c>VesselExplosionByCrystalEffectSO.TryResolveProfile</c> hands the detonation, so the icon
    /// cannot drift from the thing it depicts.</para>
    ///
    /// Cost: one <c>SetVerticesDirty</c> per meaningful change (guarded — an unchanged profile
    /// rebuilds nothing), and a mesh of <c>2 × arcSegments + 2</c> vertices.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BlastProfileGraphic : MaskableGraphic
    {
        [Header("Shape")]
        [Tooltip("Vertices per end cap. 10 is smooth at HUD size; raising it costs vertices for no " +
                 "visible gain.")]
        [SerializeField, Range(3, 32)] private int arcSegments = 10;
        [Tooltip("Fraction of the rect's half-width the profile occupies when the blast is at its " +
                 "reference extent (full banked energy). Leaves a margin so a maxed profile does not " +
                 "touch the slot's edge.")]
        [SerializeField, Range(0.1f, 1f)] private float maxExtentFraction = 0.86f;
        [Tooltip("Floor on the drawn radius as a fraction of the rect's half-width, so an empty " +
                 "meter still shows a legible dot rather than vanishing. Continuity of existence: " +
                 "the icon is never nothing.")]
        [SerializeField, Range(0f, 0.5f)] private float minRadiusFraction = 0.06f;

        [Header("Orientation")]
        [Tooltip("Degrees to rotate the profile in the icon. The blast's straight section runs along " +
                 "the vessel's GAPE (its jaws open vertically), so 90 draws it upright to match the " +
                 "Space slot's jaw pair.")]
        [SerializeField] private float rotationDegrees = 90f;

        // Normalized 0-1 against the reference extent.
        float _radius01;
        float _halfLength01;

        /// <summary>
        /// Set the profile from world-unit blast numbers. All three come from one call to
        /// <c>VesselExplosionByCrystalEffectSO.TryResolveProfile</c>, so the icon can never be fed a
        /// radius from one moment and a reference from another.
        /// </summary>
        public void SetProfile(float radius, float halfLength, float referenceExtent)
        {
            if (referenceExtent <= 0f) return;

            float r = Mathf.Clamp01(radius / referenceExtent);
            float h = Mathf.Clamp01(halfLength / referenceExtent);

            // Sub-pixel churn is not a readout. Rebuild only when the shape actually moved.
            if (Mathf.Abs(r - _radius01) < 0.002f && Mathf.Abs(h - _halfLength01) < 0.002f) return;

            _radius01 = r;
            _halfLength01 = h;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = GetPixelAdjustedRect();
            float unit = Mathf.Min(rect.width, rect.height) * 0.5f * maxExtentFraction;
            if (unit <= 0f) return;

            float radius = Mathf.Max(_radius01, minRadiusFraction) * unit;
            float halfLength = _halfLength01 * unit;

            Vector2 centre = rect.center;
            float rad = rotationDegrees * Mathf.Deg2Rad;
            // The stadium's straight section runs along this axis; the caps bulge perpendicular.
            Vector2 along = new(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 across = new(-along.y, along.x);

            var color32 = color;

            // Fan origin at the centre, so the stadium fills solid with no interior seams.
            vh.AddVert(centre, color32, Vector2.zero);

            int perCap = Mathf.Max(3, arcSegments);
            int total = perCap * 2;

            // Walk the outline once: cap at +halfLength swinging through 180 degrees, then the cap
            // at -halfLength swinging back. The two straight edges fall out of the walk for free.
            for (int i = 0; i < total; i++)
            {
                bool positiveCap = i < perCap;
                int withinCap = positiveCap ? i : i - perCap;

                // -90..+90 across each cap, measured from the 'across' axis.
                float t = perCap == 1 ? 0f : withinCap / (float)(perCap - 1);
                float theta = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, t);

                Vector2 capCentre = centre + along * (positiveCap ? halfLength : -halfLength);
                Vector2 outward = positiveCap
                    ? across * Mathf.Cos(theta) + along * Mathf.Sin(theta)
                    : -across * Mathf.Cos(theta) - along * Mathf.Sin(theta);

                vh.AddVert(capCentre + outward * radius, color32, Vector2.zero);
            }

            for (int i = 0; i < total; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % total;
                vh.AddTriangle(0, a, b);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
