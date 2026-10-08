using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The warp field that is simply DISTANCE FROM THE CENTRE (Docs/WARP_FIELD.md): the local
    /// length scale at a point is <c>(r / referenceRadius)^exponent</c>, floored and capped.
    /// <see cref="WarpFieldRuntime"/> scales every length a player observes by it — the vessel,
    /// its speed, its camera's distance and clip planes, the prisms it lays and their spacing —
    /// so a player flying inward shrinks without near-field tells, and whatever sits at the
    /// centre appears to GROW. Other players see that vessel shrink on its way in.
    ///
    /// <para>Revives the 2022 "space time warping" scene (Game_TestModeFour, the
    /// <c>TardisWarp</c> field and <c>WarpFieldController</c>), with the field read as a plain
    /// function of position so every client derives every vessel's scale from the position it
    /// already replicates.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RadialWarp", menuName = "ScriptableObjects/Warp/RadialWarp", order = 30)]
    public class RadialWarp : WarpFieldSO
    {
        [Header("Radial scale")]
        [SerializeField, Min(1f), Tooltip("Distance from the centre, world units, at which the scale is exactly 1 — " +
                                          "where everything is its authored size. Put it where players normally " +
                                          "fly (a freestyle cell's toys sit ~980 u out).")]
        float referenceRadius = 1000f;

        [SerializeField, Min(0.05f), Tooltip("Shape of the falloff: 1 = the scale is proportional to distance " +
                                             "(the 2022 field). Above 1 the shrink concentrates near the centre; " +
                                             "below 1 it starts further out.")]
        float exponent = 1f;

        [SerializeField, Range(0.001f, 1f), Tooltip("Smallest scale the field ever reaches. Keeps a vessel at the " +
                                                    "centre finite — its speed and its camera distance are " +
                                                    "multiplied by this too.")]
        float minScale = 0.02f;

        [SerializeField, Min(1f), Tooltip("Largest scale the field ever reaches, outside the reference radius. " +
                                          "1 = nothing grows beyond its authored size.")]
        float maxScale = 1f;

        public float ReferenceRadius => referenceRadius;
        public float MinScale => minScale;
        public float MaxScale => Mathf.Max(maxScale, minScale);

        /// <summary>The local length scale at <paramref name="offset"/> from the field's centre.</summary>
        public override float ScaleAt(Vector3 offset)
        {
            float r = offset.magnitude / Mathf.Max(1f, referenceRadius);
            float s = Mathf.Pow(r, Mathf.Max(0.05f, exponent));
            return Mathf.Clamp(s, Mathf.Max(0.001f, minScale), MaxScale);
        }

        /// <summary>The 2022 contract: aligned with the gradient (outward), magnitude the scale.</summary>
        public override Vector3 HybridVector(Transform node)
        {
            var p = node.position;
            return (p.sqrMagnitude > 1e-8f ? p.normalized : Vector3.up) * ScaleAt(p);
        }
    }
}
