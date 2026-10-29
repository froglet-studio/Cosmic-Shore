using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN's look, v1: every live bolt (<see cref="FulmenSystem.Bolts"/>) drawn into ONE world-space mesh on the system's
    /// own GameObject - two crossed ribbons per segment, revealed up to the bolt's front and faded by
    /// <see cref="FulmenSystem.Bolt.Alpha"/>, tinted <see cref="FulmenConfigSO.Tint"/>. Plain vertices and vertex colour, no
    /// VFX Graph and no compute, so it draws on WebGL2 / GLES3 too (the reason <see cref="ArcLightningFallback"/> exists).
    /// Buffers are made once; nothing allocates per frame, and no GameObject is made per bolt.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class FulmenView : MonoBehaviour
    {
        const int MaxDrawSegments = 4096;   // 8 verts each: 32768 verts, inside a 16-bit index buffer
        static readonly ProfilerMarker s_mDraw = new("Fulmen.Draw");

        FulmenSystem _system;
        Mesh _mesh;
        MeshRenderer _renderer;
        Material _fallback;
        Vector3[] _verts;
        Color32[] _colours;
        int[] _indices;
        int _lastQuads;

        void Awake()
        {
            _system = GetComponent<FulmenSystem>();
            _verts = new Vector3[MaxDrawSegments * 8];
            _colours = new Color32[MaxDrawSegments * 8];
            _indices = new int[MaxDrawSegments * 12];
            for (int q = 0; q < MaxDrawSegments * 2; q++)
            {
                int v = q * 4, i = q * 6;
                _indices[i] = v; _indices[i + 1] = v + 1; _indices[i + 2] = v + 2;
                _indices[i + 3] = v; _indices[i + 4] = v + 2; _indices[i + 5] = v + 3;
            }
            _mesh = new Mesh { name = "Fulmen" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            if (_fallback) Destroy(_fallback);
        }

        void LateUpdate()
        {
            if (_system == null) return;
            if (_system.LiveBolts == 0)
            {
                if (_lastQuads > 0) { _mesh.Clear(); _lastQuads = 0; _renderer.enabled = false; }
                return;
            }
            using var _ = s_mDraw.Auto();

            float now = Time.time;
            int seg = 0;
            Material mat = null;
            var bolts = _system.Bolts;
            for (int bi = 0; bi < bolts.Length && seg < MaxDrawSegments; bi++)
            {
                var b = bolts[bi];
                if (b == null || !b.Active || b.Config == null) continue;
                if (mat == null) mat = b.Config.Material;
                float alpha = b.Alpha(now);
                if (alpha <= 0f) continue;
                var tint = b.Config.Tint;
                for (int i = 0; i < b.Count && seg < MaxDrawSegments; i++)
                {
                    var s = b.Segments[i];
                    if (s.StartDistance >= b.Front) continue;
                    var end = s.EndDistance <= b.Front
                        ? s.End
                        : Vector3.Lerp(s.Start, s.End, (b.Front - s.StartDistance) / Mathf.Max(1e-4f, s.EndDistance - s.StartDistance));
                    float w = b.Width * Mathf.Max(0.2f, 1f - 0.3f * s.Depth) * 0.5f;
                    // the tip of a stream flickers brightest: fade toward the root a little
                    float a = alpha * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(s.EndDistance / Mathf.Max(1f, b.Front)));
                    var c = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(tint.a * a));
                    WriteSegment(seg++, s.Start, end, w, c);
                }
            }

            int quads = seg * 2;
            if (quads == 0)
            {
                if (_lastQuads > 0) { _mesh.Clear(); _lastQuads = 0; }
                _renderer.enabled = false;
                return;
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts, 0, quads * 4);
            _mesh.SetColors(_colours, 0, quads * 4);
            _mesh.SetIndices(_indices, 0, quads * 6, MeshTopology.Triangles, 0, false);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            _lastQuads = quads;

            _renderer.sharedMaterial = mat != null ? mat : Fallback();
            _renderer.enabled = true;
        }

        void WriteSegment(int index, Vector3 a, Vector3 b, float halfWidth, Color colour)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
            dir.Normalize();
            var n1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * halfWidth;
            var n2 = Vector3.Cross(dir, n1).normalized * halfWidth;
            int v = index * 8;
            _verts[v] = a - n1; _verts[v + 1] = a + n1; _verts[v + 2] = b + n1; _verts[v + 3] = b - n1;
            _verts[v + 4] = a - n2; _verts[v + 5] = a + n2; _verts[v + 6] = b + n2; _verts[v + 7] = b - n2;
            Color32 c = colour;
            for (int k = 0; k < 8; k++) _colours[v + k] = c;
        }

        Material Fallback()
        {
            if (_fallback) return _fallback;
            var shader = Shader.Find("Sprites/Default");
            _fallback = new Material(shader) { name = "Fulmen (fallback)" };
            return _fallback;
        }
    }
}
