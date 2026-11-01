using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Somewhere debug lines go: the Scene view's Gizmos (<see cref="GizmoLineSink"/>) or the Game view
    /// (<see cref="ThirdEyeLines"/>), so one draw routine (e.g. Switchback's AI plan and sense views) serves both.
    /// </summary>
    public interface IDebugLineSink
    {
        void Line(Vector3 a, Vector3 b, Color color);
        void WireSphere(Vector3 centre, float radius, Color color);
    }

    /// <summary>Draws into <see cref="Gizmos"/>; only valid inside OnDrawGizmos / OnDrawGizmosSelected.</summary>
    public sealed class GizmoLineSink : IDebugLineSink
    {
        public static readonly GizmoLineSink Instance = new();

        public void Line(Vector3 a, Vector3 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(a, b);
        }

        public void WireSphere(Vector3 centre, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(centre, radius);
        }
    }

    /// <summary>
    /// The Third Eye's Game-view line buffer: world-space lines collected during the frame and drawn with GL on top of
    /// the game camera's image when it finishes rendering. Nothing extra renders - no second camera, no texture.
    /// The buffer is reused (no allocation once grown) and the pipeline callback is only hooked while there is
    /// something to draw, so with every overlay off this costs nothing.
    /// </summary>
    public sealed class ThirdEyeLines : IDebugLineSink
    {
        public static readonly ThirdEyeLines Instance = new();

        struct Seg { public Vector3 A, B; public Color C; }

        Seg[] _segs = new Seg[256];
        int _count;
        bool _hooked;
        Material _mat;

        /// <summary>The camera the lines are drawn over (the game's main camera). Null draws nothing.</summary>
        public Camera Target;

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Line(Vector3 a, Vector3 b, Color color)
        {
            if (_count == _segs.Length) Array.Resize(ref _segs, _segs.Length * 2);
            _segs[_count++] = new Seg { A = a, B = b, C = color };
        }

        /// <summary>Three great circles, the way Gizmos.DrawWireSphere reads.</summary>
        public void WireSphere(Vector3 centre, float radius, Color color)
        {
            Circle(centre, Vector3.up, radius, color);
            Circle(centre, Vector3.right, radius, color);
            Circle(centre, Vector3.forward, radius, color);
        }

        const int CircleSegments = 24;

        public void Circle(Vector3 centre, Vector3 normal, float radius, Color color)
        {
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized * radius;
            Vector3 v = Vector3.Cross(normal.normalized, u);
            Vector3 prev = centre + u;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float a = i * (Mathf.PI * 2f / CircleSegments);
                Vector3 next = centre + u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Line(prev, next, color);
                prev = next;
            }
        }

        /// <summary>Hook the draw while there are lines, unhook when there are none. Call once per frame (LateUpdate).</summary>
        public void Sync()
        {
            bool want = _count > 0 && Target;
            if (want == _hooked) return;
            if (want) RenderPipelineManager.endCameraRendering += OnEndCamera;
            else RenderPipelineManager.endCameraRendering -= OnEndCamera;
            _hooked = want;
        }

        public void Shutdown()
        {
            _count = 0;
            if (_hooked) RenderPipelineManager.endCameraRendering -= OnEndCamera;
            _hooked = false;
        }

        void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != Target || _count == 0) return;
            if (!_mat)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (!shader) return;
                _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_Cull", (int)CullMode.Off);
                _mat.SetInt("_ZWrite", 0);
                _mat.SetInt("_ZTest", (int)CompareFunction.Always); // debug lines read through the world
            }

            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            _mat.SetPass(0);
            GL.Begin(GL.LINES);
            for (int i = 0; i < _count; i++)
            {
                GL.Color(_segs[i].C);
                GL.Vertex(_segs[i].A);
                GL.Vertex(_segs[i].B);
            }
            GL.End();
            GL.PopMatrix();
        }
    }

    /// <summary>
    /// Debug views a game system offers the Third Eye's Overlays list (the AI Diagnose panel, F2). A system registers a name and a draw
    /// callback; the Third Eye calls it each frame only while the overlay is switched on, into the Game-view line
    /// buffer. Registering is a list add, so it is safe in every build; only dev / editor builds have a Third Eye to
    /// call it.
    /// </summary>
    public static class ThirdEyeOverlays
    {
        public sealed class Overlay
        {
            public string Name;
            public string Tooltip;
            public Action<IDebugLineSink> Draw;
            public Action<bool> Changed;
            bool _on;

            public bool On
            {
                get => _on;
                set
                {
                    if (_on == value) return;
                    _on = value;
                    Changed?.Invoke(value);
                }
            }
        }

        static readonly List<Overlay> s_list = new();

        public static IReadOnlyList<Overlay> All => s_list;

        public static Overlay Register(string name, string tooltip, Action<IDebugLineSink> draw,
                                       Action<bool> changed = null, bool on = false)
        {
            var o = new Overlay { Name = name, Tooltip = tooltip, Draw = draw, Changed = changed };
            o.On = on; // fires Changed with the registrant's own current state: a no-op by construction
            s_list.Add(o);
            return o;
        }

        public static void Unregister(Overlay overlay)
        {
            if (overlay != null) s_list.Remove(overlay);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_list.Clear();
    }

    /// <summary>
    /// Text a mode adds to the AI Diagnose panel (e.g. Switchback's "Hits": each pilot's prism contacts by source), also
    /// printed by <c>aidiag &lt;name&gt;</c>. Read only while the panel is open; registering is a list add.
    /// </summary>
    public static class ThirdEyeReadouts
    {
        public sealed class Readout
        {
            public string Name;
            public Func<string> Text;
        }

        static readonly List<Readout> s_list = new();

        public static IReadOnlyList<Readout> All => s_list;

        public static Readout Register(string name, Func<string> text)
        {
            var r = new Readout { Name = name, Text = text };
            s_list.Add(r);
            return r;
        }

        public static void Unregister(Readout r)
        {
            if (r != null) s_list.Remove(r);
        }

        public static Readout Find(string name)
        {
            foreach (var r in s_list)
                if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_list.Clear();
    }
}
