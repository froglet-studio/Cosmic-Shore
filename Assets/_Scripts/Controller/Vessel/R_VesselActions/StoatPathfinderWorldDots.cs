using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat pathfinder's line as round dots placed IN THE SCENE (<c>R_VesselActions/STOAT_DIPOLE.md</c> §3b), and Time
    /// mode's detection / orbit rings (§1b): camera-facing discs every <c>spacing</c> world units along each polyline, each
    /// <c>size</c> across and never smaller on screen than <c>minPixels</c>. They sit in the world, so they recede with
    /// depth, pass behind prisms, and are bent by the black hole's lens like any light (the lens copies the scene after the
    /// transparents). The screen overlay (<see cref="StoatPathfinderDots"/>) stays as the fallback when <c>dotsInWorld</c> is
    /// off.
    ///
    /// <para><b>How it draws.</b> <see cref="Begin"/>, any number of <see cref="Add"/>s, <see cref="End"/>: every dot of the
    /// frame becomes a quad in ONE dynamic mesh (billboarded here, its colour in the vertex colour), drawn once for the one
    /// camera with <c>CosmicShore/StoatPathDots</c> (<c>Resources/StoatPathDots.mat</c>). Not instanced, and on no shader
    /// that reads a sprite renderer's per-draw data: the first cut drew instanced <c>Sprites/Default</c>, whose instanced
    /// variant takes its flip and colour from per-instance arrays nothing filled — both read zero, every dot collapsed to a
    /// point, and the line was never seen (2026-10-14).</para>
    /// </summary>
    public sealed class StoatPathfinderWorldDots
    {
        /// <summary><c>Resources/StoatPathDots.mat</c> (shader <c>CosmicShore/StoatPathDots</c>).</summary>
        public const string MaterialResourcePath = "StoatPathDots";
        /// <summary>Dots per frame across every polyline: 4 vertices each, inside a 16-bit index buffer.</summary>
        public const int MaxDots = 4096;

        static Material s_material;
        static bool s_warned;
        static int[] s_indices;

        readonly Vector3[] _vertices = new Vector3[MaxDots * 4];
        readonly Color[] _colors = new Color[MaxDots * 4];
        readonly Vector2[] _uvs = new Vector2[MaxDots * 4];
        Mesh _mesh;
        Camera _cam;
        Vector3 _camPos, _camRight, _camUp;
        float _pixelAtUnit;
        int _count;

        /// <summary>Dots laid so far this frame.</summary>
        public int Count => _count;

        /// <summary>Start a frame's dots for <paramref name="cam"/>. False (nothing will draw) without a camera or material.</summary>
        public bool Begin(Camera cam)
        {
            _count = 0;
            _cam = cam;
            if (!cam || !SharedMaterial()) return false;
            var t = cam.transform;
            _camPos = t.position;
            _camRight = t.right;
            _camUp = t.up;
            // world size of one screen pixel at unit distance
            _pixelAtUnit = cam.orthographic ? 0f
                : 2f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            return true;
        }

        /// <summary>
        /// Lays dots every <paramref name="spacing"/> world units of arc along <paramref name="points"/> (no dot across a
        /// wormhole jump; <paramref name="closed"/> joins the last point to the first — a ring). Returns the dots added.
        /// </summary>
        public int Add(Vector3[] points, int count, int[] jumps, int jumpCount, Color color, float size, float spacing,
            float minPixels, bool closed = false)
        {
            if (!_cam || points == null || count < 2) return 0;
            int before = _count;
            spacing = Mathf.Max(0.25f, spacing);
            size = Mathf.Max(0.01f, size);
            float carried = 0f;   // arc length since the last dot
            int segments = closed ? count : count - 1;
            for (int s = 0; s < segments && _count < MaxDots; s++)
            {
                int i = s + 1;
                if (i < count && IsJump(i, jumps, jumpCount)) { carried = 0f; continue; }
                Vector3 a = points[s], b = points[i % count];
                float seg = Vector3.Distance(a, b);
                if (!(seg > 1e-5f)) continue;
                float t = spacing - carried;
                while (t <= seg && _count < MaxDots)
                {
                    var p = Vector3.Lerp(a, b, t / seg);
                    float dot = size;
                    if (_pixelAtUnit > 0f) dot = Mathf.Max(dot, minPixels * _pixelAtUnit * Vector3.Distance(_camPos, p));
                    Quad(p, 0.5f * dot, color);
                    t += spacing;
                }
                carried = seg - (t - spacing);
            }
            return _count - before;
        }

        /// <summary>Draw this frame's dots (nothing when none were laid).</summary>
        public void End()
        {
            if (!_cam || _count == 0 || !SharedMaterial()) return;
            EnsureMesh();
            _mesh.Clear(true);
            int verts = _count * 4;
            _mesh.SetVertices(_vertices, 0, verts);
            _mesh.SetColors(_colors, 0, verts);
            _mesh.SetUVs(0, _uvs, 0, verts);
            _mesh.SetIndices(Indices(), 0, _count * 6, MeshTopology.Triangles, 0, false);
            _mesh.bounds = new Bounds(_camPos, Vector3.one * (2f * Mathf.Max(1f, _cam.farClipPlane)));
            var rp = new RenderParams(s_material)
            {
                camera = _cam,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = _mesh.bounds,
            };
            Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
        }

        /// <summary>One polyline, drawn on its own this frame (the path when nothing else is drawn with it).</summary>
        public int Draw(Camera cam, Vector3[] points, int count, int[] jumps, int jumpCount, Color color,
            float size, float spacing, float minPixels)
        {
            if (!Begin(cam)) return 0;
            int n = Add(points, count, jumps, jumpCount, color, size, spacing, minPixels);
            End();
            return n;
        }

        void Quad(Vector3 centre, float half, Color color)
        {
            int v = _count * 4;
            var r = _camRight * half;
            var u = _camUp * half;
            _vertices[v] = centre - r - u;
            _vertices[v + 1] = centre - r + u;
            _vertices[v + 2] = centre + r + u;
            _vertices[v + 3] = centre + r - u;
            _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = color;
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(0f, 1f);
            _uvs[v + 2] = new Vector2(1f, 1f);
            _uvs[v + 3] = new Vector2(1f, 0f);
            _count++;
        }

        static bool IsJump(int index, int[] jumps, int jumpCount)
        {
            if (jumps == null) return false;
            for (int j = 0; j < jumpCount && j < jumps.Length; j++) if (jumps[j] == index) return true;
            return false;
        }

        void EnsureMesh()
        {
            if (_mesh) return;
            _mesh = new Mesh { name = "StoatPathDots", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
        }

        static int[] Indices()
        {
            if (s_indices != null) return s_indices;
            s_indices = new int[MaxDots * 6];
            for (int q = 0; q < MaxDots; q++)
            {
                int v = q * 4, i = q * 6;
                s_indices[i] = v;
                s_indices[i + 1] = v + 1;
                s_indices[i + 2] = v + 2;
                s_indices[i + 3] = v + 2;
                s_indices[i + 4] = v + 3;
                s_indices[i + 5] = v;
            }
            return s_indices;
        }

        static Material SharedMaterial()
        {
            if (s_material) return s_material;
            s_material = Resources.Load<Material>(MaterialResourcePath);
            if (!s_material && !s_warned)
            {
                s_warned = true;
                CSDebug.LogError($"[Stoat] Resources/{MaterialResourcePath}.mat is missing - the pathfinder's 3D dots and Time " +
                                 "mode's rings cannot draw.");
            }
            return s_material;
        }

        public void Dispose()
        {
            if (_mesh) Object.Destroy(_mesh);
            _mesh = null;
            _cam = null;
        }

        /// <summary>Play-mode (re)entry: the material cache describes a session that is gone.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_material = null;
            s_warned = false;
        }
    }
}
