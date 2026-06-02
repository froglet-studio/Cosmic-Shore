#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// In-build diagnostics overlay (uGUI). Auto-spawns in the Editor and Development builds only
    /// (stripped from Release). Shows live performance data and can record a "diagnostic" — a
    /// timed spike capture written to the user's Documents folder as JSON + a readable .txt.
    ///
    /// • Normal mode: FPS + Frame Time (ms).
    /// • Advanced mode: + draw calls / batches / triangles / SetPass, GC per frame, and network
    ///   (RTT/ping, NetVars dirty, RPCs, bytes per frame).
    /// • Run Diagnostic: records spikes for the selected seconds (works in editor and build),
    ///   then saves Documents/CosmicShore Diagnostics/diag_*.json (+ .txt).
    ///
    /// Buttons drive everything; keyboard fallbacks exist in case the scene's EventSystem can't
    /// route clicks: F7 toggle · F6 advanced · F5 run diagnostic.
    /// </summary>
    public class DiagnosticsHUD : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static DiagnosticsHUD _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[DiagnosticsHUD]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DiagnosticsHUD>();
        }

        // ── config ──
        const Key ToggleKey = Key.F7, AdvancedKey = Key.F6, DiagnosticKey = Key.F5;
        const string OutputFolderName = "CosmicShore Diagnostics";

        // ── state ──
        bool _visible = true, _advanced;
        int _diagSeconds = 10;

        float _smoothedMs, _refreshTimer;
        float _displayFps, _displayMs;

        // recording
        bool _recording;
        float _recStart, _recEnd, _recRunningSum;
        int _recFrames;
        readonly List<float> _recFrameMs = new(8192);
        readonly List<DiagSpike> _recSpikes = new(256);
        string _lastSavedPath = "";
        float _lastSavedShownAt = -100f;

        // recorders
        ProfilerRecorder _drawCalls, _setPass, _batches, _triangles, _vertices, _gcAlloc;
        ProfilerRecorder _rpcs, _netVars, _netBytes;

        // ui
        Text _readout, _advBtnLabel, _diagBtnLabel;
        GameObject _canvasGO;
        Font _font;

        void Awake()
        {
            _instance = this;
            StartRecorders();
            BuildUI();
        }

        void OnDestroy()
        {
            DisposeRecorders();
            if (_instance == this) _instance = null;
        }

        // ── recorders ─────────────────────────────────────────────────────
        void StartRecorders()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _rpcs = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM RPCs Sent");
            _netVars = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM NetVars Dirty");
            _netBytes = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM Bytes Sent");
        }

        void DisposeRecorders()
        {
            _drawCalls.Dispose(); _setPass.Dispose(); _batches.Dispose();
            _triangles.Dispose(); _vertices.Dispose(); _gcAlloc.Dispose();
            _rpcs.Dispose(); _netVars.Dispose(); _netBytes.Dispose();
        }

        static int RInt(ProfilerRecorder r) => r.Valid && r.Count > 0 ? (int)r.LastValue : 0;
        static long RLong(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue : 0;

        double Rtt()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return -1;
            if (nm.NetworkConfig?.NetworkTransport is UnityTransport utp)
            {
                ulong target = nm.IsServer && !nm.IsClient
                    ? (nm.ConnectedClientsIds.Count > 0 ? nm.ConnectedClientsIds[0] : nm.LocalClientId)
                    : NetworkManager.ServerClientId;
                return utp.GetCurrentRtt(target);
            }
            return -1;
        }

        // ── loop ──────────────────────────────────────────────────────────
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[ToggleKey].wasPressedThisFrame) SetVisible(!_visible);
                if (kb[AdvancedKey].wasPressedThisFrame) _advanced = !_advanced;
                if (kb[DiagnosticKey].wasPressedThisFrame) ToggleDiagnostic();
            }

            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = _smoothedMs <= 0f ? ms : Mathf.Lerp(_smoothedMs, ms, 0.1f);

            if (_recording) SampleRecording(ms);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.25f)
            {
                _refreshTimer = 0f;
                _displayMs = _smoothedMs;
                _displayFps = _displayMs > 0.0001f ? 1000f / _displayMs : 0f;
                if (_visible) RefreshText();
            }
        }

        void RefreshText()
        {
            if (_readout == null) return;
            var sb = new StringBuilder(256);

            if (_recording)
            {
                float left = Mathf.Max(0f, _recEnd - Time.unscaledTime);
                sb.Append("● RECORDING  ").Append(left.ToString("F0")).Append("s left\n");
                sb.Append(_recFrames).Append(" frames · ").Append(_recSpikes.Count).Append(" spikes\n");
            }

            sb.Append("FPS ").Append(_displayFps.ToString("F1"))
              .Append("    Frame Time ").Append(_displayMs.ToString("F1")).Append(" ms");

            if (_advanced)
            {
                sb.Append('\n');
                sb.Append("Draw Calls ").Append(RInt(_drawCalls))
                  .Append("  Batches ").Append(RInt(_batches)).Append('\n');
                sb.Append("Tris ").Append(RLong(_triangles).ToString("N0"))
                  .Append("  SetPass ").Append(RInt(_setPass)).Append('\n');
                sb.Append("GC ").Append((RLong(_gcAlloc) / 1024f).ToString("F1")).Append(" KB/frame\n");

                double rtt = Rtt();
                sb.Append("RTT ").Append(rtt >= 0 ? rtt.ToString("F0") + " ms" : "—")
                  .Append("  NetVars ").Append(RInt(_netVars))
                  .Append("  RPCs ").Append(RInt(_rpcs)).Append('\n');
                sb.Append("Net Bytes/f ").Append(RLong(_netBytes).ToString("N0"));
            }

            if (Time.unscaledTime - _lastSavedShownAt < 6f && !string.IsNullOrEmpty(_lastSavedPath))
                sb.Append("\nSaved: ").Append(_lastSavedPath);

            _readout.text = sb.ToString();
        }

        // ── diagnostic recording ──────────────────────────────────────────
        void ToggleDiagnostic()
        {
            if (_recording) FinishDiagnostic();
            else StartDiagnostic();
        }

        void StartDiagnostic()
        {
            _recording = true;
            _recStart = Time.unscaledTime;
            _recEnd = _recStart + _diagSeconds;
            _recFrames = 0;
            _recRunningSum = 0f;
            _recFrameMs.Clear();
            _recSpikes.Clear();
            UpdateDiagButtonLabel();
        }

        void SampleRecording(float frameMs)
        {
            _recFrames++;
            _recRunningSum += frameMs;
            _recFrameMs.Add(frameMs);

            float mean = _recFrames > 0 ? _recRunningSum / _recFrames : frameMs;
            float threshold = Mathf.Max(33.3f, 1.75f * mean);
            if (frameMs >= threshold && _recSpikes.Count < 256)
            {
                _recSpikes.Add(new DiagSpike
                {
                    t = Time.unscaledTime - _recStart,
                    ms = frameMs,
                    fps = frameMs > 0.0001f ? 1000f / frameMs : 0f,
                    draws = RInt(_drawCalls),
                    tris = RLong(_triangles),
                    gcKB = RLong(_gcAlloc) / 1024f,
                    rttMs = Rtt(),
                });
            }

            if (Time.unscaledTime >= _recEnd)
                FinishDiagnostic();
        }

        void FinishDiagnostic()
        {
            _recording = false;
            UpdateDiagButtonLabel();

            var report = BuildReport();
            _lastSavedPath = SaveReport(report);
            _lastSavedShownAt = Time.unscaledTime;
            Debug.Log($"[DiagnosticsHUD] Diagnostic saved: {_lastSavedPath}");
        }

        DiagReport BuildReport()
        {
            int n = _recFrameMs.Count;
            var r = new DiagReport
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                durationSec = _diagSeconds,
                frames = n,
                draws = RInt(_drawCalls),
                tris = RLong(_triangles),
                rttMs = Rtt(),
                spikes = new List<DiagSpike>(_recSpikes),
            };
            if (n > 0)
            {
                var sorted = new List<float>(_recFrameMs); sorted.Sort();
                float sum = 0f; for (int i = 0; i < n; i++) sum += _recFrameMs[i];
                r.avgFrameMs = sum / n;
                r.avgFps = r.avgFrameMs > 0.0001f ? 1000f / r.avgFrameMs : 0f;
                r.p99FrameMs = sorted[Mathf.Clamp(Mathf.RoundToInt(0.99f * (n - 1)), 0, n - 1)];
                r.maxFrameMs = sorted[n - 1];
            }
            return r;
        }

        string SaveReport(DiagReport r)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) docs = Application.persistentDataPath;
                string dir = Path.Combine(docs, OutputFolderName);
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseName = $"diag_{Sanitize(r.scene)}_{stamp}";
                File.WriteAllText(Path.Combine(dir, baseName + ".json"), JsonUtility.ToJson(r, true));
                File.WriteAllText(Path.Combine(dir, baseName + ".txt"), BuildTxt(r));
                return Path.Combine(dir, baseName + ".json");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiagnosticsHUD] Could not save diagnostic: {e.Message}");
                return "(save failed: " + e.Message + ")";
            }
        }

        static string BuildTxt(DiagReport r)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine($"Cosmic Shore diagnostic — {r.scene}   {r.timestamp}");
            sb.AppendLine($"duration {r.durationSec}s · {r.frames} frames · avg {r.avgFps:F1} fps " +
                          $"({r.avgFrameMs:F1} ms) · p99 {r.p99FrameMs:F1} ms · max {r.maxFrameMs:F1} ms");
            sb.AppendLine($"draws {r.draws} · tris {r.tris:N0} · RTT {(r.rttMs >= 0 ? r.rttMs.ToString("F0") + " ms" : "n/a")}");
            sb.AppendLine($"spikes ({r.spikes?.Count ?? 0}):");
            if (r.spikes != null)
                foreach (var s in r.spikes)
                    sb.AppendLine($"  [{s.t:F1}s] {s.ms:F1} ms ({s.fps:F0} fps) · draws {s.draws} · " +
                                  $"tris {s.tris:N0} · GC {s.gcKB:F1} KB" +
                                  (s.rttMs >= 0 ? $" · RTT {s.rttMs:F0} ms" : ""));
            return sb.ToString();
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "scene";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        // ── UI construction (uGUI) ────────────────────────────────────────
        void SetVisible(bool v)
        {
            _visible = v;
            if (_canvasGO != null) _canvasGO.SetActive(v);
        }

        void BuildUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            EnsureEventSystem();

            _canvasGO = new GameObject("DiagnosticsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = _canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Panel (top-left).
            var panel = CreateRect("Panel", _canvasGO.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -8), new Vector2(330, 190));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            // Readout text.
            var textRect = CreateRect("Readout", panel, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(8, -6), new Vector2(-16, 150));
            textRect.pivot = new Vector2(0, 1);
            _readout = textRect.gameObject.AddComponent<Text>();
            _readout.font = _font;
            _readout.fontSize = 14;
            _readout.color = Color.white;
            _readout.alignment = TextAnchor.UpperLeft;
            _readout.horizontalOverflow = HorizontalWrapMode.Overflow;
            _readout.verticalOverflow = VerticalWrapMode.Overflow;
            _readout.text = "FPS —   Frame Time — ms";

            // Button row along the bottom.
            float y = 6f;
            _advBtnLabel = CreateButton("Advanced", panel, 6, y, 92, () => _advanced = !_advanced);
            _diagBtnLabel = CreateButton("Run 10s", panel, 104, y, 84, ToggleDiagnostic);
            CreateButton("-", panel, 194, y, 30, () => { _diagSeconds = Mathf.Max(1, _diagSeconds - 5); UpdateDiagButtonLabel(); });
            CreateButton("+", panel, 230, y, 30, () => { _diagSeconds = Mathf.Min(600, _diagSeconds + 5); UpdateDiagButtonLabel(); });

            SetVisible(_visible);
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        Text CreateButton(string label, Transform parent, float x, float y, float width, Action onClick)
        {
            var rt = CreateRect("Btn_" + label, parent, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(x, y), new Vector2(width, 22));
            rt.pivot = new Vector2(0, 0);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var labelRt = CreateRect("Label", rt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = Vector2.zero;
            var t = labelRt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            return t;
        }

        void UpdateDiagButtonLabel()
        {
            if (_diagBtnLabel != null)
                _diagBtnLabel.text = _recording ? "Stop" : $"Run {_diagSeconds}s";
        }

        // ── serializable report ───────────────────────────────────────────
        [Serializable]
        class DiagSpike
        {
            public float t, ms, fps, gcKB;
            public int draws;
            public long tris;
            public double rttMs;
        }

        [Serializable]
        class DiagReport
        {
            public string scene, timestamp;
            public float durationSec, avgFps, avgFrameMs, p99FrameMs, maxFrameMs;
            public int frames, draws;
            public long tris;
            public double rttMs;
            public List<DiagSpike> spikes;
        }
#endif
    }
}
