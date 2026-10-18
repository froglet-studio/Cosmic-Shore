using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// orthographic, seeing viewDepth above and below the plane, so it renders the real world - prisms, hulls, crystals -
    /// as a map of the board. The kite, its tail, the motes (lime, each element in its
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
    /// <para><b>Out of the plane</b> (Garrett, round 4d: "they should just use the normal prism depth shader instead but use
    /// distance from the plane for depth. for the prisms above the plane just use the dither effect in a cone to preserve
    /// the viewing area visibility"). For this camera's render only: the prisms' own depth look (their faces part with
    /// distance) measures distance OUT OF THE PLANE (<see cref="PrismDepthPlane"/>), so the touchable slice reads crisp
    /// and far mass opens up as it does in the pilot's view; and the occlusion corridor is re-shaped into a cone from this
    /// lens down to the plane, sized to the view (<see cref="PrismOcclusionCorridor.BeginViewOverride"/>), so the mass
    /// above the board dithers instead of hiding it. Both are restored before any other camera draws.</para>
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
        readonly Vector3[][] _outlines = new Vector3[4][];   // each petal's outline in its mesh's own space
        static readonly Color Plasma = new(0.42f, 0.72f, 1f, 1f);   // the Charge crystal's blue plasma
        readonly Material _plasmaMat;
        readonly Mesh _tailMesh, _loopMesh;
        readonly Material _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat;
        readonly Material _tailMat, _loopMat;
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
            RenderPipelineManager.beginCameraRendering += BeginRender;
            RenderPipelineManager.endCameraRendering += EndRender;

            _kiteMesh = BuildKite();
            _dotMesh = BuildDisc(1f, 12);
            _ringMesh = BuildRing(1f, 0.08f, 48);
            _quadMesh = BuildQuad();
            for (int e = 0; e < 4; e++)
            {
                _petals[e] = BuildPetal((Element)(e + 1));
                var o = ElementPetalGeometry.Outline((Element)(e + 1));
                _outlines[e] = new Vector3[o.Length];
                for (int k = 0; k < o.Length; k++) _outlines[e][k] = new Vector3((o[k].x - 0.5f) * 2f, 0f, (o[k].y - 0.5f) * 2f);
            }
            _tailMesh = new Mesh { name = "KiteTail" };
            _tailMesh.MarkDynamic();
            _loopMesh = new Mesh { name = "KiteLoops" };
            _loopMesh.MarkDynamic();

            var cfg = nav.Config;
            _tailMat = VertexColoured(Lime);
            _loopMat = VertexColoured(new Color(1f, 1f, 1f, 0.85f));
            _kiteMat = Unlit(KiteWhite);
            _kiteChargedMat = Unlit(Gold);
            _kiteFaintMat = Unlit(new Color(1f, 1f, 1f, cfg.PilotKiteAlpha));
            _limeMat = Unlit(Lime);
            _limeFaintMat = Unlit(new Color(Lime.r, Lime.g, Lime.b, cfg.PilotDotAlpha));
            _ringMat = Unlit(new Color(1f, 1f, 1f, 0.85f));
            _goldMat = Unlit(Gold);
            _plasmaMat = Unlit(Plasma);
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

        internal void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeginRender;
            RenderPipelineManager.endCameraRendering -= EndRender;
            EndRender(default, _cam);
            RestorePilot();
            if (_root) Object.Destroy(_root);
            foreach (var o in _petals) if (o) Object.Destroy(o);
            foreach (var o in new Object[] { _kiteMesh, _dotMesh, _ringMesh, _quadMesh, _tailMesh, _loopMesh, _tailMat, _loopMat,
                                             _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat, _plasmaMat })
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

            // straight down the board's up axis, centred on the disc, seeing viewDepth either side of the plane
            Vector3 up = _nav.BoardUp, fwd = _nav.BoardForward;
            _cam.transform.SetPositionAndRotation(_nav.BoardToWorld(0f, s.Anchor * s.BoardRadius) + up * cfg.ViewDepth,
                                                  Quaternion.LookRotation(-up, fwd));
            _cam.orthographicSize = s.BoardRadius * 1.04f;
            _cam.farClipPlane = 2f * cfg.ViewDepth + 1f;

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

        bool _corridorOverridden, _planePublished;

        // This camera's render only: prism depth measured from the plane, and the corridor as a cone from this lens down
        // to the plane, sized to the view. Restored in EndRender, before the pilot camera (which renders after this one
        // while the human navigates) or the Scene view draws.
        void BeginRender(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam || !_cam.enabled) return;
            var cfg = _nav.Config;
            var s = _nav.Settings;
            Vector3 up = _nav.BoardUp;
            var plane = _nav.BoardToWorld(0f, s.Anchor * s.BoardRadius);
            PrismDepthPlane.Begin(plane, up, cfg.DepthFullSpreadAt);
            _planePublished = true;

            // The corridor's base is graded over its last share and solid at tSolid = 2/3 of its length whenever it is
            // much wider than it is long (PRISM_OCCLUSION_MAX_BASE_SHARE with inner = outer / 2), so a corridor 1.5x the
            // lens height ends solid exactly at the plane: the touchable slice stays solid, the mass above dithers.
            float h = cfg.ViewDepth;
            float halfDiagonal = _cam.orthographicSize * Mathf.Sqrt(1f + _cam.aspect * _cam.aspect);
            float near = cfg.DitherNearScale;
            float outer = 2f * halfDiagonal / Mathf.Lerp(near, 1f, 2f / 3f);   // inner (outer / 2) covers the view at the plane
            _corridorOverridden = PrismOcclusionCorridor.BeginViewOverride(
                _nav.transform, _cam.transform.position - up * (1.5f * h), outer, 0.5f * outer, cfg.DitherCoreAlpha, near * outer);
        }

        void EndRender(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam) return;
            if (_planePublished) PrismDepthPlane.End();
            if (_corridorOverridden) PrismOcclusionCorridor.EndViewOverride();
            _planePublished = _corridorOverridden = false;
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
            float lift = cfg.ViewDepth - 2f;   // just under the lens: in front of every prism the view sees
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
                Flower(mote.Element, mote.X, mote.Z, mote.Spin, lift - 0.2f, ms, basis, now, kite);
                if (pilot) Add(_limeFaintMat, _dotMesh, pilot, Matrix4x4.TRS(pos, PilotFacing(pilot, pos), Vector3.one * cfg.PilotDotSize));
            }

            // the pilot's saved boosts, a dot each round the hull in the kite view
            int bd = Mathf.Min(24, _nav.SavedBoosts);
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
            DrawDynamic(_tailMesh, _tailMat, kite);
            DrawDynamic(_loopMesh, _loopMat, kite);
        }

        // ------------------------------------------------------------------ the motes: five-petal flowers (all lime)
        //
        // Each mote is a flower of FIVE of its element's petals, 72 deg apart with their apexes at the centre, as the
        // elemental bars draw an element (ElementalBarsConfigSO.PetalCount / PetalSpacing), so the element's motion reads
        // (Garrett, 2026-10-18: "use all 5 petals for each of the kite elements like we do in the element amount indicator.
        // then you can sell their effect better"):
        //   Space  - the flower SPINS, the Space crystal's group spin: 72 deg in 1.0 s, then a 0.1 s hold
        //   Time   - every petal FLIPS 180 deg about its own axis, petal by petal round the flower, on the Time crystal's 2 s
        //            loop (each flip 1.0 s, eased)
        //   Mass   - each petal moves IN and OUT along its spoke, the Mass crystal's columns: a slow climb, a quick drop
        //   Charge - a BLUE plasma bolt runs the perimeter of each petal, the Charge crystal's seam walk along its edges
        // Each mote's Spin seeds its phase, so a cluster does not move in lockstep.
        void Flower(int element, float x, float z, float seed, float height, float size, Quaternion basis, float now, Camera cam)
        {
            const int petals = ElementalBarsConfigSO.PetalCount;
            const float spacing = ElementalBarsConfigSO.PetalSpacing;
            float phase = seed / 360f, p = size * 0.5f;       // a petal is 2p long: the flower's radius is 2p
            var centre = _nav.BoardToWorld(x, z) + _nav.BoardUp * height;

            float spin = seed;
            if (element == 2)
            {
                float cycle = now / 1.1f + phase * 3f;
                int c = Mathf.FloorToInt(cycle);
                spin += c * 72f + Mathf.Clamp01((cycle - c) * 1.1f) * 72f;
            }

            for (int i = 0; i < petals; i++)
            {
                float deg = spin + i * spacing, out1 = p;      // the petal's centre sits one half-length out: apex at the centre
                float width = 1f;
                if (element == 1)                               // Mass: in and out
                {
                    float u = Mathf.Repeat(now * 0.6f + phase + i * 0.37f, 1f);
                    float climb = u < 0.85f ? u / 0.85f : 1f - (u - 0.85f) / 0.15f;
                    out1 = p * (1f + 0.6f * climb);
                }
                else if (element == 3)                          // Time: every petal flips, one after another
                {
                    float tl = Mathf.Repeat(now / 2f + phase, 1f) * 2f;
                    float u = Mathf.Clamp01((tl - (0.2f + 0.2f * i)) / 1.0f);
                    width = Mathf.Max(0.05f, Mathf.Abs(Mathf.Cos(Mathf.PI * Mathf.SmoothStep(0f, 1f, u))));
                }
                float a = deg * Mathf.Deg2Rad;
                var dir = _nav.BoardRight * Mathf.Sin(a) + _nav.BoardForward * Mathf.Cos(a);
                var rot = basis * Quaternion.Euler(0f, deg, 0f);   // the petal's local +Z (tip) points along dir
                var m = Matrix4x4.TRS(centre + dir * out1, rot, new Vector3(p * width, 1f, p));
                Add(_limeMat, _petals[element], cam, m);
                if (element == 0) PlasmaRun(m, now * 1.6f + phase + i * 0.2f, size, cam);
            }
        }

        // Charge: a short blue bolt running round one petal's outline (its seam), a lap every 1/1.6 s, its tail thinning
        void PlasmaRun(Matrix4x4 petal, float t, float size, Camera cam)
        {
            var outline = _outlines[0];
            int n = outline.Length;
            float u = Mathf.Repeat(t, 1f) * n;
            for (int k = 0; k < 3; k++)
            {
                float uk = Mathf.Repeat(u - k * 0.35f, n);
                int e = Mathf.FloorToInt(uk);
                float f0 = uk - e, f1 = Mathf.Min(1f, f0 + 0.35f);
                var a0 = Vector3.Lerp(outline[e], outline[(e + 1) % n], f0);
                var a1 = Vector3.Lerp(outline[e], outline[(e + 1) % n], f1);
                Bar(petal.MultiplyPoint3x4(a0), petal.MultiplyPoint3x4(a1), size * (k == 0 ? 0.11f : k == 1 ? 0.07f : 0.04f), _plasmaMat, cam);
            }
        }

        // a thin bar from a to b on the board
        void Bar(Vector3 a, Vector3 b, float width, Material mat, Camera cam)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var rot = Quaternion.LookRotation(d / len, _nav.BoardUp);
            Add(mat, _quadMesh, cam, Matrix4x4.TRS((a + b) * 0.5f, rot, new Vector3(width, 1f, len)));
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
