using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One gunner seat's flown missile, drawn as the game's own hardware (Garrett 2026-10-21: "increase the fidelity on
    /// the fly by wire missiles. they feel cheap"). Round 7 drew a faceted dart; this is:
    /// <list type="bullet">
    /// <item><b>The body</b>: the Sparrow's skyburst rocket (<c>Sparrow Missile.fbx</c> and its materials, as
    /// <c>SkyBurstProjectile.prefab</c> draws it), sized to the config's <c>missileLength</c>.</item>
    /// <item><b>The motor</b>: a lit, faceted exhaust cone at the nozzle in the domain's colour, which IGNITES over
    /// <c>missileIgnitionSeconds</c> after the launch and flickers, plus a point light that throws the domain's colour on
    /// the mass the missile passes.</item>
    /// <item><b>The trail</b>: a lit, faceted six-sided TUBE through the path it flew, in the firing pilot's trail colours,
    /// swelling from nothing behind the motor (so the missile's chase camera is not inside it) and tapering away over
    /// <c>missileTrailSeconds</c>. A volume, not a ribbon: nothing in the world looks flat
    /// (LOCKED, <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>). It keeps tapering after a burst, so a burst leaves the
    /// path it flew.</item>
    /// <item><b>Fly-by-wire</b>: the body BANKS into the turn its gunner's stick asks for (<c>missileBankDeg</c> at full
    /// yaw, eased), so the thumb's input reads on the missile itself.</item>
    /// </list>
    /// A look only: the flight is <see cref="OrcaMissile"/>'s (held to the lab by parity), and nothing here feeds back. The
    /// lab draws the same parts (<c>CrewOfFourLab.html</c> <c>missileView</c>, round 8). Drawn by <see cref="OrcaCrewView"/>
    /// on the machine that runs the crew.
    /// </summary>
    public sealed class OrcaMissileVisual : System.IDisposable
    {
        const int TrailRings = 96, TrailSides = 6;
        static Mesh _cone;

        readonly GameObject _root;
        readonly Renderer _body;
        readonly Transform _flame;
        readonly Renderer _flameRenderer;
        readonly Material _flameMat;
        readonly Light _light;
        readonly float _length, _lightIntensity, _ignition, _bankDeg, _bankSmooth;
        float _bank;
        bool _flying;

        // the trail: a ring buffer of the nozzle's path (oldest first) and the tube mesh rebuilt from it each frame
        readonly GameObject _trailGo;
        readonly Mesh _trailMesh;
        readonly Material _trailMat;
        readonly Vector3[] _ringPos = new Vector3[TrailRings];
        readonly float[] _ringTime = new float[TrailRings];
        readonly List<Vector3> _verts = new(TrailRings * TrailSides * 4);
        readonly List<int> _tris = new(TrailRings * TrailSides * 6);
        readonly Vector3[] _ringA = new Vector3[TrailSides], _ringB = new Vector3[TrailSides];
        readonly float _trailSeconds, _trailRadius, _trailSpacing;
        int _ringStart, _ringCount;

        public OrcaMissileVisual(OrcaCrewConfigSO cfg, Material litTemplate, string name)
        {
            _root = new GameObject(name);
            _length = Mathf.Max(0.5f, cfg.MissileLength);
            _lightIntensity = cfg.MissileLightIntensity;
            _ignition = Mathf.Max(0.01f, cfg.MissileIgnitionSeconds);
            _bankDeg = cfg.MissileBankDeg;
            _bankSmooth = Mathf.Max(0.01f, cfg.MissileBankSmooth);
            _trailSeconds = Mathf.Max(0.1f, cfg.MissileTrailSeconds);
            _trailRadius = Mathf.Max(0.025f, cfg.MissileTrailWidth * 0.5f);
            _trailSpacing = Mathf.Max(0.5f, cfg.MissileTrailSpacing);

            // the body: the rocket mesh, its long axis (mesh +Y) turned onto +Z as SkyBurstProjectile turns it
            var mesh = cfg.MissileMesh;
            if (mesh)
            {
                var go = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(_root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                _body = go.GetComponent<MeshRenderer>();
                if (cfg.MissileMaterials is { Length: > 0 }) _body.sharedMaterials = cfg.MissileMaterials;
                float s = _length / Mathf.Max(1e-4f, mesh.bounds.size.y);
                var rot = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localRotation = rot;
                go.transform.localScale = Vector3.one * s;
                go.transform.localPosition = -(rot * mesh.bounds.center) * s;
            }

            // the motor: a lit faceted cone pointing aft from the nozzle, and its light
            _cone ??= FlameCone();
            var flame = new GameObject("Flame", typeof(MeshFilter), typeof(MeshRenderer));
            flame.transform.SetParent(_root.transform, false);
            flame.GetComponent<MeshFilter>().sharedMesh = _cone;
            _flame = flame.transform;
            _flame.localPosition = new Vector3(0f, 0f, -_length * 0.5f);
            _flame.localRotation = Quaternion.LookRotation(Vector3.back);
            _flameRenderer = flame.GetComponent<MeshRenderer>();
            _flameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flameMat = EmissiveCopy(litTemplate);
            if (_flameMat) _flameRenderer.sharedMaterial = _flameMat;
            var glow = new GameObject("Glow", typeof(Light));
            glow.transform.SetParent(_flame, false);
            glow.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            _light = glow.GetComponent<Light>();
            _light.type = LightType.Point;
            _light.range = cfg.MissileLightRange;
            _light.shadows = LightShadows.None;
            _root.SetActive(false);

            // the trail: world space, so it stays where the missile flew after the body is put away
            _trailGo = new GameObject(name + " Trail", typeof(MeshFilter), typeof(MeshRenderer));
            _trailMesh = new Mesh { name = name + " Trail" };
            _trailMesh.MarkDynamic();
            _trailGo.GetComponent<MeshFilter>().sharedMesh = _trailMesh;
            var trailRenderer = _trailGo.GetComponent<MeshRenderer>();
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trailMat = EmissiveCopy(litTemplate);
            if (_trailMat) trailRenderer.sharedMaterial = _trailMat;
            _trailGo.SetActive(false);
        }

        /// <summary>The missile is flying this frame: pose it, light its motor, bank it into its turn, extend its trail.</summary>
        public void Fly(OrcaMissile m, Color domain, Color trailHead, Color trailCore, float dt)
        {
            if (!_root) return;
            if (!_flying)
            {
                _flying = true;
                _bank = 0f;
                _ringStart = _ringCount = 0;
                _root.SetActive(true);
                _trailGo.SetActive(true);
                if (_body) _body.enabled = true;
                _flameRenderer.enabled = true;
                _light.enabled = true;
                if (_trailMat)
                {
                    if (_trailMat.HasProperty("_BaseColor")) _trailMat.SetColor("_BaseColor", trailCore);
                    _trailMat.SetColor("_EmissionColor", trailHead * 1.5f);
                }
            }
            _bank = Mathf.Lerp(_bank, -m.StickYaw * _bankDeg, 1f - Mathf.Exp(-dt / _bankSmooth));
            var f = OrcaCrew.U(m.Frame.F);
            var u = OrcaCrew.U(m.Frame.U);
            var pos = OrcaCrew.U(m.Position);
            _root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f, u) * Quaternion.AngleAxis(_bank, Vector3.forward));

            // ignition, then a motor's flicker
            float lit = Mathf.Clamp01(m.Age / _ignition);
            float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 53f) * Mathf.Sin(Time.time * 31f + 1.3f);
            float flare = 1f + 6f * (1f - lit) * lit;   // a bright ignition pulse, settling
            _flame.localScale = new Vector3(0.22f, 0.22f, Mathf.Lerp(0.05f, 1f, lit) * flicker) * _length;
            if (_flameMat)
            {
                if (_flameMat.HasProperty("_BaseColor")) _flameMat.SetColor("_BaseColor", Color.Lerp(domain, Color.white, 0.55f));
                _flameMat.SetColor("_EmissionColor", domain * (2.5f * flare * flicker));
            }
            _light.color = domain;
            _light.intensity = _lightIntensity * lit * flare * flicker;

            var nozzle = pos - f * (_length * 0.5f);
            if (_ringCount == 0 || (nozzle - Ring(_ringCount - 1)).sqrMagnitude >= _trailSpacing * _trailSpacing) PushRing(nozzle);
            BuildTrail(nozzle);
        }

        /// <summary>No missile this frame: a missile that was flying has burst. Its body and motor go out at once; its
        /// trail keeps tapering where it flew, and is put away when it has gone.</summary>
        public void Idle()
        {
            if (!_root) return;
            if (_flying)
            {
                _flying = false;
                if (_body) _body.enabled = false;
                _flameRenderer.enabled = false;
                _light.enabled = false;
                _root.SetActive(false);
            }
            if (!_trailGo.activeSelf) return;
            BuildTrail(null);
            if (_ringCount == 0) _trailGo.SetActive(false);
        }

        public void Dispose()
        {
            if (_flameMat) Object.Destroy(_flameMat);
            if (_trailMat) Object.Destroy(_trailMat);
            if (_trailMesh) Object.Destroy(_trailMesh);
            if (_trailGo) Object.Destroy(_trailGo);
            if (_root) Object.Destroy(_root);
        }

        static Material EmissiveCopy(Material template)
        {
            if (!template) return null;
            var mat = new Material(template);
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        // ------------------------------------------------------------------ the trail tube

        Vector3 Ring(int i) => _ringPos[(_ringStart + i) % TrailRings];
        float RingAge(int i) => Time.time - _ringTime[(_ringStart + i) % TrailRings];

        void PushRing(Vector3 p)
        {
            if (_ringCount == TrailRings) { _ringStart = (_ringStart + 1) % TrailRings; _ringCount--; }
            int k = (_ringStart + _ringCount) % TrailRings;
            _ringPos[k] = p;
            _ringTime[k] = Time.time;
            _ringCount++;
        }

        // The tube through the rings (oldest first) and, while flying, the live nozzle: six flat faces per span, each with
        // its own vertices so the facets shade (the prism-cut read), the radius a plume's (nothing at the nozzle, widest at
        // half the trail's life, nothing again at its end). The ring frame is carried along the path (parallel transport) so the facets do not twist.
        void BuildTrail(Vector3? head)
        {
            while (_ringCount > 0 && RingAge(0) >= _trailSeconds) { _ringStart = (_ringStart + 1) % TrailRings; _ringCount--; }
            _verts.Clear();
            _tris.Clear();
            int n = _ringCount + (head.HasValue ? 1 : 0);
            if (n >= 2)
            {
                var up = Vector3.up;
                for (int i = 0; i < n; i++)
                {
                    var p = PathPoint(i, n, head);
                    var d = (PathPoint(Mathf.Min(i + 1, n - 1), n, head) - PathPoint(Mathf.Max(i - 1, 0), n, head)).normalized;
                    if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
                    up -= d * Vector3.Dot(up, d);
                    if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right);
                    up.Normalize();
                    var side = Vector3.Cross(d, up);
                    float age = i < _ringCount ? RingAge(i) : 0f;
                    float x = Mathf.Clamp01(age / _trailSeconds), r = _trailRadius * 4f * x * (1f - x);   // a plume: from nothing at the nozzle, widest at half its life
                    var ring = (i & 1) == 0 ? _ringA : _ringB;
                    for (int k = 0; k < TrailSides; k++)
                    {
                        float a = k * Mathf.PI * 2f / TrailSides;
                        ring[k] = p + (up * Mathf.Cos(a) + side * Mathf.Sin(a)) * r;
                    }
                    if (i == 0) continue;
                    var prev = (i & 1) == 0 ? _ringB : _ringA;
                    for (int k = 0; k < TrailSides; k++)
                    {
                        int k1 = (k + 1) % TrailSides, v = _verts.Count;
                        _verts.Add(prev[k]); _verts.Add(prev[k1]); _verts.Add(ring[k1]); _verts.Add(ring[k]);
                        _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 2);   // outward: Cross(b - a, c - a) faces away from the path
                        _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 3);
                    }
                }
            }
            _trailMesh.Clear();
            _trailMesh.SetVertices(_verts);
            _trailMesh.SetTriangles(_tris, 0);
            _trailMesh.RecalculateNormals();
            _trailMesh.RecalculateBounds();
        }

        Vector3 PathPoint(int i, int n, Vector3? head) => head.HasValue && i == n - 1 ? head.Value : Ring(i);

        // A faceted cone from its base ring (z 0) to its tip (z 1): eight flat faces, each with its own normals, so the
        // flame reads as a lit volume.
        static Mesh FlameCone()
        {
            const int n = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k < n; k++)
            {
                float a0 = k * Mathf.PI * 2f / n, a1 = (k + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.5f;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.5f;
                Face(verts, tris, p0, Vector3.forward, p1);   // side
                Face(verts, tris, Vector3.zero, p1, p0);      // base cap
            }
            var mesh = new Mesh { name = "OrcaMissileFlame" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // One flat face wound OUTWARD from the cone's centre (Unity's front face is the clockwise one).
        static void Face(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - new Vector3(0f, 0f, 0.25f)) > 0f;
            tris.Add(i); tris.Add(outward ? i + 1 : i + 2); tris.Add(outward ? i + 2 : i + 1);
        }
    }
}
