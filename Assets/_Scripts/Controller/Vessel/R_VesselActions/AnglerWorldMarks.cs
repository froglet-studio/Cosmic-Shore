using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's marks in the scene: a dot on every painted prism, the esca, the ball, and the brush's rim. ONE mesh a
    /// frame of camera-facing quads with the colour in the vertex colour, drawn on the Stoat's dot material
    /// (<c>Resources/StoatPathDots.mat</c>, <c>CosmicShore/StoatPathDots</c>) through one identity-matrix
    /// <c>Graphics.RenderMeshInstanced</c>, the same shape as <see cref="StoatPathfinderWorldDots"/> and for the same
    /// reasons (vessel rules 50 and 51: no sprite shader instancing, only engine calls Amoebius's shim has).
    /// Presentation only; nothing reads it back.
    /// </summary>
    public sealed class AnglerWorldMarks
    {
        public const int MaxMarks = 2048;

        static Material s_material;
        static bool s_warned;
        static readonly Matrix4x4[] s_identity = { Matrix4x4.identity };

        readonly Vector3[] _vertices = new Vector3[MaxMarks * 4];
        readonly Vector2[] _uvs = new Vector2[MaxMarks * 4];
        readonly List<Color32> _colors = new(MaxMarks * 4);
        readonly List<int> _indices = new(MaxMarks * 6);
        Mesh _mesh;
        Camera _cam;
        Vector3 _camPos, _camRight, _camUp;
        float _pixelAtUnit;
        int _count;

        public int Count => _count;

        bool _onlyThisCamera;

        /// <summary>Start a frame of marks facing <paramref name="cam"/>. <paramref name="onlyThisCamera"/> draws them in
        /// that camera alone (a vessel with two views draws a set per view); otherwise every camera sees them.</summary>
        public bool Begin(Camera cam, bool onlyThisCamera = false)
        {
            _count = 0;
            _colors.Clear();
            _cam = cam;
            _onlyThisCamera = onlyThisCamera;
            if (!cam || !SharedMaterial()) return false;
            var t = cam.transform;
            _camPos = t.position;
            _camRight = t.right;
            _camUp = t.up;
            _pixelAtUnit = cam.orthographic ? 0f
                : 2f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            return true;
        }

        /// <summary>One dot, <paramref name="size"/> world units across, never smaller than <paramref name="minPixels"/>.</summary>
        public void Dot(Vector3 p, Color color, float size, float minPixels)
        {
            if (!_cam || _count >= MaxMarks) return;
            float dot = Mathf.Max(0.01f, size);
            if (_pixelAtUnit > 0f) dot = Mathf.Max(dot, minPixels * _pixelAtUnit * Vector3.Distance(_camPos, p));
            Quad(p, 0.5f * dot, color);
        }

        /// <summary>A ring of <paramref name="dots"/> dots round <paramref name="centre"/> in the plane of <paramref name="a"/>
        /// and <paramref name="b"/> (unit, perpendicular), radius <paramref name="radius"/>.</summary>
        public void Ring(Vector3 centre, Vector3 a, Vector3 b, float radius, int dots, Color color, float size, float minPixels)
        {
            for (int i = 0; i < dots; i++)
            {
                float th = i * (2f * Mathf.PI / Mathf.Max(1, dots));
                Dot(centre + (a * Mathf.Cos(th) + b * Mathf.Sin(th)) * radius, color, size, minPixels);
            }
        }

        public void End()
        {
            if (!_cam || _count == 0 || !SharedMaterial()) return;
            if (!_mesh)
            {
                _mesh = new Mesh { name = "AnglerWorldMarks", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }
            _mesh.Clear();
            int verts = _count * 4;
            _mesh.SetVertices(_vertices, 0, verts);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs, 0, verts);
            SizeIndices(_count);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0, false);
            var bounds = new Bounds(_camPos, Vector3.one * (2f * Mathf.Max(1f, _cam.farClipPlane)));
            _mesh.bounds = bounds;
            var rp = new RenderParams(s_material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = bounds,
                camera = _onlyThisCamera ? _cam : null,
            };
            Graphics.RenderMeshInstanced(rp, _mesh, 0, s_identity);
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
            Color32 c = color;
            _colors.Add(c);
            _colors.Add(c);
            _colors.Add(c);
            _colors.Add(c);
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(0f, 1f);
            _uvs[v + 2] = new Vector2(1f, 1f);
            _uvs[v + 3] = new Vector2(1f, 0f);
            _count++;
        }

        void SizeIndices(int quads)
        {
            int want = quads * 6;
            if (_indices.Count > want) _indices.RemoveRange(want, _indices.Count - want);
            for (int q = _indices.Count / 6; q < quads; q++)
            {
                int v = q * 4;
                _indices.Add(v);
                _indices.Add(v + 1);
                _indices.Add(v + 2);
                _indices.Add(v + 2);
                _indices.Add(v + 3);
                _indices.Add(v);
            }
        }

        static Material SharedMaterial()
        {
            if (s_material) return s_material;
            s_material = Resources.Load<Material>(StoatPathfinderWorldDots.MaterialResourcePath);
            if (!s_material && !s_warned)
            {
                s_warned = true;
                CSDebug.LogError($"[Angler] Resources/{StoatPathfinderWorldDots.MaterialResourcePath}.mat is missing - the " +
                                 "Angler's paint marks, esca and ball cannot draw.");
            }
            return s_material;
        }

        public void Dispose()
        {
            if (_mesh) Object.Destroy(_mesh);
            _mesh = null;
            _cam = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_material = null;
            s_warned = false;
        }
    }
}
