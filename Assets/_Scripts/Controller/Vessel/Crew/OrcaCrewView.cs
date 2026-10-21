using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Orca's crew looks like and what a gunner sees (<see cref="OrcaCrew"/>; <c>ORCA_CREW.md</c>).
    /// <list type="bullet">
    /// <item><b>The gunner's camera</b> (round 7). Every gun looks over its turret from behind and above it (lab
    /// sniperCamBack / Up, rapidCamBack / Up, bombCamBack / Up) at the gun's line gunCamAim out from its barrel tip, so its
    /// rounds stream away from the eye toward the crosshair. The sniper also keeps the Serpent's PIP scope open in the
    /// upper left (<see cref="SniperScopeOverlay"/>, the eyepiece Serpent pilots open with the left trigger), looking from
    /// its barrel tip down its aim at the lab's sniperFov, with the same cone reticle over this view.
    /// Every view keeps the turret's own held horizon; while a missile flies, from just behind it (lab missileCamBack /
    /// missileCamUp). The hull's chase camera keeps rendering in a small square in the lower left, as the Wrasse's kite
    /// view does, so the gunner always sees where the helm is taking them.</item>
    /// <item><b>The crosshairs</b> (round 6): each weapon's own (<see cref="OrcaCrosshairGraphic"/>: a scope, a pen nib, a
    /// blast ring), on a screen-space canvas under the HUD, placed where the gun's line meets the view.</item>
    /// <item><b>The crew in the world.</b> With the Orca model (<see cref="OrcaHullRig"/>) its own three turrets are aimed
    /// and stood at their lagged mounts. Without it, each turret is a faceted pod on an outrigger strut. Each flown
    /// missile is a faceted dart, in the hull's domain colour on a LIT material (nothing in the world looks flat: LOCKED,
    /// <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>). Drawn with <c>Graphics.RenderMesh</c>, so the crew owns no
    /// GameObjects.</item>
    /// </list>
    /// The material ships through <c>Resources/OrcaCrewLit.mat</c> (URP/Lit, instancing on): a shader only
    /// <c>Shader.Find</c>s is stripped from a player build (vessel skill rule 55).
    /// </summary>
    public sealed class OrcaCrewView : System.IDisposable
    {
        const string LitTemplateResource = "OrcaCrewLit";
        const float PipMargin = 0.02f, PipHeight = 0.3f;

        readonly OrcaCrew _crew;
        readonly GameObject _root;
        readonly Camera _cam;
        readonly Material _mat;
        readonly OrcaCrosshairGraphic _crosshair;
        readonly TMP_Text _crossLabel;
        Canvas _crossCanvas;
        int _crossKey = int.MinValue;
        SniperScopeOverlay _scope;
        Camera _pilot;
        /// <summary>The model's turrets, when the hull draws the Orca model (else the view draws pods).</summary>
        public OrcaHullRig Rig { get; }
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;

        static Mesh _octa, _box;

        public OrcaCrewView(OrcaCrew crew)
        {
            _crew = crew;
            _root = new GameObject("OrcaCrewView");
            _cam = _root.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 12000f;
            _mat = LitMaterial(DomainColor());
            _octa ??= Octahedron();
            _box ??= Box();
            (_crosshair, _crossLabel) = CrosshairCanvas(_root.transform, out _crossCanvas);
            Rig = new OrcaHullRig(crew.Hull);
        }

        // A screen-space canvas under the HUD (the GameCanvas sorts at 1) holding the crosshair and its one word.
        static (OrcaCrosshairGraphic, TMP_Text) CrosshairCanvas(Transform parent, out Canvas canvas)
        {
            var go = new GameObject("OrcaCrosshairs", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvas.enabled = false;
            var g = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer));
            g.transform.SetParent(go.transform, false);
            var graphic = g.AddComponent<OrcaCrosshairGraphic>();
            Stretch(graphic.rectTransform);
            graphic.raycastTarget = false;
            var t = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            t.transform.SetParent(go.transform, false);
            var label = t.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Bold;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 40f);
            return (graphic, label);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void Dispose()
        {
            RestorePilot();
            if (_scope) Object.Destroy(_scope.gameObject);
            if (_root) Object.Destroy(_root);
            if (_mat) Object.Destroy(_mat);
        }

        Color DomainColor() => _crew.Status != null ? ToyFactory.DomainAccentColor(_crew.Status.Domain) : Color.white;

        static Material LitMaterial(Color color)
        {
            var template = Resources.Load<Material>(LitTemplateResource);
            Material m;
            if (template && template.shader && template.shader.isSupported) m = new Material(template);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader)
                {
                    CSDebug.LogError("[OrcaCrewView] Resources/OrcaCrewLit.mat is missing and URP/Lit was not found: the turrets and missiles will not draw.");
                    shader = Shader.Find("Standard");
                }
                m = new Material(shader);
            }
            m.enableInstancing = true;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            else m.color = color;
            return m;
        }

        // ------------------------------------------------------------------ the gunner's camera

        /// <summary>Place the camera for the seat the local person holds (Helm: the chase camera alone).</summary>
        public void Frame(int humanSeat)
        {
            var hull = _crew.Hull;
            if (humanSeat == OrcaCrew.Helm || !UnityLiveness.Alive(hull))
            {
                _cam.enabled = false;
                if (_crossCanvas) _crossCanvas.enabled = false;
                if (_scope) _scope.Hide();
                RestorePilot();
                return;
            }
            var cfg = _crew.Config;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot)
            {
                RestorePilot();
                _pilot = pilot;
                if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; }
                // a bare AddComponent<Camera> is URP's default: no post-processing (no bloom, no tonemapper), the wrong
                // volume mask, SDR, no anti-aliasing or shadows. The turret view is the full screen, so it takes all of
                // the game camera's image (OffscreenCameraSetup; Garrett, 2026-10-21: "don't have the bloom")
                OffscreenCameraSetup.AdoptGameCameraImage(_cam, postProcessing: true, antiAliasing: true, shadows: true);
            }

            var m = _crew.MissileOf(humanSeat);
            Vector3 pos, fwd, up, aimPoint;
            float fov = cfg.TurretFov;
            bool guns = cfg.GunsOn;
            if (m != null)
            {
                fwd = OrcaCrew.U(m.Frame.F); up = OrcaCrew.U(m.Frame.U);
                pos = OrcaCrew.U(m.Position) - fwd * cfg.MissileCamBack + up * cfg.MissileCamUp;
                aimPoint = OrcaCrew.U(m.Position) + fwd * 40f;
            }
            else
            {
                var t = _crew.TurretOf(humanSeat);
                var aim = OrcaCrew.U(t.Aim);
                up = OrcaCrew.U(t.Up);
                var mount = OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(hull.position), _crew.Pod, humanSeat, cfg.HullRadius));
                // the line every shot flies (round 7): from this frame's barrel tip along the aim, so the view, the
                // crosshair, the barrel and the rounds all agree
                Rig.PoseSeat(_crew, humanSeat);
                var tip = Rig.TryMuzzle(humanSeat, out var muzzle) ? muzzle : mount + aim * 3f;
                if (guns)
                {
                    float back = humanSeat == OrcaCrew.FastGunSeat ? cfg.FastGunCamBack : humanSeat == OrcaCrew.BombSeat ? cfg.BombCamBack : cfg.SniperCamBack;
                    float high = humanSeat == OrcaCrew.FastGunSeat ? cfg.FastGunCamUp : humanSeat == OrcaCrew.BombSeat ? cfg.BombCamUp : cfg.SniperCamUp;
                    pos = mount - aim * back + up * high;
                    aimPoint = tip + aim * (humanSeat == OrcaCrew.SniperSeat ? Mathf.Max(cfg.GunCamAim, 1000f) : cfg.GunCamAim);
                    fwd = (tip + aim * cfg.GunCamAim - pos).normalized;
                }
                else
                {
                    pos = mount + up * (PodSize(cfg) * 0.6f);
                    fwd = aim;
                    aimPoint = mount + aim * 1000f;
                }
            }
            TickScope(humanSeat, m == null && guns, cfg);
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
            _cam.fieldOfView = fov;

            float baseDepth = 0f;
            if (_pilot)
            {
                _cam.cullingMask = _pilot.cullingMask;
                _cam.clearFlags = _pilot.clearFlags;
                _cam.backgroundColor = _pilot.backgroundColor;
                baseDepth = _pilotDepth;
                float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
                _pilot.rect = new Rect(PipMargin / aspect, PipMargin, PipHeight / aspect, PipHeight);
                _pilot.depth = baseDepth + 2f;
                _pilotLifted = true;
            }
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
            _cam.depth = baseDepth + 1f;
            _cam.enabled = true;
            DrawCrosshair(humanSeat, m != null, aimPoint, cfg);
        }

        // ------------------------------------------------------------------ the sniper's pip scope (round 7)

        // The Serpent's eyepiece, open for as long as the person holds the sniper seat (Garrett 2026-10-21: "always have the
        // pip scope open like the serpent does when holding the left trigger, not have the whole camera zoomed in"). It
        // looks from the barrel tip down the aim, and its flight reticle is projected through this turret camera.
        void TickScope(int seat, bool open, OrcaCrewConfigSO cfg)
        {
            if (!open || seat != OrcaCrew.SniperSeat)
            {
                if (_scope) _scope.Hide();
                return;
            }
            if (!_scope) _scope = SniperScopeOverlay.Create();
            var t = _crew.TurretOf(seat);
            var aim = OrcaCrew.U(t.Aim);
            var up = OrcaCrew.U(t.Up);
            var hull = _crew.Hull;
            var eye = Rig.TryMuzzle(seat, out var tip) ? tip
                : OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(hull.position), _crew.Pod, seat, cfg.HullRadius)) + aim * (cfg.HullRadius * 1.8f);
            float cooldown01 = cfg.SniperCooldown > 0f ? Mathf.Clamp01(_crew.SniperCooldownLeft / cfg.SniperCooldown) : 0f;
            _scope.Tick(eye, Quaternion.LookRotation(aim, up), _cam, cfg.SniperScopeFov, cfg.SniperConeDeg, cfg.SniperRange,
                cooldown01, _crew.DomainSignalColour);
        }

        // ------------------------------------------------------------------ the crosshairs (round 6)

        void DrawCrosshair(int seat, bool missileFlying, Vector3 aimPoint, OrcaCrewConfigSO cfg)
        {
            if (!_crosshair) return;
            _crossCanvas.enabled = true;
            float w = Screen.width, h = Screen.height, scale = Mathf.Max(0.5f, h / 1080f);
            var half = new Vector2(w, h) * 0.5f;
            var g = _crosshair;
            g.Scale = scale;
            g.LineColor = cfg.CrosshairColor; g.ReadyColor = cfg.CrosshairReadyColor; g.HotColor = cfg.CrosshairHotColor;
            g.WarmColor = cfg.CrosshairWarmColor;
            var sp = _cam.WorldToScreenPoint(aimPoint);
            g.Aim = sp.z > 0f ? (Vector2)sp - half : Vector2.zero;
            float focal = h * 0.5f / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);   // px per unit of tangent
            int key = -3;   // which words the label shows: -3 none, -2 the bomb's
            Vector2 textAt = Vector2.zero;
            Color textColor = cfg.CrosshairColor;

            if (missileFlying || !cfg.GunsOn) g.Kind = OrcaCrosshairGraphic.Style.Missile;
            else if (seat == OrcaCrew.SniperSeat) g.Kind = OrcaCrosshairGraphic.Style.None;   // the Serpent's scope draws both of its reticles
            else if (seat == OrcaCrew.FastGunSeat)
            {
                g.Kind = OrcaCrosshairGraphic.Style.Nib;
                g.Value01 = _crew.SeatVeil01(seat);
                g.Hot = g.Value01 >= 1f;
                g.PenReady = _crew.PenReady;
            }
            else
            {
                g.Kind = OrcaCrosshairGraphic.Style.Bomb;
                float blast = _crew.BombBlastRadius;
                var bomb = _crew.BombTransform;
                g.BombOut = bomb;
                g.Radius = blast / Mathf.Max(1f, Vector3.Distance(_cam.transform.position, aimPoint)) * focal;
                if (bomb)
                {
                    var bp = _cam.WorldToScreenPoint(bomb.position);
                    g.BombVisible = bp.z > 0f;
                    g.BombAt = (Vector2)bp - half;
                    g.BombRadius = blast / Mathf.Max(1f, bp.z) * focal;
                    g.Pulse01 = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
                    if (g.BombVisible)
                    {
                        key = -2;
                        textAt = g.BombAt + new Vector2(0f, Mathf.Max(8f * scale, g.BombRadius) + 14f * scale);
                        textColor = cfg.CrosshairWarmColor;
                    }
                }
            }
            g.Refresh();

            if (!_crossLabel) return;
            if (key != _crossKey)   // the words change only with the key: no per-frame strings
            {
                _crossKey = key;
                _crossLabel.text = key == -2 ? cfg.BombBurstText : string.Empty;
            }
            _crossLabel.enabled = !string.IsNullOrEmpty(_crossLabel.text);
            _crossLabel.color = textColor;
            _crossLabel.fontSize = 22f * scale;
            _crossLabel.rectTransform.anchoredPosition = textAt;
        }

        void RestorePilot()
        {
            if (!_pilotLifted) return;
            _pilotLifted = false;
            if (!_pilot) return;
            _pilot.rect = _pilotRect;
            _pilot.depth = _pilotDepth;
        }

        // ------------------------------------------------------------------ the crew in the world

        static float PodSize(OrcaCrewConfigSO cfg) => cfg.HullRadius * 0.35f;

        public void Draw()
        {
            var hull = _crew.Hull;
            if (!UnityLiveness.Alive(hull) || !_mat) return;
            var cfg = _crew.Config;
            var rp = new RenderParams(_mat)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = new Bounds(hull.position, Vector3.one * 100000f),
            };
            if (Rig.Bound) Rig.Pose(_crew);
            float pod = PodSize(cfg);
            var hullPos = hull.position;
            var podFrame = _crew.Pod;
            var rot = Quaternion.LookRotation(OrcaCrew.U(podFrame.F), OrcaCrew.U(podFrame.U));
            for (int s = 1; s < OrcaCrew.Seats; s++)
            {
                var mount = OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(hullPos), podFrame, s, cfg.HullRadius));
                var t = _crew.TurretOf(s);
                var aim = Quaternion.LookRotation(OrcaCrew.U(t.Aim), OrcaCrew.U(t.Up));
                var m = _crew.MissileOf(s);
                if (m != null)
                    Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(OrcaCrew.U(m.Position),
                        Quaternion.LookRotation(OrcaCrew.U(m.Frame.F), OrcaCrew.U(m.Frame.U)), new Vector3(1.4f, 1.4f, 5f)));
                if (Rig.Bound) continue;   // the model draws its own turrets
                // each gun reads at a glance (round 5): the sniper long and thin, the fast gun middling, the bomb short and fat
                var podScale = s == OrcaCrew.SniperSeat ? new Vector3(pod * 0.8f, pod * 0.8f, pod * 3.2f)
                    : s == OrcaCrew.BombSeat ? new Vector3(pod * 1.4f, pod * 1.4f, pod * 1.3f)
                    : new Vector3(pod, pod, pod * 1.8f);
                Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(mount, aim, podScale));
                // the strut: from a hull-skin point toward the mount, in the lagged frame
                var root = hullPos + (mount - hullPos) * 0.35f;
                var span = mount - root;
                if (span.sqrMagnitude > 0.01f)
                    Graphics.RenderMesh(rp, _box, 0, Matrix4x4.TRS((root + mount) * 0.5f, Quaternion.LookRotation(span, rot * Vector3.up),
                        new Vector3(pod * 0.35f, pod * 0.35f, span.magnitude)));
            }
        }

        // ------------------------------------------------------------------ faceted meshes (flat normals, one per face)

        static Mesh Octahedron()
        {
            var p = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[][] faces =
            {
                new[] { 4, 0, 2 }, new[] { 4, 2, 1 }, new[] { 4, 1, 3 }, new[] { 4, 3, 0 },
                new[] { 5, 2, 0 }, new[] { 5, 1, 2 }, new[] { 5, 3, 1 }, new[] { 5, 0, 3 },
            };
            return Faceted("OrcaCrewOctahedron", p, faces);
        }

        static Mesh Box()
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++) p[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            return Faceted("OrcaCrewBox", p, faces);
        }

        // Each face gets its own vertices, so its normal is its own: lit facets, never a smooth or flat-shaded blob.
        static Mesh Faceted(string name, Vector3[] p, int[][] faces)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            foreach (var f in faces)
            {
                int start = verts.Count;
                foreach (int i in f) verts.Add(p[i]);
                for (int k = 1; k + 1 < f.Length; k++)
                {
                    // wind each face outward whatever order it was listed in: Unity's front face is the one whose
                    // Cross(b - a, c - a) points at the viewer (clockwise, left-handed)
                    var a = verts[start]; var b = verts[start + k]; var c = verts[start + k + 1];
                    bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
                    tris.Add(start); tris.Add(outward ? start + k : start + k + 1); tris.Add(outward ? start + k + 1 : start + k);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
