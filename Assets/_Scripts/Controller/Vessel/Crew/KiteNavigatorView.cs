using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
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
    /// lower left. Navigating: the kite view has the screen and the pilot camera is lifted over it in the same small
    /// square (Garrett, 2026-10-18: "the kite view when flying the pilot was good; use that"). The pilot camera's rect and
    /// depth are restored the moment the human flies again, the camera changes, or the seat is unbound.</para>
    ///
    /// <para><b>Out of the slice.</b> A prism further than reachY from the plane cannot be touched, so the camera clips it
    /// away, and it is drawn back in UNLIT: a flat box in its domain's tint, dimmed, under everything in the slice
    /// (Garrett, 2026-10-18: "the prisms that go out of plane for the kite should be unlit but not disappear").</para>
    /// </summary>
    sealed class KiteNavigatorView
    {
        static readonly Color Lime = new(0.66f, 1f, 0.18f, 1f);
        static readonly Color KiteWhite = new(1f, 1f, 1f, 0.95f);
        static readonly Color Gold = new(1f, 0.82f, 0.32f, 1f);

        readonly KiteNavigator _nav;
        readonly GameObject _root;
        readonly Camera _cam;
        readonly Mesh _kiteMesh, _dotMesh, _ringMesh, _quadMesh;
        readonly Mesh[] _petals = new Mesh[4];
        readonly Mesh _tailMesh, _loopMesh;
        readonly Material _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat;
        readonly Material _tailMat, _loopMat;
        readonly Mesh _boxMesh;
        readonly Dictionary<CosmicShore.Data.Domains, Material> _ghostMats = new();
        readonly List<Vector3> _verts = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();
        readonly Dictionary<(Material, Mesh, Camera), Batch> _batches = new();

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
            _quadMesh = BuildQuad();
            for (int e = 0; e < 4; e++) _petals[e] = BuildPetal((Element)(e + 1));
            _tailMesh = new Mesh { name = "KiteTail" };
            _tailMesh.MarkDynamic();
            _loopMesh = new Mesh { name = "KiteLoops" };
            _loopMesh.MarkDynamic();

            var cfg = nav.Config;
            _boxMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _tailMat = VertexColoured(Lime);
            _loopMat = VertexColoured(new Color(1f, 1f, 1f, 0.85f));
            _kiteMat = Unlit(KiteWhite);
            _kiteChargedMat = Unlit(Gold);
            _kiteFaintMat = Unlit(new Color(1f, 1f, 1f, cfg.PilotKiteAlpha));
            _limeMat = Unlit(Lime);
            _limeFaintMat = Unlit(new Color(Lime.r, Lime.g, Lime.b, cfg.PilotDotAlpha));
            _ringMat = Unlit(new Color(1f, 1f, 1f, 0.85f));
            _goldMat = Unlit(Gold);
        }

        internal Camera Camera => _cam;

        // Instanced draws: URP/Unlit set up as a transparent, double-sided surface (the shader GobyPistolDuo draws with).
        // NOT Sprites/Default: its instanced path reads the colour from unity_SpriteRendererColorArray, which only a
        // SpriteRenderer fills, so a RenderMeshInstanced draw with it comes out at zero alpha - which is why the motes
        // could not be seen (Garrett, 2026-10-18).
        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return VertexColoured(c);
            var m = new Material(shader) { enableInstancing = true };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + 100;
            return m;
        }

        // Single-mesh draws whose vertices carry their own fade (the tail, the loops): Sprites/Default, which multiplies
        // vertex colour in and, drawn without instancing, takes its renderer colour from the material (white).
        static Material VertexColoured(Color c)
        {
            var shader = Shader.Find("Sprites/Default");
            return new Material(shader) { color = c, renderQueue = (int)RenderQueue.Transparent + 100 };
        }

        Material GhostMaterial(CosmicShore.Data.Domains domain)
        {
            if (_ghostMats.TryGetValue(domain, out var m)) return m;
            var c = PrismLit.DomainTint(domain) * _nav.Config.GhostBrightness;
            c.a = _nav.Config.GhostAlpha;
            var ghost = Unlit(c);
            ghost.renderQueue = (int)RenderQueue.Transparent - 50;   // under the kite, its tail and the motes
            _ghostMats[domain] = ghost;
            return ghost;
        }

        internal void Dispose()
        {
            RestorePilot();
            if (_root) Object.Destroy(_root);
            foreach (var o in _petals) if (o) Object.Destroy(o);
            foreach (var o in new Object[] { _kiteMesh, _dotMesh, _ringMesh, _quadMesh, _tailMesh, _loopMesh, _tailMat, _loopMat,
                                             _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat })
                if (o) Object.Destroy(o);
            foreach (var g in _ghostMats.Values) if (g) Object.Destroy(g);
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
                    _pilot.rect = new Rect(m / aspect, m, h / aspect, h);  // the kite view's own small square
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
                var pos = _nav.BoardToWorld(mote.X, mote.Z);
                Add(_limeMat, _petals[mote.Element], kite,
                    Matrix4x4.TRS(pos + _nav.BoardUp * (lift - 0.2f), basis * Quaternion.Euler(0f, mote.Spin, 0f), Vector3.one * ms));
                CrystalEffect(mote.Element, mote.X, mote.Z, mote.Spin, lift - 0.3f, ms, basis, now, kite);
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

            // the prisms out of the slice: unlit, dimmed, flat on the floor of the view
            var floor = -_nav.BoardUp * (cfg.ReachY * 0.95f);
            foreach (var g in _nav.Ghosts)
            {
                var pos = _nav.BoardToWorld(g.X, g.Z) + floor;
                Add(GhostMaterial(g.Domain), _boxMesh, kite, Matrix4x4.TRS(pos, g.Rotation, g.Scale));
            }

            BuildTail(lift);
            BuildLoops(lift, now);
            Flush();
            DrawDynamic(_tailMesh, _tailMat, kite);
            DrawDynamic(_loopMesh, _loopMat, kite);
        }

        // ------------------------------------------------------------------ the crystal effects (all lime)
        //
        // The motes are all lime and told apart by their petal shapes; each also wears a small, flat imitation of its
        // element's CRYSTAL effect (Garrett, 2026-10-18: "they need effects that match their crystal effects"), from the
        // shipped crystals' own numbers:
        //   Charge - the seam walk (CrystalChargeArcs SeamWalk): a bolt hops seam to seam round a pentagon, a strike every
        //            ~0.2 s (0.07 s strike + 0.1-0.28 s hold), the seams it struck thinning out behind it (0.85 s fade)
        //   Mass   - the columns (CrystalDepthPrisms): six small triangles on spokes, each climbing out to 1.5x and down to
        //            0.6 scale (the column top, _PlateScaleStart), then dropping back flush - the ratchet
        //   Space  - the group spins (SpaceCrystalAnimator): five kites turn 72 deg in 1.0 s and hold 0.1 s, alternating
        //            with an inner three turning 120 deg
        //   Time   - the flip wave (CrystalFlipWave): a ring of diamonds flips 180 deg ring by ring on a 2 s loop
        //            (ring k at 0.2 + 0.2k s, each flip 1.0 s eased), from a new start each loop
        // Each mote's Spin seeds its phase, so a cluster does not move in lockstep.
        void CrystalEffect(int element, float x, float z, float seed, float height, float size, Quaternion basis, float now, Camera cam)
        {
            float phase = seed / 360f;
            var up = _nav.BoardUp * height;
            Vector3 At(float ox, float oz) => _nav.BoardToWorld(x + ox, z + oz) + up;
            switch (element)
            {
                case 0:   // Charge: the seam walk
                {
                    float r = size * 1.45f, step = 0.2f, t = now / step + phase * 7f;
                    int s0 = Mathf.FloorToInt(t);
                    for (int back = 0; back < 3; back++)
                    {
                        int n = s0 - back;
                        int k = (int)((uint)(n * 2654435761u + (uint)(seed * 97f)) % 5u);
                        float a0 = (k * 72f + seed) * Mathf.Deg2Rad, a1 = ((k + 1) * 72f + seed) * Mathf.Deg2Rad;
                        Vector2 p0 = new(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r), p1 = new(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r);
                        // the bolt bends: its middle pushed off the seam by a flicker
                        var mid = (p0 + p1) * 0.5f;
                        var n2 = new Vector2(-(p1.y - p0.y), p1.x - p0.x).normalized;
                        mid += n2 * (Mathf.Sin(now * 31f + n * 1.7f) * 0.18f * size);
                        float width = size * (back == 0 ? 0.16f : back == 1 ? 0.09f : 0.045f);
                        Bar(At(p0.x, p0.y), At(mid.x, mid.y), width, cam);
                        Bar(At(mid.x, mid.y), At(p1.x, p1.y), width, cam);
                    }
                    break;
                }
                case 1:   // Mass: the columns
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * 60f + seed) * Mathf.Deg2Rad;
                        float u = Mathf.Repeat(now * 0.6f + phase + i * 0.37f, 1f);
                        float climb = u < 0.85f ? u / 0.85f : 1f - (u - 0.85f) / 0.15f;   // a slow climb, a quick drop
                        float reach = size * (1.05f + 0.5f * climb), scale = size * 0.32f * (1f - 0.4f * climb);
                        var p = At(Mathf.Cos(a) * reach, Mathf.Sin(a) * reach);
                        Add(_limeMat, _petals[1], cam, Matrix4x4.TRS(p, basis * Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 0f), Vector3.one * scale));
                    }
                    break;
                }
                case 2:   // Space: the group spins
                {
                    float cycle = now / 1.1f + phase * 3f;
                    int c = Mathf.FloorToInt(cycle);
                    float turn = Mathf.Clamp01((cycle - c) * 1.1f / 1.0f);
                    float outer = (c / 2 * 72f) + (c % 2 == 0 ? turn * 72f : 72f);
                    float inner = ((c + 1) / 2 * 120f) + (c % 2 == 1 ? turn * 120f : 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = (i * 72f + outer + seed) * Mathf.Deg2Rad, r = size * 1.5f;
                        Add(_limeMat, _petals[2], cam, Matrix4x4.TRS(At(Mathf.Cos(a) * r, Mathf.Sin(a) * r),
                            basis * Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 0f), Vector3.one * size * 0.3f));
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i * 120f + inner + seed) * Mathf.Deg2Rad, r = size * 0.95f;
                        Add(_limeMat, _petals[2], cam, Matrix4x4.TRS(At(Mathf.Cos(a) * r, Mathf.Sin(a) * r),
                            basis * Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 0f), Vector3.one * size * 0.2f));
                    }
                    break;
                }
                default:  // Time: the flip wave
                {
                    float loop = now / 2f + phase;
                    int l = Mathf.FloorToInt(loop);
                    float tl = (loop - l) * 2f, start = (l * 137.5f + seed) % 360f;
                    for (int i = 0; i < 8; i++)
                    {
                        float deg = i * 45f, a = deg * Mathf.Deg2Rad, r = size * 1.4f;
                        float dist = Mathf.Abs(Mathf.DeltaAngle(deg, start)) / 45f;            // rings 0..4 from the start
                        float u = Mathf.Clamp01((tl - (0.2f + 0.2f * dist)) / 1.0f);
                        float flip = Mathf.Abs(Mathf.Cos(Mathf.PI * Mathf.SmoothStep(0f, 1f, u)));   // 180 deg about its diagonal
                        var p = At(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                        var rot = basis * Quaternion.Euler(0f, 90f - deg, 0f);
                        Add(_limeMat, _petals[3], cam, Matrix4x4.TRS(p, rot, new Vector3(size * 0.28f * Mathf.Max(0.05f, flip), 1f, size * 0.28f)));
                    }
                    break;
                }
            }
        }

        // a thin lime bar from a to b on the board (a struck seam)
        void Bar(Vector3 a, Vector3 b, float width, Camera cam)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var rot = Quaternion.LookRotation(d / len, _nav.BoardUp);
            Add(_limeMat, _quadMesh, cam, Matrix4x4.TRS((a + b) * 0.5f, rot, new Vector3(width, 1f, len)));
        }

        // a dot faces the pilot camera, so it reads as a dot from any angle
        static Quaternion PilotFacing(Camera cam, Vector3 at) => Quaternion.LookRotation(cam.transform.position - at) * Quaternion.Euler(90f, 0f, 0f);

        void Add(Material mat, Mesh mesh, Camera cam, Matrix4x4 m)
        {
            if (!cam || !mesh) return;
            // one batch per material, mesh and camera: a frame's motes cost one draw per petal shape, not one per mote
            var key = (mat, mesh, cam);
            if (!_batches.TryGetValue(key, out var b)) _batches[key] = b = new Batch { Mesh = mesh, Camera = cam };
            if (b.N == b.M.Length)
            {
                if (b.N >= 1023) DrawBatch(mat, b);
                else System.Array.Resize(ref b.M, Mathf.Min(1023, b.N * 2));
            }
            b.M[b.N++] = m;
        }

        void Flush()
        {
            foreach (var kv in _batches) if (kv.Value.N > 0) DrawBatch(kv.Key.Item1, kv.Value);
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

        // a unit quad in the board plane, one long along Z
        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "KiteBar" };
            m.SetVertices(new List<Vector3> { new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(0.5f, 0f, 0.5f), new(-0.5f, 0f, 0.5f) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
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

        // The element's own petal, the outline the HUD's elemental bars draw (ElementPetalGeometry: Mass a triangle, Space a
        // kite, Time a diamond, Charge a five-sided gem), fanned from its centre into a flat mesh on the board, a unit wide.
        static Mesh BuildPetal(Element element)
        {
            var outline = ElementPetalGeometry.Outline(element);
            var v = new List<Vector3> { Vector3.zero };
            var t = new List<int>();
            foreach (var p in outline) v.Add(new Vector3((p.x - 0.5f) * 2f, 0f, (p.y - 0.5f) * 2f));
            for (int i = 1; i <= outline.Length; i++) { t.Add(0); t.Add(i); t.Add(i % outline.Length + 1); }
            var m = new Mesh { name = $"KitePetal_{element}" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }
    }
}
