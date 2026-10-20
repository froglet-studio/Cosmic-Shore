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
        readonly Mesh _kiteMesh, _dotMesh, _ringMesh;
        readonly Mesh[] _petals = new Mesh[4];
        readonly Vector3[][] _outlines = new Vector3[4][];   // each petal's outline in its mesh's own space
        static readonly Color Plasma = new(0.42f, 0.72f, 1f, 1f);   // the Charge crystal's blue plasma
        readonly Material _plasmaMat;
        readonly Mesh _tailMesh, _loopMesh, _cordMesh;
        readonly Material _kiteMat, _kiteChargedMat, _kiteFaintMat, _limeMat, _limeFaintMat, _ringMat, _goldMat;
        readonly Material _tailMat, _loopMat, _cordMat;
        readonly List<Vector3> _verts = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();
        readonly Dictionary<(Material, Mesh, Camera), Batch> _batches = new();

        Camera _pilot;
        AbilityLockupView _lockup;
        float _lockupRetryAt, _line01;
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
            _cordMesh = new Mesh { name = "KiteCord" };
            _cordMesh.MarkDynamic();

            var cfg = nav.Config;
            var unlit = LoadUnlitTemplate();
            _tailMat = VertexColoured(Lime);
            _loopMat = VertexColoured(new Color(1f, 1f, 1f, 0.85f));
            _cordMat = VertexColoured(new Color(1f, 1f, 1f, cfg.CordAlpha));
            _kiteMat = Unlit(unlit, KiteWhite);
            _kiteChargedMat = Unlit(unlit, Gold);
            _kiteFaintMat = Unlit(unlit, new Color(1f, 1f, 1f, cfg.PilotKiteAlpha));
            _limeMat = Unlit(unlit, Lime);
            _limeFaintMat = Unlit(unlit, new Color(Lime.r, Lime.g, Lime.b, cfg.PilotDotAlpha));
            _ringMat = Unlit(unlit, new Color(1f, 1f, 1f, 0.85f));
            _goldMat = Unlit(unlit, Gold);
            _plasmaMat = Unlit(unlit, Plasma);
            _plasmaMat.renderQueue += 1;                     // the bolt over the lime petals
        }

        internal Camera Camera => _cam;

        // Instanced draws: URP/Unlit set up as a transparent, double-sided surface (the shader GobyPistolDuo draws with).
        // NOT Sprites/Default: its instanced path reads the colour from unity_SpriteRendererColorArray, which only a
        // SpriteRenderer fills, so a RenderMeshInstanced draw with it comes out at zero alpha - which is why the motes
        // could not be seen (Garrett, 2026-10-18).
        //
        // The shader comes from a Resources material (Resources/KiteNavigatorUnlit.mat), never from Shader.Find alone:
        // nothing else in the project references URP/Unlit, so a player build stripped it and Shader.Find returned null
        // on the phone - the kite, its tail and the motes drew nothing while the editor drew them (Garrett, Android dev
        // build, 2026-10-19). The material is what makes the build carry the shader and its transparent + instanced
        // variant. Copies of it are tinted per use below.
        const string UnlitTemplateResource = "KiteNavigatorUnlit";

        // The material to copy, or the shader alone when the material is missing (it then has to be found by name).
        static Object LoadUnlitTemplate()
        {
            var template = Resources.Load<Material>(UnlitTemplateResource);
            if (template && template.shader && template.shader.isSupported) return template;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader) return shader;
            CSDebug.LogError($"[KiteNavigatorView] Resources/{UnlitTemplateResource}.mat or its URP/Unlit shader is " +
                             "missing from this build, so the kite and its motes cannot be drawn.");
            return null;
        }

        static Material Unlit(Object template, Color c)
        {
            Material m;
            if (template is Material material) m = new Material(material);
            else if (template is Shader shader) m = new Material(shader);
            else
            {
                // Instancing on so the draw does not throw; it still cannot show colour (see above).
                var fallback = VertexColoured(c);
                fallback.enableInstancing = true;
                return fallback;
            }
            m.enableInstancing = true;
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
            foreach (var o in new Object[] { _kiteMesh, _dotMesh, _ringMesh, _tailMesh, _loopMesh, _cordMesh, _tailMat, _loopMat, _cordMat,
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

            // straight down the board's up axis, centred on the HULL (Garrett, 2026-10-20: "center the grouper in the wrasses
            // camera"), viewScale board radii high (1.04 framed the disc; 1.352 is 30% further out), seeing viewDepth either
            // side of the plane
            Vector3 up = _nav.BoardUp, fwd = _nav.BoardForward;
            _cam.transform.SetPositionAndRotation(_nav.BoardToWorld(0f, KiteNavigator.ViewCentreZ) + up * cfg.ViewDepth,
                                                  Quaternion.LookRotation(-up, fwd));
            _cam.orthographicSize = s.BoardRadius * cfg.ViewScale;
            _cam.farClipPlane = 2f * cfg.ViewDepth + 1f;

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float m = cfg.ViewMargin, h = cfg.PipSize, aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            float cardsLeft01 = FrameHandLine(cfg);
            if (humanNavigates)
            {
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.depth = baseDepth + 1f;
                if (_pilot)
                {
                    // below the hand line, left of the ability cards (Garrett, 2026-10-20: "to the left of the totem fill the
                    // space below that line with the grouper's view and don't let that view go above that line"); the old
                    // small square when there are no cards to fit beside
                    var below = Rect.MinMaxRect(m / aspect, m, cardsLeft01 - m / aspect, _line01 - m);
                    _pilot.rect = cardsLeft01 > 0f && below.width > 0.05f && below.height > 0.05f
                        ? below
                        : new Rect(m / aspect, m, h / aspect, h);
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

        /// <summary>The hand line, as a fraction of the screen's height: just above the ability cards (handLineGap), or
        /// handLineFallback with no cards. While the human navigates, the hand never goes below it.</summary>
        internal float HandLine01 => _line01;

        // The line from the HUD's ability cards (the totems), read live so it follows the HUD's layout and the screen's shape.
        // Returns the cards' left edge as a fraction of the screen's width, or -1 with no cards.
        float FrameHandLine(KiteNavigatorConfigSO cfg)
        {
            _line01 = cfg.HandLineFallback;
            if (!_lockup && Time.unscaledTime >= _lockupRetryAt)
            {
                _lockupRetryAt = Time.unscaledTime + 1f;   // the HUD builds after the seat binds: look again once a second
                foreach (var l in _nav.GetComponentsInChildren<AbilityLockupView>(true))
                    if (l.isActiveAndEnabled) { _lockup = l; break; }
            }
            if (!_lockup || Screen.width <= 0 || Screen.height <= 0 || !_lockup.TryGetCardsScreenRect(out var cards)) return -1f;
            _line01 = Mathf.Clamp01(cards.yMax / Screen.height + cfg.HandLineGap);
            return Mathf.Clamp01(cards.xMin / Screen.width);
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

        /// <summary>
        /// True when <paramref name="screen"/> is inside the small view in the lower left: the kite's own view while the
        /// human pilots, the pilot's view while the human navigates (Frame lifts the pilot camera into that square).
        /// </summary>
        internal bool PipContains(Vector2 screen, bool humanNavigates)
        {
            var small = humanNavigates ? (_pilotLifted ? _pilot : null) : _cam;
            return small && small.enabled && small.pixelRect.Contains(screen);
        }

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
            // a crew body draws the Wrasse as a model everyone sees (WrasseCrewBodies); the flat kite is the fallback
            if (!_nav.BodyDrawsKite)
                Add(_nav.Charge >= 1f ? _kiteChargedMat : _kiteMat, _kiteMesh, kite,
                    Matrix4x4.TRS(_nav.BoardToWorld(at.X, at.Z) + _nav.BoardUp * lift, kiteRot, Vector3.one * kr));
            // the pilot keeps a larger, faint stand-in for the Wrasse, model or not (Garrett, 2026-10-20: "The grouper can keep
            // a larger representative view of the wrasse")
            if (pilot)
                Add(_kiteFaintMat, _kiteMesh, pilot,
                    Matrix4x4.TRS(_nav.BoardToWorld(at.X, at.Z), kiteRot, Vector3.one * kr * cfg.PilotKiteScale));

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
                if (!KiteNavigatorCore.InBounds(s, mote.X, mote.Z)) continue;
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
            BuildCord(lift);
            Flush();
            DrawDynamic(_tailMesh, _tailMat, kite);
            DrawDynamic(_loopMesh, _loopMat, kite);
            DrawDynamic(_cordMesh, _cordMat, kite);
        }

        // the rope, hand to kite, while it is held (Garrett, 2026-10-20: "make the wrasse cord visible to the wrasse player");
        // gone the moment the thumb lifts or the trigger or click lets go
        void BuildCord(float lift)
        {
            _verts.Clear(); _colors.Clear(); _tris.Clear();
            if (_nav.CordHeld)
                Segment(_nav.Aim, _nav.Kite.Position, _nav.Config.TailWidth * _nav.Config.CordWidth * 0.5f, 1f, lift + 0.1f);
            Fill(_cordMesh);
        }

        // ------------------------------------------------------------------ the motes: five-petal flowers (all lime)
        //
        // Each mote is a flower of FIVE of its element's petals, 72 deg apart with their apexes at the centre, as the
        // elemental bars draw an element (ElementalBarsConfigSO.PetalCount / PetalSpacing), so the element's motion reads
        // (Garrett, 2026-10-18: "use all 5 petals for each of the kite elements like we do in the element amount indicator.
        // then you can sell their effect better"):
        //   Space  - the flower SPINS without pausing (round 4e: a 72 deg step left a five-petal flower looking unchanged)
        //   Time   - every petal FLIPS 180 deg about its own axis, petal by petal round the flower, on a 2 s loop
        //   Mass   - three petals held in while the other two push out along their spokes, a new pair each step (round 4f)
        //   Charge - ONE blue plasma bolt laps all five petals' outlines (round 4f)
        // Each mote's Spin seeds its phase, so a cluster does not move in lockstep.
        //
        // Each element's petal as the totem's element flower draws it (Resources/ElementPetals/<element>_petal.png, one
        // sprite the size of the whole flower, turned 72 degrees per petal): where the petal's centre sits out from the
        // flower's centre, and the scale of its outline mesh (whose longer side spans 2), both in flower radii. Traced from
        // each sprite's alpha bounds (>= 0.5), so the kite's motes keep the totem's sizes, shapes and spacing (round 4f).
        static readonly float[] PetalCentreOut = { 0.445f, 0.395f, 0.461f, 0.461f };   // Charge, Mass, Space, Time
        static readonly float[] PetalScale = { 0.367f, 0.453f, 0.383f, 0.383f };
        readonly Matrix4x4[] _flowerPetals = new Matrix4x4[ElementalBarsConfigSO.PetalCount];

        // A mote: five petals of its element's shape, laid out as the totem lays them, each moving like its element.
        // Space spins without pausing; Mass holds three petals in and pushes the other two out, a different pair each step;
        // Time flips every petal, one after another; Charge has one blue plasma bolt lapping all five petals.
        void Flower(int element, float x, float z, float seed, float height, float radius, Quaternion basis, float now, Camera cam)
        {
            const int petals = ElementalBarsConfigSO.PetalCount;
            const float spacing = ElementalBarsConfigSO.PetalSpacing;
            var cfg = _nav.Config;
            float phase = seed / 360f;
            float rest = PetalCentreOut[element] * radius, scale = PetalScale[element] * radius;
            var centre = _nav.BoardToWorld(x, z) + _nav.BoardUp * height;

            float spin = seed;
            if (element == 2) spin += now * cfg.SpaceSpinDegreesPerSecond;

            float massCycle = now / cfg.MassStepSeconds + phase * petals;
            int massStep = Mathf.FloorToInt(massCycle);
            float massBump = Mathf.Sin(Mathf.PI * (massCycle - massStep));

            for (int i = 0; i < petals; i++)
            {
                float deg = spin + i * spacing, out1 = rest, width = 1f;
                if (element == 1)                               // Mass: the step's petals out, the rest held in
                {
                    // star order (2i), so the petals out together are never neighbours
                    int order = ((2 * i - massStep) % petals + petals) % petals;
                    if (order < cfg.MassPetalsOut) out1 = rest + radius * cfg.MassPulse * massBump;
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
                var m = Matrix4x4.TRS(centre + dir * out1, rot, new Vector3(scale * width, 1f, scale));
                _flowerPetals[i] = m;
                Add(_limeMat, _petals[element], cam, m);
            }

            if (element == 0) PlasmaBolt(now / cfg.ChargeBoltLapSeconds + phase, radius, basis, cam);
        }

        // Charge: ONE bolt laps the whole flower, running each petal's outline from its apex in turn (the apexes all sit by
        // the centre, so the hop to the next petal is short), a few fading sparks behind its head.
        void PlasmaBolt(float laps, float radius, Quaternion basis, Camera cam)
        {
            var cfg = _nav.Config;
            var outline = _outlines[0];
            int n = outline.Length, total = n * _flowerPetals.Length;
            float head = Mathf.Repeat(laps, 1f) * total;
            var lift = _nav.BoardUp * 0.05f;
            for (int k = 0; k <= cfg.ChargeBoltTrail; k++)
            {
                float u = Mathf.Repeat(head - k * 0.3f, total);
                int seg = Mathf.FloorToInt(u), petal = seg / n, e = seg % n;
                var local = Vector3.Lerp(outline[e], outline[(e + 1) % n], u - seg);
                var at = _flowerPetals[petal].MultiplyPoint3x4(local) + lift;
                float fade = 1f - k / (cfg.ChargeBoltTrail + 1f);
                Add(_plasmaMat, _dotMesh, cam, Matrix4x4.TRS(at, basis, Vector3.one * (radius * cfg.ChargeSparkSize * fade)));
            }
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
