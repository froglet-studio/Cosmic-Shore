using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Trailer
{
    /// <summary>
    /// Films trailer footage of the real game with the Unity Recorder: post, bloom, fog and the dark sea exactly as a
    /// player sees them on Ultra, every frame stepped at 1/60 s however long it takes to draw. Editor-only: no runtime
    /// code changes, and it writes no asset. <c>Unity -projectPath . -executeMethod
    /// CosmicShore.Editor.Trailer.TrailerShoot.Run -trailerDir D:/TrailerWork -trailerScale 150</c> (with a window: a batchmode editor has no volume stack, so no post, and WaitForEndOfFrame never fires).
    /// It boots through Bootstrap to the Menu_Main cell (offline), then runs command scripts dropped into
    /// <c>DIR/inbox/*.txt</c>, so a staging or camera change needs no recompile and no editor boot; answers go to
    /// <c>DIR/director.log</c>, frames to <c>DIR/raw/NAME/</c>.
    ///
    /// <para>Verbs: hud off | gfx SCALE | wait FRAMES | sec SECONDS | status | cell NAME | cells | lifeforms |
    /// vessel CLASS | autopilot on|off | spawn vessel CLASS,..|all N [DOMAIN] [RADIUS] | spawn fauna|flora NAME [N] |
    /// clearfleet | aim SUBJ TARGET|X,Y,Z|clear | circle SUBJ RADIUS SECONDS | action SUBJ|all INPUTEVENT [FRAMES] |
    /// blast SUBJ COUNT [FORCE] | herd follow SUBJ DIST [NAME] | herd off | teleport SUBJ TARGET DIST [UP] [SIDE] |
    /// place SUBJ X,Y,Z FACINGX,Y,Z | aimcrystal SUBJ | cue FRAME VERB.. (runs at that frame of the next record) | super N (supersampling) |
    /// arcade MODE PLAYERS INTENSITY CLASS | menu |
    /// chase SUBJ BACK UP SIDE [LEAD] [SMOOTH] | ease BACK UP SIDE FRAMES | tq SUBJ DIST AZIMUTH HEIGHT [DRIFT] [SMOOTH] |
    /// orbit SUBJ DIST HEIGHT DEG/S [PHASE] | rail SUBJ AHEAD SIDE UP [SMOOTH] | crane SUBJ A B FRAMES [SMOOTH] (offsets
    /// from where SUBJ is when the shot starts) | fov DEGREES | release | record NAME FRAMES | still NAME | recorder NAME FRAMES (the Unity Recorder, for comparison) | quit.</para>
    /// <para>A SUBJ is local, aiN, class:Rhino, nearest, fastest, fleet, flora, fauna, crystal, or name:TEXT. It resolves
    /// once per shot and is held. The camera rides the subject's position exactly and low-passes only the heading,
    /// keeps a world-up horizon, and is posed after every LateUpdate, just before the frame draws.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerShoot
    {
        const string PendingKey = "CosmicShore.TrailerShoot.Pending";
        const string DirKey = "CosmicShore.TrailerShoot.Dir";
        const string ScaleKey = "CosmicShore.TrailerShoot.Scale";
        const string OfflineWasKey = "CosmicShore.TrailerShoot.OfflineWas";
        const string OfflineKey = "CosmicShore.OfflinePreferred";

        static TrailerShoot()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("FrogletTools/Build/Trailer Shoot: Start Director", false, 340)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Boot to the menu cell and run trailer shot scripts from the inbox folder, recording with the Unity Recorder.")]
        public static void Run()
        {
            string dir = Arg("-trailerDir", Path.GetFullPath("Recordings/Trailer"));
            SessionState.SetString(DirKey, dir);
            SessionState.SetInt(ScaleKey, int.Parse(Arg("-trailerScale", "100"), CultureInfo.InvariantCulture));
            SessionState.SetBool(PendingKey, true);
            // Boot offline (a local host on 127.0.0.1), as the soaks do; the player's own preference is put back at the end.
            SessionState.SetInt(OfflineWasKey, PlayerPrefs.GetInt(OfflineKey, 0));
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene("Assets/_Scenes/Bootstrap.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                var go = new GameObject("[Trailer Director]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var d = go.AddComponent<TrailerDirector>();
                d.Begin(SessionState.GetString(DirKey, "Recordings/Trailer"), SessionState.GetInt(ScaleKey, 100));
            }
            else if (change == PlayModeStateChange.ExitingPlayMode) RestorePrefs();
        }

        internal static void RestorePrefs()
        {
            int was = SessionState.GetInt(OfflineWasKey, -1);
            if (was < 0) return;
            PlayerPrefs.SetInt(OfflineKey, was);
            PlayerPrefs.Save();
            SessionState.EraseInt(OfflineWasKey);
        }

        internal static void Quit()
        {
            RestorePrefs();
            double giveUp = EditorApplication.timeSinceStartup + 60.0;
            EditorApplication.isPlaying = false;
            void Tick()
            {
                if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < giveUp) return;
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            EditorApplication.update += Tick;
        }
    }

    /// <summary>The play-mode half of <see cref="TrailerShoot"/>: boots to the menu, runs the inbox, poses the camera.</summary>
    public class TrailerDirector : MonoBehaviour
    {
        enum CamMode { None, Chase, ThreeQuarter, Orbit, Rail, Crane }

        string _dir, _logPath;
        int _scale;
        GameDataSO _gameData;
        bool _hudOff;

        CamMode _mode;
        Transform _cam;
        bool _taken;
        readonly List<MonoBehaviour> _stoodDown = new();
        string _subjectSpec = "local";
        Transform _subject;
        Transform _fleetAnchor;
        Vector3 _fleetSmoothed;
        bool _fleetHave;
        float _fov = 58f;
        float _back = 16f, _up = 5f, _side = 6f, _lead = 30f, _smooth = 1.4f;
        float _easeBack0, _easeUp0, _easeSide0, _easeBack1, _easeUp1, _easeSide1;
        int _easeStart = -1, _easeFrames;
        float _dist = 18f, _azimuth = 35f, _height = 3f, _drift;
        float _orbitDist = 40f, _orbitHeight = 12f, _degPerSec = 4f, _phase;
        Vector3 _anchor, _offsetA, _offsetB, _railPos;
        int _craneStart, _craneFrames;
        Vector3 _heading = Vector3.forward, _lookDir = Vector3.forward;
        bool _haveHeading;
        int _lastPosedFrame = -1;

        readonly List<(int at, VesselController vessel, InputEvents ev)> _releases = new();
        bool _herding;
        string _herdFollow, _herdName;
        float _herdDist;

        public void Begin(string dir, int scale)
        {
            _dir = dir;
            _scale = Mathf.Clamp(scale, 50, 200);
            Directory.CreateDirectory(Path.Combine(_dir, "inbox"));
            Directory.CreateDirectory(Path.Combine(_dir, "raw"));
            _logPath = Path.Combine(_dir, "director.log");
            Log($"director up, {SystemInfo.graphicsDeviceName} {SystemInfo.graphicsDeviceType}, batch {Application.isBatchMode}");
            Application.runInBackground = true;
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            StartCoroutine(Main());
        }

        void OnDestroy() => RenderPipelineManager.beginContextRendering -= OnBeginRendering;

        void Log(string line)
        {
            string s = $"{DateTime.Now:HH:mm:ss} f{Time.frameCount} {line}";
            Debug.Log("[Trailer] " + line);
            try { File.AppendAllText(_logPath, s + "\n"); } catch (IOException) { }
        }

        // ---- boot ----

        IEnumerator Main()
        {
            while (DisplayGraphicsSettings.Instance == null) yield return null;
            ApplyGraphics(_scale);
            _gameData = FindGameData();
            if (_gameData == null) { Log("FATAL GameDataSO not found"); yield break; }

            bool skippedName = false;
            float deadline = Time.unscaledTime + 300f;
            while (Time.unscaledTime < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
            {
                // Nobody is at the keyboard for the first-run age gate or the username step (the soaks do the same).
                var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
                if (privacy) { Log("closing the unanswered privacy overlay"); Destroy(privacy.gameObject); }
                if (!skippedName)
                {
                    var auth = FindAnyObjectByType<AuthenticationSceneController>();
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
                    {
                        skippedName = true;
                        Log("skipping the username step");
                        auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (Time.unscaledTime >= deadline) { Log("FATAL the menu never gave a local pilot"); yield break; }
            ApplyGraphics(_scale);   // the menu's camera exists now: its AA follows the settings
            Log("menu ready: " + SceneManager.GetActiveScene().name);
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = 60;  // every frame is 1/60 s of game, however long it takes to draw

            string inbox = Path.Combine(_dir, "inbox");
            while (true)
            {
                string next = Directory.GetFiles(inbox, "*.txt").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
                if (next == null) { yield return new WaitForSecondsRealtime(0.5f); continue; }
                string[] lines;
                try { lines = File.ReadAllLines(next); File.Move(next, next + ".done." + DateTime.Now.Ticks); }
                catch (IOException) { continue; }
                Log("=== script " + Path.GetFileName(next));
                foreach (var raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    Log("> " + line);
                    yield return Do(line);
                }
                Log("=== end " + Path.GetFileName(next));
            }
        }

        bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static GameDataSO FindGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (all is { Length: > 0 }) return all[0];
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        /// <summary>Ultra, adaptive off, Lush, full VFX: what a player on a strong PC has. Changes the live snapshot and never
        /// calls the persisting setters, so the machine's shared gfx.* PlayerPrefs (other editors on this box) stay as they were.</summary>
        void ApplyGraphics(int scale)
        {
            var s = DisplayGraphicsSettings.Instance.Current;
            s.QualityPreset = QualityPresetSetting.Ultra;
            s.BasePreset = QualityPresetSetting.Ultra;
            s.AntiAliasing = AntiAliasingSetting.MSAA4x;
            s.RenderScalePercent = scale;
            s.Upscaling = UpscalingSetting.Auto;
            s.TextureQuality = 0;
            s.AdaptivePerformance = AdaptivePerformanceSetting.Off;
            s.EcosystemDensity = EcosystemDensitySetting.Lush;
            s.PhysicsDetail = PhysicsDetailSetting.High;
            s.VfxDensityPercent = 100;
            GraphicsSettingsApplier.ApplyQuality(s);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Log($"gfx: quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, msaa {(urp ? urp.msaaSampleCount : 0)}, scale {(urp ? urp.renderScale : 0):F2}, hdr {(urp && urp.supportsHDR)}, sky {(RenderSettings.skybox ? RenderSettings.skybox.name : "none")}");
        }

        // ---- commands ----

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        static Vector3 V(string s) { var p = s.Split(','); return new Vector3(F(p[0]), F(p[1]), F(p[2])); }
        static float At(string[] w, int i, float fallback) => w.Length > i ? F(w[i]) : fallback;

        IEnumerator Do(string line)
        {
            var w = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            IEnumerator blocking = null;
            try { blocking = Dispatch(w); }
            catch (Exception e) { Log($"FAILED '{line}': {e.GetType().Name} {e.Message}"); }
            if (blocking == null) yield break;
            while (true)
            {
                object step;
                try
                {
                    if (!blocking.MoveNext()) yield break;
                    step = blocking.Current;
                }
                catch (Exception e) { Log($"FAILED '{line}': {e.GetType().Name} {e.Message}"); yield break; }
                yield return step;
            }
        }

        /// <summary>Runs one verb; a verb that takes time returns its coroutine.</summary>
        IEnumerator Dispatch(string[] w)
        {
            switch (w[0])
            {
                case "hud": _hudOff = w[1] == "off"; return null;
                case "gfx": ApplyGraphics(I(w[1])); return null;
                case "wait": return WaitFrames(I(w[1]));
                case "sec": return WaitSeconds(F(w[1]));
                case "status": Status(); return null;
                case "cell": return SwapCell(string.Join(' ', w.Skip(1)));
                case "cells": Log("cells: " + string.Join(", ", CellConfigPaths().Select(Path.GetFileNameWithoutExtension))); return null;
                case "lifeforms": ListLifeforms(); return null;
                case "vessel": return SwapVessel(w[1]);
                case "autopilot": Autopilot(w[1] == "on"); return null;
                case "spawn":
                    if (w[1] == "vessel") { SpawnVessels(w[2], w.Length > 3 ? I(w[3]) : 1, w.Length > 4 ? w[4] : null, At(w, 5, 60f)); return null; }
                    int k = 2; while (k < w.Length && !int.TryParse(w[k], out _)) k++;
                    SpawnLife(w[1] == "fauna", string.Join(' ', w.Skip(2).Take(k - 2)), w.Length > k ? I(w[k]) : 1, w.Length > k + 1 ? w[k + 1] : null);
                    return null;
                case "clearfleet": ClearFleet(); return null;
                case "aim": Aim(w[1], w[2]); return null;
                case "circle": Circle(w[1], F(w[2]), F(w[3])); return null;
                case "action": Action(w[1], w[2], w.Length > 3 ? I(w[3]) : 30); return null;
                case "blast": Blast(w[1], I(w[2]), At(w, 3, 30f)); return null;
                case "herd":
                    if (w[1] == "off") { _herding = false; return null; }
                    _herding = true; _herdFollow = w[2]; _herdDist = At(w, 3, 40f); _herdName = w.Length > 4 ? w[4] : null;
                    return null;
                case "teleport": Teleport(w[1], w[2], F(w[3]), At(w, 4, 0f), At(w, 5, 0f)); return null;
                case "arcade": return Arcade(w[1], I(w[2]), I(w[3]), w[4]);
                case "menu": return BackToMenu();
                case "chase":
                    Take(); _mode = CamMode.Chase; Subject(w[1]); _back = F(w[2]); _up = F(w[3]); _side = F(w[4]);
                    _lead = At(w, 5, 30f); _smooth = At(w, 6, 1.4f); _easeStart = -1; _haveHeading = false;
                    return null;
                case "ease":
                    _easeBack0 = _back; _easeUp0 = _up; _easeSide0 = _side;
                    _easeBack1 = F(w[1]); _easeUp1 = F(w[2]); _easeSide1 = F(w[3]); _easeFrames = I(w[4]); _easeStart = Time.frameCount;
                    return null;
                case "tq":
                    Take(); _mode = CamMode.ThreeQuarter; Subject(w[1]); _dist = F(w[2]); _azimuth = F(w[3]); _height = F(w[4]);
                    _drift = At(w, 5, 0f); _smooth = At(w, 6, 1.4f); _haveHeading = false;
                    return null;
                case "orbit":
                    Take(); _mode = CamMode.Orbit; Subject(w[1]); _orbitDist = F(w[2]); _orbitHeight = F(w[3]); _degPerSec = F(w[4]);
                    _phase = At(w, 5, 0f); _fleetHave = false;
                    return null;
                case "rail":
                {
                    Take(); _mode = CamMode.Rail; Subject(w[1]); _smooth = At(w, 5, 6f); _haveHeading = false;
                    if (_subject == null) return null;
                    var flat = Flat(_subject.forward);
                    var right = Vector3.Cross(Vector3.up, flat).normalized;
                    _railPos = _subject.position + flat * F(w[2]) + right * F(w[3]) + Vector3.up * F(w[4]);
                    return null;
                }
                case "crane":
                    Take(); _mode = CamMode.Crane; Subject(w[1]); _offsetA = V(w[2]); _offsetB = V(w[3]); _craneFrames = I(w[4]);
                    _craneStart = Time.frameCount; _anchor = _subject != null ? _subject.position : Vector3.zero; _haveHeading = false; _smooth = At(w, 5, 3f);
                    return null;
                case "fov": _fov = F(w[1]); return null;
                case "release": Release(); return null;
                case "record": return RecordRt(w[1], I(w[2]));
                case "still": return RecordRt(w[1], 1);
                case "recorder": return Record(w[1], I(w[2]));
                case "super": _super = Mathf.Clamp(I(w[1]), 1, 3); return null;
                case "diag": Diag(); return null;
                case "cue": _cues.Add((I(w[1]), string.Join(' ', w.Skip(2)))); return null;
                case "place": Place(w[1], V(w[2]), V(w[3])); return null;
                case "aimcrystal": AimCrystal(w[1]); return null;
                case "quit": TrailerShoot.Quit(); return null;
                default: Log("unknown verb " + w[0]); return null;
            }
        }

        static IEnumerator WaitFrames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitSeconds(float s) { yield return new WaitForSecondsRealtime(s); }

        void Status()
        {
            var cam = Camera.main;
            Log($"status: scene {SceneManager.GetActiveScene().name}; cam {(cam ? cam.name + " at " + cam.transform.position + " post " + cam.GetUniversalAdditionalCameraData().renderPostProcessing : "none")}; fauna {FindObjectsByType<Fauna>(FindObjectsSortMode.None).Length} flora {FindObjectsByType<Flora>(FindObjectsSortMode.None).Length} crystals {FindObjectsByType<Crystal>(FindObjectsSortMode.None).Length} prisms {FindObjectsByType<Prism>(FindObjectsSortMode.None).Length}; turn {(_gameData && _gameData.IsTurnRunning)}");
            foreach (var v in Vessels())
            {
                var st = v.GetComponent<IVesselStatus>();
                Log($"  {v.name} {st?.VesselType} local {st?.Player?.IsLocalPilot} speed {(st != null ? st.Speed : 0f):F0} at {v.transform.position}");
            }
            var host = HostCell();
            Log($"  cell {(host ? host.name : "none")} scale {(host ? host.transform.lossyScale.x : 0f):F1}");
            foreach (var vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                Log($"  volume {vol.name} global {vol.isGlobal} weight {vol.weight:F2} profile {(vol.sharedProfile ? vol.sharedProfile.name : "none")}: {(vol.sharedProfile ? string.Join(",", vol.sharedProfile.components.Where(c => c.active).Select(c => c.GetType().Name)) : "")}");
        }

        // ---- every frame ----

        void Update()
        {
            for (int i = _releases.Count - 1; i >= 0; i--)
            {
                if (_releases[i].at > Time.frameCount) continue;
                if (_releases[i].vessel) _releases[i].vessel.StopShipControllerActions(_releases[i].ev);
                _releases.RemoveAt(i);
            }
            if (_herding) Herd();
        }

        void LateUpdate()
        {
            if (_hudOff)
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c && c.enabled) c.enabled = false;
        }

        void OnBeginRendering(ScriptableRenderContext ctx, List<Camera> cams)
        {
            if (_lastPosedFrame == Time.frameCount) return;
            _lastPosedFrame = Time.frameCount;
            if (_mode == CamMode.None) return;
            var active = ShotCamera();
            if (active != null && (_cam == null || active.transform != _cam))
            {
                // The game switched its active camera (a cell swap, a spawn, a scene): take the new one the same way.
                _taken = false; _cam = null;
                Take();
            }
            if (_cam != null) Pose(Time.deltaTime);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        void Pose(float dt)
        {
            if (_subject == null || !_subject.gameObject.activeInHierarchy) _subject = Resolve(_subjectSpec);
            var s = _subject;
            if (s == null) return;
            if (_subjectSpec == "fleet") Resolve("fleet");
            var at = s.position;
            if (_subjectSpec == "fleet")
            {
                _fleetSmoothed = _fleetHave ? Vector3.Lerp(_fleetSmoothed, at, 1f - Mathf.Exp(-0.8f * dt)) : at;
                _fleetHave = true;
                at = _fleetSmoothed;
            }
            if (!_haveHeading) { _heading = s.forward; _lookDir = s.forward; _haveHeading = true; }
            _heading = Vector3.Lerp(_heading, s.forward, 1f - Mathf.Exp(-_smooth * dt)).normalized;

            Vector3 pos, look;
            switch (_mode)
            {
                case CamMode.Chase:
                {
                    if (_easeStart >= 0)
                    {
                        float t = Mathf.Clamp01((Time.frameCount - _easeStart) / (float)Mathf.Max(1, _easeFrames));
                        t = t * t * (3f - 2f * t);
                        _back = Mathf.Lerp(_easeBack0, _easeBack1, t);
                        _up = Mathf.Lerp(_easeUp0, _easeUp1, t);
                        _side = Mathf.Lerp(_easeSide0, _easeSide1, t);
                    }
                    var right = Vector3.Cross(Vector3.up, _heading).normalized;
                    pos = at - _heading * _back + Vector3.up * _up + right * _side;
                    look = at + _heading * _lead;
                    break;
                }
                case CamMode.ThreeQuarter:
                {
                    _azimuth += _drift * dt;
                    var f = Flat(_heading);
                    pos = at + Quaternion.AngleAxis(_azimuth, Vector3.up) * f * _dist + Vector3.up * _height;
                    look = at + _heading * 2f;
                    break;
                }
                case CamMode.Orbit:
                {
                    _phase += _degPerSec * dt;
                    float a = _phase * Mathf.Deg2Rad;
                    pos = at + new Vector3(Mathf.Cos(a) * _orbitDist, _orbitHeight, Mathf.Sin(a) * _orbitDist);
                    look = at;
                    break;
                }
                case CamMode.Rail:
                {
                    pos = _railPos;
                    var want = (at - pos).sqrMagnitude > 1e-3f ? (at - pos).normalized : _lookDir;
                    _lookDir = Vector3.Lerp(_lookDir, want, 1f - Mathf.Exp(-_smooth * dt)).normalized;
                    look = pos + _lookDir * 50f;
                    break;
                }
                case CamMode.Crane:
                {
                    float t = Mathf.Clamp01((Time.frameCount - _craneStart) / (float)Mathf.Max(1, _craneFrames));
                    t = t * t * (3f - 2f * t);
                    pos = _anchor + Vector3.Lerp(_offsetA, _offsetB, t);
                    var want = (at - pos).sqrMagnitude > 1e-3f ? (at - pos).normalized : _lookDir;
                    if (Time.frameCount == _craneStart + 1) _lookDir = want;
                    _lookDir = Vector3.Lerp(_lookDir, want, 1f - Mathf.Exp(-_smooth * dt)).normalized;
                    look = pos + _lookDir * 50f;
                    break;
                }
                default: return;
            }
            var dir = look - pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            _cam.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var cam = _cam.GetComponent<Camera>();
            if (cam) cam.fieldOfView = _fov;
        }

        // ---- the camera ----

        Camera _ownCamera;

        /// <summary>Camera.main; else the deepest live base camera (an arcade scene's gameplay camera may carry no MainCamera tag);
        /// else one of our own built like the menu's, with post on, so a shot never records black.</summary>
        Camera ShotCamera()
        {
            if (Camera.main) return Camera.main;
            var live = FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c && c.enabled && c.gameObject.activeInHierarchy && c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base)
                .OrderByDescending(c => c.depth).FirstOrDefault();
            if (live) return live;
            if (_ownCamera) return _ownCamera;
            var go = new GameObject("TrailerCamera");
            DontDestroyOnLoad(go);
            _ownCamera = go.AddComponent<Camera>();
            _ownCamera.clearFlags = CameraClearFlags.Skybox;
            _ownCamera.farClipPlane = 8000f;
            _ownCamera.allowHDR = true;
            var data = _ownCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1;
            Log("no live camera in " + SceneManager.GetActiveScene().name + ": using our own");
            return _ownCamera;
        }

        /// <summary>Stand down what MOVES Camera.main (its rig and any camera controller), never what renders it: the gameplay
        /// camera keeps the dark sea, the fog and the post, where the replay camera clears to flat blue.</summary>
        void Take()
        {
            if (_taken && _cam != null) return;
            var main = ShotCamera();
            if (main == null) { Log("no camera to take"); return; }
            foreach (var mb in main.GetComponentsInParent<MonoBehaviour>())
            {
                string tn = mb.GetType().Name;
                if (tn is "UniversalAdditionalCameraData" or "AudioListener" or "Volume" or "DontDestroyOnLoad") continue;
                if (mb.enabled) { mb.enabled = false; _stoodDown.Add(mb); }
            }
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || !mb.enabled || _stoodDown.Contains(mb)) continue;
                string tn = mb.GetType().Name;
                if (tn.EndsWith("CameraController", StringComparison.Ordinal) || tn == "CameraManager") { mb.enabled = false; _stoodDown.Add(mb); }
            }
            _cam = main.transform;
            _taken = true;
            var data = main.GetUniversalAdditionalCameraData();
            Log($"took {main.name}: stood down {string.Join(",", _stoodDown.Where(m => m).Select(m => m.GetType().Name).Distinct())}; post {data.renderPostProcessing} layers {data.volumeLayerMask.value} clear {main.clearFlags} far {main.farClipPlane}");
        }

        void Release()
        {
            _mode = CamMode.None;
            foreach (var mb in _stoodDown) if (mb) mb.enabled = true;
            _stoodDown.Clear();
            _taken = false; _cam = null;
        }

        void Subject(string spec)
        {
            _subjectSpec = spec;
            _subject = Resolve(spec);
            Log($"subject {spec} -> {(_subject ? _subject.name : "none")}" + (_subject ? " at " + _subject.position : ""));
        }

        static List<VesselController> Vessels() =>
            FindObjectsByType<VesselController>(FindObjectsSortMode.InstanceID).Where(v => v && v.gameObject.activeInHierarchy).ToList();

        static bool IsLocal(VesselController v) => v.GetComponent<IVesselStatus>()?.Player?.IsLocalPilot ?? false;

        Transform Resolve(string spec)
        {
            if (spec.StartsWith("name:", StringComparison.Ordinal))
            {
                string text = spec[5..];
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb && mb.gameObject.activeInHierarchy && mb.name.Contains(text, StringComparison.OrdinalIgnoreCase)) return mb.transform;
                return null;
            }
            var vessels = Vessels();
            var local = vessels.FirstOrDefault(IsLocal)?.transform;
            var others = vessels.Where(v => !IsLocal(v)).Select(v => v.transform).ToList();
            var from = local ? local.position : Vector3.zero;
            if (spec == "local") return local ? local : others.FirstOrDefault();
            if (spec.StartsWith("class:", StringComparison.Ordinal))
            {
                string cls = spec[6..];
                return vessels.FirstOrDefault(v => string.Equals(v.GetComponent<IVesselStatus>()?.VesselType.ToString(), cls, StringComparison.OrdinalIgnoreCase))?.transform;
            }
            if (spec == "fleet")
            {
                if (others.Count == 0) return local;
                if (!_fleetAnchor) { _fleetAnchor = new GameObject("TrailerFleetAnchor").transform; DontDestroyOnLoad(_fleetAnchor.gameObject); }
                var c = Vector3.zero;
                foreach (var o in others) c += o.position;
                _fleetAnchor.position = c / others.Count;
                _fleetAnchor.rotation = others[0].rotation;
                return _fleetAnchor;
            }
            if (spec == "crystal") return Nearest<Crystal>(from);
            if (spec == "flora") return Nearest<Flora>(from);
            if (spec == "fauna") return Nearest<Fauna>(from);
            if (spec == "nearest") return others.OrderBy(o => (o.position - from).sqrMagnitude).FirstOrDefault();
            if (spec == "fastest")
                return vessels.OrderByDescending(v => v.GetComponent<IVesselStatus>()?.Speed ?? 0f).FirstOrDefault()?.transform;
            if (spec.StartsWith("ai", StringComparison.Ordinal) && int.TryParse(spec[2..], out int i))
                return i < others.Count ? others[i] : others.LastOrDefault();
            return local;
        }

        static Transform Nearest<T>(Vector3 from) where T : Component
        {
            Transform best = null;
            float bestD = float.MaxValue;
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!c || !c.gameObject.activeInHierarchy) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c.transform; }
            }
            return best;
        }

        // ---- recording ----

        int _super = 2;
        readonly List<(int at, string line)> _cues = new();

        /// <summary>The camera draws its whole stack, post included, into a supersampled target, box-filtered to 1080p and
        /// written as PNG. This path keeps the game's bloom and Panini. The Recorder was only tried in batchmode, where frames came
        /// out with no post (no volume stack there), and in a 2-frame windowed test that wrote no file; `recorder` keeps it to compare.</summary>
        IEnumerator RecordRt(string name, int frames)
        {
            string dir = Path.Combine(_dir, "raw", name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            int w = 1920 * _super, h = 1080 * _super;
            var hi = new RenderTexture(w, h, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1, name = "TrailerHi" };
            var lo = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "TrailerLo" };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false, false);
            var end = new WaitForEndOfFrame();
            var t0 = DateTime.UtcNow;
            Camera prev = null;
            for (int i = 0; i < frames; i++)
            {
                foreach (var cue in _cues.Where(c => c.at == i).ToList())
                {
                    Log($"cue {i}: {cue.line}");
                    StartCoroutine(Do(cue.line));
                }
                var cam = ShotCamera();
                if (cam != prev) { if (prev) prev.targetTexture = null; prev = cam; }
                if (cam) cam.targetTexture = hi;
                yield return end;
                Graphics.Blit(hi, lo);
                var was = RenderTexture.active;
                RenderTexture.active = lo;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(dir, $"f{i:D5}.png"), tex.EncodeToPNG());
                if (i % 120 == 119) Log($"  {name} {i + 1}/{frames}, {(i + 1) / (DateTime.UtcNow - t0).TotalSeconds:F1} fps");
            }
            _cues.Clear();
            if (prev) prev.targetTexture = null;
            hi.Release(); lo.Release(); Destroy(hi); Destroy(lo); Destroy(tex);
            Log($"recorded {name}: {frames} frames in {(DateTime.UtcNow - t0).TotalSeconds:F0} s -> {dir}");
        }

        void Diag()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Log($"urp {(urp ? urp.name : "none")} renderer {(urp ? urp.scriptableRenderer?.GetType().Name : "")}");
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var d = c.GetUniversalAdditionalCameraData();
                Log($"  cam {c.name} tag {c.tag} on {c.enabled} depth {c.depth} type {d.renderType} post {d.renderPostProcessing} stack [{(d.cameraStack != null ? string.Join(",", d.cameraStack.Select(o => o ? o.name : "null")) : "")}] layers {d.volumeLayerMask.value} target {(c.targetTexture ? c.targetTexture.name : "screen")} hdr {c.allowHDR} rendererIdx? mask {c.cullingMask}");
            }
            foreach (var vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                Log($"  volume {vol.name} layer {vol.gameObject.layer} enabled {vol.isActiveAndEnabled} prio {vol.priority}");
            var stack = VolumeManager.instance.stack;
            if (stack == null) { Log("  no volume stack (a batchmode editor has none: run with a window)"); return; }
            var b = stack.GetComponent<Bloom>();
            var pp = stack.GetComponent<PaniniProjection>();
            Log($"  stack bloom active {b.IsActive()} intensity {b.intensity.value} threshold {b.threshold.value}; panini active {pp.IsActive()} dist {pp.distance.value}");
        }

        IEnumerator Record(string name, int frames)
        {
            string dir = Path.Combine(_dir, "raw", name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;

            var png = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            png.name = "trailer-frames";
            png.Enabled = true;
            png.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            png.CaptureAlpha = false;
            png.imageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                OutputWidth = 1920,
                OutputHeight = 1080,
                CaptureUI = false,
                FlipFinalOutput = false,
            };
            png.OutputFile = dir.Replace('\\', '/') + "/f<Frame>";
            settings.AddRecorderSettings(png);

            var controller = new RecorderController(settings);
            controller.PrepareRecording();
            if (!controller.StartRecording()) { Log("Recorder did not start for " + name); yield break; }
            var t0 = DateTime.UtcNow;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                if (i % 120 == 119) Log($"  {name} {i + 1}/{frames}, {(i + 1) / (DateTime.UtcNow - t0).TotalSeconds:F1} fps");
            }
            controller.StopRecording();
            yield return null;
            Time.captureFramerate = 60;  // the Recorder clears it when it stops
            Log($"recorded {name}: {frames} frames in {(DateTime.UtcNow - t0).TotalSeconds:F0} s -> {dir}");
        }

        // ---- staging ----

        static Cell HostCell()
        {
            Cell best = null;
            float bestScale = -1f;
            foreach (var c in FindObjectsByType<Cell>(FindObjectsSortMode.None))
            {
                if (!c || !c.gameObject.activeInHierarchy) continue;
                float sc = c.transform.lossyScale.x;
                if (sc > bestScale) { bestScale = sc; best = c; }
            }
            return best;
        }

        static List<string> CellConfigPaths() =>
            AssetDatabase.FindAssets("t:CellConfigDataSO").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToList();

        IEnumerator SwapCell(string query)
        {
            var cell = HostCell();
            if (!cell) { Log("cell: no Cell in the scene"); yield break; }
            var path = CellConfigPaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains(query, StringComparison.OrdinalIgnoreCase));
            var cfg = path != null ? AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(path) : null;
            if (!cfg) { Log($"cell: no config matching '{query}'"); yield break; }
            Log($"cell -> {cfg.name}: {cell.RequestCellSwap(cfg, true)}");
            float deadline = Time.realtimeSinceStartup + 120f;
            do yield return null; while (cell && cell.IsSwappingConfig && Time.realtimeSinceStartup < deadline);
            Log("cell swap finished, swapping=" + (cell && cell.IsSwappingConfig));
        }

        SpawnMatrixToyDefinitionSO Catalogue()
        {
            var box = Resources.Load<ToyboxSO>("Toybox");
            return box?.Toys?.OfType<SpawnMatrixToyDefinitionSO>().FirstOrDefault();
        }

        void ListLifeforms()
        {
            var def = Catalogue();
            if (!def) { Log("no spawn matrix catalogue"); return; }
            Log("fauna: " + string.Join(", ", def.Fauna.Select(f => $"{f.Name}({f.ElementConfigs?.Count(c => c) ?? 0})")));
            Log("flora: " + string.Join(", ", def.Flora.Select(f => $"{f.Name}({f.ElementConfigs?.Count(c => c) ?? 0})")));
        }

        void SpawnLife(bool fauna, string name, int count, string element)
        {
            var def = Catalogue();
            var cell = HostCell();
            if (!def || !cell) { Log("spawn: needs the Toy Box catalogue and a Cell"); return; }
            var local = Resolve("local");
            var origin = local ? local.position : cell.transform.position;
            var fwd = local ? local.forward : Vector3.forward;
            int spawned = 0;
            if (fauna)
            {
                var entry = def.Fauna.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                            ?? def.Fauna.FirstOrDefault(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                var configs = entry?.ElementConfigs?.Where(c => c).ToArray();
                if (configs is not { Length: > 0 }) { Log($"spawn fauna: no species '{name}'"); return; }
                var pick = string.IsNullOrEmpty(element) ? configs : configs.Where(c => c.name.Contains(element, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(configs[0]).ToArray();
                for (int i = 0; i < count; i++)
                {
                    var cfg = Instantiate(pick[i % pick.Length]);
                    var pos = origin + UnityEngine.Random.insideUnitSphere * 80f + fwd * 60f;
                    if (CellLifeSpawnerBase.SpawnFaunaBanded(cell, cfg, cell.ControllingDomain, origin, pos)) spawned++;
                }
                Log($"spawned {spawned}/{count} {entry.Name}");
            }
            else
            {
                var entry = def.Flora.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                            ?? def.Flora.FirstOrDefault(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                var configs = entry?.ElementConfigs?.Where(c => c).ToArray();
                if (configs is not { Length: > 0 }) { Log($"spawn flora: no species '{name}'"); return; }
                var pick = string.IsNullOrEmpty(element) ? configs : configs.Where(c => c.name.Contains(element, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(configs[0]).ToArray();
                for (int i = 0; i < count; i++)
                    if (CellLifeSpawnerBase.PlantFlora(cell, Instantiate(pick[i % pick.Length]), null)) spawned++;
                Log($"planted {spawned}/{count} {entry.Name}");
            }
        }

        IEnumerator SwapVessel(string cls)
        {
            var init = FindAnyObjectByType<MenuServerPlayerVesselInitializer>();
            if (!init) { Log("vessel: no menu vessel initializer"); yield break; }
            if (!Enum.TryParse<VesselClassType>(cls, true, out var vc)) { Log("vessel: no class " + cls); yield break; }
            init.RequestSwap(vc);
            float deadline = Time.realtimeSinceStartup + 60f;
            do yield return null; while (init && init.IsSwapping && Time.realtimeSinceStartup < deadline);
            for (int i = 0; i < 30; i++) yield return null;
            _subject = null;
            Autopilot(true);
            var local = Resolve("local");
            Log($"vessel -> {vc}: local is {(local ? local.name : "none")}, swapping {init && init.IsSwapping}");
        }

        void Autopilot(bool on)
        {
            foreach (var v in Vessels())
            {
                var st = v.GetComponent<IVesselStatus>();
                if (st?.Player == null || !st.Player.IsLocalPilot) continue;
                v.ToggleAIPilot(on);
                st.Player.InputController?.SetPause(on);
            }
        }

        void SpawnVessels(string cls, int count, string domainName, float radius)
        {
            var init = FindAnyObjectByType<MenuServerPlayerVesselInitializer>();
            if (!init) { Log("spawn vessel: no menu vessel initializer"); return; }
            var local = Resolve("local");
            var origin = local ? local.position : Vector3.zero;
            var fwd = local ? Flat(local.forward) : Vector3.forward;
            var classes = new List<VesselClassType>();
            if (cls == "all") classes.AddRange(ToyVesselRoster.Default);
            else foreach (var part in cls.Split(','))
                if (Enum.TryParse<VesselClassType>(part, true, out var vc)) classes.Add(vc);
            if (classes.Count == 0) { Log("spawn vessel: no class in " + cls); return; }
            var domains = new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold };
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            for (int i = 0; i < count; i++)
            {
                var domain = !string.IsNullOrEmpty(domainName) && domainName != "any" && Enum.TryParse<Domains>(domainName, true, out var d) ? d : domains[i % domains.Length];
                float a = i * (2f * Mathf.PI / Math.Max(count, 1));
                var pos = origin + (right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * radius + fwd * (radius * 0.5f);
                init.RequestSpawnAiCompanion(classes[i % classes.Count], domain, new Pose(pos, Quaternion.LookRotation(fwd, Vector3.up)));
            }
            Log($"released {count} of {string.Join(",", classes)} (radius {radius})");
        }

        void ClearFleet()
        {
            var init = FindAnyObjectByType<MenuServerPlayerVesselInitializer>();
            if (!init) { Log("clearfleet: no menu vessel initializer"); return; }
            int n = 0;
            foreach (var v in Vessels())
                if (!IsLocal(v) && init.DespawnAiCompanion(v)) n++;
            Log($"cleared {n} companions");
        }

        void Aim(string subject, string target)
        {
            if (subject == "all")
            {
                int n = Vessels().Count(v => !IsLocal(v));
                for (int i = 0; i < n; i++) Aim("ai" + i, target);
                return;
            }
            var t = Resolve(subject);
            var pilot = t ? t.GetComponent<IVesselStatus>()?.AIPilot : null;
            if (pilot == null) { Log("aim: no AI pilot on " + subject); return; }
            if (target == "clear") { pilot.ClearExternalTargetProvider(); return; }
            if (target.Contains(','))
            {
                var p = V(target);
                pilot.SetExternalTargetProvider(() => p);
                return;
            }
            var other = Resolve(target);
            if (!other) { Log("aim: no subject " + target); return; }
            pilot.SetExternalTargetProvider(() => other ? other.position + other.forward * 10f : t.position + t.forward * 50f);
        }

        void Circle(string subject, float radius, float seconds)
        {
            var t = Resolve(subject);
            var pilot = t ? t.GetComponent<IVesselStatus>()?.AIPilot : null;
            if (pilot == null) { Log("circle: no AI pilot on " + subject); return; }
            var fwd = Flat(t.forward);
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var centre = t.position + right * radius;
            float t0 = Time.time, w = 2f * Mathf.PI / Mathf.Max(seconds, 0.5f);
            pilot.SetExternalTargetProvider(() =>
            {
                float a = (Time.time - t0) * w + Mathf.PI + 0.35f;
                return centre + (fwd * Mathf.Sin(a) + right * Mathf.Cos(a)) * radius;
            });
        }

        void Action(string subject, string ev, int frames)
        {
            if (!Enum.TryParse<InputEvents>(ev, true, out var ie)) { Log("action: no InputEvents " + ev); return; }
            if (subject == "all")
            {
                foreach (var v in Vessels())
                {
                    if (IsLocal(v)) continue;
                    v.PerformShipControllerActions(ie);
                    _releases.Add((Time.frameCount + frames, v, ie));
                }
                return;
            }
            var t = Resolve(subject);
            var vc = t ? t.GetComponent<VesselController>() : null;
            if (!vc) { Log("action: no vessel for " + subject); return; }
            vc.PerformShipControllerActions(ie);
            _releases.Add((Time.frameCount + frames, vc, ie));
            Log($"action {t.name} {ie} for {frames} frames");
        }

        void Blast(string subject, int count, float force)
        {
            var at = Resolve(subject);
            if (!at) { Log("blast: no subject " + subject); return; }
            var origin = at.position;
            var prisms = FindObjectsByType<Prism>(FindObjectsSortMode.None)
                .Where(p => p && p.isActiveAndEnabled && !p.destroyed)
                .OrderBy(p => (p.transform.position - origin).sqrMagnitude).Take(count).ToList();
            foreach (var p in prisms)
            {
                var dir = p.transform.position - origin;
                p.Damage((dir.sqrMagnitude < 1e-4f ? Vector3.up : dir.normalized) * force, Domains.Blue, "director", devastate: true);
            }
            Log($"blast {prisms.Count} prisms around {subject}");
        }

        void Herd()
        {
            var s = Resolve(_herdFollow);
            if (!s) return;
            var goal = s.position + s.forward * _herdDist;
            foreach (var b in FindObjectsByType<Boid>(FindObjectsSortMode.None))
                if (b && (_herdName == null || b.name.Contains(_herdName, StringComparison.OrdinalIgnoreCase))) b.Goal = goal;
        }

        /// <summary>Sets a vessel down at a world point, facing a world point, through its own SetPose (the trail is cut).</summary>
        void Place(string subject, Vector3 pos, Vector3 facing)
        {
            var t = Resolve(subject);
            var vc = t ? t.GetComponent<VesselController>() : null;
            if (!vc) { Log("place: no vessel " + subject); return; }
            var look = facing - pos;
            vc.SetPose(new Pose(pos, Quaternion.LookRotation(look.sqrMagnitude > 1e-3f ? look.normalized : Vector3.forward, Vector3.up)));
            _haveHeading = false;
        }

        /// <summary>The subject's pilot flies at the crystal nearest it.</summary>
        void AimCrystal(string subject)
        {
            var t = Resolve(subject);
            var pilot = t ? t.GetComponent<IVesselStatus>()?.AIPilot : null;
            var c = t ? Nearest<Crystal>(t.position) : null;
            if (pilot == null || !c) { Log("aimcrystal: no pilot or crystal for " + subject); return; }
            var at = c.position;
            pilot.SetExternalTargetProvider(() => at);
            Log($"{t.name} at {t.position} flies at crystal {at}, {(at - t.position).magnitude:F0} u");
        }

        void Teleport(string subject, string target, float dist, float up, float side)
        {
            var t = Resolve(subject);
            var at = Resolve(target);
            var vc = t ? t.GetComponent<VesselController>() : null;
            if (!vc || !at) { Log($"teleport: no {(vc ? target : subject)}"); return; }
            var right = Vector3.Cross(Vector3.up, Flat(at.forward)).normalized;
            var pos = at.position + Flat(at.forward) * dist + Vector3.up * up + right * side;
            var look = at.position - pos;
            vc.SetPose(new Pose(pos, Quaternion.LookRotation(look.sqrMagnitude > 1e-3f ? look.normalized : at.forward, Vector3.up)));
            _haveHeading = false;
            Log($"teleported {t.name} to {pos}, facing {at.name}");
        }

        // ---- the arcade ----

        IEnumerator Arcade(string mode, int players, int intensity, string cls)
        {
            if (!Enum.TryParse<GameModes>(mode, true, out var gm)) { Log("arcade: no GameModes " + mode); yield break; }
            SO_ArcadeGame card = null;
            foreach (var guid in AssetDatabase.FindAssets("t:SO_ArcadeGame"))
            {
                var g = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(guid));
                if (g && g.Mode == gm) { card = g; break; }
            }
            if (!card) { Log("arcade: no card for " + gm); yield break; }
            Release();
            _gameData.SyncFromArcadeGame(card);
            _gameData.IsTraining = false;
            if (_gameData.selectedVesselClass != null && Enum.TryParse<VesselClassType>(cls, true, out var vc)) _gameData.selectedVesselClass.Value = vc;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(intensity, 1, 4);
            _gameData.ConfigurePlayerCounts(Mathf.Max(1, players), 1);
            _gameData.RequestedDomainCount = Mathf.Clamp(players, 1, 3);
            _gameData.RequestedAIDifficulties.Clear();
            _gameData.InvokeGameLaunch();
            Log($"arcade launched {gm} players {players} I{intensity} -> {_gameData.SceneName}");

            MiniGameControllerBase ctl = null;
            float deadline = Time.realtimeSinceStartup + 180f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == _gameData.SceneName)
                {
                    ctl = FindAnyObjectByType<MiniGameControllerBase>();
                    int seated = 0;
                    foreach (var p in _gameData.Players) if (p != null && p.Vessel != null) seated++;
                    if (ctl && seated >= 1 && seated >= players) break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!ctl) { Log("arcade: the scene never got ready"); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            while (!_gameData.IsTurnRunning && Time.realtimeSinceStartup < deadline)
            {
                ctl.OnReadyClicked();
                float repress = Time.realtimeSinceStartup + 10f;
                while (!_gameData.IsTurnRunning && Time.realtimeSinceStartup < repress) yield return new WaitForSecondsRealtime(0.25f);
            }
            Log("arcade turn running: " + _gameData.IsTurnRunning);
            yield return null;
            if (_gameData.LocalPlayer is Player host && !PilotControl.TrySetAIFlies(host, true, _gameData, out var refusal))
                Log("host seat not handed to the AI: " + refusal);
            _taken = false; _cam = null;
            Status();
        }

        IEnumerator BackToMenu()
        {
            Release();
            var loader = FindAnyObjectByType<SceneLoader>();
            if (loader) loader.ReturnToMainMenu();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
                yield return new WaitForSecondsRealtime(0.25f);
            Log("back at the menu: " + IsMenu());
            yield return WaitFrames(60);
        }
    }
}
