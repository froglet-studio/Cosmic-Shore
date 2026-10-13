using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat pathfinder's line as round dots placed IN THE SCENE (<c>R_VesselActions/STOAT_DIPOLE.md</c> §3b):
    /// camera-facing discs every <c>worldDotSpacing</c> world units along the predicted 3D path, each
    /// <c>worldDotSize</c> across and never smaller on screen than <c>worldDotMinPixels</c>, one GPU-instanced draw.
    /// They sit in the world, so they recede with depth, pass behind prisms, and are bent by the black hole's lens
    /// like any light (the lens copies the scene after the transparents). The screen overlay
    /// (<see cref="StoatPathfinderDots"/>) stays as the fallback when <c>dotsInWorld</c> is off.
    ///
    /// <para>Shader: <c>Sprites/Default</c>, which the project always includes in builds (GraphicsSettings), so the
    /// dots cannot vanish from a player build the way a <c>Shader.Find</c> of a stripped shader would.</para>
    /// </summary>
    public sealed class StoatPathfinderWorldDots
    {
        const int MaxDots = 1023;   // one instanced batch

        static Mesh s_quad;
        static Texture2D s_disc;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        readonly Matrix4x4[] _matrices = new Matrix4x4[MaxDots];
        readonly MaterialPropertyBlock _block = new();
        Material _material;
        int _count;

        /// <summary>
        /// Lays dots every <paramref name="spacing"/> world units of arc along <paramref name="points"/> (no dot across
        /// a wormhole jump), and draws them this frame. Returns the number drawn.
        /// </summary>
        public int Draw(Camera cam, Vector3[] points, int count, int[] jumps, int jumpCount, Color color,
            float size, float spacing, float minPixels)
        {
            _count = 0;
            if (!cam || points == null || count < 2 || !EnsureMaterial()) return 0;
            spacing = Mathf.Max(0.25f, spacing);
            size = Mathf.Max(0.01f, size);
            var camTf = cam.transform;
            Quaternion facing = camTf.rotation;
            Vector3 camPos = camTf.position;
            // world size of one screen pixel at unit distance
            float pixelAtUnit = cam.orthographic ? 0f
                : 2f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);

            float carried = 0f;   // arc length since the last dot
            for (int i = 1; i < count && _count < MaxDots; i++)
            {
                if (IsJump(i, jumps, jumpCount)) { carried = 0f; continue; }
                Vector3 a = points[i - 1], b = points[i];
                float seg = Vector3.Distance(a, b);
                if (!(seg > 1e-5f)) continue;
                float t = spacing - carried;
                while (t <= seg && _count < MaxDots)
                {
                    Vector3 p = Vector3.Lerp(a, b, t / seg);
                    float s = size;
                    if (pixelAtUnit > 0f) s = Mathf.Max(s, minPixels * pixelAtUnit * Vector3.Distance(camPos, p));
                    _matrices[_count++] = Matrix4x4.TRS(p, facing, new Vector3(s, s, s));
                    t += spacing;
                }
                carried = seg - (t - spacing);
            }
            if (_count == 0) return 0;

            _block.SetColor(ColorId, color);
            _block.SetTexture(MainTexId, Disc());
            var rp = new RenderParams(_material)
            {
                matProps = _block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshInstanced(rp, Quad(), 0, _matrices, _count);
            return _count;
        }

        static bool IsJump(int index, int[] jumps, int jumpCount)
        {
            if (jumps == null) return false;
            for (int j = 0; j < jumpCount && j < jumps.Length; j++) if (jumps[j] == index) return true;
            return false;
        }

        bool EnsureMaterial()
        {
            if (_material) return true;
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                CSDebug.LogError("[Stoat] Sprites/Default is missing from this build - the pathfinder's 3D dots cannot draw " +
                                 "(it is in GraphicsSettings' always-included shaders; check that list).");
                return false;
            }
            _material = new Material(shader) { name = "StoatPathDots", enableInstancing = true, hideFlags = HideFlags.DontSave };
            return true;
        }

        public void Dispose()
        {
            if (_material) Object.Destroy(_material);
            _material = null;
        }

        /// <summary>A unit quad centred on the origin, facing -Z (the camera's rotation turns it to the viewer).</summary>
        static Mesh Quad()
        {
            if (s_quad) return s_quad;
            s_quad = new Mesh { name = "StoatPathDotQuad", hideFlags = HideFlags.DontSave };
            s_quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            s_quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            s_quad.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            s_quad.RecalculateBounds();
            s_quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return s_quad;
        }

        /// <summary>A white disc with a dark rim, so a dot never reads as a star (made once, no asset).</summary>
        static Texture2D Disc()
        {
            if (s_disc) return s_disc;
            const int size = 32;
            s_disc = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StoatPathDotDisc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float c = 0.5f * (size - 1);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (0.5f * size);
                byte alpha = (byte)(255f * Mathf.Clamp01((1f - d) * 8f));
                byte lum = (byte)(d < 0.7f ? 255 : 18);   // the dark rim, the outer 30% of the radius
                px[y * size + x] = new Color32(lum, lum, lum, alpha);
            }
            s_disc.SetPixels32(px);
            s_disc.Apply(false, true);
            return s_disc;
        }
    }
}
