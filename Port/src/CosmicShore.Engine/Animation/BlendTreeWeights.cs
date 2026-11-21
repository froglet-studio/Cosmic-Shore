using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Child weights of a <see cref="BlendTree"/> for the current parameter values:
    ///  • Simple1D - linear between the two thresholds that bracket the parameter (clamped at the ends);
    ///  • FreeformCartesian2D - gradient-band interpolation over the child positions;
    ///  • SimpleDirectional2D / FreeformDirectional2D - gradient-band interpolation in polar space
    ///    (magnitude difference plus angle), which keeps a direction's weight independent of how far
    ///    the stick is pushed;
    ///  • Direct - each child's own parameter value, normalized when the tree asks for it.
    /// Every mode but Direct sums to exactly 1.
    /// </summary>
    public static class BlendTreeWeights
    {
        /// <summary>Weight of the angular term against the (normalized) magnitude term in polar gradient bands.</summary>
        const float PolarAngleWeight = 2f;

        public static void Compute(BlendTree tree, float x, float y, Func<string, float> parameter, Span<float> weights)
        {
            var kids = tree.Children;
            int n = kids.Count;
            weights.Clear();
            if (n == 0) return;
            switch (tree.Type)
            {
                case BlendTreeType.Simple1D:
                    OneD(tree, x, weights);
                    return;
                case BlendTreeType.Direct:
                {
                    float sum = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        weights[i] = Math.Max(0f, parameter(kids[i].DirectParameter));
                        sum += weights[i];
                    }
                    if (tree.NormalizedBlendValues && sum > 0f)
                        for (int i = 0; i < n; i++) weights[i] /= sum;
                    return;
                }
                case BlendTreeType.FreeformCartesian2D:
                    GradientBand(tree, new Vector2(x, y), polar: false, weights);
                    return;
                default:
                    GradientBand(tree, new Vector2(x, y), polar: true, weights);
                    return;
            }
        }

        static void OneD(BlendTree tree, float x, Span<float> weights)
        {
            var kids = tree.Children;
            int n = kids.Count;
            // Children are authored in threshold order; find the bracketing pair without assuming it.
            int lo = -1, hi = -1;
            for (int i = 0; i < n; i++)
            {
                float t = kids[i].Threshold;
                if (t <= x && (lo < 0 || t > kids[lo].Threshold)) lo = i;
                if (t >= x && (hi < 0 || t < kids[hi].Threshold)) hi = i;
            }
            if (lo < 0 && hi < 0) { weights[0] = 1f; return; }
            if (lo < 0) { weights[hi] = 1f; return; }
            if (hi < 0 || hi == lo) { weights[lo] = 1f; return; }
            float span = kids[hi].Threshold - kids[lo].Threshold;
            float f = span > 0f ? (x - kids[lo].Threshold) / span : 0f;
            weights[lo] = 1f - f;
            weights[hi] = f;
        }

        static void GradientBand(BlendTree tree, Vector2 p, bool polar, Span<float> weights)
        {
            var kids = tree.Children;
            int n = kids.Count;
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                var pi = kids[i].Position;
                float h = 1f;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    var pj = kids[j].Position;
                    Vector2 vij, vip;
                    if (polar)
                    {
                        float mi = pi.magnitude, mj = pj.magnitude, mp = p.magnitude;
                        float avg = (mi + mj) * 0.5f;
                        if (avg <= 1e-6f) continue;
                        float aij = mi > 1e-6f && mj > 1e-6f ? SignedAngle(pi, pj) : 0f;
                        float aip = mi > 1e-6f && mp > 1e-6f ? SignedAngle(pi, p) : 0f;
                        vij = new Vector2((mj - mi) / avg, aij * PolarAngleWeight);
                        vip = new Vector2((mp - mi) / avg, aip * PolarAngleWeight);
                    }
                    else
                    {
                        vij = pj - pi;
                        vip = p - pi;
                    }
                    float d = vij.x * vij.x + vij.y * vij.y;
                    if (d <= 1e-12f) continue;
                    float hij = 1f - (vip.x * vij.x + vip.y * vij.y) / d;
                    if (hij < h) h = hij;
                }
                weights[i] = Math.Max(0f, h);
                sum += weights[i];
            }
            if (sum > 0f)
                for (int i = 0; i < n; i++) weights[i] /= sum;
            else
            {
                // Outside every band (cannot happen for a well-formed layout): take the nearest child.
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = (kids[i].Position - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                weights[best] = 1f;
            }
        }

        static float SignedAngle(Vector2 a, Vector2 b) => MathF.Atan2(a.x * b.y - a.y * b.x, a.x * b.x + a.y * b.y);
    }
}
