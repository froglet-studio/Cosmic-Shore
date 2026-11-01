#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Dev tool: compiled only into the editor and development builds (as PilotControl, which it drives).
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI Diagnose panel's IMGUI (F2): camera buttons, pilots with the You / AI toggle, overlays, settings and a
    /// command field (DiagnosticsHUD's console), plus the
    /// watched pilot's info strip, pilot labels and the hint bar. A separate component so the
    /// <see cref="ThirdEye"/> can switch it off - Unity calls OnGUI (and allocates for it) on every enabled
    /// behaviour that has one, so it is enabled only while the camera, the panel or labels are on.
    /// </summary>
    public sealed class ThirdEyeHud : MonoBehaviour
    {
        public ThirdEye Eye;

        Rect _window = new(12f, 60f, 380f, 620f);
        string _command = "";
        string _reply;
        const string CommandControl = "aidiag.command";

        /// <summary>The command field has keyboard focus: keys belong to it, not to flight or the vessel.</summary>
        public bool TypingCommand { get; private set; }
        Vector2 _scroll;
        bool _showSettings, _dirty;
        GUIStyle _label, _small, _pill;

        /// <summary>The mouse is over the panel, so drag / wheel belong to it, not the camera.</summary>
        public bool MouseOverPanel { get; private set; }

        void OnDisable() { MouseOverPanel = false; TypingCommand = false; }

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
            DrawHint();

            if (Eye.PanelOpen)
            {
                _window = GUILayout.Window(0x3E7E, _window, DrawPanel, "AI Diagnose  (F2 closes)");
                Vector2 m = Event.current.mousePosition;
                if (Event.current.type == EventType.Repaint) MouseOverPanel = _window.Contains(m);
            }
            else { MouseOverPanel = false; TypingCommand = false; }
        }

        void DrawPanel(int id)
        {
            var s = Eye.Settings;
            _scroll = GUILayout.BeginScrollView(_scroll);

            // Camera
            GUILayout.Label("<b>Camera</b>", _label);
            if (GUILayout.Button(Eye.Driving ? "Release camera (back to the game's)" : "Take camera")) Eye.SetDriving(!Eye.Driving);
            GUILayout.BeginHorizontal();
            bool chase = Eye.Driving && s.Mode == ThirdEyeCameraMode.Chase;
            bool follow = Eye.Driving && s.Mode == ThirdEyeCameraMode.Follow;
            bool fly = Eye.Driving && s.Mode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Fly;
            bool orbit = Eye.Driving && s.Mode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Orbit;
            if (GUILayout.Toggle(chase, "Chase", "Button") && !chase) { Eye.SetDriving(true); Eye.SetMode(ThirdEyeCameraMode.Chase); }
            if (GUILayout.Toggle(follow, "Follow", "Button") && !follow) { Eye.SetDriving(true); Eye.SetMode(ThirdEyeCameraMode.Follow); }
            if (GUILayout.Toggle(fly, "Free fly", "Button") && !fly) { Eye.SetDriving(true); Eye.SetFreeMode(ThirdEyeFreeMode.Fly); Eye.SetMode(ThirdEyeCameraMode.Free); }
            if (GUILayout.Toggle(orbit, "Orbit", "Button") && !orbit) { Eye.SetDriving(true); Eye.SetFreeMode(ThirdEyeFreeMode.Orbit); Eye.SetMode(ThirdEyeCameraMode.Free); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Prev pilot")) Eye.Step(-1);
            if (GUILayout.Button("Next pilot ▶")) Eye.Step(+1);
            if (GUILayout.Button("Frame pilot")) { Eye.SetDriving(true); Eye.Frame(); }
            GUILayout.EndHorizontal();
            if (fly)
            {
                GUILayout.Label("Free fly = the lava lamp's god mode: WASD, E/Space up, Q/Ctrl down, hold RMB look, Shift fast, " +
                                "Alt slow, wheel speed. Your vessel's input is paused while flying.", _small);
                float sp = Eye.Fly.Speed;
                if (Slider("Fly speed", ref sp, Eye.Fly.moveSpeedRange.x, Eye.Fly.moveSpeedRange.y)) Eye.Fly.Speed = sp;
            }
            else if (orbit) GUILayout.Label("Orbit: hold RMB and drag to circle the pilot; distance in Settings.", _small);

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
            GUILayout.Label("<b>Overlays</b>  (drawn in the Game view)", _label);
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
                c |= Slider("Orbit distance", ref s.OrbitDistance, 2f, 2000f);
                c |= Slider("Look sensitivity", ref s.LookSensitivity, 0.02f, 2f);
                c |= Slider("Trail seconds", ref s.TrailSeconds, 1f, 30f);
                if (c) _dirty = true;
                if (_dirty && Event.current.rawType == EventType.MouseUp) { s.Save(); _dirty = false; }
                if (GUILayout.Button("Save")) s.Save();
            }

            GUILayout.EndScrollView();

            // Command (DiagnosticsHUD's console: fps, timescale, diag, ... and aidiag)
            GUILayout.Space(4f);
            GUILayout.Label("<b>Command</b>  (\"help\" lists them)", _label);
            bool focused = GUI.GetNameOfFocusedControl() == CommandControl;
            var e = Event.current;
            bool enter = focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (enter) e.Use();
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(CommandControl);
            _command = GUILayout.TextField(_command ?? "", GUILayout.MinWidth(220f));
            bool send = GUILayout.Button("Send", GUILayout.Width(60f)) || enter;
            GUILayout.EndHorizontal();
            if (send) SendCommand();
            if (!string.IsNullOrEmpty(_reply)) GUILayout.Label(_reply, _small);
            if (e.type == EventType.Repaint) TypingCommand = GUI.GetNameOfFocusedControl() == CommandControl;

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void SendCommand()
        {
            string raw = (_command ?? "").Trim();
            if (raw.Length == 0) return;
            _reply = raw.Equals("help", System.StringComparison.OrdinalIgnoreCase)
                ? "commands: " + string.Join(", ", Utility.PerformanceBenchmark.DiagnosticsHUD.CommandNames) +
                  "  ·  aidiag alone shows its usage"
                : $"› {raw}\n{Utility.PerformanceBenchmark.DiagnosticsHUD.RunCommand(raw)}";
            _command = "";
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
            if (!Eye.Driving) return;
            var s = Eye.Settings;
            string mode = s.Mode == ThirdEyeCameraMode.Free ? $"Free {s.FreeMode}" : s.Mode.ToString();
            string text = s.Mode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Fly
                ? $"AI DIAGNOSE [{mode}]  F2 panel · WASD move · E/Space up Q/Ctrl down · RMB look · Shift fast Alt slow · wheel speed {Eye.Fly.Speed:0.#}"
                : $"AI DIAGNOSE [{mode}]  F2 panel" + (s.FreeMode == ThirdEyeFreeMode.Orbit && s.Mode == ThirdEyeCameraMode.Free ? " · RMB drag orbit" : "");
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
