#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// The "Load Time Insights" tab of the Performance Benchmark window. Front-end onto
    /// <see cref="LoadInsights"/> (the runtime recorder): arm/disarm Record Insight Mode, watch a
    /// load record live, then read the finished report — donut chart + category table (exact
    /// wall-clock attribution), top costs, scripted delays, stalls, errors, insights, timeline —
    /// with one-click Copy (Claude-ready text) and the auto-saved .txt/.json on disk.
    /// </summary>
    public static class LoadInsightsTab
    {
        // ── view state (statics reset on domain reload; everything rebuilds from disk) ──
        static Vector2 s_scroll;
        static bool s_foldBreakdown = true;
        static bool s_foldTopCosts = true;
        static bool s_foldHotPath = true;
        static bool s_foldHints = true;
        static bool s_foldStalls;
        static bool s_foldErrors;
        static bool s_foldTimeline;
        static bool s_foldHistory;

        static LoadInsightReport s_report;          // report currently shown
        static string s_reportPath = "";
        static string s_adoptedReportId = "";

        static List<HistoryEntry> s_history;
        static Texture2D s_pieTexture;
        static string s_pieTextureId = "";

        struct HistoryEntry
        {
            public string path;
            public string scene, mode, role, date;
            public float totalMs;
            public bool interrupted;
        }

        static string OutputDir => Path.Combine(Application.persistentDataPath, LoadInsights.OutputSubfolder);

        // ── category palette (pie slices + swatches; distinguishable on dark + light skins) ──
        static Color CategoryColor(int category) => category switch
        {
            (int)LoadInsightCategory.SceneLoad => EditorUIStyles.Sky,
            (int)LoadInsightCategory.Netcode => EditorUIStyles.Lavender,
            (int)LoadInsightCategory.ScriptedDelay => EditorUIStyles.Rose,
            (int)LoadInsightCategory.Vessels => EditorUIStyles.Mint,
            (int)LoadInsightCategory.AiBackfill => EditorUIStyles.Peach,
            (int)LoadInsightCategory.Environment => new Color(0.55f, 0.86f, 0.86f), // teal
            (int)LoadInsightCategory.Flora => new Color(0.72f, 0.92f, 0.55f),       // leaf
            (int)LoadInsightCategory.Fauna => EditorUIStyles.Amber,
            (int)LoadInsightCategory.Prisms => new Color(0.62f, 0.70f, 0.98f),      // periwinkle
            (int)LoadInsightCategory.Crystals => new Color(0.95f, 0.68f, 0.92f),    // orchid
            (int)LoadInsightCategory.Pooling => new Color(0.86f, 0.80f, 0.62f),     // sand
            (int)LoadInsightCategory.UiHud => new Color(0.70f, 0.94f, 0.98f),       // ice
            (int)LoadInsightCategory.GameFlow => new Color(0.99f, 0.62f, 0.54f),    // coral
            (int)LoadInsightCategory.Other => EditorUIStyles.Slate,
            _ => new Color(0.52f, 0.55f, 0.60f)                                     // unattributed
        };

        static Color DurationColor(float ms) =>
            ms <= 10_000f ? EditorUIStyles.Mint :
            ms <= 20_000f ? EditorUIStyles.Amber :
            ms <= 45_000f ? EditorUIStyles.Peach : EditorUIStyles.Rose;

        /// <summary>Window OnEnable hook — rebuild disk-backed state after a domain reload.</summary>
        public static void OnWindowEnable()
        {
            RefreshHistory();
            if (s_report == null && s_history is { Count: > 0 })
                TryLoadReport(s_history[0].path);
        }

        /// <summary>True while the window should repaint frequently for this tab.</summary>
        public static bool IsBusy => Application.isPlaying && (LoadInsights.IsRecording || LoadInsights.Armed);

        // ═════════════════════════════════════════════════
        public static void Draw(EditorWindow host)
        {
            AdoptFreshReport();

            s_scroll = EditorGUILayout.BeginScrollView(s_scroll);
            EditorGUILayout.Space(6);

            DrawRecordControls();

            if (LoadInsights.IsRecording)
            {
                DrawLiveStatus();
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(4);
            if (s_report == null)
            {
                EditorGUILayout.HelpBox(
                    "No load report yet.\n\n" +
                    "1. Arm Record Insight Mode above.\n" +
                    "2. Enter Play Mode and launch a game from the arcade (host or client — both record).\n" +
                    "3. The report is ready when the arena is complete — the connecting screen finishes " +
                    "and the pre-game cinematic starts — and lands here automatically.\n\n" +
                    "Loads that get force-quit still leave an in-flight snapshot that is recovered on the next run.",
                    MessageType.Info);
            }
            else
            {
                DrawReport(host);
            }

            DrawHistory();
            EditorGUILayout.EndScrollView();
        }

        // ── recording controls + live status ────────────

        static void DrawRecordControls()
        {
            EditorUIStyles.SectionHeader("Record Insight Mode", EditorUIStyles.Mint);
            EditorGUILayout.LabelField(
                "Records one game launch — menu tap → scene load → netcode sync → spawning → arena complete " +
                "(connecting screen done, cinematic starting) — and attributes every millisecond to what caused it.",
                EditorUIStyles.Wrap);
            EditorGUILayout.Space(2);

            bool armed = LoadInsights.Armed;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = armed ? EditorUIStyles.Rose : EditorUIStyles.Mint;
            string label = armed
                ? "■  Record Insight Mode: ARMED — every game load records (click to disarm)"
                : "●  Arm Record Insight Mode — record the next game load";
            if (GUILayout.Button(label, GUILayout.Height(32)))
            {
                LoadInsights.Armed = !armed;
                if (LoadInsights.Armed && Application.isPlaying)
                    LoadInsightsRuntime.EnsureSpawned();
            }
            GUI.backgroundColor = prev;

            if (LoadInsights.Armed && !LoadInsights.IsRecording)
            {
                string hint = Application.isPlaying
                    ? "Armed — launch a game (arcade card → Start). Recording begins at the launch tap."
                    : "Armed — enter Play Mode and launch a game. Arming persists across sessions until disarmed.";
                EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
            }
        }

        static void DrawLiveStatus()
        {
            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorUIStyles.TintLastRect(rect, EditorUIStyles.Rose, 0.10f);
            EditorGUILayout.LabelField(
                $"● Recording load — {LoadInsights.ElapsedMs / 1000f:F1}s", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Now: {LoadInsights.CurrentActivity}");
            EditorGUILayout.LabelField(
                $"{LoadInsights.LiveSpanCount} spans · {LoadInsights.LiveStallCount} frame stalls so far",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.LabelField(
                "The report completes automatically at arena-complete (connecting screen done), or on abort/timeout.",
                EditorStyles.miniLabel);
        }

        // ── the report ──────────────────────────────────

        static void AdoptFreshReport()
        {
            var latest = LoadInsights.LastReport;
            if (latest == null || latest.reportId == s_adoptedReportId) return;

            s_report = latest;
            s_reportPath = LoadInsights.LastReportPath;
            s_adoptedReportId = latest.reportId;
            RefreshHistory();
        }

        static void DrawReport(EditorWindow host)
        {
            var r = s_report;

            EditorUIStyles.SectionHeader("Load Report", EditorUIStyles.Lavender);

            // Headline: total duration bar, colored by how painful it was.
            var barRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.17f, 1f));
            var c = DurationColor(r.totalMs);
            EditorGUI.DrawRect(barRect, new Color(c.r, c.g, c.b, 0.30f));
            var headline = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 14, normal = { textColor = Color.white }
            };
            // Old reports ended at countdown GO — show the visual-ready split only when it differs.
            string visual = r.visualReadyMs >= 0f && Mathf.Abs(r.totalMs - r.visualReadyMs) > 50f
                ? $"   (visually loaded at {r.visualReadyMs / 1000f:F1}s)" : "";
            GUI.Label(barRect, $"{r.totalMs / 1000f:F2}s to loaded{visual}", headline);

            EditorGUILayout.Space(2);

            // Context badges.
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge(ShortSceneName(r.sceneTo), EditorUIStyles.Sky);
            if (!string.IsNullOrEmpty(r.gameMode)) EditorUIStyles.Badge(r.gameMode, EditorUIStyles.Lavender);
            if (r.intensity > 0) EditorUIStyles.Badge($"intensity {r.intensity}", EditorUIStyles.Amber);
            if (r.totalPlayers > 0) EditorUIStyles.Badge($"{r.humanPlayers}H + {r.aiBackfill}AI", EditorUIStyles.Mint);
            EditorUIStyles.Badge(r.isMultiplayer ? $"MP · {r.networkRole}" : "Single player", EditorUIStyles.Peach);
            if (r.interrupted) EditorUIStyles.Badge("INTERRUPTED", EditorUIStyles.Rose);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"{r.completionReason}  ·  {FormatDate(r.timestamp)}  ·  {r.gitBranch}/{r.gitCommitHash}" +
                (r.framesDuringLoad > 0 ? $"  ·  {r.avgFpsDuringLoad:F0} fps during load, worst frame {r.worstFrameMs:F0} ms" : ""),
                EditorStyles.miniLabel);

            // Actions.
            EditorGUILayout.Space(2);
            DrawActionRow(host, r);

            // Where the time went — donut + legend.
            EditorGUILayout.Space(4);
            if (Fold(ref s_foldBreakdown, "Where the time went"))
                DrawBreakdown(r);

            // Waiting vs working strip.
            DrawWaitWorkStrip(r);

            // Top costs.
            if (Fold(ref s_foldTopCosts, $"Top costs ({Mathf.Min(r.topCosts.Count, 15)})"))
                DrawTopCosts(r);

            // Hot-path breakdown (accumulated sub-stages inside the big spans).
            if (r.accumulators is { Count: > 0 } &&
                Fold(ref s_foldHotPath, $"Hot-path breakdown ({r.accumulators.Count} stages)"))
                DrawAccumulators(r);

            // Insights.
            if (Fold(ref s_foldHints, $"Insights ({r.hints.Count})"))
                DrawHints(r);

            // Stalls.
            if (Fold(ref s_foldStalls, $"Frame stalls ({r.stalls.Count})"))
                DrawStalls(r);

            // Errors.
            if (Fold(ref s_foldErrors, $"Errors during load ({r.errors.Count})"))
                DrawErrors(r);

            // Timeline.
            if (Fold(ref s_foldTimeline, "Timeline"))
                DrawTimeline(r);
        }

        static void DrawActionRow(EditorWindow host, LoadInsightReport r)
        {
            EditorGUILayout.BeginHorizontal();

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = EditorUIStyles.Lavender;
            if (GUILayout.Button("📋  Copy insight report", GUILayout.Height(24)))
            {
                EditorGUIUtility.systemCopyBuffer = r.BuildText();
                host.ShowNotification(new GUIContent("Insight report copied — paste it to Claude"));
            }
            GUI.backgroundColor = EditorUIStyles.Mint;
            string txtPath = TxtSibling(s_reportPath);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(txtPath) || !File.Exists(txtPath)))
            {
                if (GUILayout.Button("Open .txt", GUILayout.Height(24), GUILayout.Width(90)))
                    EditorUtility.OpenWithDefaultApp(txtPath);
            }
            GUI.backgroundColor = EditorUIStyles.Sky;
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(s_reportPath) || !File.Exists(s_reportPath)))
            {
                if (GUILayout.Button("Reveal files", GUILayout.Height(24), GUILayout.Width(100)))
                    EditorUtility.RevealInFinder(s_reportPath);
            }
            GUI.backgroundColor = prev;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Reports auto-save as .json + .txt — the .txt is the shareable/downloadable insight file.",
                EditorStyles.miniLabel);
        }

        // ── breakdown: donut + legend table ─────────────

        static void DrawBreakdown(LoadInsightReport r)
        {
            var slices = r.slices.Where(s => s.attributedMs > 0.5f).ToList();
            if (slices.Count == 0)
            {
                EditorGUILayout.HelpBox("No attribution data (empty load window).", MessageType.None);
                return;
            }

            EnsurePieTexture(r, slices);

            EditorGUILayout.BeginHorizontal();

            // Donut.
            const int pieSize = 190;
            var pieRect = GUILayoutUtility.GetRect(pieSize + 12, pieSize + 8,
                GUILayout.Width(pieSize + 12), GUILayout.Height(pieSize + 8));
            var drawRect = new Rect(pieRect.x + 6, pieRect.y + 4, pieSize, pieSize);
            if (s_pieTexture != null) GUI.DrawTexture(drawRect, s_pieTexture, ScaleMode.ScaleToFit, true);

            var centerBig = new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            var centerSmall = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(drawRect.x, drawRect.y + drawRect.height * 0.38f, drawRect.width, 22),
                $"{r.totalMs / 1000f:F1}s", centerBig);
            GUI.Label(new Rect(drawRect.x, drawRect.y + drawRect.height * 0.38f + 20, drawRect.width, 14),
                "to loaded", centerSmall);

            // Legend table.
            EditorGUILayout.BeginVertical();
            foreach (var s in slices)
            {
                EditorGUILayout.BeginHorizontal();

                var swatch = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
                swatch.y += 2;
                EditorGUI.DrawRect(swatch, CategoryColor(s.category));

                EditorGUILayout.LabelField(s.name, GUILayout.MinWidth(130));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(FormatMs(s.attributedMs), EditorUIStyles.CardValue, GUILayout.Width(76));
                EditorGUILayout.LabelField($"{s.percent:F1}%", EditorUIStyles.CardValue, GUILayout.Width(52));
                EditorGUILayout.EndHorizontal();

                if (s.waitMs > 0.5f && s.category != (int)LoadInsightCategory.ScriptedDelay)
                    EditorGUILayout.LabelField($"        of which waiting: {FormatMs(s.waitMs)}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Exact attribution: every ms goes to the innermost active span — percentages sum to 100.",
                EditorStyles.miniLabel);
        }

        static void EnsurePieTexture(LoadInsightReport r, List<LoadCategorySlice> slices)
        {
            if (s_pieTexture != null && s_pieTextureId == r.reportId) return;

            var parts = new List<(float frac, Color color)>(slices.Count);
            float total = Mathf.Max(1f, r.totalMs);
            foreach (var s in slices)
                parts.Add((s.attributedMs / total, CategoryColor(s.category)));

            if (s_pieTexture != null) UnityEngine.Object.DestroyImmediate(s_pieTexture);
            s_pieTexture = BuildDonutTexture(parts, 256);
            s_pieTextureId = r.reportId;
        }

        /// <summary>
        /// CPU-rendered donut chart texture: per-pixel slice lookup with anti-aliased inner/outer
        /// edges and hairline gaps between slices. IMGUI has no reliable arc primitive in editor
        /// windows, and a one-off 256² fill per report is effectively free.
        /// </summary>
        static Texture2D BuildDonutTexture(List<(float frac, Color color)> parts, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            // Cumulative slice ends in [0,1], starting at 12 o'clock going clockwise.
            var ends = new float[parts.Count];
            float acc = 0f;
            for (int i = 0; i < parts.Count; i++) { acc += parts[i].frac; ends[i] = acc; }

            const float outer = 0.98f, inner = 0.58f;
            const float aa = 0.015f;                    // edge softness in radius units
            const float gap = 0.0035f;                  // hairline gap between slices (fraction of turn)
            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float rad = Mathf.Sqrt(dx * dx + dy * dy);

                    if (rad > outer + aa || rad < inner - aa)
                    {
                        pixels[y * size + x] = clear;
                        continue;
                    }

                    // Angle → turn fraction, 0 at top, clockwise (texture rows are bottom-up,
                    // so +dy is "up" on screen once drawn).
                    float t = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
                    if (t < 0f) t += 1f;

                    int idx = 0;
                    while (idx < ends.Length - 1 && t > ends[idx]) idx++;

                    // Hairline gap at slice boundaries (skip when a slice is ~the whole ring).
                    float sliceStart = idx == 0 ? 0f : ends[idx - 1];
                    float sliceEnd = ends[idx];
                    bool nearEdge = (t - sliceStart < gap || sliceEnd - t < gap) && parts.Count > 1
                                    && sliceEnd - sliceStart < 0.995f;

                    float alpha = Mathf.InverseLerp(outer + aa, outer - aa, rad)
                                * Mathf.InverseLerp(inner - aa, inner + aa, rad);
                    var col = parts[idx].color;
                    pixels[y * size + x] = nearEdge
                        ? clear
                        : new Color(col.r, col.g, col.b, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static void DrawWaitWorkStrip(LoadInsightReport r)
        {
            if (r.totalMs <= 0f) return;

            EditorGUILayout.Space(2);
            float waitFrac = Mathf.Clamp01(r.waitMs / r.totalMs);
            var rect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            var waitRect = new Rect(rect.x, rect.y, rect.width * waitFrac, rect.height);
            var workRect = new Rect(rect.x + waitRect.width, rect.y, rect.width - waitRect.width, rect.height);
            EditorGUI.DrawRect(waitRect, new Color(EditorUIStyles.Lavender.r, EditorUIStyles.Lavender.g, EditorUIStyles.Lavender.b, 0.8f));
            EditorGUI.DrawRect(workRect, new Color(EditorUIStyles.Mint.r, EditorUIStyles.Mint.g, EditorUIStyles.Mint.b, 0.8f));

            var mini = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
            if (waitRect.width > 60) GUI.Label(waitRect, $"waiting {r.waitMs / 1000f:F1}s", mini);
            if (workRect.width > 60) GUI.Label(workRect, $"working {(r.totalMs - r.waitMs) / 1000f:F1}s", mini);

            string human = r.humanWaitMs > 500f ? $"  (incl. {r.humanWaitMs / 1000f:F1}s waiting on Ready clicks)" : "";
            EditorGUILayout.LabelField($"Waiting = delays + replication + sync{human} · Working = actual execution.", EditorStyles.miniLabel);
        }

        static void DrawTopCosts(LoadInsightReport r)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int rank = 1;
            foreach (var t in r.topCosts.Take(15))
            {
                EditorGUILayout.BeginHorizontal();

                var swatch = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10), GUILayout.Height(10));
                swatch.y += 3;
                EditorGUI.DrawRect(swatch, CategoryColor(t.category));

                EditorGUILayout.LabelField($"{rank}.", EditorStyles.miniLabel, GUILayout.Width(20));
                string count = t.count > 1 ? $"  ×{t.count}" : "";
                string wait = t.isWait ? "  (wait)" : "";
                EditorGUILayout.LabelField($"{t.label}{count}{wait}", EditorUIStyles.CardLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(FormatMs(t.exclusiveMs), EditorUIStyles.CardValue, GUILayout.Width(78));
                if (t.count > 1)
                    EditorGUILayout.LabelField($"max {FormatMs(t.maxSingleMs)}", EditorStyles.miniLabel, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();
                rank++;
            }
            EditorGUILayout.EndVertical();
        }

        static void DrawAccumulators(LoadInsightReport r)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Per-item stage timing accumulated inside the spans above (e.g. what each of a " +
                "25k-prism lay's stages cost in total).", EditorStyles.miniLabel);
            foreach (var a in r.accumulators.OrderByDescending(a => a.totalMs).Take(16))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(a.label, EditorUIStyles.CardLabel);
                GUILayout.FlexibleSpace();
                float avg = a.count > 0 ? a.totalMs / a.count : 0f;
                EditorGUILayout.LabelField($"×{a.count:N0}", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField($"avg {avg:F2} ms", EditorStyles.miniLabel, GUILayout.Width(86));
                EditorGUILayout.LabelField(FormatMs(a.totalMs), EditorUIStyles.CardValue, GUILayout.Width(78));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        static void DrawHints(LoadInsightReport r)
        {
            if (r.hints.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing flagged — this load looks healthy. 🎉", MessageType.None);
                return;
            }

            foreach (var h in r.hints.OrderByDescending(h => (int)h.severity))
            {
                var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.TintLastRect(rect, EditorUIStyles.ForSeverity(h.severity), 0.14f);

                EditorGUILayout.BeginHorizontal();
                EditorUIStyles.Badge(h.severity.ToString(), EditorUIStyles.ForSeverity(h.severity), 70);
                EditorGUILayout.LabelField(h.title, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(h.finding)) EditorGUILayout.LabelField(h.finding, EditorUIStyles.Wrap);
                if (!string.IsNullOrEmpty(h.fixAdvice)) EditorGUILayout.LabelField($"Fix: {h.fixAdvice}", EditorUIStyles.Wrap);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        static void DrawStalls(LoadInsightReport r)
        {
            if (r.stalls.Count == 0)
            {
                EditorGUILayout.HelpBox($"No frames over {LoadInsights.StallThresholdMs:F0} ms during the load. 🎉", MessageType.None);
                return;
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var st in r.stalls.Take(15))
            {
                var prevC = GUI.contentColor;
                GUI.contentColor = st.durationMs >= 1000f
                    ? new Color(0.97f, 0.45f, 0.45f) : new Color(0.98f, 0.80f, 0.52f);
                EditorGUILayout.LabelField(
                    $"at {st.atMs / 1000f,6:F1}s   {st.durationMs,6:F0} ms frame   during \"{st.during}\"",
                    EditorStyles.miniLabel);
                GUI.contentColor = prevC;
            }
            if (r.stalls.Count > 15)
                EditorGUILayout.LabelField($"   …and {r.stalls.Count - 15} more (see .txt/.json)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        static void DrawErrors(LoadInsightReport r)
        {
            if (r.errors.Count == 0)
            {
                EditorGUILayout.HelpBox("No errors during the load. 🎉", MessageType.None);
                return;
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var e in r.errors.Take(12))
            {
                var prevC = GUI.contentColor;
                GUI.contentColor = e.type == "Exception"
                    ? new Color(0.97f, 0.45f, 0.45f) : new Color(0.98f, 0.80f, 0.52f);
                EditorGUILayout.LabelField($"[{e.timeSeconds:F1}s] {e.type}: {e.message}", EditorUIStyles.Wrap);
                GUI.contentColor = prevC;
            }
            if (r.errors.Count > 12)
                EditorGUILayout.LabelField($"   …and {r.errors.Count - 12} more", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        static void DrawTimeline(LoadInsightReport r)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var events = new List<(float at, int order, string line, Color tint)>(r.marks.Count + r.spans.Count);
            foreach (var m in r.marks)
                events.Add((m.atMs, 0, $"▶ {m.label}", Color.white));
            foreach (var sp in r.spans)
            {
                string indent = new string(' ', Mathf.Min(sp.depth, 8) * 3);
                string flags = (sp.isWait ? " (wait)" : "") + (sp.truncated ? " (open at end)" : "");
                events.Add((sp.startMs, 1,
                    $"{indent}• [{sp.durationMs,7:F0} ms] {sp.label}{flags}",
                    CategoryColor(sp.category)));
            }

            int shown = 0;
            foreach (var e in events.OrderBy(e => e.at).ThenBy(e => e.order))
            {
                if (shown++ >= 90)
                {
                    EditorGUILayout.LabelField($"   …{events.Count - 90} more entries — the .txt/.json has everything", EditorStyles.miniLabel);
                    break;
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{e.at / 1000f,7:F2}s", EditorStyles.miniLabel, GUILayout.Width(52));
                var prevC = GUI.contentColor;
                GUI.contentColor = Color.Lerp(e.tint, Color.white, 0.35f);
                EditorGUILayout.LabelField(e.line, EditorStyles.miniLabel);
                GUI.contentColor = prevC;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        // ── history (past load reports on disk) ─────────

        static void RefreshHistory()
        {
            s_history = new List<HistoryEntry>();
            try
            {
                if (!Directory.Exists(OutputDir)) return;
                foreach (var path in Directory.GetFiles(OutputDir, "load_*.json")
                             .OrderByDescending(File.GetLastWriteTimeUtc)
                             .Take(20))
                {
                    var rep = LoadInsightReport.LoadFromFile(path);
                    if (rep == null) continue;
                    s_history.Add(new HistoryEntry
                    {
                        path = path,
                        scene = ShortSceneName(rep.sceneTo),
                        mode = rep.gameMode,
                        role = rep.isMultiplayer ? rep.networkRole : "SP",
                        date = FormatDate(rep.timestamp),
                        totalMs = rep.totalMs,
                        interrupted = rep.interrupted
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoadInsights] Could not scan report folder: {e.Message}");
            }
        }

        static void DrawHistory()
        {
            EditorGUILayout.Space(6);
            if (!Fold(ref s_foldHistory, $"Past load reports ({s_history?.Count ?? 0})")) return;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshHistory();
            if (GUILayout.Button("Reveal folder", GUILayout.Width(100)) && Directory.Exists(OutputDir))
                EditorUtility.RevealInFinder(OutputDir);
            EditorGUILayout.EndHorizontal();

            if (s_history == null || s_history.Count == 0)
            {
                EditorGUILayout.HelpBox("No saved load reports yet.", MessageType.None);
                return;
            }

            foreach (var h in s_history)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorUIStyles.Badge($"{h.totalMs / 1000f:F1}s", DurationColor(h.totalMs), 56);
                if (h.interrupted) EditorUIStyles.Badge("⚠", EditorUIStyles.Rose, 24);
                EditorGUILayout.LabelField($"{h.scene} · {h.mode} · {h.role}", GUILayout.MinWidth(160));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(h.date, EditorStyles.miniLabel, GUILayout.Width(120));
                if (GUILayout.Button("View", GUILayout.Width(46)))
                    TryLoadReport(h.path);
                if (GUILayout.Button("Del", GUILayout.Width(36)))
                {
                    DeleteReport(h.path);
                    RefreshHistory();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        static void TryLoadReport(string path)
        {
            var rep = LoadInsightReport.LoadFromFile(path);
            if (rep == null) return;
            s_report = rep;
            s_reportPath = path;
            s_adoptedReportId = rep.reportId;   // don't re-adopt LastReport over an explicit pick
            s_pieTextureId = "";                // force pie rebuild
        }

        static void DeleteReport(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                string txt = TxtSibling(path);
                if (File.Exists(txt)) File.Delete(txt);
                if (s_reportPath == path) { s_report = null; s_reportPath = ""; }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoadInsights] Delete failed: {e.Message}");
            }
        }

        // ── helpers ─────────────────────────────────────

        static bool Fold(ref bool state, string title)
        {
            EditorGUILayout.Space(2);
            state = EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
            return state;
        }

        static string TxtSibling(string jsonPath) =>
            string.IsNullOrEmpty(jsonPath) ? "" : Path.ChangeExtension(jsonPath, ".txt");

        static string ShortSceneName(string scene) =>
            string.IsNullOrEmpty(scene) ? "?" : scene.Replace("Minigame", "").Replace("_Gameplay", "");

        static string FormatDate(string isoTimestamp) =>
            isoTimestamp?.Length > 19 ? isoTimestamp[..19].Replace("T", " ") : isoTimestamp ?? "?";

        static string FormatMs(float ms) => ms >= 10_000f ? $"{ms / 1000f:F1} s" : $"{ms:F0} ms";
    }
}
#endif
