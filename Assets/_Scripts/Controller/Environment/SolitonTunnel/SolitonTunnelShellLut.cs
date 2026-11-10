using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shell LUT for SolitonTunnel.shader's march-free wall bend (_MW_STANDIN, SolitonTunnelStandin.hlsl): the march's
    /// integrals through a straight cylindrical shell of radius 1 (inner wallInside, outer wallOutside), per
    /// (beta = impact / radius over [0, BetaMax], sin of the ray-to-axis angle over [0, 1]), all x sin. Port of
    /// unity-standins/lens/sol/lut.py. Rebake only when WallInside / WallOutside / RimTightness change (about 1 ms).
    /// </summary>
    public static class SolitonTunnelShellLut
    {
        public const int NB = 64, NS = 32, NL = 1025;
        public const float BetaMax = 1.3f;
        public static readonly int LutId = Shader.PropertyToID("_MWShellLut");
        static Texture2D s_lut;
        static Vector3 s_key = new(-1f, -1f, -1f);

        static float Prof(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (6f * t - 15f) + 10f); }

        public static Texture2D Get(float wallInside, float wallOutside, float rimTightness)
        {
            float ti = Mathf.Max(0.02f, wallInside), to = Mathf.Max(0.02f, wallOutside), rt = Mathf.Max(1f, rimTightness);
            var key = new Vector3(ti, to, rt);
            if (s_lut != null && key == s_key) return s_lut;
            s_key = key;
            if (s_lut == null)
                s_lut = new Texture2D(NB, NS, TextureFormat.RGBAFloat, false, true)
                    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SolitonShellLut" };
            var px = new Color[NB * NS];
            float lm = 1f + to, dl = 2f * lm / (NL - 1), k = 1f / (0.5f * (ti + to));
            for (int j = 0; j < NS; j++)
            {
                float sg = Mathf.Max((j + 0.5f) / NS, 1e-3f);
                for (int i = 0; i < NB; i++)
                {
                    float b = (i + 0.5f) / NB * BetaMax, r = 0f, g = 0f, bl = 0f, a = 0f;
                    for (int n = 0; n < NL; n++)
                    {
                        float l = -lm + n * dl, rho = Mathf.Sqrt(b * b + l * l), d = rho - 1f, th = d > 0f ? to : ti;
                        if (Mathf.Abs(d) >= th) continue;
                        float w = Prof(1f - Mathf.Abs(d) / th);
                        float dn = sg * Mathf.Abs(l) / Mathf.Max(rho, 1e-9f);
                        float gz = 0.1f + 0.9f * Mathf.Max(dn, 0.12f);
                        r += gz * w; g += gz * w * b / Mathf.Max(rho, 1e-9f);
                        bl += w / th; a += w / th * Mathf.Pow(1f - dn, rt);
                    }
                    px[j * NB + i] = new Color(k * r * dl, k * g * dl, bl * dl, a * dl);
                }
            }
            s_lut.SetPixels(px);
            s_lut.Apply(false, false);
            return s_lut;
        }
    }
}
