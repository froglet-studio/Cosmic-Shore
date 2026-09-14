using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A chiropteran nose leaf: a spear-shaped fleshy plate standing on the nose tip, facing
    /// forward. Skin slot with projected UVs (ridges are painted). Embedded; head units.
    /// </summary>
    public static class NoseLeafFeature
    {
        const int Rows = 9, Cols = 7;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.NoseLeaf;
            var leaf = ctx.Begin("NoseLeaf", CharacterMaterialSlot.Skin, projectUvs: true);
            var front = new Vector3[Rows, Cols];
            var back = new Vector3[Rows, Cols];
            var uvs = new Vector2[Rows, Cols];
            for (int r = 0; r < Rows; r++)
            {
                float t = r / (float)(Rows - 1);
                float halfW = p.Width * 0.5f * GeometryKit.SafePow(Mathf.Sin(Mathf.PI * Mathf.Clamp(0.08f + 0.92f * t, 0f, 1f)), 0.5f + 0.6f * p.Spear);
                float y = -p.Height * 0.25f + t * p.Height;
                for (int c = 0; c < Cols; c++)
                {
                    float u = c / (float)(Cols - 1);
                    float x = (u - 0.5f) * 2f * halfW;
                    // Lean back a little toward the tip so it reads as standing on the nose.
                    float z = -0.006f + t * 0.03f - (x * x) * 0.4f;
                    front[r, c] = new Vector3(x, y, z);
                    back[r, c] = new Vector3(x, y, z - p.Thickness);
                    uvs[r, c] = new Vector2(u, t);
                }
            }
            GeometryKit.Grid(leaf, front, uvs);
            int before = leaf.Tris.Count;
            GeometryKit.Grid(leaf, back, uvs);
            for (int i = before; i < leaf.Tris.Count; i += 3)
            {
                int tt = leaf.Tris[i + 1]; leaf.Tris[i + 1] = leaf.Tris[i + 2]; leaf.Tris[i + 2] = tt;
            }
            GeometryKit.RecalculateNormals(leaf);
        }
    }
}
