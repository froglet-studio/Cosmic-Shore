using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// The Vessel Studio in Unity (<c>Docs/Studios/VESSEL_STUDIO_PLAN.md</c>, <c>/vessel-studio</c> D21 and D32).
    ///
    /// <para><b>Home</b> (what FrogletTools ▸ Vessels ▸ Vessel Studio opens): the web hub's front page, the "Vessel
    /// Studio" heading and one card per studio in <c>Docs/Studios/VesselStudio/studios.json</c>, each with the hub's
    /// own looping preview (<see cref="StudioPreviews"/>). A card opens THAT studio built from this checkout and served by
    /// Amoebius (<see cref="LaunchPrisma.OpenStudio"/>, /vessel-studio D33): the artifact's own build, so it looks and plays
    /// exactly as on claude.ai, with Sync, Ask and Decisions working. Unity has no web view, so the studio itself is a browser app window, never an IMGUI copy.</para>
    ///
    /// <para><b>Tune in Unity</b> (a card's second button, for a vessel that has a page here): the web studio's six
    /// settings tabs, Scene Config · Game Config · AI Config · Play Style Config · Input · Vessel Config, over the
    /// vessel's REAL assets, so a number is tuned while the game runs and is the project's the moment it moves. No
    /// inspector: every row is a slider with the field's own tooltip, and a STUDIO column beside it shows the web
    /// studio's value for that row (read from the studio's own file, <c>StoatFlightStudio.html</c>'s <c>SHIPPED</c>
    /// block) with one click to adopt it.</para>
    ///
    /// <para><b>Live.</b> The dipole config and the black-hole config are read every frame by the game, so an edit
    /// lands on the next frame. The camera asset is applied when a vessel spawns; the window re-applies it to every
    /// gameplay camera using it, so it is live too. Edits made in Play mode stay (they are asset edits).</para>
    ///
    /// <para><b>Shipping.</b> The window is a writer: every asset it changes is recorded on
    /// <see cref="FrogletToolChangeLedger"/>, and the ship panel at the bottom commits and pushes ONLY those
    /// (<c>Docs/TOOLING.md</c> § "Tool output is a deliverable"). The Stoat's generator
    /// (<c>Tools/Build/author_stoat_assets.py</c>) seeds these assets once and then leaves their numbers to this
    /// window.</para>
    /// </summary>
    public sealed partial class VesselStudioWindow : EditorWindow
    {
        const string ToolName = "Vessel Studio (Unity)";
        const string StudioUrl = "https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa";
        const string StoatStudioFile = "Docs/Studios/StoatFlightStudio.html";
        const string DipoleConfigPath = "Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset";
        const string CameraPath = "Assets/_SO_Assets/Camera/StoatCameraSettingsSO.asset";
        const string BlackHolePath = "Assets/Resources/BlackHoleConfig.asset";
        const string TabPref = "CosmicShore.VesselStudio.Tab";

        static readonly string[] Tabs = { "Scene Config", "Game Config", "AI Config", "Play Style Config", "Input", "Vessel Config" };

        /// <summary>One slider: an asset's serialized field, its label, and (optionally) the web studio's key for it.</summary>
        sealed class Row
        {
            public readonly string Asset, Field, Label, StudioKey;
            public readonly float StudioScale;
            public Row(string asset, string field, string label, string studioKey = null, float studioScale = 1f)
            {
                Asset = asset; Field = field; Label = label; StudioKey = studioKey; StudioScale = studioScale;
            }
        }

        sealed class Section
        {
            public readonly string Title, Note;
            public readonly Row[] Rows;
            public Section(string title, string note, params Row[] rows) { Title = title; Note = note; Rows = rows; }
        }

        // The Stoat's page. A new vessel adds its own page the same way (its assets, the same six tabs).
        static readonly Dictionary<int, Section[]> StoatTabs = new()
        {
            [0] = new[]
            {
                new Section("Chase camera", "The studio's chase: 6.5 up, 21 behind, looking 40 past the nose, eased at 7/s, framed for 68°.",
                    new Row(CameraPath, "followOffset", "Offset (x, up, back)"),
                    new Row(CameraPath, "lookAheadDistance", "Look ahead (u)"),
                    new Row(CameraPath, "lookAheadLift", "Look lift (u)"),
                    new Row(CameraPath, "chaseEaseRate", "Ease (/s)"),
                    new Row(CameraPath, "framingFieldOfView", "Framed for FOV (°)")),
                new Section("Black hole and white hole look", "The studio's lens: black shadow, warm photon ring, white-hot core.",
                    new Row(BlackHolePath, "photonRingGlow", "Photon ring glow", "bhRingGlow"),
                    new Row(BlackHolePath, "photonRingWidth", "Photon ring width", "bhRingWidth"),
                    new Row(BlackHolePath, "whiteCoreBrightness", "White core brightness", "whCoreBrightness"),
                    new Row(BlackHolePath, "whiteCoreSkyMix", "White core sky mix", "whCoreSkyMix"),
                    new Row(BlackHolePath, "lensRadiusMultiplier", "Lens reach (× r_s)", "bhLensReach"),
                    new Row(BlackHolePath, "lensFadeStart", "Lens fade start", "bhLensFade"),
                    new Row(DipoleConfigPath, "domainTintAmount", "Owner domain tint (0 = studio)")),
            },
            [1] = new[]
            {
                new Section("Session", "Simulation speed and the windows that go with this one (Play mode)."),
            },
            [2] = new[]
            {
                new Section("Field AI (the path-watching autopilot)", "The studio's Field AI rows.",
                    new Row(DipoleConfigPath, "autopilotWatchPath", "Watch the path"),
                    new Row(DipoleConfigPath, "autopilotHold01", "Squeeze", "aiWarpQ"),
                    new Row(DipoleConfigPath, "autopilotLetGoNear", "Let go this close (u)", "aiNear"),
                    new Row(DipoleConfigPath, "autopilotDrySeconds", "Let go after unwarped (s)", "aiLimeWait"),
                    new Row(DipoleConfigPath, "autopilotMinHoldSeconds", "Min hold (s)"),
                    new Row(DipoleConfigPath, "autopilotMaxHoldSeconds", "Max hold (s)"),
                    new Row(DipoleConfigPath, "autopilotRelaySeconds", "Re-lay after (s)"),
                    new Row(DipoleConfigPath, "autopilotMaxOrbitDegrees", "Max orbit (°)")),
            },
            [3] = new[]
            {
                new Section("Field dipole: where the poles go", "Laid ahead in the frame you had at the press; sideways = the triggers' difference, lengthways = their sum.",
                    new Row(DipoleConfigPath, "aheadDistance", "Laid ahead (u)", "ftAhead"),
                    new Row(DipoleConfigPath, "sidewaysMax", "Sideways, one full trigger (u)", "ftSepMax"),
                    new Row(DipoleConfigPath, "lengthwaysMax", "Lengthways, both full (u)", "ftSepLong"),
                    new Row(DipoleConfigPath, "followRate", "Poles follow the trigger (/s)", "ftSepFollow")),
                new Section("Field dipole: the poles", null,
                    new Row(DipoleConfigPath, "poleGM", "Pole GM (studio strength × 20 000)", "ftStrength", 20000f),
                    new Row(DipoleConfigPath, "poleHorizon", "Black hole horizon (u)", "ftHorizon"),
                    new Row(DipoleConfigPath, "sourcePush", "White hole push (× pull)", "ftWhite"),
                    new Row(DipoleConfigPath, "poleSize", "Pole size (Space)"),
                    new Row(DipoleConfigPath, "sinkGrowSeconds", "Black hole grows in (s)", "bornS"),
                    new Row(DipoleConfigPath, "sourceGrowSeconds", "White hole grows in (s)", "whInS")),
                new Section("Field flight", null,
                    new Row(DipoleConfigPath, "grip", "Grip", "ftGrip"),
                    new Row(DipoleConfigPath, "turnCap", "Fastest bend (rad/s)", "dpTurnCap"),
                    new Row(DipoleConfigPath, "accelerationCap", "Force ceiling (u/s²)", "dpAccelCap"),
                    new Row(DipoleConfigPath, "gravitySpeedCeilingCruises", "Gravity speed ceiling (× cruise)"),
                    new Row(DipoleConfigPath, "boost", "Warp boost (Time)"),
                    new Row(DipoleConfigPath, "boostRise", "Boost rise (/s)", "ftRise"),
                    new Row(DipoleConfigPath, "boostFadeSeconds", "Boost fade (s)", "ftFade")),
            },
            [4] = new[]
            {
                new Section("Triggers", "A key squeezes to Key Squeeze at once (the studio's); a gamepad trigger is its own depth.",
                    new Row(DipoleConfigPath, "holdExponent", "Squeeze curve", "holdExponent"),
                    new Row(DipoleConfigPath, "keySqueeze", "Key squeeze", "dpKeySqueeze"),
                    new Row(DipoleConfigPath, "holdRampSeconds", "Ramp when Key Squeeze is 0 (s)")),
            },
            [5] = new[]
            {
                new Section("Pathfinder: the predicted path", null,
                    new Row(DipoleConfigPath, "pathLength", "Path length (u)", "ftLength"),
                    new Row(DipoleConfigPath, "pathStep", "Step (u)", "ftStep"),
                    new Row(DipoleConfigPath, "pathNoseOffset", "Starts past the nose (u)", "ftNose"),
                    new Row(DipoleConfigPath, "loopMargin", "Loop margin (u)", "ftMargin"),
                    new Row(DipoleConfigPath, "minLoop", "Shortest loop (u)", "ftMinLoop"),
                    new Row(DipoleConfigPath, "warpDegrees", "Warped at a bend of (°)", "ftWarpDeg")),
                new Section("Pathfinder: the dots", "3D: dots in the scene along the path. Off: the studio's flat screen dots.",
                    new Row(DipoleConfigPath, "dotsInWorld", "Dots in the 3D scene"),
                    new Row(DipoleConfigPath, "worldDotSize", "3D dot size (u)"),
                    new Row(DipoleConfigPath, "worldDotSpacing", "3D dot spacing (u)"),
                    new Row(DipoleConfigPath, "worldDotMinPixels", "3D dot smallest (px)"),
                    new Row(DipoleConfigPath, "dotPixels", "Screen dot size (px)", "ftDotPx"),
                    new Row(DipoleConfigPath, "dotGap", "Screen dot gap (× size)", "ftDotGap"),
                    new Row(DipoleConfigPath, "openColor", "Open colour"),
                    new Row(DipoleConfigPath, "warpedColor", "Warped colour")),
            },
        };

        /// <summary>The studios with a Tune in Unity page, by studios.json id.</summary>
        static readonly Dictionary<string, Dictionary<int, Section[]>> Tuners = new() { ["stoat"] = StoatTabs };

        readonly Dictionary<string, SerializedObject> _assets = new();
        Dictionary<string, float> _studio = new();
        string _studioError;
        int _tab;
        Vector2 _scroll;
        FrogletToolShipContext _ship;

        /// <summary>Null = the home page; otherwise the studios.json id whose Tune in Unity page is open.</summary>
        [SerializeField] string _view;

        [MenuItem("FrogletTools/Vessels/Vessel Studio", false, 0)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 5,
            Description = "The Vessel Studio home: every studio as a card with its live preview, as on the web hub. A card " +
                          "opens that studio (the artifact's own pages, from this checkout) in its own window; Tune in Unity " +
                          "puts the studio's six tabs over the vessel's real assets, live while you play.",
            DocPath = "Docs/Studios/VESSEL_STUDIO_PLAN.md")]
        public static void Open() => OpenOn(null);

        /// <summary>Opens the window on the home page (<paramref name="tuner"/> null) or on a studio's Tune in Unity page.</summary>
        static void OpenOn(string tuner)
        {
            var w = GetWindow<VesselStudioWindow>();
            w.titleContent = new GUIContent("Vessel Studio");
            w.minSize = new Vector2(420f, 360f);
            w._view = tuner;
            w.Show();
            w.Focus();
        }

        void OnEnable()
        {
            _tab = Mathf.Clamp(EditorPrefs.GetInt(TabPref, 3), 0, Tabs.Length - 1);
            _ship = new FrogletToolShipContext(ToolName)
            {
                CommitType = "tune",
                CommitScope = "stoat",
                Validate = Validate,
            };
            wantsMouseMove = true;
            LoadStudio();
            LoadHome();
            EditorApplication.update += Animate;
        }

        void OnDisable()
        {
            EditorApplication.update -= Animate;
            foreach (var tex in _previewTex.Values)
                if (tex) DestroyImmediate(tex);
            _previewTex.Clear();
        }

        void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying && !string.IsNullOrEmpty(_view)) Repaint();
        }

        SerializedObject Asset(string path)
        {
            if (_assets.TryGetValue(path, out var so) && so != null && so.targetObject) return so;
            var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            so = obj ? new SerializedObject(obj) : null;
            _assets[path] = so;
            return so;
        }

        void OnGUI()
        {
            if (_view == null || !Tuners.TryGetValue(_view, out var tabs)) { _view = null; DrawHome(); return; }

            DrawBanner();
            int tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(24f));
            if (tab != _tab) { _tab = tab; EditorPrefs.SetInt(TabPref, tab); }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tab == 1) DrawSession();
            if (tabs.TryGetValue(_tab, out var sections))
                foreach (var s in sections) DrawSection(s);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            FrogletToolShipPanel.Draw(_ship, this);
        }

        void DrawBanner()
        {
            var r = EditorGUILayout.GetControlRect(false, 46f);
            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.SurfaceRaised, FrogletEditorPalette.Violet.WithAlpha(0.6f));
            FrogletEditorPalette.DrawAccentStripe(r, FrogletEditorPalette.Violet);
            if (FrogletEditorPalette.ColorButton(new Rect(r.x + 10f, r.y + 11f, 84f, 24f), "◂ Studios", FrogletEditorPalette.Slate,
                    "Back to the Vessel Studio home"))
            {
                _view = null;
                GUIUtility.ExitGUI();
            }
            string name = StudioName(_view).ToUpperInvariant();
            GUI.Label(new Rect(r.x + 104f, r.y + 4f, r.width - 330f, 20f), $"VESSEL STUDIO · {name} · TUNE IN UNITY", FrogletEditorPalette.Title);
            string state = EditorApplication.isPlaying ? "LIVE: edits land next frame" : "Edit mode: edits land on the assets";
            GUI.Label(new Rect(r.x + 104f, r.y + 24f, r.width - 330f, 18f), state, FrogletEditorPalette.Subtitle);
            if (FrogletEditorPalette.ColorButton(new Rect(r.xMax - 206f, r.y + 11f, 96f, 24f), "Open studio", FrogletEditorPalette.Azure,
                    "Open this vessel's studio page (the artifact's own page, from this checkout) in its own window"))
                OpenStudio(_view);
            if (FrogletEditorPalette.ColorButton(new Rect(r.xMax - 104f, r.y + 11f, 96f, 24f), "Reload studio", FrogletEditorPalette.Slate,
                    "Re-read the web studio's numbers from " + StoatStudioFile))
                LoadStudio();
            if (!string.IsNullOrEmpty(_studioError)) EditorGUILayout.HelpBox(_studioError, MessageType.Warning);
        }

        void DrawSession()
        {
            EditorGUILayout.Space(4f);
            GUILayout.Label("Session", FrogletEditorPalette.SectionHeader);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                float ts = EditorGUILayout.Slider(new GUIContent("Simulation speed", "Time.timeScale while playing (the studio's Speed)."),
                    Time.timeScale, 0.1f, 4f);
                if (!Mathf.Approximately(ts, Time.timeScale)) Time.timeScale = ts;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Third Eye", "Watch any pilot from a second camera: Chase / Follow / Free.")))
                    EditorApplication.ExecuteMenuItem("FrogletTools/AI/Third Eye");
                if (GUILayout.Button(new GUIContent("Select my Stoat", "Ping the Stoat hull in the running scene.")))
                    SelectStoat();
            }
        }

        static void SelectStoat()
        {
            foreach (var status in FindObjectsByType<VesselStatus>(FindObjectsSortMode.None))
                if (status && status.VesselType == VesselClassType.Stoat)
                {
                    Selection.activeGameObject = status.gameObject;
                    EditorGUIUtility.PingObject(status.gameObject);
                    return;
                }
        }

        void DrawSection(Section s)
        {
            EditorGUILayout.Space(6f);
            GUILayout.Label(s.Title, FrogletEditorPalette.SectionHeader);
            if (!string.IsNullOrEmpty(s.Note)) GUILayout.Label(s.Note, FrogletEditorPalette.CardBodyWrapped);
            foreach (var row in s.Rows) DrawRow(row);
        }

        void DrawRow(Row row)
        {
            var so = Asset(row.Asset);
            if (so == null) { EditorGUILayout.HelpBox($"{row.Asset} is missing.", MessageType.Error); return; }
            so.Update();
            var prop = so.FindProperty(row.Field);
            if (prop == null)
            {
                EditorGUILayout.LabelField(row.Label, $"(no field '{row.Field}' on {Path.GetFileName(row.Asset)})");
                return;
            }

            bool hasStudio = row.StudioKey != null && _studio.TryGetValue(row.StudioKey, out _);
            float studio = hasStudio ? _studio[row.StudioKey] * row.StudioScale : 0f;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, new GUIContent(row.Label, prop.tooltip), true);
                bool changed = EditorGUI.EndChangeCheck();

                if (hasStudio && (prop.propertyType == SerializedPropertyType.Float || prop.propertyType == SerializedPropertyType.Integer))
                {
                    float now = prop.propertyType == SerializedPropertyType.Float ? prop.floatValue : prop.intValue;
                    bool same = Mathf.Abs(now - studio) <= 1e-4f * Mathf.Max(1f, Mathf.Abs(studio));
                    var accent = same ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn;
                    string label = "studio " + studio.ToString("0.###", CultureInfo.InvariantCulture);
                    if (FrogletEditorPalette.ColorButton(label, accent, 104f, 18f,
                            same ? "Matches the web studio" : "Use the web studio's value") && !same)
                    {
                        if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = studio;
                        else prop.intValue = Mathf.RoundToInt(studio);
                        changed = true;
                    }
                }
                else GUILayout.Space(108f);

                if (changed) Commit(so, row.Asset);
            }
        }

        void Commit(SerializedObject so, string path)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            FrogletToolChangeLedger.Record(ToolName, path);
            if (path == CameraPath && EditorApplication.isPlaying) ReapplyCamera(so.targetObject as CameraSettingsSO);
        }

        /// <summary>The camera asset is applied at spawn; re-apply it to every camera flying with it, so it is live.</summary>
        static void ReapplyCamera(CameraSettingsSO settings)
        {
            if (!settings) return;
            foreach (var cam in FindObjectsByType<CustomCameraController>(FindObjectsSortMode.None))
                if (cam && cam.CurrentSettings == settings) cam.ApplySettings(settings);
        }

        // ------------------------------------------------------------------ the web studio's numbers

        void LoadStudio()
        {
            _studioError = null;
            string file = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", StoatStudioFile);
            try { _studio = ParseShipped(File.ReadAllText(file)); }
            catch (Exception e)
            {
                _studio = new Dictionary<string, float>();
                _studioError = $"Could not read the web studio's numbers from {StoatStudioFile}: {e.Message}";
            }
            if (_studioError == null && _studio.Count == 0)
                _studioError = $"{StoatStudioFile} has no SHIPPED block - the studio column is empty.";
        }

        /// <summary>The numeric rows of the studio's <c>const SHIPPED = { … }</c> literal, by key. Pure, tested.</summary>
        public static Dictionary<string, float> ParseShipped(string html)
        {
            var result = new Dictionary<string, float>();
            int start = html.IndexOf("const SHIPPED = {", StringComparison.Ordinal);
            if (start < 0) return result;
            int end = html.IndexOf("\n  };", start, StringComparison.Ordinal);
            if (end < 0) return result;
            string block = html.Substring(start, end - start);
            foreach (Match m in Regex.Matches(block, @"\b([A-Za-z_]\w*)\s*:\s*(-?\d+(?:\.\d+)?(?:[eE]-?\d+)?)\b"))
                if (!result.ContainsKey(m.Groups[1].Value) &&
                    float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    result[m.Groups[1].Value] = v;
            return result;
        }

        FrogletToolValidation Validate()
        {
            var problems = new List<string>();
            foreach (var path in new[] { DipoleConfigPath, CameraPath, BlackHolePath })
                if (!AssetDatabase.LoadAssetAtPath<ScriptableObject>(path)) problems.Add($"{path} is missing");
            return problems.Count == 0
                ? FrogletToolValidation.Pass("Stoat tuning assets present")
                : FrogletToolValidation.Fail("Missing tuning assets", problems);
        }
    }
}
