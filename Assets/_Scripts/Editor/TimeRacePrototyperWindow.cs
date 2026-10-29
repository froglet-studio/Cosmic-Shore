using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools &gt; Game Modes &gt; Time Race Prototyper - shape, see, save and play the courses of the time races
    /// cut from Slingshot's circuit: the Stoat's Slingshot, the Meerkat's Warpline and Burrow Run (Yash, 2026-10-27:
    /// "create prototypes with various configuration options that I can click and see and also save buttons ... inside
    /// Unity directly"). Edits the one <see cref="TimeRaceCourseConfigSO"/> at
    /// <c>Assets/Resources/TimeRaceCourseConfig.asset</c> (made on the first Save); the course sources read it, and a race
    /// whose course is not switched on flies its shipped one. <c>Docs/TIME_RACE_PROTOTYPER.md</c>.
    ///
    /// <para>The preview is the game's own generator (<see cref="HeadlongCircuit.Generate"/>) on the same settings the
    /// course source hands it, so what the window draws is the course the server lays for that seed. The shell is the
    /// fallback 480..1080 u (the race cell's nucleus is only measured in its scene).</para>
    /// </summary>
    public class TimeRacePrototyperWindow : EditorWindow
    {
        const string ToolName = "Time Race Prototyper";
        const string AssetPath = "Assets/Resources/" + TimeRaceCourseConfigSO.ResourcePath + ".asset";
        const float PreviewInner = RaceCourseSource.DefaultInnerRadiusFallback;
        const float PreviewOuter = RaceCourseSource.DefaultOuterRadius;

        /// <summary>The vessel each race seats and its cruise speed (u/s, its prefab's <c>DefaultThrottleScaler</c>), for the
        /// lap-time estimate only.</summary>
        static readonly (GameModes mode, string vessel, float cruise, float fast, string fastWord)[] Races =
        {
            (GameModes.Slingshot, "Stoat", SlingshotCourse.StoatThrottleScaler, SlingshotCourse.SpeedAtSling(1f), "slung flat out"),
            (GameModes.Warpline, "Meerkat", 60f, 150f, "at a tunnel's throat"),
            (GameModes.BurrowRun, "Meerkat", 60f, 150f, "at a tunnel's throat"),
        };

        static readonly Color Accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes);

        TimeRaceCourseConfigSO _config;
        SerializedObject _so;
        int _race;
        int _intensity = 1;
        int _seed = 7;
        bool _showShipped = true;
        string _presetName = string.Empty;
        string _presetNote = string.Empty;
        Vector2 _scroll;
        string _message = string.Empty;
        FrogletToolShipContext _ship;

        [MenuItem("FrogletTools/Game Modes/Time Race Prototyper", false, 165)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
            Description = "Shape, see, save and play the Stoat's Slingshot and the Meerkat's Warpline / Burrow Run courses, per intensity, with named presets.")]
        static void Open()
        {
            var w = GetWindow<TimeRacePrototyperWindow>("Time Race Prototyper");
            w.minSize = new Vector2(460, 560);
            w.Show();
        }

        void OnEnable()
        {
            _config = AssetDatabase.LoadAssetAtPath<TimeRaceCourseConfigSO>(AssetPath);
            _so = _config ? new SerializedObject(_config) : null;
            _ship = new FrogletToolShipContext(ToolName)
            {
                CommitType = "feat",
                CommitScope = "time-race",
                Validate = Validate,
                CommitSubject = n => $"feat(time-race): prototyped courses from the Time Race Prototyper - {n} file(s)",
            };
        }

        // ── The shipped race, from the game's own numbers ───────────────────────

        static int ShippedLaps(GameModes mode) => mode switch
        {
            GameModes.Slingshot => SlingshotCourseSource.DefaultLaps,
            GameModes.Warpline => WarplineCourseSource.DefaultLaps,
            GameModes.BurrowRun => BurrowRunCourseSource.DefaultLaps,
            _ => 2,
        };

        static int ShippedTarget(GameModes mode)
        {
            var o = EndConditionOverridesSO.Instance;
            return mode switch
            {
                GameModes.Slingshot => o ? o.GetSlingshotGateTarget() : EndConditionOverridesSO.DefaultSlingshotGateTarget,
                GameModes.Warpline => o ? o.GetWarplineGateTarget() : EndConditionOverridesSO.DefaultWarplineGateTarget,
                GameModes.BurrowRun => o ? o.GetBurrowRunGateTarget() : EndConditionOverridesSO.DefaultBurrowRunGateTarget,
                _ => 16,
            };
        }

        static int ShippedRings(GameModes mode) =>
            Mathf.Max(3, Mathf.CeilToInt(ShippedTarget(mode) / (float)Mathf.Max(1, ShippedLaps(mode))));

        // ── Drawing ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Time Race Prototyper",
                "The Stoat's Slingshot and the Meerkat's Warpline: shape each intensity, see the course, save presets, play it.",
                Accent);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRacePicker();
            var mode = Races[_race].mode;

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "No prototyped courses yet: every race flies its shipped course. Start one to shape it here; nothing " +
                    "changes in the game until you switch the course on and Save.", MessageType.Info);
                if (FrogletEditorPalette.ColorButton("Start a prototype from the shipped course", Accent, 300, 26))
                {
                    CreateAsset();
                    EnsureEntry(mode);
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            var entry = EnsureEntry(mode);
            _so.Update();
            var entryProp = EntryProperty(mode);
            if (entryProp == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawStatus(entry, entryProp);
            DrawRaceShape(entryProp, mode);
            DrawIntensity(entryProp, entry, mode);
            _so.ApplyModifiedProperties();

            DrawPreview(entry, mode);
            DrawPresets(entry);
            DrawSaveAndPlay(mode);

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.None);

            EditorGUILayout.EndScrollView();
            FrogletToolShipPanel.Draw(_ship, this);
        }

        void DrawRacePicker()
        {
            GUILayout.Space(6);
            var labels = new string[Races.Length];
            for (int i = 0; i < Races.Length; i++)
                labels[i] = $"{ObjectNames.NicifyVariableName(Races[i].mode.ToString())}  ({Races[i].vessel})";
            _race = EditorGUILayout.Popup(new GUIContent("Race", "Which time race to shape. All three cut Slingshot's circuit."),
                _race, labels);
        }

        void DrawStatus(TimeRaceCourse entry, SerializedProperty entryProp)
        {
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("useThisCourse"),
                    new GUIContent("Use this course in the game",
                        "On: the race flies the laps, rings and circuits below (after Save). Off: its shipped course."));
                var r = GUILayoutUtility.GetRect(110, 18, GUILayout.Width(110));
                if (entry.UseThisCourse)
                    FrogletEditorPalette.StatusPill(r, "PROTOTYPE", FrogletEditorPalette.Warn);
                else
                    FrogletEditorPalette.StatusPill(r, "SHIPPED", FrogletEditorPalette.Ok);
            }
            if (!string.IsNullOrEmpty(entry.ActivePreset))
                EditorGUILayout.LabelField("Preset", entry.ActivePreset, EditorStyles.miniLabel);
        }

        void DrawRaceShape(SerializedProperty entryProp, GameModes mode)
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("The race", FrogletEditorPalette.SectionHeader);
            var laps = entryProp.FindPropertyRelative("laps");
            var rings = entryProp.FindPropertyRelative("ringsPerLap");
            EditorGUILayout.IntSlider(laps, 1, 6, new GUIContent("Laps", "Laps of the ring set that make one race."));
            EditorGUILayout.PropertyField(rings, new GUIContent("Rings a lap",
                "Rings in one lap. Fewer rings make longer legs (Warpline ships 5, Slingshot and Burrow Run 8)."));
            EditorGUILayout.LabelField(" ",
                $"Race: {laps.intValue * rings.intValue} rings threaded.  Shipped: {ShippedLaps(mode)} laps x {ShippedRings(mode)} rings.",
                EditorStyles.miniLabel);
        }

        void DrawIntensity(SerializedProperty entryProp, TimeRaceCourse entry, GameModes mode)
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("Intensity", FrogletEditorPalette.SectionHeader);
            _intensity = GUILayout.Toolbar(_intensity - 1, new[] { "I1", "I2", "I3", "I4" }) + 1;

            var rung = entryProp.FindPropertyRelative("rungs").GetArrayElementAtIndex(_intensity - 1);
            Field(rung, "ringRadius", "Ring mouth radius (u)");
            Field(rung, "baseRadius", "Circuit radius (u)");
            Field(rung, "cornerProfile", "Corner turns (deg)");
            Field(rung, "cornerFloorRadius", "Tightest corner (u)");
            Field(rung, "radialSwing", "Corner swing");
            Field(rung, "angularSpread", "Uneven legs");
            Field(rung, "lateralPerturbation", "Climb and dive (u)");
            Field(rung, "axisJitterDegrees", "Ring twist (deg)");
            Field(rung, "maxPresentDegrees", "Max ring facing (deg)");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton($"Reset I{_intensity} to shipped", FrogletEditorPalette.Slate, 140, 22, outline: true))
                    Mutate(() => entry.ResetRungToShipped(_intensity), $"I{_intensity} is the shipped circuit again.");
                if (FrogletEditorPalette.ColorButton($"Copy I{_intensity} to all", FrogletEditorPalette.Slate, 120, 22, outline: true))
                    Mutate(CopyRungToAll, $"I{_intensity}'s circuit copied to every intensity.");
                if (FrogletEditorPalette.ColorButton("Reset race to shipped", FrogletEditorPalette.Ruby, 150, 22, outline: true))
                    Mutate(() => entry.ResetToShipped(ShippedLaps(mode), ShippedRings(mode)), "The whole race is the shipped one again.");
            }
        }

        static void Field(SerializedProperty rung, string name, string label)
        {
            var p = rung.FindPropertyRelative(name);
            if (p == null) return;
            EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip), true);
        }

        void CopyRungToAll()
        {
            var entry = EnsureEntry(Races[_race].mode);
            var json = JsonUtility.ToJson(entry.Rung(_intensity));
            for (int i = 1; i <= 4; i++)
                if (i != _intensity) JsonUtility.FromJsonOverwrite(json, entry.Rung(i));
        }

        // ── The course, drawn ───────────────────────────────────────────────────

        void DrawPreview(TimeRaceCourse entry, GameModes mode)
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("The course", FrogletEditorPalette.SectionHeader);
            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField(new GUIContent("Seed", "The course seed. A match rolls a fresh one unless its scene pins courseSeed."), _seed);
                if (GUILayout.Button(new GUIContent("New seed", "Roll another course on the same settings."), GUILayout.Width(80)))
                    _seed = Random.Range(1, 100000);
                _showShipped = GUILayout.Toggle(_showShipped, new GUIContent("Shipped too", "Draw the shipped course for this seed, faint, under yours."), GUILayout.Width(95));
            }

            var mine = Generate(entry.Rung(_intensity).ApplyTo(SlingshotCourse.ForIntensity(_intensity)), entry.RingsPerLap);
            var shipped = _showShipped ? Generate(SlingshotCourse.ForIntensity(_intensity), ShippedRings(mode)) : null;

            float w = Mathf.Max(200f, position.width - 30f);
            var plan = GUILayoutUtility.GetRect(w, Mathf.Min(320f, w * 0.75f));
            var side = GUILayoutUtility.GetRect(w, 70f);
            if (Event.current.type == EventType.Repaint)
            {
                Basis(mine, out var u, out var v, out var n);
                DrawPlan(plan, mine, shipped, u, v);
                DrawSide(side, mine, n);
            }

            var race = Races[_race];
            var stats = Measure(mine);
            EditorGUILayout.LabelField("Lap",
                $"{stats.lap:0} u: legs {stats.shortest:0} to {stats.longest:0} u, sharpest turn {stats.sharpest:0} deg, mouths {entry.Rung(_intensity).RingRadius:0} u.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Time",
                $"A {race.vessel} at cruise ({race.cruise:0} u/s): {stats.lap / race.cruise:0} s a lap, {stats.lap * entry.Laps / race.cruise:0} s a race; " +
                $"{race.fastWord} ({race.fast:0} u/s): {stats.lap / race.fast:0} s a lap. Straight-line distance only.",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>The course source's recipe (<c>HeadlongCourseSource.BuildCircuit</c>) on the preview shell, at the window's seed.</summary>
        List<RaceGate> Generate(HeadlongCircuitSettings s, int rings)
        {
            s.InnerRadius = PreviewInner;
            s.OuterRadius = PreviewOuter;
            s.FirstGateDirection = Vector3.up;
            s.GateCount = Mathf.Max(3, rings);
            return HeadlongCircuit.Generate(_seed, s);
        }

        static void Basis(List<RaceGate> gates, out Vector3 u, out Vector3 v, out Vector3 n)
        {
            // Newell's normal of the lap's polygon: the plane the circuit swings in.
            n = Vector3.zero;
            for (int i = 0; i < gates.Count; i++)
            {
                var a = gates[i].Position;
                var b = gates[(i + 1) % gates.Count].Position;
                n += new Vector3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));
            }
            n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
            u = gates.Count > 0 ? Vector3.ProjectOnPlane(gates[0].Position, n) : Vector3.right;
            u = u.sqrMagnitude > 1e-6f ? u.normalized : Vector3.Cross(n, Vector3.forward).normalized;
            v = Vector3.Cross(n, u);
        }

        static void DrawPlan(Rect r, List<RaceGate> mine, List<RaceGate> shipped, Vector3 u, Vector3 v)
        {
            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.4f));
            var c = r.center;
            float scale = (Mathf.Min(r.width, r.height) * 0.5f - 8f) / PreviewOuter;
            Vector3 P(Vector3 p) => new(c.x + Vector3.Dot(p, u) * scale, c.y - Vector3.Dot(p, v) * scale, 0f);

            Handles.color = FrogletEditorPalette.Muted.WithAlpha(0.35f);
            Handles.DrawWireDisc(new Vector3(c.x, c.y), Vector3.forward, PreviewOuter * scale);
            Handles.DrawWireDisc(new Vector3(c.x, c.y), Vector3.forward, PreviewInner * scale);

            if (shipped != null) Loop(shipped, P, FrogletEditorPalette.Muted.WithAlpha(0.45f), 1.5f, false);
            Loop(mine, P, FrogletEditorPalette.Adapt(Accent), 2.5f, true);
            Handles.color = Color.white;
        }

        static void Loop(List<RaceGate> gates, System.Func<Vector3, Vector3> project, Color color, float width, bool rings)
        {
            if (gates == null || gates.Count < 2) return;
            var pts = new Vector3[gates.Count + 1];
            for (int i = 0; i <= gates.Count; i++) pts[i] = project(gates[i % gates.Count].Position);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, pts);
            if (!rings) return;
            float scale = (project(Vector3.right * 100f) - project(Vector3.zero)).magnitude / 100f;
            for (int i = 0; i < gates.Count; i++)
            {
                Handles.color = i == 0 ? FrogletEditorPalette.Adapt(FrogletEditorPalette.Lime) : color;
                Handles.DrawWireDisc(pts[i], Vector3.forward, Mathf.Max(3f, gates[i].Radius * scale));
                GUI.Label(new Rect(pts[i].x + 5f, pts[i].y - 16f, 24f, 16f), (i + 1).ToString(), EditorStyles.miniBoldLabel);
            }
        }

        static void DrawSide(Rect r, List<RaceGate> gates, Vector3 n)
        {
            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.4f));
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width, 16f), "Climb and dive, round the lap", EditorStyles.miniLabel);
            if (gates == null || gates.Count < 2) return;
            float mid = r.center.y + 6f;
            float yScale = (r.height * 0.5f - 12f) / 400f;
            var pts = new Vector3[gates.Count + 1];
            for (int i = 0; i <= gates.Count; i++)
            {
                float x = r.x + 8f + (r.width - 16f) * i / gates.Count;
                pts[i] = new Vector3(x, mid - Vector3.Dot(gates[i % gates.Count].Position, n) * yScale, 0f);
            }
            Handles.color = FrogletEditorPalette.Muted.WithAlpha(0.4f);
            Handles.DrawLine(new Vector3(r.x + 8f, mid), new Vector3(r.xMax - 8f, mid));
            Handles.color = FrogletEditorPalette.Adapt(Accent);
            Handles.DrawAAPolyLine(2f, pts);
            Handles.color = Color.white;
        }

        static (float lap, float shortest, float longest, float sharpest) Measure(List<RaceGate> g)
        {
            if (g == null || g.Count < 3) return (0, 0, 0, 0);
            float lap = 0f, shortest = float.MaxValue, longest = 0f, sharpest = 0f;
            for (int i = 0; i < g.Count; i++)
            {
                var a = g[i].Position;
                var b = g[(i + 1) % g.Count].Position;
                var c = g[(i + 2) % g.Count].Position;
                float leg = Vector3.Distance(a, b);
                lap += leg;
                shortest = Mathf.Min(shortest, leg);
                longest = Mathf.Max(longest, leg);
                sharpest = Mathf.Max(sharpest, Vector3.Angle(b - a, c - b));
            }
            return (lap, shortest, longest, sharpest);
        }

        // ── Presets ─────────────────────────────────────────────────────────────

        void DrawPresets(TimeRaceCourse entry)
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("Presets", FrogletEditorPalette.SectionHeader);
            _presetName = EditorGUILayout.TextField(new GUIContent("Name", "Saving under a name that exists replaces it."), _presetName);
            _presetNote = EditorGUILayout.TextField(new GUIContent("Note", "What this course is for (optional)."), _presetNote);
            if (FrogletEditorPalette.ColorButton("Save as preset", Accent, 140, 22, enabled: !string.IsNullOrWhiteSpace(_presetName)))
                Mutate(() => entry.SavePreset(_presetName, _presetNote), $"Saved preset '{_presetName.Trim()}'. Press Save to keep it.");

            if (entry.Presets.Count == 0)
            {
                EditorGUILayout.LabelField("No presets yet.", EditorStyles.miniLabel);
                return;
            }

            string load = null, delete = null;
            foreach (var p in entry.Presets)
            {
                if (p == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = $"{p.Name}  ({p.Laps} x {p.RingsPerLap}, {p.SavedAt})";
                    EditorGUILayout.LabelField(new GUIContent(label, p.Note), p.Name == entry.ActivePreset ? EditorStyles.boldLabel : EditorStyles.label);
                    if (GUILayout.Button("Load", GUILayout.Width(52))) load = p.Name;
                    if (GUILayout.Button("Delete", GUILayout.Width(56))) delete = p.Name;
                }
            }
            if (load != null) Mutate(() => entry.LoadPreset(load), $"Loaded preset '{load}'. Press Save to race it.");
            if (delete != null) Mutate(() => entry.DeletePreset(delete), $"Deleted preset '{delete}'. Press Save to keep that.");
        }

        // ── Save and play ───────────────────────────────────────────────────────

        void DrawSaveAndPlay(GameModes mode)
        {
            FrogletEditorPalette.HorizontalRule();
            bool dirty = _config && EditorUtility.IsDirty(_config);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton(dirty ? "Save *" : "Save", FrogletEditorPalette.Ok, 110, 28,
                        "Write the courses and presets to Assets/Resources/TimeRaceCourseConfig.asset."))
                    Save();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (FrogletEditorPalette.ColorButton("Save and Play", FrogletEditorPalette.Azure, 130, 28,
                            "Save, then enter Play mode (from Bootstrap). Open Arcade, pick the race and the intensity."))
                    {
                        Save();
                        _message = $"Playing: open Arcade > {ObjectNames.NicifyVariableName(mode.ToString())} and pick intensity {_intensity}.";
                        EditorApplication.EnterPlaymode();
                    }
                }
            }
            EditorGUILayout.LabelField(" ",
                "A course switched off is saved too, and flies only once it is on. Play mode reads the saved file.",
                EditorStyles.miniLabel);
        }

        void Save()
        {
            if (!_config) return;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            _message = $"Saved {AssetPath}.";
        }

        // ── The asset ───────────────────────────────────────────────────────────

        void CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            _config = CreateInstance<TimeRaceCourseConfigSO>();
            AssetDatabase.CreateAsset(_config, AssetPath);
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            _so = new SerializedObject(_config);
        }

        TimeRaceCourse EnsureEntry(GameModes mode)
        {
            if (_config.Find(mode) is { HasRungs: true } found) return found;
            Undo.RecordObject(_config, "Prototype a time race");
            var entry = _config.GetOrAdd(mode, ShippedLaps(mode), ShippedRings(mode));
            if (!entry.HasRungs) entry.ResetToShipped(ShippedLaps(mode), ShippedRings(mode));
            EditorUtility.SetDirty(_config);
            _so = new SerializedObject(_config);
            return entry;
        }

        SerializedProperty EntryProperty(GameModes mode)
        {
            var list = _so.FindProperty("courses");
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("mode").intValue == (int)mode) return e;
            }
            return null;
        }

        /// <summary>A change made in C# (a preset, a reset), with undo, then the serialized view refreshed.</summary>
        void Mutate(System.Action change, string message)
        {
            _so.ApplyModifiedProperties();
            Undo.RecordObject(_config, ToolName);
            change();
            EditorUtility.SetDirty(_config);
            _so.Update();
            _message = message;
            GUI.FocusControl(null);
        }

        FrogletToolValidation Validate()
        {
            if (!_config) return FrogletToolValidation.Pass("No prototyped courses: every race flies its shipped course.");
            var problems = new List<string>();
            foreach (var c in _config.Courses)
            {
                if (c == null) continue;
                if (!c.HasRungs) problems.Add($"{c.Mode}: missing an intensity's circuit (Reset race to shipped).");
                if (c.Laps * c.RingsPerLap < 3) problems.Add($"{c.Mode}: a race of fewer than 3 rings.");
            }
            return problems.Count == 0
                ? FrogletToolValidation.Pass($"{_config.Courses.Count} race(s) prototyped.")
                : FrogletToolValidation.Fail("Fix the prototyped courses first.", problems);
        }
    }
}
