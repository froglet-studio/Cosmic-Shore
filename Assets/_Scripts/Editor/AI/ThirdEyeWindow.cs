using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// <b>Third Eye</b>: watch the running game from a second camera in its own editor window while you
    /// play in the Game view. Pick any pilot (you, a rival, an AI), then Chase, Follow or Free (fly or
    /// orbit), the Vessel Studio's three cameras (/vessel-studio D16). With <i>AI thinking</i> on, each
    /// AI's aim is drawn over the view: a line from its hull to the point it is steering for, coloured by
    /// its state, and a ring on the crystal it is really after. That is the studio's "Show AI thinking",
    /// on the game's own AI.
    ///
    /// <para><b>How it renders.</b> A hidden camera of its own (<c>HideAndDontSave</c>, so it survives
    /// the menu-to-match scene load and never appears in a scene) draws into a RenderTexture the window
    /// shows. It never borrows the gameplay camera: <c>CameraManager.BeginWindowedPlayerCamera</c> lends
    /// out the rig that IS the screen in a match. Its look is adopted from the game's camera
    /// (<see cref="OffscreenCameraSetup"/>), and its pose is set in
    /// <see cref="RenderPipelineManager.beginCameraRendering"/>, after every vessel has moved this frame,
    /// so nothing lags a frame. Paused, it keeps rendering on demand, so you can fly round a frozen frame.</para>
    ///
    /// <para><b>Known limit.</b> The prism occlusion corridor and vision shading are bound to the
    /// gameplay camera, so prisms between THAT camera and your hull may look dithered from here.</para>
    ///
    /// <para>A READER: it writes no asset and changes no game state. Its settings live in
    /// <c>UserSettings/ThirdEyeSettings.asset</c> (gitignored). See <c>.claude/skills/vessel-ai/SKILL.md</c>.</para>
    /// </summary>
    public sealed class ThirdEyeWindow : EditorWindow
    {
        const string Title = "Third Eye";
        const float InfoHeight = 44f;
        const double ScanInterval = 0.5;
        const double RepaintInterval = 1.0 / 60.0;
        const double PausedRenderInterval = 1.0 / 30.0;
        const double SettingsSaveDelay = 1.0;

        static readonly string[] CameraLabels = { "Chase", "Follow", "Free" };
        static readonly string[] FreeLabels = { "Fly", "Orbit" };

        GameObject _rigObject;
        Camera _cam;
        RenderTexture _rt;
        int _adoptedFrom;

        readonly List<ThirdEyePilot> _pilots = new();
        string[] _pilotLabels = System.Array.Empty<string>();
        int _watchedId;

        ThirdEyeRig.Pose _pose = new(Vector3.zero, Quaternion.identity);
        Vector3 _chaseOffset, _chaseUp = Vector3.up, _followOffset, _followHeading = Vector3.forward;
        float _yaw, _pitch, _orbitYaw, _orbitPitch = 15f;
        bool _snap = true;
        bool _frameOnFirstStep = true;

        readonly HashSet<KeyCode> _held = new();
        bool _shift;
        bool _visible = true;
        bool _showSettings;
        bool _settingsDirty;
        double _nextScan, _nextRepaint, _nextPausedRender, _saveAt, _lastStep;
        Rect _viewRect;

        [MenuItem("FrogletTools/AI/Third Eye", false, 1)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Watch Play mode from a second camera while you play: any pilot, Chase / Follow / Free, " +
                          "with each AI's aim drawn over the view.",
            DocPath = ".claude/skills/vessel-ai/SKILL.md")]
        public static void Open()
        {
            var window = GetWindow<ThirdEyeWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        static ThirdEyeSettings Settings => ThirdEyeSettings.instance;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        void OnEnable()
        {
            titleContent = new GUIContent(Title);
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            TearDown();
            if (_settingsDirty) Settings.SaveNow();
            _settingsDirty = false;
        }

        void OnBecameVisible() => _visible = true;
        void OnBecameInvisible() => _visible = false;
        void OnLostFocus() => _held.Clear();

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) TearDown();
        }

        void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_settingsDirty && now >= _saveAt)
            {
                Settings.SaveNow();
                _settingsDirty = false;
            }

            if (!EditorApplication.isPlaying)
            {
                if (_rigObject) TearDown();
                return;
            }

            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                ThirdEyePilots.Scan(_pilots);
                RebuildPilotLabels();
                EnsureRig();
            }

            if (_cam) _cam.enabled = _visible && _rt;

            if (EditorApplication.isPaused && _visible && _cam && _rt && now >= _nextPausedRender)
            {
                _nextPausedRender = now + PausedRenderInterval;
                RenderNow();
            }

            if (now >= _nextRepaint)
            {
                _nextRepaint = now + RepaintInterval;
                Repaint();
            }
        }

        void EnsureRig()
        {
            if (!_rigObject)
            {
                _rigObject = new GameObject("Third Eye Camera") { hideFlags = HideFlags.HideAndDontSave };
                _cam = _rigObject.AddComponent<Camera>();
                _cam.depth = -100f;
                _cam.enabled = false;
                _adoptedFrom = 0;
                _snap = true;
                _frameOnFirstStep = true;
            }

            // Re-adopt the look whenever the game's camera changes (the menu and each mode have their own).
            var main = Camera.main;
            int mainId = main ? main.GetInstanceID() : 0;
            if (mainId == _adoptedFrom) return;
            _adoptedFrom = mainId;
            OffscreenCameraSetup.AdoptGameCameraFraming(_cam, excludeUiLayer: true);
            OffscreenCameraSetup.AdoptGameCameraImage(_cam, postProcessing: true, antiAliasing: true, shadows: true);
        }

        void EnsureTarget(Rect view)
        {
            if (!_cam) return;
            float scale = EditorGUIUtility.pixelsPerPoint * Settings.ResolutionScale;
            int w = Mathf.Max(16, Mathf.RoundToInt(view.width * scale));
            int h = Mathf.Max(16, Mathf.RoundToInt(view.height * scale));
            if (_rt && _rt.width == w && _rt.height == h) return;

            ReleaseTarget();
            _rt = new RenderTexture(w, h, 24, SystemInfo.GetGraphicsFormat(DefaultFormat.LDR))
            {
                name = "Third Eye",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _rt.Create();
            _cam.targetTexture = _rt;
        }

        void ReleaseTarget()
        {
            if (_cam) _cam.targetTexture = null;
            if (!_rt) return;
            _rt.Release();
            DestroyImmediate(_rt);
            _rt = null;
        }

        void TearDown()
        {
            ReleaseTarget();
            if (_rigObject) DestroyImmediate(_rigObject);
            _rigObject = null;
            _cam = null;
            _pilots.Clear();
            _pilotLabels = System.Array.Empty<string>();
            _held.Clear();
            _snap = true;
        }

        // Paused, the player loop renders nothing; draw this one camera on request so the Free camera
        // can still move round the frozen frame.
        void RenderNow()
        {
            var request = new RenderPipeline.StandardRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, request))
                RenderPipeline.SubmitRenderRequest(_cam, request);
        }

        // ── The rig ──────────────────────────────────────────────────────────────

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_cam || camera != _cam) return;
            StepRig();
        }

        void StepRig()
        {
            var s = Settings;
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastStep), 0f, 0.1f);
            _lastStep = now;

            _cam.fieldOfView = s.FieldOfView;
            _cam.nearClipPlane = s.NearClip;
            _cam.farClipPlane = s.FarClip;

            var watched = Watched();
            bool snap = _snap;
            _snap = false;

            // A Free camera restored from the settings has no pose yet: start it behind the pilot, not at the origin.
            if (_frameOnFirstStep && watched != null)
            {
                _frameOnFirstStep = false;
                FrameWatched();
            }

            switch (s.CameraMode)
            {
                case ThirdEyeCameraMode.Chase when watched != null:
                    _pose = ThirdEyeRig.Chase(watched.Hull.position, watched.Hull.rotation, ref _chaseOffset, ref _chaseUp,
                        s.ChaseDistance, s.ChaseHeight, s.ChaseLookAhead, s.ChaseSmoothing, dt, snap);
                    break;
                case ThirdEyeCameraMode.Follow when watched != null:
                    _pose = ThirdEyeRig.Follow(watched.Hull.position, watched.Hull.rotation, ref _followOffset, ref _followHeading,
                        s.FollowDistance, s.FollowHeight, s.FollowSmoothing, dt, snap);
                    break;
                case ThirdEyeCameraMode.Free when s.FreeMode == ThirdEyeFreeMode.Orbit && watched != null:
                    _pose = ThirdEyeRig.Orbit(watched.Hull.position, _orbitYaw, _orbitPitch, s.OrbitDistance);
                    break;
                case ThirdEyeCameraMode.Free:
                    float speed = s.FreeSpeed * (_shift ? s.FreeFastMultiplier : 1f);
                    _pose = ThirdEyeRig.FreeFly(_pose.Position, _yaw, _pitch, FlyInput(), speed, dt);
                    break;
                // A Chase / Follow with nobody to watch holds where it is.
            }

            _cam.transform.SetPositionAndRotation(_pose.Position, _pose.Rotation);
        }

        Vector3 FlyInput()
        {
            Vector3 m = Vector3.zero;
            if (Held(KeyCode.I, KeyCode.W)) m.z += 1f;
            if (Held(KeyCode.K, KeyCode.S)) m.z -= 1f;
            if (Held(KeyCode.L, KeyCode.D)) m.x += 1f;
            if (Held(KeyCode.J, KeyCode.A)) m.x -= 1f;
            if (Held(KeyCode.O, KeyCode.E)) m.y += 1f;
            if (Held(KeyCode.U, KeyCode.Q)) m.y -= 1f;
            return m;
        }

        bool Held(KeyCode a, KeyCode b) => _held.Contains(a) || _held.Contains(b);

        ThirdEyePilot Watched()
        {
            foreach (var p in _pilots)
                if (p.Id == _watchedId && p.Alive) return p;

            // The watched pilot is gone (or none was picked): you, else the first pilot.
            foreach (var p in _pilots)
            {
                if (!p.Alive) continue;
                _watchedId = p.Id;
                _snap = true;
                return p;
            }
            return null;
        }

        void Watch(int index)
        {
            if (index < 0 || index >= _pilots.Count) return;
            _watchedId = _pilots[index].Id;
            _snap = true;
        }

        void CyclePilot(int direction)
        {
            if (_pilots.Count == 0) return;
            int i = Mathf.Max(0, WatchedIndex());
            Watch((i + direction + _pilots.Count) % _pilots.Count);
        }

        int WatchedIndex()
        {
            for (int i = 0; i < _pilots.Count; i++)
                if (_pilots[i].Id == _watchedId) return i;
            return -1;
        }

        void RebuildPilotLabels()
        {
            if (_pilotLabels.Length != _pilots.Count) _pilotLabels = new string[_pilots.Count];
            for (int i = 0; i < _pilots.Count; i++) _pilotLabels[i] = $"{i + 1}. {_pilots[i].Label}";
        }

        void SetCamera(ThirdEyeCameraMode mode)
        {
            var s = Settings;
            if (mode == ThirdEyeCameraMode.Free) EnterFree(s.FreeMode);
            s.CameraMode = mode;
            _snap = true;
            MarkSettingsDirty();
        }

        void SetFreeMode(ThirdEyeFreeMode mode)
        {
            EnterFree(mode);
            Settings.FreeMode = mode;
            MarkSettingsDirty();
        }

        // Start the Free camera where the current view is, so switching never jumps.
        void EnterFree(ThirdEyeFreeMode mode)
        {
            ThirdEyeRig.YawPitch(_pose.Rotation, out _yaw, out _pitch);
            var watched = Watched();
            if (mode != ThirdEyeFreeMode.Orbit || watched == null) return;
            Vector3 toHull = watched.Hull.position - _pose.Position;
            if (toHull.sqrMagnitude < 1e-4f) return;
            ThirdEyeRig.YawPitch(Quaternion.LookRotation(toHull), out _orbitYaw, out _orbitPitch);
            Settings.OrbitDistance = toHull.magnitude;
        }

        // F: put the Free camera behind the watched hull.
        void FrameWatched()
        {
            var watched = Watched();
            if (watched == null) return;
            var s = Settings;
            Vector3 offset = Vector3.zero, heading = Vector3.zero;
            _pose = ThirdEyeRig.Follow(watched.Hull.position, watched.Hull.rotation, ref offset, ref heading,
                s.FollowDistance, s.FollowHeight, 0f, 0f, true);
            ThirdEyeRig.YawPitch(_pose.Rotation, out _yaw, out _pitch);
            ThirdEyeRig.YawPitch(Quaternion.LookRotation(watched.Hull.position - _pose.Position), out _orbitYaw, out _orbitPitch);
        }

        void MarkSettingsDirty()
        {
            _settingsDirty = true;
            _saveAt = EditorApplication.timeSinceStartup + SettingsSaveDelay;
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            HandleKeys(Event.current);

            FrogletEditorPalette.Banner(Title,
                "Watch Play mode from any pilot while you play: Chase, Follow or Free, with each AI's aim drawn over the view.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Vessels));
            DrawToolbar();
            if (_showSettings) DrawSettings();

            Rect view = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint) _viewRect = view;
            DrawView(_viewRect.width > 1f ? _viewRect : view);
            DrawInfoStrip();
        }

        void DrawToolbar()
        {
            var s = Settings;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool playing = EditorApplication.isPlaying;
                Rect pill = GUILayoutUtility.GetRect(96f, 18f, GUILayout.Width(96f));
                FrogletEditorPalette.StatusPill(pill,
                    !playing ? "NOT PLAYING" : _pilots.Count == 0 ? "NO PILOTS" : $"{_pilots.Count} PILOTS",
                    !playing ? FrogletEditorPalette.Muted : _pilots.Count == 0 ? FrogletEditorPalette.Warn : FrogletEditorPalette.Ok);

                int mode = EditorGUILayout.Popup((int)s.CameraMode, CameraLabels, EditorStyles.toolbarPopup, GUILayout.Width(72f));
                if (mode != (int)s.CameraMode) SetCamera((ThirdEyeCameraMode)mode);

                if (s.CameraMode == ThirdEyeCameraMode.Free)
                {
                    int free = EditorGUILayout.Popup((int)s.FreeMode, FreeLabels, EditorStyles.toolbarPopup, GUILayout.Width(60f));
                    if (free != (int)s.FreeMode) SetFreeMode((ThirdEyeFreeMode)free);
                }

                using (new EditorGUI.DisabledScope(_pilots.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("<", "Previous pilot ( [ )"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
                        CyclePilot(-1);
                    int current = Mathf.Max(0, WatchedIndex());
                    int picked = EditorGUILayout.Popup(current, _pilotLabels, EditorStyles.toolbarPopup, GUILayout.MinWidth(140f));
                    if (picked != current) Watch(picked);
                    if (GUILayout.Button(new GUIContent(">", "Next pilot ( ] or Tab )"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
                        CyclePilot(+1);
                }

                GUILayout.FlexibleSpace();

                bool thinking = GUILayout.Toggle(s.ShowThinking, new GUIContent("AI thinking",
                    "Draw each AI's aim and the crystal it is after."), EditorStyles.toolbarButton);
                if (thinking != s.ShowThinking) { s.ShowThinking = thinking; MarkSettingsDirty(); }

                bool labels = GUILayout.Toggle(s.ShowLabels, new GUIContent("Labels",
                    "Name every pilot and pin off-screen pilots to the edge."), EditorStyles.toolbarButton);
                if (labels != s.ShowLabels) { s.ShowLabels = labels; MarkSettingsDirty(); }

                _showSettings = GUILayout.Toggle(_showSettings, "Settings", EditorStyles.toolbarButton);
            }
        }

        void DrawSettings()
        {
            var s = Settings;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Camera", FrogletEditorPalette.SectionLabel);
                s.FieldOfView = EditorGUILayout.Slider(new GUIContent("Field of view", "Vertical, degrees."), s.FieldOfView, 30f, 110f);
                s.ResolutionScale = EditorGUILayout.Slider(new GUIContent("Resolution",
                    "Fraction of the window's pixels. Lower it if the second render costs too much."), s.ResolutionScale, 0.25f, 1f);
                s.FarClip = EditorGUILayout.Slider("Far clip", s.FarClip, 1000f, 100000f);

                EditorGUILayout.LabelField("Chase", FrogletEditorPalette.SectionLabel);
                s.ChaseDistance = EditorGUILayout.Slider("Distance", s.ChaseDistance, 5f, 200f);
                s.ChaseHeight = EditorGUILayout.Slider("Height", s.ChaseHeight, 0f, 80f);
                s.ChaseLookAhead = EditorGUILayout.Slider("Look ahead", s.ChaseLookAhead, 0f, 200f);
                s.ChaseSmoothing = EditorGUILayout.Slider(new GUIContent("Smoothing",
                    "How fast the offset catches up with a turn, per second. 0 = rigid."), s.ChaseSmoothing, 0f, 30f);

                EditorGUILayout.LabelField("Follow", FrogletEditorPalette.SectionLabel);
                s.FollowDistance = EditorGUILayout.Slider("Distance", s.FollowDistance, 10f, 400f);
                s.FollowHeight = EditorGUILayout.Slider("Height", s.FollowHeight, 0f, 200f);
                s.FollowSmoothing = EditorGUILayout.Slider("Smoothing", s.FollowSmoothing, 0f, 30f);

                EditorGUILayout.LabelField("Free", FrogletEditorPalette.SectionLabel);
                s.FreeSpeed = EditorGUILayout.Slider("Fly speed", s.FreeSpeed, 5f, 2000f);
                s.FreeFastMultiplier = EditorGUILayout.Slider("Shift multiplier", s.FreeFastMultiplier, 1f, 10f);
                s.OrbitDistance = EditorGUILayout.Slider("Orbit distance", s.OrbitDistance, 5f, 1000f);
                s.LookSensitivity = EditorGUILayout.Slider("Look sensitivity", s.LookSensitivity, 0.05f, 1f);
            }
            if (EditorGUI.EndChangeCheck()) MarkSettingsDirty();
        }

        void DrawView(Rect rect)
        {
            if (rect.width < 2f || rect.height < 2f) return;
            var e = Event.current;
            bool repaint = e.type == EventType.Repaint;

            if (!EditorApplication.isPlaying || !_cam)
            {
                if (!repaint) return;
                FrogletEditorPalette.DrawCard(rect, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.4f));
                GUI.Label(rect, EditorApplication.isPlaying
                        ? "Waiting for a vessel..."
                        : "Enter Play mode, then pick a pilot. This window shows the game from its own camera while you play.",
                    CenteredBody());
                return;
            }

            if (repaint)
            {
                EnsureTarget(rect);
                if (_rt) GUI.DrawTexture(rect, _rt, ScaleMode.StretchToFill, false);
                DrawOverlay(rect);
                DrawHint(rect);
            }
            HandleViewInput(rect, e);
        }

        void DrawOverlay(Rect rect)
        {
            var s = Settings;
            if (!_cam || (!s.ShowLabels && !s.ShowThinking)) return;

            GUI.BeginClip(rect);
            var local = new Rect(0f, 0f, rect.width, rect.height);
            var watched = Watched();
            foreach (var pilot in _pilots)
            {
                if (!pilot.Alive) continue;
                if (s.ShowThinking) DrawThinking(local, pilot);
                if (s.ShowLabels) DrawPilotLabel(local, pilot, pilot == watched);
            }
            GUI.EndClip();
        }

        void DrawThinking(Rect local, ThirdEyePilot pilot)
        {
            var t = ThirdEyePilots.Read(pilot);
            if (!t.Has) return;

            if (t.HasTarget)
            {
                Vector3 target = _cam.WorldToViewportPoint(t.Target);
                if (target.z > _cam.nearClipPlane)
                {
                    Vector2 c = ToGui(local, target);
                    Handles.color = FrogletEditorPalette.Cyan;
                    Handles.DrawWireDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, 9f);
                    Handles.DrawWireDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, 12f);
                }
            }

            Vector3 a = _cam.WorldToViewportPoint(pilot.Hull.position);
            Vector3 b = _cam.WorldToViewportPoint(t.Aim);
            if (!ThirdEyeRig.ClipToFront(pilot.Hull.position, a.z, t.Aim, b.z, _cam.nearClipPlane + 0.01f,
                    out Vector3 wa, out Vector3 wb)) return;

            Vector2 ga = ToGui(local, _cam.WorldToViewportPoint(wa));
            Vector2 gb = ToGui(local, _cam.WorldToViewportPoint(wb));
            Handles.color = t.Color;
            Handles.DrawAAPolyLine(3f, new Vector3(ga.x, ga.y, 0f), new Vector3(gb.x, gb.y, 0f));
            if (b.z > _cam.nearClipPlane)
                Handles.DrawSolidDisc(new Vector3(gb.x, gb.y, 0f), Vector3.forward, 4f);
        }

        void DrawPilotLabel(Rect local, ThirdEyePilot pilot, bool watched)
        {
            Vector3 vp = _cam.WorldToViewportPoint(pilot.Hull.position);
            bool onScreen = vp.z > _cam.nearClipPlane && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            if (watched && onScreen && Settings.CameraMode == ThirdEyeCameraMode.Chase) return;

            string who = pilot.IsLocalHuman ? "You" : pilot.IsAI ? "AI" : pilot.Name;
            string speed = pilot.Status ? $"{pilot.Status.Speed:F0} u/s" : "";
            var content = new GUIContent(onScreen ? $"{who} {pilot.HullClass}  {speed}" : who);
            var style = FrogletEditorPalette.Pill;
            Vector2 size = style.CalcSize(content) + new Vector2(10f, 2f);

            Vector2 at = onScreen
                ? ToGui(local, vp) + new Vector2(0f, -22f)
                : ToGui(local, (Vector3)ThirdEyeRig.EdgePoint(vp, 0.04f));
            var box = new Rect(at.x - size.x * 0.5f, at.y - size.y * 0.5f, size.x, size.y);
            box.x = Mathf.Clamp(box.x, 2f, local.width - box.width - 2f);
            box.y = Mathf.Clamp(box.y, 2f, local.height - box.height - 2f);

            Color domain = ThirdEyePilots.DomainColor(pilot);
            FrogletEditorPalette.DrawCard(box, FrogletEditorPalette.Surface.WithAlpha(0.85f), domain, watched ? 2f : 1f);
            GUI.Label(box, content, style);
        }

        void DrawHint(Rect rect)
        {
            var s = Settings;
            string hint = s.CameraMode switch
            {
                ThirdEyeCameraMode.Free when s.FreeMode == ThirdEyeFreeMode.Fly =>
                    "Drag: look   I J K L / W A S D: move   U O / Q E: down / up   Shift: fast   Wheel: speed   F: frame pilot",
                ThirdEyeCameraMode.Free => "Drag: orbit   Wheel: zoom   F: frame pilot",
                _ => "Wheel: distance",
            };
            hint += "   C: camera   [ ] Tab: pilot";
            var style = FrogletEditorPalette.Pill;
            var content = new GUIContent(hint);
            Vector2 size = style.CalcSize(content) + new Vector2(12f, 4f);
            var box = new Rect(rect.x + 6f, rect.yMax - size.y - 6f, Mathf.Min(size.x, rect.width - 12f), size.y);
            FrogletEditorPalette.DrawRect(box, FrogletEditorPalette.Surface.WithAlpha(0.7f));
            GUI.Label(box, content, style);
        }

        void DrawInfoStrip()
        {
            Rect strip = GUILayoutUtility.GetRect(10f, InfoHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;

            FrogletEditorPalette.DrawRect(strip, FrogletEditorPalette.SurfaceRaised);
            var watched = EditorApplication.isPlaying ? Watched() : null;
            if (watched == null)
            {
                GUI.Label(strip, "  No pilot watched.", FrogletEditorPalette.CardBody);
                return;
            }

            FrogletEditorPalette.DrawAccentStripe(strip, ThirdEyePilots.DomainColor(watched));
            var t = ThirdEyePilots.Read(watched);
            var line1 = new Rect(strip.x + 10f, strip.y + 4f, strip.width - 14f, 18f);
            var line2 = new Rect(line1.x, line1.yMax + 2f, line1.width, 18f);

            Rect pill = new(line1.x, line1.y + 1f, 150f, 16f);
            FrogletEditorPalette.StatusPill(pill, t.Has ? t.State.ToUpperInvariant() : watched.IsLocalHuman ? "YOU" : "NO AI READOUT",
                t.Has ? t.Color : FrogletEditorPalette.Muted);
            var text = new Rect(pill.xMax + 8f, line1.y, line1.width - pill.width - 8f, 18f);
            string speed = watched.Status ? $"{watched.Status.Speed:F0} u/s" : "";
            GUI.Label(text, $"{watched.Label}   {speed}   {(t.Has ? t.Pilot : "")}", FrogletEditorPalette.CardTitle);
            GUI.Label(line2, t.Has ? t.Detail : "Pick an AI pilot to see what it is aiming for.", FrogletEditorPalette.CardBody);
        }

        static GUIStyle s_centered;
        static GUIStyle CenteredBody() => s_centered ??= new GUIStyle(FrogletEditorPalette.CardBodyWrapped)
        {
            alignment = TextAnchor.MiddleCenter,
        };

        static Vector2 ToGui(Rect local, Vector3 viewport) =>
            new(local.x + viewport.x * local.width, local.y + (1f - viewport.y) * local.height);

        // ── Input ────────────────────────────────────────────────────────────────

        void HandleViewInput(Rect rect, Event e)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var s = Settings;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when rect.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    GUIUtility.keyboardControl = 0;
                    Focus();
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    float k = s.LookSensitivity;
                    if (s.CameraMode == ThirdEyeCameraMode.Free && s.FreeMode == ThirdEyeFreeMode.Fly)
                    {
                        _yaw += e.delta.x * k;
                        _pitch = Mathf.Clamp(_pitch + e.delta.y * k, -89f, 89f);
                    }
                    else if (s.CameraMode == ThirdEyeCameraMode.Free)
                    {
                        _orbitYaw += e.delta.x * k;
                        _orbitPitch = Mathf.Clamp(_orbitPitch + e.delta.y * k, -89f, 89f);
                    }
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition):
                    float zoom = 1f + e.delta.y * 0.05f;
                    switch (s.CameraMode)
                    {
                        case ThirdEyeCameraMode.Chase: s.ChaseDistance *= zoom; break;
                        case ThirdEyeCameraMode.Follow: s.FollowDistance *= zoom; break;
                        case ThirdEyeCameraMode.Free when s.FreeMode == ThirdEyeFreeMode.Orbit: s.OrbitDistance *= zoom; break;
                        default: s.FreeSpeed /= zoom; break;
                    }
                    MarkSettingsDirty();
                    e.Use();
                    break;
            }

            if (rect.Contains(e.mousePosition) && s.CameraMode == ThirdEyeCameraMode.Free)
                EditorGUIUtility.AddCursorRect(rect, s.FreeMode == ThirdEyeFreeMode.Fly ? MouseCursor.FPS : MouseCursor.Orbit);
        }

        void HandleKeys(Event e)
        {
            _shift = e.shift;
            // A release always counts, wherever focus is, or a key let go over a slider keeps flying.
            if (e.type == EventType.KeyUp)
            {
                _held.Remove(e.keyCode);
                return;
            }
            if (EditorGUIUtility.editingTextField || GUIUtility.keyboardControl != 0) return;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;

            bool first = _held.Add(e.keyCode);
            var s = Settings;
            switch (e.keyCode)
            {
                case KeyCode.C:
                    if (first) SetCamera((ThirdEyeCameraMode)(((int)s.CameraMode + 1) % CameraLabels.Length));
                    e.Use();
                    break;
                case KeyCode.Tab:
                case KeyCode.RightBracket:
                    if (first) CyclePilot(+1);
                    e.Use();
                    break;
                case KeyCode.LeftBracket:
                    if (first) CyclePilot(-1);
                    e.Use();
                    break;
                case KeyCode.F:
                    if (first) FrameWatched();
                    e.Use();
                    break;
                case KeyCode.I: case KeyCode.J: case KeyCode.K: case KeyCode.L: case KeyCode.U: case KeyCode.O:
                case KeyCode.W: case KeyCode.A: case KeyCode.S: case KeyCode.D: case KeyCode.Q: case KeyCode.E:
                    e.Use();
                    break;
            }
        }
    }
}
