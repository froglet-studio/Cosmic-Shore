using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The fauna attacking the Ark, marked in the world (Frogma Rev 113, Hud; Shombith, 2026-11-16: "red markers on fauna
    /// attacking the Ark - do it"). Each hostile creature inside the Ark's guard radius
    /// (<see cref="IArkEscortHost.CountThreats"/>, <see cref="ArkEscortPilot.GuardRadius"/>) wears a lit, faceted red
    /// diamond hovering over it, sized to the creature. When any of them is off screen, a lit red arrow at the screen's
    /// edge, in the world just in front of the camera, points at them, carrying one pip per attacker (up to
    /// <see cref="MaxPips"/>). Both are 3D volumes, never a flat ring or a label (Docs/claude/SHADERS_AND_PERFORMANCE.md,
    /// "In the world, not on the screen" and "Nothing in the world looks flat"). Escort the Ark and a job voyage each own
    /// one while they sail.
    /// </summary>
    public sealed class ArkAttackerMarks : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        const int MaxMarks = 24;
        public const int MaxPips = 6;
        /// <summary>How far in front of the camera the edge arrow floats, and how far in from the screen's edge.</summary>
        const float ArrowDepth = 24f;
        const float ArrowInset = 0.4f;

        // The breach red of Rev 113, traced through the worn colour set.
        static readonly Color ThreatAuthored = new(1f, 0.294f, 0.227f, 1f);   // #FF4B3A

        IArkEscortHost _host;
        System.Func<Ark> _ark;
        readonly List<Fauna> _attackers = new();
        readonly List<Transform> _marks = new();
        Transform _arrow;
        readonly List<Transform> _pips = new();
        Material _material;
        Mesh _diamond, _arrowMesh;
        float _nextRefresh;
        int _colourVersion = -1;

        /// <summary>Mark <paramref name="host"/>'s attackers on <paramref name="ark"/>.</summary>
        public static ArkAttackerMarks Create(Transform parent, IArkEscortHost host, System.Func<Ark> ark)
        {
            var go = new GameObject("ArkAttackerMarks");
            go.transform.SetParent(parent, false);
            var marks = go.AddComponent<ArkAttackerMarks>();
            marks._host = host;
            marks._ark = ark;
            return marks;
        }

        void Awake()
        {
            _diamond = Diamond();
            _arrowMesh = Arrow();
            _material = ToyModelBuilder.BuildPreviewMaterial(Threat);
            _colourVersion = ColorTrace.Version;
            _arrow = Part("Arrow", _arrowMesh, transform);
            _arrow.gameObject.SetActive(false);
            for (int i = 0; i < MaxPips; i++)
            {
                var pip = Part("Pip", _diamond, _arrow);
                pip.localScale = Vector3.one * 0.32f;
                pip.localPosition = new Vector3(0f, 0f, -0.9f - 0.55f * i);
                _pips.Add(pip);
            }
        }

        static Color Threat => ColorTrace.Retrace(ColorTrace.World, ThreatAuthored);

        Transform Part(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void Update()
        {
            if (_colourVersion != ColorTrace.Version) Repaint();
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                var ark = _ark?.Invoke();
                if (ark && _host != null) _host.CountThreats(ark.Position, ArkEscortPilot.GuardRadius, _attackers);
                else _attackers.Clear();
            }
            PlaceMarks();
            PlaceArrow();
        }

        void Repaint()
        {
            _colourVersion = ColorTrace.Version;
            var c = Threat;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_material.HasProperty("_EmissionColor")) _material.SetColor("_EmissionColor", c * 0.6f);
            _material.color = c;
        }

        /// <summary>A diamond over each live attacker, turning slowly, sized to the creature.</summary>
        void PlaceMarks()
        {
            int n = 0;
            float spin = Time.time * 90f;
            for (int i = 0; i < _attackers.Count && n < MaxMarks; i++)
            {
                var f = _attackers[i];
                if (!f || !f.IsAlivePrey) continue;
                if (n >= _marks.Count) _marks.Add(Part("Mark", _diamond, transform));
                var mark = _marks[n++];
                float size = MarkSize(f);
                mark.gameObject.SetActive(true);
                mark.SetPositionAndRotation(f.transform.position + Vector3.up * size * 1.6f, Quaternion.Euler(0f, spin, 0f));
                mark.localScale = Vector3.one * size;
            }
            for (int i = n; i < _marks.Count; i++)
                if (_marks[i].gameObject.activeSelf) _marks[i].gameObject.SetActive(false);
        }

        static float MarkSize(Fauna f)
        {
            float extent = 4f;
            if (f.TryGetComponent<Renderer>(out var r)) extent = r.bounds.extents.magnitude;
            else
            {
                var child = f.GetComponentInChildren<Renderer>();
                if (child) extent = child.bounds.extents.magnitude;
            }
            return Mathf.Clamp(extent * 0.5f, 3f, 30f);
        }

        /// <summary>When attackers are off screen: an arrow at the screen's edge toward the nearest of them, one pip per
        /// attacker off screen.</summary>
        void PlaceArrow()
        {
            var cam = Camera.main;
            int off = 0;
            Fauna nearest = null;
            float best = float.MaxValue;
            if (cam)
                foreach (var f in _attackers)
                {
                    if (!f || !f.IsAlivePrey) continue;
                    var v = cam.WorldToViewportPoint(f.transform.position);
                    if (OnScreen(v)) continue;
                    off++;
                    float d = (f.transform.position - cam.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = f;
                    }
                }
            bool show = off > 0 && nearest;
            if (_arrow.gameObject.activeSelf != show) _arrow.gameObject.SetActive(show);
            if (!show) return;

            var target = nearest.transform.position;
            var edge = EdgePoint(cam.WorldToViewportPoint(target), ArrowInset);
            var at = cam.ViewportToWorldPoint(new Vector3(edge.x, edge.y, ArrowDepth));
            var to = target - at;
            _arrow.SetPositionAndRotation(at, to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to, cam.transform.up) : cam.transform.rotation);
            _arrow.localScale = Vector3.one * 1.1f;
            int pips = Mathf.Min(off, MaxPips);
            for (int i = 0; i < _pips.Count; i++)
                if (_pips[i].gameObject.activeSelf != i < pips) _pips[i].gameObject.SetActive(i < pips);
        }

        /// <summary>True when a viewport point is in front of the camera and inside the screen.</summary>
        public static bool OnScreen(Vector3 viewport) =>
            viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        /// <summary>
        /// Where on the screen the edge arrow stands for a thing at <paramref name="viewport"/>: on the ellipse
        /// <paramref name="inset"/> from the centre (0.5 is the very edge), in the thing's direction from the centre,
        /// mirrored when it is behind the camera (so the arrow still points the way to turn).
        /// </summary>
        public static Vector2 EdgePoint(Vector3 viewport, float inset)
        {
            var d = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f) d = -d;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            return new Vector2(0.5f, 0.5f) + d.normalized * inset;
        }

        // ── The volumes (flat-shaded: every facet its own vertices) ─────────

        /// <summary>A faceted diamond (an octahedron stretched tall), unit size, its point down at the creature.</summary>
        public static Mesh Diamond()
        {
            var top = new Vector3(0f, 1.2f, 0f);
            var bottom = new Vector3(0f, -1.6f, 0f);
            var ring = new Vector3[6];
            for (int i = 0; i < ring.Length; i++)
            {
                float a = i * Mathf.PI * 2f / ring.Length;
                ring[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            var tris = new List<(Vector3, Vector3, Vector3)>();
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                tris.Add((top, b, a));
                tris.Add((bottom, a, b));
            }
            return Faceted("ArkAttackerDiamond", tris);
        }

        /// <summary>A faceted arrow along +Z: a four-sided head and a shaft behind it.</summary>
        public static Mesh Arrow()
        {
            var tip = new Vector3(0f, 0f, 1.6f);
            var h = new[] { new Vector3(-0.8f, 0f, 0f), new Vector3(0f, 0.55f, 0f), new Vector3(0.8f, 0f, 0f), new Vector3(0f, -0.55f, 0f) };
            var back = new Vector3(0f, 0f, -0.3f);
            var tris = new List<(Vector3, Vector3, Vector3)>();
            for (int i = 0; i < h.Length; i++)
            {
                var a = h[i];
                var b = h[(i + 1) % h.Length];
                tris.Add((tip, b, a));
                tris.Add((back, a, b));
            }
            return Faceted("ArkAttackerArrow", tris);
        }

        static Mesh Faceted(string name, List<(Vector3 a, Vector3 b, Vector3 c)> tris)
        {
            var verts = new List<Vector3>(tris.Count * 3);
            var index = new List<int>(tris.Count * 3);
            foreach (var (a, b, c) in tris)
            {
                index.Add(verts.Count); verts.Add(a);
                index.Add(verts.Count); verts.Add(b);
                index.Add(verts.Count); verts.Add(c);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(index, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (_material) Destroy(_material);
            if (_diamond) Destroy(_diamond);
            if (_arrowMesh) Destroy(_arrowMesh);
        }
    }
}
