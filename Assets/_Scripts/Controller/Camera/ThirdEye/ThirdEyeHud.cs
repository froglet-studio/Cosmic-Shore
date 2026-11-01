#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Dev tool: compiled only into the editor and development builds (as PilotControl, which it drives).
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Third Eye's IMGUI: the F3 panel (camera, pilots with the You / AI toggle, overlays, settings), the
    /// watched pilot's info strip, pilot labels and the hint bar. A separate component so the
    /// <see cref="ThirdEye"/> can switch it off - Unity calls OnGUI (and allocates for it) on every enabled
    /// behaviour that has one, so it is enabled only while the camera, the panel or labels are on.
    /// </summary>
    public sealed class ThirdEyeHud : MonoBehaviour
    {
        public ThirdEye Eye;

        Rect _window = new(12f, 60f, 360f, 560f);
        Vector2 _scroll;
        bool _showSettings, _dirty;
        GUIStyle _label, _small, _pill;

        /// <summary>The mouse is over the panel, so drag / wheel belong to it, not the camera.</summary>
        public bool MouseOverPanel { get; private set; }

        void OnDisable() => MouseOverPanel = false;

        void Styles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, richText = true };
            _pill = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true };
        }

        void OnGUI()
        {
            if (!Eye) return;
            Styles();
            var s = Eye.Settings;

            if (s.ShowLabels) DrawLabels();
            if (Eye.Driving) DrawInfoStrip();
            if (Eye.Driving || Eye.PanelOpen) DrawHint();

            if (Eye.PanelOpen)
            {
                _window = GUILayout.Window(0x3E7E, _window, DrawPanel, "Third Eye  (F3)");
                Vector2 m = Event.current.mousePosition;
                if (Event.current.type == EventType.Repaint) MouseOverPanel = _window.Contains(m);
            }
            else MouseOverPanel = false;
        }

        void DrawPanel(int id)
        {
            var s = Eye.Settings;
            _scroll = GUILayout.BeginScrollView(_scroll);

            // Camera
            GUILayout.Label("<b>Camera</b>", _label);
            bool driving = GUILayout.Toggle(Eye.Driving, " Third Eye drives the game camera (F2)");
            if (driving != Eye.Driving) Eye.SetDriving(driving);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(s.Mode == ThirdEyeCameraMode.Chase, "Chase", "Button") && s.Mode != ThirdEyeCameraMode.Chase) Eye.SetMode(ThirdEyeCameraMode.Chase);
            if (GUILayout.Toggle(s.Mode == ThirdEyeCameraMode.Follow, "Follow", "Button") && s.Mode != ThirdEyeCameraMode.Follow) Eye.SetMode(ThirdEyeCameraMode.Follow);
            bool fly = s.Mode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Fly;
            bool orbit = s.Mode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Orbit;
            if (GUILayout.Toggle(fly, "Free fly", "Button") && !fly) { Eye.SetFreeMode(ThirdEyeFreeMode.Fly); Eye.SetMode(ThirdEyeCameraMode.Free); }
            if (GUILayout.Toggle(orbit, "Orbit", "Button") && !orbit) { Eye.SetFreeMode(ThirdEyeFreeMode.Orbit); Eye.SetMode(ThirdEyeCameraMode.Free); }
            GUILayout.EndHorizontal();

            // Pilots
            GUILayout.Space(6f);
            GUILayout.Label("<b>Pilots</b>  (click to watch; You / AI = who flies it)", _label);
            bool host = PilotControl.CanControl;
            var you = Eye.LocalHuman();
            var watched = Eye.Watched;
            if (Eye.Pilots.Count == 0) GUILayout.Label("No pilots yet.", _small);
            foreach (var p in Eye.Pilots)
            {
                if (!p.Alive) continue;
                GUILayout.BeginHorizontal();
                var old = GUI.color;
                GUI.color = ThirdEyePilots.DomainColor(p);
                GUILayout.Box("", GUILayout.Width(6f), GUILayout.Height(18f));
                GUI.color = old;
                string label = (watched == p ? "▶ " : "") + p.Label;
                if (GUILayout.Button(label, GUILayout.MinWidth(150f))) Eye.Watch(p);
                bool otherHuman = !p.IsLocalHuman && !p.IsAI;
                bool aiFlies = p.AIFlying;
                GUI.enabled = host && !otherHuman && p.Player != null && you != null;
                bool pickYou = GUILayout.Toggle(!aiFlies && !otherHuman, "You", "Button", GUILayout.Width(44f));
                bool pickAI = GUILayout.Toggle(aiFlies, "AI", "Button", GUILayout.Width(36f));
                GUI.enabled = true;
                if (pickYou && aiFlies) Eye.SetFlier(p, youFly: true);
                else if (pickAI && !aiFlies) Eye.SetFlier(p, youFly: false);
                GUILayout.EndHorizontal();
            }
            string note = !host ? "Only the host (or a solo game) can change who flies a hull."
                : you == null ? "No local pilot in this match." : Eye.Note;
            if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, _small);

            // Overlays
            GUILayout.Space(6f);
            GUILayout.Label("<b>Overlays</b>  (Game view; F4 = the first three)", _label);
            bool changed = false;
            changed |= Toggle(ref s.ShowThinking, "AI thinking (aim line, target rings)");
            changed |= Toggle(ref s.ShowTrails, "Trails");
            changed |= Toggle(ref s.ShowLabels, "Labels");
            if (changed) s.Save();
            var all = ThirdEyeOverlays.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                bool on = GUILayout.Toggle(o.On, new GUIContent(" " + o.Name, o.Tooltip));
                if (on != o.On) o.On = on;
            }
            if (all.Count == 0) GUILayout.Label("(Modes add their own here, e.g. Switchback's AI plan / AI sense.)", _small);

            // Settings
            GUILayout.Space(6f);
            _showSettings = GUILayout.Toggle(_showSettings, " <b>Settings</b>", _label);
            if (_showSettings)
            {
                bool c = false;
                c |= Slider("FOV", ref s.Fov, 20f, 120f);
                c |= Slider("Chase distance", ref s.ChaseDistance, 2f, 400f);
                c |= Slider("Chase height", ref s.ChaseHeight, -100f, 200f);
                c |= Slider("Chase look-ahead", ref s.ChaseLookAhead, 0f, 400f);
                c |= Slider("Chase smoothing", ref s.ChaseSmoothing, 0.5f, 30f);
                c |= Slider("Follow distance", ref s.FollowDistance, 5f, 1000f);
                c |= Slider("Follow height", ref s.FollowHeight, -200f, 500f);
                c |= Slider("Follow smoothing", ref s.FollowSmoothing, 0.5f, 30f);
                c |= Slider("Free speed", ref s.FreeSpeed, 1f, 2000f);
                c |= Slider("Shift multiplier", ref s.FastMultiplier, 1f, 20f);
                c |= Slider("Orbit distance", ref s.OrbitDistance, 2f, 2000f);
                c |= Slider("Look sensitivity", ref s.LookSensitivity, 0.02f, 2f);
                c |= Slider("Trail seconds", ref s.TrailSeconds, 1f, 30f);
                if (c) _dirty = true;
                if (_dirty && Event.current.rawType == EventType.MouseUp) { s.Save(); _dirty = false; }
                if (GUILayout.Button("Save")) s.Save();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        static bool Toggle(ref bool value, string label)
        {
            bool v = GUILayout.Toggle(value, " " + label);
            if (v == value) return false;
            value = v;
            return true;
        }

        bool Slider(string name, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _small, GUILayout.Width(120f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(v.ToString("F1"), _small, GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(v, value)) return false;
            value = v;
            return true;
        }

        void DrawInfoStrip()
        {
            var w = Eye.Watched;
            if (w == null) return;
            var t = ThirdEyePilots.Read(w);
            float speed = w.Status ? w.Status.Speed : 0f;
            string head = $"<b>{w.Label}</b>   {speed:F0} u/s   {(w.AIFlying ? "AI flying" : "You flying")}";
            if (t.Has) head += $"   <color=#{ColorUtility.ToHtmlStringRGB(t.Color)}>{t.State}</color>   ({t.Pilot})";
            float h = t.Has ? (string.IsNullOrEmpty(t.Skim) ? 56f : 76f) : 26f;
            var r = new Rect(10f, Screen.height - h - 34f, Screen.width - 20f, h);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, r.height - 8f));
            GUILayout.Label(head, _label);
            if (t.Has && !string.IsNullOrEmpty(t.Detail)) GUILayout.Label(t.Detail, _small);
            if (t.Has && !string.IsNullOrEmpty(t.Skim)) GUILayout.Label(t.Skim, _small);
            GUILayout.EndArea();
        }

        void DrawHint()
        {
            var s = Eye.Settings;
            string mode = !Eye.Driving ? "camera off" : s.Mode == ThirdEyeCameraMode.Free ? $"Free {s.FreeMode}" : s.Mode.ToString();
            string text = Eye.Driving
                ? $"THIRD EYE [{mode}]  F2 off · F3 panel · F4 overlays · C camera · V fly/orbit · Tab/[ ] pilot · F frame · RMB look (+WASD/QE fly, Shift fast) · wheel speed/distance"
                : "THIRD EYE  F2 camera · F3 panel · F4 overlays";
            GUI.Label(new Rect(10f, Screen.height - 28f, Screen.width - 20f, 24f), text, _pill);
        }

        // "You / AI  hull  speed" over each pilot, pinned to the screen edge when off-screen (as the window did).
        void DrawLabels()
        {
            var cam = Eye.DrivenCamera ? Eye.DrivenCamera : Camera.main;
            if (!cam) return;
            var watched = Eye.Watched;
            bool chase = Eye.Driving && Eye.Settings.Mode == ThirdEyeCameraMode.Chase;
            foreach (var p in Eye.Pilots)
            {
                if (!p.Alive) continue;
                Vector3 vp = cam.WorldToViewportPoint(p.Hull.position + p.Hull.up * 4f);
                bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                if (chase && onScreen && p == watched) continue;
                Vector2 v = onScreen ? new Vector2(vp.x, vp.y) : ThirdEyeRig.EdgePoint(vp, 0.04f);
                var pos = new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height);
                float speed = p.Status ? p.Status.Speed : 0f;
                string text = $"{(p.AIFlying ? "AI" : "You")} {p.HullClass} {speed:F0}";
                var old = GUI.color;
                GUI.color = ThirdEyePilots.DomainColor(p);
                GUI.Label(new Rect(pos.x - 60f, pos.y - 20f, 120f, 20f), text, _pill);
                GUI.color = old;
            }
        }
    }
}
#endif
