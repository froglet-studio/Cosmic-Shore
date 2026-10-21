using CosmicShore.Core;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Orca's crew looks like and what a gunner sees (<see cref="OrcaCrew"/>; <c>ORCA_CREW.md</c>).
    /// <list type="bullet">
    /// <item><b>The gunner's camera.</b> In a gunner seat the person looks out of the turret (first person, the lab's
    /// turretFov), along the stabilized aim with the turret's own held horizon; while their missile flies, from just behind
    /// it (lab missileCamBack / missileCamUp). The hull's chase camera keeps rendering in a small square in the lower left,
    /// as the Wrasse's kite view does, so the gunner always sees where the helm is taking them.</item>
    /// <item><b>The crew in the world.</b> Each turret is a faceted pod on an outrigger strut and each flown missile a
    /// faceted dart, in the hull's domain colour on a LIT material (nothing in the world looks flat: LOCKED,
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
        Camera _pilot;
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
        }

        public void Dispose()
        {
            RestorePilot();
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
            Vector3 pos, fwd, up;
            if (m != null)
            {
                fwd = OrcaCrew.U(m.Frame.F); up = OrcaCrew.U(m.Frame.U);
                pos = OrcaCrew.U(m.Position) - fwd * cfg.MissileCamBack + up * cfg.MissileCamUp;
            }
            else
            {
                var t = _crew.TurretOf(humanSeat);
                fwd = OrcaCrew.U(t.Aim); up = OrcaCrew.U(t.Up);
                var mount = OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(hull.position), _crew.Pod, humanSeat, cfg.HullRadius));
                pos = mount + fwd * (PodSize(cfg) * 1.2f);   // just ahead of its own pod
            }
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
            _cam.fieldOfView = cfg.TurretFov;

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
            float pod = PodSize(cfg);
            var hullPos = hull.position;
            var podFrame = _crew.Pod;
            var rot = Quaternion.LookRotation(OrcaCrew.U(podFrame.F), OrcaCrew.U(podFrame.U));
            for (int s = 1; s < OrcaCrew.Seats; s++)
            {
                var mount = OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(hullPos), podFrame, s, cfg.HullRadius));
                var t = _crew.TurretOf(s);
                var aim = Quaternion.LookRotation(OrcaCrew.U(t.Aim), OrcaCrew.U(t.Up));
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
                var m = _crew.MissileOf(s);
                if (m != null)
                    Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(OrcaCrew.U(m.Position),
                        Quaternion.LookRotation(OrcaCrew.U(m.Frame.F), OrcaCrew.U(m.Frame.U)), new Vector3(1.4f, 1.4f, 5f)));
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
