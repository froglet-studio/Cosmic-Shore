using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the two seats SEE, drawn by the game's own renderer (Garrett, 2026-10-18: "it should actually use the game's
    /// rendering for the kite").
    ///
    /// <para><b>The kite view</b> is a second camera looking straight down the hull's up axis onto its x-z plane,
    /// orthographic, clipped to the slice the kite can touch (reachY either side of the plane), so it renders the real
    /// world - prisms, hulls, crystals - as a map of the board. The kite, its tail, the motes (lime, each element in its
    /// own petal shape), the loop and effect rings are world-space meshes drawn ONLY for that camera.</para>
    ///
    /// <para><b>The pilot view</b> is the game's own camera (<see cref="CameraManager.ViewCamera"/>). It sees the kite
    /// as a faint transparent kite and the motes as faint lime dots, drawn ONLY for that camera, so the crew role is
    /// visible to the pilot without getting in the way.</para>
    ///
    /// <para><b>Which is big.</b> Piloting: the pilot camera has the screen and the kite view is a small square in the
    /// lower left. Navigating: the kite view has the screen and the pilot camera is lifted over it as a small view in
    /// the lower left. The pilot camera's rect and depth are restored the moment the human flies again, the camera
    /// changes, or the seat is unbound.</para>
    /// </summary>
    sealed class KiteNavigatorView
    {
        static readonly Color Lime = new(0.66f, 1f, 0.18f, 1f);
        static readonly Color KiteWhite = new(1f, 1f, 1f, 0.95f);
        static readonly Color Gold = new(1f, 0.82f, 0.32f, 1f);

        readonly KiteNavigator _nav;
        readonly GameObject _root;
        readonly Camera _cam;
        readonly Mesh _kiteMesh, _dotMesh, _ringMesh, _petalFallback;
        readonly Mesh _tailMesh, _loopMesh;
        readonly Material _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat;
        readonly List<Vector3> _verts = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();
        readonly Dictionary<Material, Batch> _batches = new();

        Camera _pilot;
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;

        sealed class Batch
        {
            public Mesh Mesh;
            public Camera Camera;
            public Matrix4x4[] M = new Matrix4x4[64];
            public int N;
        }

        internal KiteNavigatorView(KiteNavigator nav)
        {
            _nav = nav;
            _root = new GameObject("KiteNavigatorView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _root.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.enabled = false;

            _kiteMesh = BuildKite();
            _dotMesh = BuildDisc(1f, 12);
            _ringMesh = BuildRing(1f, 0.08f, 48);
            _petalFallback = BuildPetal();
            _tailMesh = new Mesh { name = "KiteTail" };
            _tailMesh.MarkDynamic();
            _loopMesh = new Mesh { name = "KiteLoops" };
            _loopMesh.MarkDynamic();

            var cfg = nav.Config;
            _kiteMat = Unlit(KiteWhite);
            _kiteChargedMat = Unlit(Gold);
            _kiteFaintMat = Unlit(new Color(1f, 1f, 1f, cfg.PilotKiteAlpha));
            _limeMat = Unlit(Lime);
            _limeFaintMat = Unlit(new Color(Lime.r, Lime.g, Lime.b, cfg.PilotDotAlpha));
            _ringMat = Unlit(new Color(1f, 1f, 1f, 0.85f));
            _goldMat = Unlit(Gold);
        }

        internal Camera Camera => _cam;

        // Sprites/Default: unlit, vertex-coloured, alpha-blended and double-sided, and in every build's always-included
        // shaders. A transparent URP/Unlit needs its surface keywords set by hand and is the shader GobyPistolDuo falls
        // back FROM.
        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            return new Material(shader) { color = c, enableInstancing = true, renderQueue = 3100 };
        }

        internal void Dispose()
        {
            RestorePilot();
            if (_root) Object.Destroy(_root);
            foreach (var o in new Object[] { _kiteMesh, _dotMesh, _ringMesh, _petalFallback, _tailMesh, _loopMesh,
                                             _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat })
                if (o) Object.Destroy(o);
        }

        // ------------------------------------------------------------------ cameras

        /// <summary>Place the kite camera over the board and give each view its rect for the seat the human is in.</summary>
        internal void Frame(bool humanNavigates)
        {
            var cfg = _nav.Config;
            var s = _nav.Settings;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot) { RestorePilot(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            _cam.enabled = true;
            if (_pilot)
            {
                _cam.cullingMask = _pilot.cullingMask;
                _cam.clearFlags = _pilot.clearFlags;
                _cam.backgroundColor = _pilot.backgroundColor;
            }

            // straight down the board's up axis, centred on the disc, seeing only the slice the kite can touch
            Vector3 up = _nav.BoardUp, fwd = _nav.BoardForward;
            _cam.transform.SetPositionAndRotation(_nav.BoardToWorld(0f, s.Anchor * s.BoardRadius) + up * cfg.ReachY,
                                                  Quaternion.LookRotation(-up, fwd));
            _cam.orthographicSize = s.BoardRadius * 1.04f;
            _cam.farClipPlane = 2f * cfg.ReachY + 1f;

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float m = cfg.ViewMargin, h = cfg.PipSize, aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            if (humanNavigates)
            {
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.depth = baseDepth + 1f;
                if (_pilot)
                {
                    _pilot.rect = new Rect(m, m, h * 1.25f, h * 1.25f);   // a small window keeping the screen's shape
                    _pilot.depth = baseDepth + 2f;
                    _pilotLifted = true;
                }
            }
            else
            {
                RestorePilot();
                _cam.rect = new Rect(m / aspect, m, h / aspect, h);       // a square in the lower left
                _cam.depth = baseDepth + 1f;
            }
        }

        void RestorePilot()
        {
            if (_pilotLifted && _pilot)
            {
                _pilot.rect = _pilotRect;
                _pilot.depth = _pilotDepth;
            }
            _pilotLifted = false;
        }

        /// <summary>A screen pixel as a point on the board (u), through the kite camera.</summary>
        internal KiteVec ScreenToBoard(Vector2 screen)
        {
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _cam.nearClipPlane));
            return _nav.WorldToBoard(w);
        }

        /// <summary>True when <paramref name="screen"/> falls inside the kite view's rect.</summary>
        internal bool Contains(Vector2 screen) => _cam.pixelRect.Contains(screen);

        // ------------------------------------------------------------------ drawing

        internal void Draw()
        {
            var cfg = _nav.Config;
            var s = _nav.Settings;
            var basis = Quaternion.LookRotation(_nav.BoardForward, _nav.BoardUp);
            Camera kite = _cam, pilot = _pilot;
            float lift = cfg.ReachY * 0.9f;   // the kite view's top: in front of everything in the slice
            float now = Time.time;

            // the kite: pointed along its flight, gold while charged; faint for the pilot
            var kv = _nav.Kite.Velocity;
            var heading = new Vector2(kv.X, kv.Z).sqrMagnitude > 4f ? Quaternion.LookRotation(new Vector3(kv.X, 0f, kv.Z)) : Quaternion.identity;
            var at = _nav.Kite.Position;
            var kiteRot = basis * heading;
            float kr = cfg.KiteRadius;
            Add(_nav.Charge >= 1f ? _kiteChargedMat : _kiteMat, _kiteMesh, kite,
                Matrix4x4.TRS(_nav.BoardToWorld(at.X, at.Z) + _nav.BoardUp * lift, kiteRot, Vector3.one * kr));
            if (pilot)
                Add(_kiteFaintMat, _kiteMesh, pilot, Matrix4x4.TRS(_nav.BoardToWorld(at.X, at.Z), kiteRot, Vector3.one * kr));

            // charge pips round the kite
            int pips = Mathf.Min(12, Mathf.FloorToInt(_nav.Charge));
            for (int i = 0; i < pips; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var p = _nav.BoardToWorld(at.X + Mathf.Cos(a) * kr * 1.9f, at.Z + Mathf.Sin(a) * kr * 1.9f) + _nav.BoardUp * lift;
                Add(_goldMat, _dotMesh, kite, Matrix4x4.TRS(p, basis, Vector3.one * kr * 0.18f));
            }

            // the motes: lime, each element its petal; lime dots for the pilot
            float ms = cfg.MoteSize;
            foreach (var mote in _nav.Motes)
            {
                if (!KiteNavigatorCore.InDisc(s, mote.X, mote.Z)) continue;
                var mesh = cfg.PetalMesh(mote.Element);
                var pos = _nav.BoardToWorld(mote.X, mote.Z);
                Add(_limeMat, mesh ? mesh : _petalFallback, kite,
                    Matrix4x4.TRS(pos + _nav.BoardUp * (lift - 0.2f), basis * Quaternion.Euler(0f, mote.Spin, 0f), Vector3.one * ms));
                if (pilot) Add(_limeFaintMat, _dotMesh, pilot, Matrix4x4.TRS(pos, PilotFacing(pilot, pos), Vector3.one * cfg.PilotDotSize));
            }

            // the pilot's navigator boost, as a ring of dots round the hull in the kite view
            float boost = Mathf.Clamp01(_nav.BoostEnergy / Mathf.Max(0.01f, cfg.TimeBoostCap));
            int bd = Mathf.CeilToInt(boost * 24f);
            for (int i = 0; i < bd; i++)
            {
                float a = Mathf.PI * 0.5f + i / 24f * Mathf.PI * 2f;
                var p = _nav.BoardToWorld(Mathf.Cos(a) * 9f, Mathf.Sin(a) * 9f) + _nav.BoardUp * lift;
                Add(_limeMat, _dotMesh, kite, Matrix4x4.TRS(p, basis, Vector3.one * 0.7f));
            }

            // effect rings
            foreach (var fx in _nav.Effects)
            {
                float t = now - fx.T0;
                var c = _nav.BoardToWorld(fx.X, fx.Z) + _nav.BoardUp * lift;
                float r = fx.Kind switch
                {
                    KiteNavigator.FxKind.Space => KiteNavigatorCore.BlastRadius(fx.MaxScale, t, KiteNavigator.BlastDelay, KiteNavigator.BlastDuration),
                    KiteNavigator.FxKind.Pop => 2f + t * 18f,
                    KiteNavigator.FxKind.Loop => 0f,
                    _ => 5f + t * 30f,
                };
                if (r < 0.5f) continue;
                Add(fx.Kind is KiteNavigator.FxKind.Charge or KiteNavigator.FxKind.Steal ? _goldMat : _ringMat, _ringMesh, kite,
                    Matrix4x4.TRS(c, basis, Vector3.one * r));
            }

            BuildTail(lift);
            BuildLoops(lift, now);
            Flush();
            DrawDynamic(_tailMesh, _limeMat, kite);
            DrawDynamic(_loopMesh, _ringMat, kite);
        }

        // a dot faces the pilot camera, so it reads as a dot from any angle
        static Quaternion PilotFacing(Camera cam, Vector3 at) => Quaternion.LookRotation(cam.transform.position - at) * Quaternion.Euler(90f, 0f, 0f);

        void Add(Material mat, Mesh mesh, Camera cam, Matrix4x4 m)
        {
            if (!cam || !mesh) return;
            // one batch per material: the kite view and the pilot view never share a material, so a batch has one camera
            if (!_batches.TryGetValue(mat, out var b)) _batches[mat] = b = new Batch();
            if (b.N > 0 && (b.Mesh != mesh || b.Camera != cam)) DrawBatch(mat, b);
            b.Mesh = mesh;
            b.Camera = cam;
            if (b.N == b.M.Length)
            {
                if (b.N >= 1023) DrawBatch(mat, b);
                else System.Array.Resize(ref b.M, Mathf.Min(1023, b.N * 2));
            }
            b.M[b.N++] = m;
        }

        void Flush()
        {
            foreach (var kv in _batches) if (kv.Value.N > 0) DrawBatch(kv.Key, kv.Value);
        }

        static void DrawBatch(Material mat, Batch b)
        {
            var rp = new RenderParams(mat)
            {
                camera = b.Camera,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(b.Camera.transform.position, Vector3.one * 100000f),
            };
            Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.M, b.N);
            b.N = 0;
        }

        void DrawDynamic(Mesh mesh, Material mat, Camera cam)
        {
            if (!cam || mesh.vertexCount == 0) return;
            var rp = new RenderParams(mat)
            {
                camera = cam,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 100000f),
            };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.identity);
        }

        // the tail as a ribbon on the plane, fading to its end
        void BuildTail(float lift)
        {
            _verts.Clear(); _colors.Clear(); _tris.Clear();
            var tail = _nav.Kite.Tail;
            float w = _nav.Config.TailWidth * 0.5f;
            for (int i = 1; i < tail.Count; i++)
                Segment(tail[i - 1], tail[i], w, (float)i / tail.Count, lift);
            Fill(_tailMesh);
        }

        // every loop closed in the last moments, its outline fading
        void BuildLoops(float lift, float now)
        {
            _verts.Clear(); _colors.Clear(); _tris.Clear();
            float w = _nav.Config.TailWidth * 0.4f;
            foreach (var fx in _nav.Effects)
            {
                if (fx.Kind != KiteNavigator.FxKind.Loop || fx.Loop is not { Length: > 2 }) continue;
                float life = Mathf.Clamp01(1f - (now - fx.T0) / 1.4f);
                for (int i = 0; i < fx.Loop.Length; i++)
                    Segment(fx.Loop[i], fx.Loop[(i + 1) % fx.Loop.Length], w, life, lift);
            }
            Fill(_loopMesh);
        }

        void Segment(KiteVec a, KiteVec b, float halfWidth, float alpha, float lift)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) return;
            float nx = -dz / len * halfWidth, nz = dx / len * halfWidth;
            var up = _nav.BoardUp * lift;
            int v = _verts.Count;
            _verts.Add(_nav.BoardToWorld(a.X - nx, a.Z - nz) + up);
            _verts.Add(_nav.BoardToWorld(a.X + nx, a.Z + nz) + up);
            _verts.Add(_nav.BoardToWorld(b.X + nx, b.Z + nz) + up);
            _verts.Add(_nav.BoardToWorld(b.X - nx, b.Z - nz) + up);
            var c = new Color(1f, 1f, 1f, alpha);
            _colors.Add(c); _colors.Add(c); _colors.Add(c); _colors.Add(c);
            _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 2);
            _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 3);
        }

        void Fill(Mesh mesh)
        {
            mesh.Clear();
            if (_verts.Count == 0) return;
            mesh.SetVertices(_verts);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ meshes (local X = board right, Z = board forward)

        static Mesh BuildKite()
        {
            var m = new Mesh { name = "Kite" };
            m.SetVertices(new List<Vector3> { new(0f, 0f, 1.3f), new(0.75f, 0f, 0.25f), new(0f, 0f, -1f), new(-0.75f, 0f, 0.25f) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildDisc(float r, int seg)
        {
            var v = new List<Vector3> { Vector3.zero };
            var t = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); }
            for (int i = 1; i <= seg; i++) { t.Add(0); t.Add(i); t.Add(i + 1); }
            var m = new Mesh { name = "KiteDot" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        static Mesh BuildRing(float r, float width, int seg)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * (r - width), 0f, s * (r - width)));
                v.Add(new Vector3(c * r, 0f, s * r));
            }
            for (int i = 0; i < seg; i++) { int k = i * 2; t.Add(k); t.Add(k + 1); t.Add(k + 3); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
            var m = new Mesh { name = "KiteRing" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        // a pointed petal (a lens), used for an element whose petal shape is not wired in the config
        static Mesh BuildPetal()
        {
            var v = new List<Vector3> { Vector3.zero };
            var t = new List<int>();
            const int seg = 16;
            for (int i = 0; i <= seg; i++)
            {
                float k = i / (float)seg, z = Mathf.Lerp(-1f, 1f, k), x = 0.55f * Mathf.Sin(k * Mathf.PI);
                v.Add(new Vector3(x, 0f, z));
            }
            for (int i = 0; i <= seg; i++)
            {
                float k = i / (float)seg, z = Mathf.Lerp(1f, -1f, k), x = -0.55f * Mathf.Sin(k * Mathf.PI);
                v.Add(new Vector3(x, 0f, z));
            }
            for (int i = 1; i < v.Count - 1; i++) { t.Add(0); t.Add(i); t.Add(i + 1); }
            var m = new Mesh { name = "KitePetal" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }
    }
}
