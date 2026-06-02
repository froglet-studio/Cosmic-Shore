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
        Text _readout, _valueCol, _advBtnLabel, _diagBtnLabel;
        RectTransform _panel, _readoutRT, _valueRT, _buttonRow;
        GameObject _canvasGO;
        Font _font;

        // cached once — local machine region + UTC offset (UGS auto-picks the Relay region and
        // doesn't surface it, so we report the client's OS region; ping gives latency to host).
        string _regionCache, _utcCache;

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
                if (kb[AdvancedKey].wasPressedThisFrame) ToggleAdvanced();
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

        // palette
        const string Label = "#8b97a8", White = "#ffffff", Good = "#5fe07a", Warn = "#ffd066",
                     Bad = "#ff6b6b", Accent = "#7cc2ff", Dim = "#6b7686";

        // layout
        const float Pad = 10f, ValueX = 132f, BtnH = 22f, RowGap = 10f, TopY = 8f;

        static string Col(string hex, string body) => "<color=" + hex + ">" + body + "</color>";
        static string FpsColor(float fps) => fps >= 55f ? Good : fps >= 30f ? Warn : Bad;
        static string MsColor(float ms) => ms <= 17f ? Good : ms <= 33.4f ? Warn : Bad;

        static void Row(StringBuilder l, StringBuilder v, string label, string value)
        {
            l.Append(Col(Label, label)).Append('\n');
            v.Append(value).Append('\n');
        }

        static void Header(StringBuilder l, StringBuilder v, string title)
        {
            l.Append(Col(Accent, title)).Append('\n');
            v.Append('\n');
        }

        void RefreshText()
        {
            if (_readout == null) return;
            var l = new StringBuilder(384);
            var v = new StringBuilder(384);

            if (_recording)
            {
                float left = Mathf.Max(0f, _recEnd - Time.unscaledTime);
                Row(l, v, "● Recording", Col(Warn, left.ToString("F0") + "s left"));
                Row(l, v, "Captured", Col(Dim, _recFrames + " f · " + _recSpikes.Count + " spikes"));
            }

            // ── live ──
            Row(l, v, "FPS", Col(FpsColor(_displayFps), _displayFps.ToString("F0")));
            Row(l, v, "Frame Time", Col(MsColor(_displayMs), _displayMs.ToString("F1") + " ms"));

            if (_advanced)
            {
                Header(l, v, "Render");
                Row(l, v, "Draw Calls", Col(White, RInt(_drawCalls).ToString()));
                Row(l, v, "Batches", Col(White, RInt(_batches).ToString()));
                Row(l, v, "SetPass", Col(White, RInt(_setPass).ToString()));
                Row(l, v, "Triangles", Col(White, RLong(_triangles).ToString("N0")));
                Row(l, v, "Vertices", Col(White, RLong(_vertices).ToString("N0")));

                Header(l, v, "Memory");
                float gcKB = RLong(_gcAlloc) / 1024f;
                Row(l, v, "GC / frame", Col(gcKB > 4f ? Warn : Good, gcKB.ToString("F1") + " KB"));

                Header(l, v, "Network");
                double rtt = Rtt();
                Row(l, v, "Ping", rtt >= 0
                    ? Col(rtt <= 80 ? Good : rtt <= 160 ? Warn : Bad, rtt.ToString("F0") + " ms")
                    : Col(Dim, "offline"));
                Row(l, v, "NetVars", Col(White, RInt(_netVars).ToString()));
                Row(l, v, "RPCs", Col(White, RInt(_rpcs).ToString()));
                Row(l, v, "Bytes / frame", Col(White, RLong(_netBytes).ToString("N0")));

                Header(l, v, "Region");
                Row(l, v, "Location", Col(White, RegionName()));
                Row(l, v, "UTC offset", Col(White, UtcOffset()));
            }

            if (Time.unscaledTime - _lastSavedShownAt < 6f && !string.IsNullOrEmpty(_lastSavedPath))
            {
                Header(l, v, "Saved");
                Row(l, v, "File", Col(Dim, _lastSavedPath));
            }

            _readout.text = l.ToString();
            if (_valueCol != null) _valueCol.text = v.ToString();
            Relayout();
        }

        string RegionName()
        {
            if (_regionCache != null) return _regionCache;
            try { _regionCache = System.Globalization.RegionInfo.CurrentRegion.DisplayName; }
            catch { _regionCache = "Unknown"; }
            return _regionCache;
        }

        string UtcOffset()
        {
            if (_utcCache != null) return _utcCache;
            TimeSpan off;
            try { off = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now); }
            catch { off = TimeSpan.Zero; }
            string sign = off < TimeSpan.Zero ? "-" : "+";
            _utcCache = $"UTC{sign}{Math.Abs(off.Hours):D2}:{Math.Abs(off.Minutes):D2}";
            return _utcCache;
        }

        // Resize the panel to fit the current rows (small in normal mode, taller in advanced) and
        // slide the bottom button row up to sit just under the table.
        void Relayout()
        {
            if (_panel == null || _readout == null) return;
            float textH = _readout.preferredHeight;
            float valW = _valueCol != null ? _valueCol.preferredWidth : 80f;
            float panelW = Mathf.Clamp(ValueX + valW + Pad, 300f, 680f);
            float panelH = TopY + textH + RowGap + BtnH + Pad;
            _panel.sizeDelta = new Vector2(panelW, panelH);
            if (_readoutRT != null) _readoutRT.sizeDelta = new Vector2(ValueX - Pad - 4f, textH);
            if (_valueRT != null) _valueRT.sizeDelta = new Vector2(panelW - ValueX - Pad, textH);
            if (_buttonRow != null) _buttonRow.anchoredPosition = new Vector2(Pad, -(TopY + textH + RowGap));
        }

        void ToggleAdvanced()
        {
            _advanced = !_advanced;
            if (_advBtnLabel != null) _advBtnLabel.text = _advanced ? "Simple" : "Advanced";
            if (_visible) RefreshText();
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

            // Panel (top-left). Size is recomputed every refresh by Relayout().
            _panel = CreateRect("Panel", _canvasGO.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -8), new Vector2(300, 80));
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            // Two-column table: labels on the left, values aligned in a column at ValueX.
            _readoutRT = CreateRect("Labels", _panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(Pad, -TopY), new Vector2(ValueX - Pad - 4f, 40));
            _readout = MakeColumn(_readoutRT, TextAnchor.UpperLeft);
            _readout.text = "FPS —";

            _valueRT = CreateRect("Values", _panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(ValueX, -TopY), new Vector2(160, 40));
            _valueCol = MakeColumn(_valueRT, TextAnchor.UpperLeft);

            // Button row — a container Relayout() slides up to sit just below the table.
            _buttonRow = CreateRect("ButtonRow", _panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(Pad, -52), new Vector2(0, BtnH));
            _advBtnLabel = CreateButton("Advanced", _buttonRow, 0, 92, ToggleAdvanced);
            _diagBtnLabel = CreateButton("Run 10s", _buttonRow, 98, 84, ToggleDiagnostic);
            CreateButton("-", _buttonRow, 188, 30, () => { _diagSeconds = Mathf.Max(1, _diagSeconds - 5); UpdateDiagButtonLabel(); });
            CreateButton("+", _buttonRow, 224, 30, () => { _diagSeconds = Mathf.Min(600, _diagSeconds + 5); UpdateDiagButtonLabel(); });

            SetVisible(_visible);
            RefreshText();
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

        Text MakeColumn(RectTransform rt, TextAnchor anchor)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 14;
            t.color = Color.white;
            t.supportRichText = true;
            t.alignment = anchor;
            t.lineSpacing = 1.1f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Text CreateButton(string label, Transform parent, float x, float width, Action onClick)
        {
            var rt = CreateRect("Btn_" + label, parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, 0), new Vector2(width, 22));
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
