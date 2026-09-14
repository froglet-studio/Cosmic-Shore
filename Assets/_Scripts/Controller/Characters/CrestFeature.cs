using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A feather crest: a fan of tapered plates rooted along the crown's centre line, leaning
    /// back. Hair slot. Embedded; head units. Expressed only up a corvid's ladder — the beak is
    /// bought first, the crest is what 0.45+ buys.
    /// </summary>
    public static class CrestFeature
    {
        const int Rows = 6;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Crest;
            var part = ctx.Begin("Crest", CharacterMaterialSlot.Hair);
            int n = Mathf.Max(1, p.Feathers);
            float lean = p.LeanDeg * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0.5f : i / (float)(n - 1);
                float yaw = (f - 0.5f) * 2f * p.FanDeg * Mathf.Deg2Rad;
                // Root walks back along +y (toward the crown/back) so the crest is a ridge, not a clump.
                Vector3 root = new Vector3(0f, (f - 0.5f) * 0.10f, -0.008f);
                // Feather axis: outward (+z) leaned back toward +y, then fanned about z.
                Vector3 axis = new Vector3(0f, Mathf.Sin(lean), Mathf.Cos(lean));
                axis = GeometryKit.Rotate(axis, Vector3.forward, yaw).normalized;
                Vector3 side = Vector3.Cross(axis, new Vector3(0f, 1f, 0f)).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
                float length = p.Length * (0.75f + 0.25f * Mathf.Sin(Mathf.PI * f));
                var pts = new Vector3[Rows, 3];
                var uvs = new Vector2[Rows, 3];
                for (int r = 0; r < Rows; r++)
                {
                    float t = r / (float)(Rows - 1);
                    float halfW = p.Width * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.12f + 0.88f * t));
                    float curl = 0.15f * length * t * t;                 // tips curl back
                    Vector3 c = root + axis * (length * t) + new Vector3(0f, curl, -curl * 0.6f);
                    for (int k = 0; k < 3; k++)
                    {
                        pts[r, k] = c + side * ((k - 1) * halfW);
                        uvs[r, k] = new Vector2(k * 0.5f, t);
                    }
                }
                int firstV = part.Verts.Count;
                GeometryKit.Grid(part, pts, uvs);
                // Double-sided.
                for (int r = 0; r < Rows - 1; r++)
                    for (int k = 0; k < 2; k++)
                    {
                        int a = firstV + r * 3 + k, b = a + 1, cc = a + 3 + 1, d = a + 3;
                        part.AddTri(a, cc, b); part.AddTri(a, d, cc);
                    }
            }
            GeometryKit.RecalculateNormals(part);
        }
    }
}
