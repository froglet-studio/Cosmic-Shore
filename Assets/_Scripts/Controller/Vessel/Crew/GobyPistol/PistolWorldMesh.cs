using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's world as ONE mesh a frame: every prism in view warped through the unroll (<see cref="PistolWarp"/>),
    /// the trail it rides, the Pistol, the rings, the flares, the aim, laid on the screen plane with 2.5D parallax about
    /// the Pistol and drawn in two passes from two submeshes — SOLID (<c>Resources/PistolWorld.mat</c>: depth written,
    /// opaque) and GLASS (<c>Resources/PistolWorldGlass.mat</c>: blended, no depth write, in the far-to-near order it was
    /// emitted) — each as one instance of <c>CosmicShore/PistolWorld</c> with the frame's matrix, the draw call Unity and
    /// Amoebius share (vessel skill rules 50/51; <c>StoatPathfinderWorldDots</c> is the pattern). Everything the shader
    /// needs rides the vertices: colour (rgb, a = opacity), uv0 = (glow, reach), uv1 = (fog, 0). Allocation-free after
    /// warm-up: fixed arrays, the lists reused, a mesh's vertex data cached once per mesh.
    /// </summary>
    public sealed class PistolWorldMesh
    {
        public const string SolidMaterialPath = "PistolWorld", GlassMaterialPath = "PistolWorldGlass";
        public const int MaxVertices = 60000, MaxIndices = 180000;
        /// <summary>A prism mesh with more vertices than this (a high-poly morph) is drawn as its box instead.</summary>
        public const int MaxWarpedVertices = 512;

        /// <summary>A mesh's vertices as fractions of its bounds (u, v, w ∈ [0, 1]) and its triangles: what the warp deforms.</summary>
        public sealed class MeshData { public Vector3[] Local01; public int[] Tris; public Vector2[] UV; public int[][] SubTris; }

        static Dictionary<Mesh, MeshData> s_cache = new();
        static Material s_solid, s_glass; static bool s_warned;
        static readonly Matrix4x4[] s_instance = { Matrix4x4.identity };
        static readonly MeshData s_box = new()
        {
            Local01 = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0), new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1) },
            Tris = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7 },
        };

        readonly Vector3[] _v = new Vector3[MaxVertices];
        readonly Vector2[] _uv0 = new Vector2[MaxVertices], _uv1 = new Vector2[MaxVertices];
        readonly List<Color32> _c = new(MaxVertices);
        readonly List<int> _solid = new(MaxIndices), _glass = new(MaxIndices);
        readonly Vector3[] _scratch8 = new Vector3[8];
        int _n;
        Mesh _mesh;
        float _scX, _scY, _cx, _cy, _depth;

        public int VertexCount => _n;
        public int SolidIndexCount => _solid.Count;
        public int GlassIndexCount => _glass.Count;

        /// <summary>The unit box, for a prism that is not warped (a straight path, a far layer, or a mesh too big to deform).</summary>
        public static MeshData Box => s_box;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_cache = new Dictionary<Mesh, MeshData>(); s_solid = null; s_glass = null; s_warned = false; }
#endif

        /// <summary>A prism mesh's deformable data, cached; null when the mesh is too big to warp a frame (draw its box).</summary>
        public static MeshData DataOf(Mesh mesh)
        {
            if (!mesh) return null;
            if (s_cache.TryGetValue(mesh, out var data)) return data;
            if (mesh.vertexCount > MaxWarpedVertices || !mesh.isReadable) { s_cache[mesh] = null; return null; }
            var verts = mesh.vertices; var b = mesh.bounds; var local = new Vector3[verts.Length];
            Vector3 min = b.min, size = b.size;
            for (int i = 0; i < verts.Length; i++)
                local[i] = new Vector3(size.x > 1e-6f ? (verts[i].x - min.x) / size.x : 0.5f,
                                       size.y > 1e-6f ? (verts[i].y - min.y) / size.y : 0.5f,
                                       size.z > 1e-6f ? (verts[i].z - min.z) / size.z : 0.5f);
            var subs = new int[Mathf.Max(1, mesh.subMeshCount)][];
            for (int s = 0; s < subs.Length; s++) subs[s] = mesh.subMeshCount > 0 ? mesh.GetTriangles(s) : mesh.triangles;
            var uv = mesh.uv; if (uv == null || uv.Length != verts.Length) uv = new Vector2[verts.Length];
            data = new MeshData { Local01 = local, Tris = mesh.triangles, UV = uv, SubTris = subs };
            s_cache[mesh] = data;
            return data;
        }

        /// <summary>Start a frame: the camera centred at (scX, scY), the parallax about (cx, cy) (the Pistol's x, the
        /// camera's height less its lift), the plane <paramref name="depth"/> away (the lab's pParallax).</summary>
        public void Begin(float scX, float scY, float cx, float cy, float depth)
        {
            _n = 0; _solid.Clear(); _glass.Clear(); _c.Clear();
            _scX = scX; _scY = scY; _cx = cx; _cy = cy; _depth = Mathf.Max(1f, depth);
        }

        /// <summary>(x, h, d) in the Pistol's world is where it is drawn, in the flat frame (x right, h up, d into the screen): the
        /// side camera is a real perspective camera whose off-axis frustum IS <see cref="PistolWarp.Parallax"/>, so nothing is
        /// projected here any more (the world is drawn as itself; this mesh is the marks over it).</summary>
        public Vector3 Project(float x, float h, float d) => new(x, h, d);
        public Vector3 Project(Vector3 xhd) => Project(xhd.x, xhd.y, xhd.z);
        public float ScaleAt(float d) => PistolWarp.Scale(d, _depth);

        public bool HasRoom(int verts, int indices) => _n + verts <= MaxVertices && (_solid.Count + _glass.Count) + indices <= MaxIndices;

        int Push(Vector3 at, Color32 color, float glow, float reach, float fog)
        {
            _v[_n] = at; _c.Add(color); _uv0[_n] = new Vector2(glow, reach); _uv1[_n] = new Vector2(fog, 0f);
            return _n++;
        }

        void Tri(bool solid, int a, int b, int c) { var l = solid ? _solid : _glass; l.Add(a); l.Add(b); l.Add(c); }

        /// <summary>A mesh deformed inside the warped box <paramref name="xhd8"/> (<see cref="PistolWarp.BoxCorners"/>'s order).</summary>
        public void AddWarped(MeshData data, Vector3[] xhd8, Color32 color, float glow, float reach, float fog, bool solid)
        {
            if (data == null) data = s_box;
            int nv = data.Local01.Length, ni = data.Tris.Length;
            if (!HasRoom(nv, ni)) return;
            int b = _n;
            for (int i = 0; i < nv; i++)
            {
                var l = data.Local01[i];
                Push(Project(PistolWarp.Trilinear(xhd8, l.x, l.y, l.z)), color, glow, reach, fog);
            }
            var list = solid ? _solid : _glass;
            for (int i = 0; i < ni; i++) list.Add(b + data.Tris[i]);
        }

        /// <summary>An unwarped box at (x, h, d) with half-axes in the Pistol's world (ax along x, ay up, az into the depth).</summary>
        public void AddBox(Vector3 xhd, Vector3 ax, Vector3 ay, Vector3 az, Color32 color, float glow, float reach, float fog, bool solid)
        {
            PistolWarp.BoxCorners(xhd, ax, ay, az, _scratch8);
            AddWarped(s_box, _scratch8, color, glow, reach, fog, solid);
        }

        /// <summary>A quad a-b-c-d (in x, h, d), both faces visible (Cull Off).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color, float glow, float fog, bool solid)
        {
            if (!HasRoom(4, 6)) return;
            int i0 = Push(Project(a), color, glow, 0f, fog), i1 = Push(Project(b), color, glow, 0f, fog);
            int i2 = Push(Project(c), color, glow, 0f, fog), i3 = Push(Project(d), color, glow, 0f, fog);
            Tri(solid, i0, i1, i2); Tri(solid, i0, i2, i3);
        }

        /// <summary>A screen-facing square of half-size r at (x, h, d): a dot of the aim, a spark. Glass.</summary>
        public void AddDot(Vector3 xhd, float r, Color32 color, float glow = 1f, float fog = 0f)
        {
            AddQuad(xhd + new Vector3(-r, -r, 0f), xhd + new Vector3(-r, r, 0f), xhd + new Vector3(r, r, 0f), xhd + new Vector3(r, -r, 0f), color, glow, fog, false);
        }

        /// <summary>A screen-facing dash of half-size (rx, ry) at (x, h, d). Glass.</summary>
        public void AddDash(Vector3 xhd, float rx, float ry, Color32 color, float glow = 1f)
        {
            AddQuad(xhd + new Vector3(-rx, -ry, 0f), xhd + new Vector3(-rx, ry, 0f), xhd + new Vector3(rx, ry, 0f), xhd + new Vector3(rx, -ry, 0f), color, glow, 0f, false);
        }

        /// <summary>A tube along the depth axis from d0 to d1 at (x, h): the Pistol's body, end on. Radius may differ at each
        /// end (a squash), the tube can lean along (dx, dh) per unit of depth, and <paramref name="phase"/> turns its facets.</summary>
        public void AddTube(float x, float h, float d0, float d1, float r0, float r1, int sides, Color32 color, float glow, float reach, Vector2 lean, float phase, bool solid)
        {
            if (!HasRoom(sides * 2, sides * 6)) return;
            int b = _n;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + phase, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Push(Project(x + ca * r0 + lean.x * d0, h + sa * r0 + lean.y * d0, d0), color, glow, reach, 0f);
                Push(Project(x + ca * r1 + lean.x * d1, h + sa * r1 + lean.y * d1, d1), color, glow, reach, 0f);
            }
            for (int i = 0; i < sides; i++)
            {
                int i0 = b + i * 2, i1 = b + ((i + 1) % sides) * 2;
                Tri(solid, i0, i1, i0 + 1); Tri(solid, i1, i1 + 1, i0 + 1);
            }
        }

        /// <summary>A disc of radius r in the screen plane at (x, h, d): a cap, a glowing core; 3 sides = a triangle (an arrow).</summary>
        public void AddDisc(Vector3 xhd, float r, int sides, Color32 centre, Color32 edge, float glow, bool solid, float phase = 0f)
        {
            if (!HasRoom(sides + 1, sides * 3)) return;
            int c = Push(Project(xhd), centre, glow, 0f, 0f), b = _n;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + phase;
                Push(Project(xhd + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)), edge, glow, 0f, 0f);
            }
            for (int i = 0; i < sides; i++) Tri(solid, c, b + i, b + (i + 1) % sides);
        }

        /// <summary>A torus of radius R (tube radius r) at (x, h, d), its axis <paramref name="axis"/> in the Pistol's world. Glass,
        /// or the solid pass when <paramref name="solid"/> (the vortex ring: a lit faceted volume, never a glow).</summary>
        public void AddRing(Vector3 xhd, Vector3 axis, float R, float r, int segs, int sides, Color32 color, float glow, bool solid = false)
        {
            if (!HasRoom(segs * sides, segs * sides * 6)) return;
            Vector3 n = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.forward;
            Vector3 e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized, e2 = Vector3.Cross(n, e1);
            int b = _n;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f; Vector3 radial = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                for (int j = 0; j < sides; j++)
                {
                    float t = j / (float)sides * Mathf.PI * 2f;
                    Push(Project(xhd + radial * (R + r * Mathf.Cos(t)) + n * (r * Mathf.Sin(t))), color, glow, 0f, 0f);
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < sides; j++)
                {
                    int i1 = (i + 1) % segs, j1 = (j + 1) % sides;
                    Tri(solid, b + i * sides + j, b + i1 * sides + j, b + i1 * sides + j1);
                    Tri(solid, b + i * sides + j, b + i1 * sides + j1, b + i * sides + j1);
                }
        }

        /// <summary>A ribbon through the points (x, h, d), tapering from 15 % to <paramref name="width"/>: the Pistol's tiny trail. Glass.</summary>
        public void AddStrip(List<Vector3> points, float width, Color32 headColor, Color32 tailColor, float glow)
        {
            int n = points.Count; if (n < 2 || !HasRoom(n * 2, (n - 1) * 6)) return;
            int b = _n;
            for (int i = 0; i < n; i++)
            {
                float f = i / (float)(n - 1), w = width * (0.15f + 0.85f * f);
                Color32 col = Color.Lerp(tailColor, headColor, f);   // Color.Lerp: Amoebius has no Color32.Lerp
                Push(Project(points[i] + new Vector3(0f, w, 0f)), col, glow, 0f, 0f);
                Push(Project(points[i] - new Vector3(0f, w, 0f)), col, glow, 0f, 0f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = b + i * 2, i1 = i0 + 2;
                Tri(false, i0, i1, i0 + 1); Tri(false, i1, i1 + 1, i0 + 1);
            }
        }

        /// <summary>A line of constant half-height through the points (x, h, d), one colour per point: the ground line.</summary>
        public void AddLine(List<Vector3> points, List<Color32> colors, float halfHeight, float glow, bool solid)
        {
            int n = points.Count; if (n < 2 || colors.Count < n || !HasRoom(n * 2, (n - 1) * 6)) return;
            int b = _n;
            for (int i = 0; i < n; i++)
            {
                Push(Project(points[i] + new Vector3(0f, halfHeight, 0f)), colors[i], glow, 1f, 0f);
                Push(Project(points[i] - new Vector3(0f, halfHeight, 0f)), colors[i], glow, 1f, 0f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = b + i * 2, i1 = i0 + 2;
                Tri(solid, i0, i1, i0 + 1); Tri(solid, i1, i1 + 1, i0 + 1);
            }
        }

        /// <summary>Draw this frame's world: the solid pass, then the glass pass (nothing when empty or the materials are missing).
        /// <paramref name="frame"/> places the flat frame in the world (the camera shares it).</summary>
        public void End(Camera cam, Matrix4x4 frame)
        {
            if (_n == 0 || (_solid.Count == 0 && _glass.Count == 0) || !SharedMaterials()) return;
            if (!_mesh) { _mesh = new Mesh { name = "PistolWorld", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 }; _mesh.MarkDynamic(); }
            _mesh.Clear();
            _mesh.SetVertices(_v, 0, _n);
            _mesh.SetColors(_c);
            _mesh.SetUVs(0, _uv0, 0, _n);
            _mesh.SetUVs(1, _uv1, 0, _n);
            _mesh.subMeshCount = 2;
            _mesh.SetIndices(_solid, MeshTopology.Triangles, 0, false);
            _mesh.SetIndices(_glass, MeshTopology.Triangles, 1, false);
            float reach = cam ? Mathf.Max(1f, cam.farClipPlane) : 4000f;
            _mesh.bounds = new Bounds(new Vector3(0f, _scY, 0f), Vector3.one * (2f * reach));
            var world = new Bounds(cam ? cam.transform.position : (Vector3)frame.GetColumn(3), Vector3.one * (2f * reach));
            s_instance[0] = frame;
            if (_solid.Count > 0)
            {
                var rp = new RenderParams(s_solid) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = world, camera = cam };
                Graphics.RenderMeshInstanced(rp, _mesh, 0, s_instance);
            }
            if (_glass.Count > 0)
            {
                var rp = new RenderParams(s_glass) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = world, camera = cam };
                Graphics.RenderMeshInstanced(rp, _mesh, 1, s_instance);
            }
        }

        public void Dispose()
        {
            if (_mesh) Object.Destroy(_mesh);
            _mesh = null;
        }

        static bool SharedMaterials()
        {
            if (s_solid && s_glass) return true;
            if (!s_solid) s_solid = Resources.Load<Material>(SolidMaterialPath);
            if (!s_glass) s_glass = Resources.Load<Material>(GlassMaterialPath);
            if ((!s_solid || !s_glass) && !s_warned)
            {
                s_warned = true;
                CSDebug.LogError($"[GobyPistol] Resources/{SolidMaterialPath}.mat or Resources/{GlassMaterialPath}.mat is missing - the Pistol's world cannot draw.");
            }
            return s_solid && s_glass;
        }
    }
}
