using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The warp field of a <b>wormhole neck</b> (Docs/CRYSTAL_WORMHOLE.md §2, Docs/WARP_FIELD.md §4): the scale
    /// shrinks toward a pole and holds at <see cref="ThroatScale"/> on the throat sphere, shaped so that — measured
    /// in the pilot's own lengths — the sphere at distance r has felt radius
    /// <code>F(r) = r + λ·e^(−u − u²/2),  u = (r − throat)/λ,  λ = throat/throatScale − throat</code>
    /// and <c>s = r / F</c>. F is smallest, and STATIONARY, at the throat: a catenoid-like neck, which is what
    /// makes light that skims it wind only near one ring (an Ellis wormhole's optics) instead of all along a
    /// tube, the way <see cref="RadialWarp"/>'s <c>s ∝ r</c> does. Far out it eases to exactly 1 (a smootherstep
    /// on ln s between 3λ and 4.5λ beyond the throat), so the field has an edge nobody can see.
    ///
    /// <para><c>CrystalWormholeLens.hlsl</c> carries the SAME lines (<c>CrystalWormholeLnS</c> /
    /// <c>CrystalWormholeDLnS</c>): the lens bends light by this field's gradient, so the light a pilot sees and
    /// the scale they fly at are one geometry. <see cref="LnS"/> is the shared form, and
    /// <c>Tools/Shaders/simulate_crystal_wormhole.py --check</c> holds the shader to it.</para>
    ///
    /// <para>Inside the throat (where nothing outside ever gets — a crystal wormhole's throats are glued) the
    /// scale holds at its floor.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ThroatWarp", menuName = "ScriptableObjects/Warp/ThroatWarp", order = 31)]
    public class ThroatWarp : WarpFieldSO
    {
        /// <summary>Where the taper starts and ends, in λ beyond the throat.</summary>
        public const float TaperStart = 3f, TaperEnd = 4.5f;

        [Header("Neck")]
        [SerializeField, Min(1f), Tooltip("The throat's radius, world units: the sphere a crystal wormhole glues to its " +
                                          "partner, where the neck is narrowest. Keep it equal to the wormhole's own " +
                                          "throat (CrystalWormholeTests holds the cell to it).")]
        float throatRadius = 20f;

        [SerializeField, Range(0.02f, 0.9f), Tooltip("The scale ON the throat: how small a pilot is when they go through. " +
                                                     "Also sets how far the neck reaches — λ = throat/scale − throat, and " +
                                                     "the field is flat 4.5 λ beyond the throat — and how big the throat " +
                                                     "looks: its ring is throat/scale across, in impact parameter.")]
        float throatScale = 0.4f;

        public float ThroatRadius => throatRadius;
        public float ThroatScale => throatScale;
        /// <summary>The throat's felt radius: what it measures in a pilot's own lengths.</summary>
        public float FeltThroat => throatRadius / Mathf.Max(throatScale, 1e-3f);
        /// <summary>λ: the neck's length scale, world units.</summary>
        public float Lambda => Mathf.Max(FeltThroat - throatRadius, 1e-3f);
        public float TaperIn => throatRadius + TaperStart * Lambda;
        /// <summary>Beyond this distance from the pole the field is exactly 1.</summary>
        public float Reach => throatRadius + TaperEnd * Lambda;

        public override float ScaleAt(Vector3 offset) =>
            Mathf.Exp(LnS(offset.magnitude, throatRadius, FeltThroat, TaperIn, Reach));

        /// <summary>ln s at distance <paramref name="r"/> — CrystalWormholeLnS, line for line.</summary>
        public static float LnS(float r, float throat, float feltThroat, float taperIn, float taperOut)
        {
            r = Mathf.Max(r, 1e-3f);
            float lambda = Mathf.Max(feltThroat - throat, 1e-3f);
            float lnS = Mathf.Log(Mathf.Max(throat, 1e-3f)) - Mathf.Log(Mathf.Max(feltThroat, 1e-3f));
            if (r > throat)
            {
                float u = (r - throat) / lambda;
                lnS = Mathf.Log(r) - Mathf.Log(r + lambda * Mathf.Exp(-u - 0.5f * u * u));
            }
            float t = Mathf.Clamp01((r - taperIn) / Mathf.Max(taperOut - taperIn, 1e-3f));
            return (1f - t * t * t * (t * (t * 6f - 15f) + 10f)) * lnS;
        }

        public override Vector3 HybridVector(Transform node)
        {
            var p = node.position;
            return (p.sqrMagnitude > 1e-8f ? p.normalized : Vector3.up) * ScaleAt(p);
        }
    }
}
