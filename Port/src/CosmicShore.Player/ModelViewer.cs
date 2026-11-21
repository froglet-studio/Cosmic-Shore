using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Models;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--view-model FILE</c> (a model, or a .prefab as the game spawns it, particles and all):
    /// the model on its own, drawn by the engine's renderer with the
    /// materials the game gives it (<see cref="ModelMaterialUsage"/>: the prefab that draws its
    /// meshes), on a turntable. Drag to turn it, wheel to zoom, right-drag to pan, F to frame it,
    /// R to reset, Space to stop or start the spin, Tab to show the next prefab's materials
    /// (0 is the model's own). Blend-shape sliders, the animation takes and the controls are on
    /// screen (<c>ModelViewer.Ui.cs</c>). No game scene loads: nothing else is in the picture.
    /// </summary>
    static partial class ModelViewer
    {
        /// <summary>The model to show (project-relative or full); null when not viewing.</summary>
        public static string Path;
        /// <summary>Sets the window title (the player window wires it).</summary>
        public static Action<string> SetTitle;

        static ImportedModel s_model;
        static List<(MeshRenderer mr, SkinnedMeshRenderer smr, ImportedMesh mesh)> s_renderers = new();
        static List<ModelMaterialUse> s_uses = new();
        static List<string> s_sources = new();   // null = the model's own; else a prefab path
        static int s_source;
        static Vector3 s_center, s_pan;
        static float s_radius = 1f, s_yaw = 145f, s_pitch = 22f, s_distance = 3f;
        static bool s_spin = true;
        static int s_particles;
        static bool s_framedLines;
        static Vector3 s_lastMouse;
        static Transform s_camera;

        public static void Build()
        {
            var content = ContentRuntime.Current;
            if (content == null) { Console.WriteLine("[viewer] content not booted yet"); return; }
            var db = content.Db;
            string full = System.IO.Path.IsPathRooted(Path) ? Path : System.IO.Path.GetFullPath(System.IO.Path.Combine(db.ProjectRoot, Path));
            var guid = db.GuidOf(full);
            // A .prefab shows as the game spawns it: its own materials, its particle systems playing.
            bool prefab = full.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            s_model = guid == null || prefab ? null : db.LoadModel(guid);
            if (s_model == null && !prefab) { Console.WriteLine($"[viewer] {Path} is not a model this project imports"); return; }
            if (guid == null) { Console.WriteLine($"[viewer] {Path} is not in this project"); return; }

            foreach (var other in Camera.allCameras) other.enabled = false;
            var template = content.Assets.Load<GameObject>(new ObjRef(prefab ? 100100000 : ModelFileIds.PrefabAsset, guid, 3));
            if (template == null) { Console.WriteLine($"[viewer] {Path}: no {(prefab ? "prefab" : "model prefab")}"); return; }
            var go = CosmicShore.Engine.Object.Instantiate(template);
            go.name = System.IO.Path.GetFileNameWithoutExtension(full);
            go.transform.position = Vector3.zero;
            go.SetActive(true);

            if (prefab)
            {
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) s_renderers.Add((mr, null, null));
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s_renderers.Add((null, smr, null));
                s_sources = new List<string>();
            }
            else
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    if (s_model.Meshes.FirstOrDefault(m => ReferenceEquals(m.Mesh, mf.sharedMesh)) is { } im && mf.TryGetComponent<MeshRenderer>(out var mr))
                        s_renderers.Add((mr, null, im));
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s_model.Meshes.FirstOrDefault(m => ReferenceEquals(m.Mesh, smr.sharedMesh)) is { } im)
                        s_renderers.Add((null, smr, im));

                s_uses = ModelMaterialUsage.Find(db, s_model);
                ModelMaterialUsage.Resolve(s_model, s_uses, out var best);
                s_sources = new List<string> { null };
                s_sources.AddRange(s_uses.Select(u => u.Prefab).Distinct().OrderBy(p => p == best ? 0 : 1).ThenBy(p => p, StringComparer.OrdinalIgnoreCase));
                s_source = best != null ? 1 : 0;
            }
            s_particles = go.GetComponentsInChildren<ParticleSystem>(true).Length;

            (s_center, s_radius) = RendererBounds() ?? (s_model != null ? Bounds(s_model) : (go.transform.position, 2f));
            Frame();

            var camGo = new GameObject("ModelViewerCamera") { tag = "MainCamera" };
            CosmicShore.Engine.Object.DontDestroyOnLoad(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.nearClipPlane = Math.Max(0.001f, s_radius * 0.01f);
            cam.farClipPlane = s_radius * 200f;
            cam.depth = 100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            s_camera = camGo.transform;
            // A key light that rides with the camera, so the side you look at is always lit.
            var lightGo = new GameObject("ModelViewerLight");
            lightGo.transform.SetParent(camGo.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            RenderSettings.sun = light;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.47f, 0.52f, 1f);

            BuildUi(go, guid);
            ApplySource();
            Console.WriteLine($"[viewer] {db.ProjectRelative(full)}: {s_renderers.Count} renderers, radius {s_radius:G3}, "
                              + $"{s_shapes.Count} blend shapes, {s_takes.Count} takes; drag to turn, wheel to zoom, right-drag to pan, "
                              + "F frame, R reset, Space spin, Tab materials, T take, P pause, B zero shapes, H help");
        }

        static void ApplySource()
        {
            var content = ContentRuntime.Current;
            if (s_model == null)
            {
                SetTitle?.Invoke($"Amoebius model viewer - {System.IO.Path.GetFileName(Path)} - its own materials");
                RefreshInfo();
                return;
            }
            string prefab = s_sources.Count > 0 ? s_sources[s_source] : null;
            var resolved = prefab == null
                ? s_model.Meshes.Where(m => m.Mesh != null).ToDictionary(m => m.FileId, m => ModelMaterialUsage.Own(s_model, m))
                : ModelMaterialUsage.Resolve(s_model, s_uses.Where(u => u.Prefab == prefab).ToList(), out _);
            foreach (var (mr, smr, mesh) in s_renderers)
            {
                if (!resolved.TryGetValue(mesh.FileId, out var refs) || refs.Count == 0) continue;
                // No material (a mesh with no slot, a missing file): Unity's default, a light grey Lit.
                var mats = refs.Select(r => (r.IsNull ? null : content.Assets.Load<Material>(r)) ?? DefaultMaterial).ToArray();
                if (mr != null) mr.sharedMaterials = mats; else smr.sharedMaterials = mats;
            }
            string label = prefab == null ? "the model's own materials" : "materials from " + content.Db.ProjectRelative(prefab);
            Console.WriteLine($"[viewer] showing {label}");
            SetTitle?.Invoke($"Amoebius model viewer - {System.IO.Path.GetFileName(Path)} - {label}");
            RefreshInfo();
        }

        static Material s_default;
        static Material DefaultMaterial
        {
            get
            {
                if (s_default != null) return s_default;
                s_default = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Default-Material" };
                s_default.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 1f));
                s_default.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 1f));
                return s_default;
            }
        }

        static void Frame() { s_pan = Vector3.zero; s_distance = s_radius / MathF.Tan(17.5f * MathF.PI / 180f) * 0.9f; }

        public static void Tick(float dt)
        {
            if (s_camera == null) return;
            // A prefab with nothing but effects: frame what they drew on the first frame they drew.
            if (s_renderers.Count == 0 && !s_framedLines && ProceduralLines.Current.Count > 0)
            {
                s_framedLines = true;
                var b = new Bounds(ProceduralLines.Current[0].Points[0], Vector3.zero);
                foreach (var l in ProceduralLines.Current) foreach (var p in l.Points) b.Encapsulate(p);
                (s_center, s_radius) = (b.center, Math.Max(0.5f, b.extents.magnitude));
                Frame();
            }
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            var pos = mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
            var d = pos - s_lastMouse;
            s_lastMouse = pos;
            bool onUi = TickUi(dt, mouse, keys);
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed && !onUi) { s_yaw += d.x * 0.35f; s_pitch = Math.Clamp(s_pitch - d.y * 0.35f, -89f, 89f); s_spin = false; }
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                {
                    var rot = Quaternion.Euler(s_pitch, s_yaw, 0f);
                    float k = s_distance * 0.0015f;
                    s_pan -= rot * new Vector3(d.x * k, d.y * k, 0f);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0) s_distance = Math.Clamp(s_distance * MathF.Pow(0.9f, Math.Sign(wheel)), s_radius * 0.2f, s_radius * 50f);
            }
            if (keys != null)
            {
                if (keys.fKey.wasPressedThisFrame) Frame();
                if (keys.rKey.wasPressedThisFrame) { Frame(); s_yaw = 145f; s_pitch = 22f; s_spin = true; }
                if (keys.spaceKey.wasPressedThisFrame) s_spin = !s_spin;
                if (keys.tabKey.wasPressedThisFrame && s_sources.Count > 1) { s_source = (s_source + 1) % s_sources.Count; ApplySource(); }
            }
            if (s_spin) s_yaw += dt * 20f;
            var q = Quaternion.Euler(s_pitch, s_yaw, 0f);
            var target = s_center + s_pan;
            s_camera.position = target + q * new Vector3(0, 0, -s_distance);
            s_camera.rotation = q;
        }

        /// <summary>What the renderers actually cover (world AABB), as the camera will see them.</summary>
        static (Vector3 center, float radius)? RendererBounds()
        {
            Bounds? all = null;
            foreach (var (mr, smr, _) in s_renderers)
            {
                var b = mr != null ? mr.bounds : smr.bounds;
                if (b.size == Vector3.zero) continue;
                if (all is { } a) { a.Encapsulate(b); all = a; } else all = b;
            }
            return all is { } r ? (r.center, Math.Max(1e-3f, r.extents.magnitude)) : null;
        }

        /// <summary>The model's centre and radius at its bind pose.</summary>
        static (Vector3 center, float radius) Bounds(ImportedModel model)
        {
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var hi = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var im in model.Meshes)
            {
                if (im.Mesh == null) continue;
                var m = im.Node?.ModelMatrix ?? DMat4.Identity;
                foreach (var v in im.Mesh.vertices)
                {
                    var p = m.Point(new DVec3(v.x, v.y, v.z));
                    var w = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                    lo = Vector3.Min(lo, w); hi = Vector3.Max(hi, w);
                }
            }
            if (lo.x > hi.x) return (Vector3.zero, 1f);
            return ((lo + hi) * 0.5f, Math.Max(1e-3f, (hi - lo).magnitude * 0.5f));
        }
    }
}
