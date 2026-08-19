using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The scarab-wing dais, drawn from its own numbers.
    ///
    /// <para>The rosette is closed-form geometry with a dozen coupled dials — move the sun radius
    /// and the spar length follows it, move the hinge width and the wrap follows that — so the only
    /// honest way to author it is to see the shape and the checks together. This window runs the
    /// SHIPPED <see cref="ScarabWingDais"/> (never a preview copy of it), draws every prism at its
    /// real silhouette (a rectangle per plain/danger blade, a RHOMBUS per shielded one, the
    /// stella's eight-point hull per sun — the distinction that got the sun core sized 73% too big
    /// once), runs the same overlap / confinement / reach / wrap checks the edit-mode tests do, and
    /// writes the result into <c>PlaceSwitchAction.asset</c>.</para>
    ///
    /// <para>Reader-plus-writer, so it carries the ship contract (<c>Docs/TOOLING.md</c>): the
    /// asset it writes is the deliverable, and it lands in the working tree rather than the
    /// branch.</para>
    /// </summary>
    public sealed class ScarabWingDaisLab : EditorWindow
    {
        const string ToolName = "Scarab Wing Dais Lab";
        const string ActionAssetPath = "Assets/_SO_Assets/VesselActions/Scarab/PlaceSwitchAction.asset";

        ScarabWingDaisSettings _settings = ScarabWingDaisSettings.Default;
        float _ringRadius = 20f;
        bool _showTiers = true;
        bool _showSectors;
        bool _showRing = true;
        Vector2 _scroll;
        string _message = string.Empty;
        bool _messageIsError;

        readonly List<ScarabWingDais.Element> _elements = new();
        Report _report;

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            CommitType = "feat",
            CommitScope = "scarab",
            CommitSubject = _ => "feat(scarab): retune the wing dais",
        };

        [MenuItem("FrogletTools/Vessels/Scarab Wing Dais Lab", false, 42)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Draw the Scarab switch's wing dais from its own dials, check that nothing " +
                          "overlaps and that every pair stays in its sector, then bake it into PlaceSwitchAction.")]
        public static void Open()
        {
            var w = GetWindow<ScarabWingDaisLab>("Dais Lab");
            w.minSize = new Vector2(460f, 620f);
            w.Show();
        }

        void OnEnable()
        {
            LoadFromAsset(quiet: true);
            Rebuild();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // What the rosette is doing, in numbers
        // ─────────────────────────────────────────────────────────────────────────
        struct Report
        {
            public int Prisms, Plain, Danger, Shielded, Suns;
            public int Overlaps, OutOfSector;
            public float Inner, Outer, Wrap, SunClearance, BoxVolume;
            public float LongestBlade, ShortestBlade;

            public bool Clean => Overlaps == 0 && OutOfSector == 0 && SunClearance > 0f;
        }

        void Rebuild()
        {
            ScarabWingDais.Generate(_settings, Vector3.zero, Vector3.forward, Vector3.right,
                                    Vector3.up, _ringRadius, _elements);
            _report = Measure();
            Repaint();
        }

        Report Measure()
        {
            var r = new Report
            {
                Prisms = _elements.Count,
                Inner = ScarabWingDais.InnerReach(_settings, _ringRadius),
                Outer = ScarabWingDais.OuterReach(_settings, _ringRadius),
                Wrap = ScarabWingDais.WrapDegrees(_settings, _ringRadius),
                SunClearance = ScarabWingDais.SunClearance(_settings, _ringRadius),
                ShortestBlade = float.MaxValue,
            };

            var sil = new List<Vector2[]>(_elements.Count);
            var box = new List<Vector4>(_elements.Count);   // AABB prefilter: this runs O(n^2)
            foreach (var e in _elements)
            {
                var poly = Silhouette(e);
                sil.Add(poly);
                Vector4 b = new(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
                foreach (var q in poly)
                {
                    b.x = Mathf.Min(b.x, q.x); b.y = Mathf.Min(b.y, q.y);
                    b.z = Mathf.Max(b.z, q.x); b.w = Mathf.Max(b.w, q.y);
                }
                box.Add(b);
            }

            float half = 180f / Mathf.Max(1, _settings.PairCount);
            for (int i = 0; i < _elements.Count; i++)
            {
                var e = _elements[i];
                switch (e.Kind)
                {
                    case PrismKind.Plain: r.Plain++; break;
                    case PrismKind.Danger: r.Danger++; break;
                    case PrismKind.Shielded: r.Shielded++; break;
                    default: r.Suns++; break;
                }
                r.BoxVolume += e.Scale.x * e.Scale.y * e.Scale.z;
                if (!e.IsSunCore)
                {
                    float len = e.Scale.z / (e.Kind == PrismKind.Shielded ? ScarabWingDais.ShieldedFit : 1f);
                    r.LongestBlade = Mathf.Max(r.LongestBlade, len);
                    r.ShortestBlade = Mathf.Min(r.ShortestBlade, len);

                    float pairAngle = e.Pair * 360f / Mathf.Max(1, _settings.PairCount);
                    foreach (var q in sil[i])
                        if (Mathf.Abs(Mathf.DeltaAngle(pairAngle, Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg)) > half + 0.05f)
                        { r.OutOfSector++; break; }
                }

                for (int j = i + 1; j < _elements.Count; j++)
                {
                    Vector4 a = box[i], b2 = box[j];
                    if (a.z < b2.x || b2.z < a.x || a.w < b2.y || b2.w < a.y) continue;
                    if (!Separated(sil[i], sil[j])) r.Overlaps++;
                }
            }
            if (r.ShortestBlade == float.MaxValue) r.ShortestBlade = 0f;
            return r;
        }

        /// <summary>The outline a prism actually presents, which is the SHIELD mesh and not the
        /// box — the two tiers have the same axis extent and different apparent size.</summary>
        static Vector2[] Silhouette(ScarabWingDais.Element e)
        {
            Vector3 fwd = e.Rotation * Vector3.forward;
            Vector2 p = new(e.Position.x, e.Position.y);
            Vector2 d = new Vector2(fwd.x, fwd.y).normalized;
            Vector2 n = new(-d.y, d.x);
            float hw = e.Scale.x * 0.5f, hl = e.Scale.z * 0.5f;

            if (e.Kind == PrismKind.Shielded)
            {
                float a = hl * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                float b = hw * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                return new[] { p + d * a, p + n * b, p - d * a, p - n * b };
            }
            if (e.Kind == PrismKind.SuperShielded)
            {
                float a = hw * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                float r = a * Mathf.Sqrt(2f);
                var poly = new Vector2[8];
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4f;
                    poly[i] = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ((i % 2 == 1) ? r : a);
                }
                return poly;
            }
            return new[] { p + d * hl + n * hw, p + d * hl - n * hw, p - d * hl - n * hw, p - d * hl + n * hw };
        }

        static bool Separated(Vector2[] a, Vector2[] b)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var poly = pass == 0 ? a : b;
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 edge = poly[(i + 1) % poly.Length] - poly[i];
                    Vector2 axis = new(-edge.y, edge.x);
                    if (axis.sqrMagnitude < 1e-12f) continue;
                    axis = axis.normalized;
                    float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                    foreach (var q in a) { float t = Vector2.Dot(axis, q); a0 = Mathf.Min(a0, t); a1 = Mathf.Max(a1, t); }
                    foreach (var q in b) { float t = Vector2.Dot(axis, q); b0 = Mathf.Min(b0, t); b1 = Mathf.Max(b1, t); }
                    if (a1 < b0 - 1e-5f || b1 < a0 - 1e-5f) return true;
                }
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            FrogletEditorPalette.Banner("Scarab Wing Dais",
                "Ten suns, each cradled by a wing pair that begins at the switch ring.",
                FrogletEditorPalette.Coral);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawPreview();
            DrawReport();
            DrawDials();
            DrawActions();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageIsError ? MessageType.Error : MessageType.Info);

            FrogletToolShipPanel.Draw(Ship, this);
            EditorGUILayout.EndScrollView();
        }

        void DrawPreview()
        {
            Rect box = GUILayoutUtility.GetRect(10f, 10000f, 300f, 300f);
            FrogletEditorPalette.DrawCard(box, FrogletEditorPalette.Surface,
                                          FrogletEditorPalette.Muted.WithAlpha(0.35f));
            if (_elements.Count == 0) return;

            float reach = Mathf.Max(_report.Outer, _ringRadius) * 1.05f;
            float scale = Mathf.Min(box.width, box.height) * 0.5f / Mathf.Max(0.01f, reach);
            Vector2 origin = box.center;
            Vector2 Project(Vector2 q) => new(origin.x + q.x * scale, origin.y - q.y * scale);

            Handles.BeginGUI();
            if (_showSectors)
            {
                Handles.color = FrogletEditorPalette.Muted.WithAlpha(0.35f);
                for (int p = 0; p < _settings.PairCount; p++)
                {
                    float a = (p + 0.5f) * Mathf.PI * 2f / _settings.PairCount;
                    Handles.DrawLine(Project(Vector2.zero),
                                     Project(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach));
                }
            }
            if (_showRing)
            {
                Handles.color = FrogletEditorPalette.HeaderText.WithAlpha(0.8f);
                var ring = new Vector3[65];
                for (int i = 0; i < ring.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / (ring.Length - 1);
                    ring[i] = Project(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _ringRadius);
                }
                Handles.DrawPolyLine(ring);
            }

            foreach (var e in _elements)
            {
                Handles.color = _showTiers ? TierColor(e.Kind) : FrogletEditorPalette.Azure;
                var poly = Silhouette(e);
                var pts = new Vector3[poly.Length + 1];
                for (int i = 0; i < poly.Length; i++) pts[i] = Project(poly[i]);
                pts[poly.Length] = pts[0];
                Handles.DrawAAConvexPolygon(pts);
            }
            Handles.EndGUI();
        }

        static Color TierColor(PrismKind kind) => kind switch
        {
            PrismKind.Danger => FrogletEditorPalette.Coral,
            PrismKind.Shielded => FrogletEditorPalette.Cyan,
            PrismKind.SuperShielded => FrogletEditorPalette.Ruby,
            _ => FrogletEditorPalette.Azure,
        };

        void DrawReport()
        {
            EditorGUILayout.Space(4f);
            var r = _report;
            Rect pill = GUILayoutUtility.GetRect(10f, 10000f, 20f, 20f);
            FrogletEditorPalette.StatusPill(pill,
                r.Clean ? "CLEAN — nothing overlaps, every pair inside its sector"
                        : $"{r.Overlaps} overlap(s), {r.OutOfSector} blade(s) out of sector",
                r.Clean ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);

            EditorGUILayout.LabelField(
                $"{r.Prisms} prisms   ({r.Plain} plain / {r.Danger} danger / {r.Shielded} shielded / {r.Suns} suns)",
                FrogletEditorPalette.CardBody);
            EditorGUILayout.LabelField(
                $"band {r.Inner:F1} → {r.Outer:F1}   (ring {_ringRadius:F0})   wrap {r.Wrap:F0}°   " +
                $"sun clearance {r.SunClearance:F1}",
                FrogletEditorPalette.CardBody);
            EditorGUILayout.LabelField(
                $"blades {r.ShortestBlade:F1} → {r.LongestBlade:F1} long   box volume {r.BoxVolume:F0}",
                FrogletEditorPalette.CardBody);

            // The three readings that are decisions rather than measurements.
            if (r.Inner > _ringRadius * 1.25f)
                EditorGUILayout.HelpBox(
                    $"The wings begin {r.Inner:F0} out from a ring of {_ringRadius:F0} — there is a void " +
                    "around the switch. Lower WingRootReach, or raise SunRadius so the spar has room to " +
                    "grow back in.", MessageType.Warning);
            if (r.Inner < _ringRadius)
                EditorGUILayout.HelpBox(
                    "The dais reaches inside the ring the ball threads. Raise WingRootReach.",
                    MessageType.Warning);
            if (r.Wrap < 180f)
                EditorGUILayout.HelpBox(
                    $"A pair only wraps {r.Wrap:F0}° of its sun — that reads as a fan beside it, not a " +
                    "cradle around it. Add blades, or widen the hinges.", MessageType.Warning);
            if (r.SunClearance <= 0f)
                EditorGUILayout.HelpBox(
                    "The sun's in-plane spikes reach past the ring of blade roots — the wings are growing " +
                    "through their own sun. Raise WingHoleRadius or shrink SunApparentDiameter. (Remember " +
                    "the authored number is the APPARENT diameter: a stella's spikes point at the cube's " +
                    "corners, so its sphere is √3 wider than its box.)", MessageType.Error);

            EditorGUILayout.LabelField(
                "Cell ladder: a cell this vessel plays in wants its *EnterVolume authored off the box " +
                "volume above, not the ×16 count derivation.", EditorStyles.miniLabel);
        }

        void DrawDials()
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUI.BeginChangeCheck();

            _ringRadius = EditorGUILayout.Slider(
                new GUIContent("Ring radius (preview)",
                    "The switch's own radius. Everything below is a multiple of it, so this only " +
                    "changes the preview's scale — it is not authored here."),
                _ringRadius, 5f, 60f);

            EditorGUILayout.LabelField("Rosette", FrogletEditorPalette.SectionLabel);
            _settings.PairCount = EditorGUILayout.IntSlider("Pair count", _settings.PairCount, 3, 24);
            _settings.BladesPerWing = EditorGUILayout.IntSlider("Blades per wing", _settings.BladesPerWing, 4, 32);
            _settings.HingeEvery = EditorGUILayout.IntSlider("Hinge every", _settings.HingeEvery, 2, 8);

            EditorGUILayout.LabelField("Wing (× ring radius)", FrogletEditorPalette.SectionLabel);
            _settings.SunRadius = EditorGUILayout.FloatField("Sun radius", _settings.SunRadius);
            _settings.WingHoleRadius = EditorGUILayout.FloatField("Wing hole radius", _settings.WingHoleRadius);
            _settings.WingRootReach = EditorGUILayout.FloatField("Wing root reach", _settings.WingRootReach);
            _settings.WingHalfGapDeg = EditorGUILayout.FloatField("Wing half gap (deg)", _settings.WingHalfGapDeg);
            _settings.BladeGapDeg = EditorGUILayout.FloatField("Blade gap (deg)", _settings.BladeGapDeg);
            _settings.SectorMargin = EditorGUILayout.FloatField("Sector margin", _settings.SectorMargin);

            EditorGUILayout.LabelField("Blades", FrogletEditorPalette.SectionLabel);
            _settings.BladeTipLength = EditorGUILayout.FloatField("Tip length", _settings.BladeTipLength);
            _settings.BladeMinLength = EditorGUILayout.FloatField("Min length", _settings.BladeMinLength);
            _settings.BladeTaper = EditorGUILayout.FloatField("Taper", _settings.BladeTaper);
            _settings.BladeWidthStart = EditorGUILayout.FloatField("Width start", _settings.BladeWidthStart);
            _settings.BladeWidthEnd = EditorGUILayout.FloatField("Width end", _settings.BladeWidthEnd);
            _settings.BladeWidthShape = EditorGUILayout.FloatField("Width shape", _settings.BladeWidthShape);
            _settings.HingeWidthScale = EditorGUILayout.FloatField("Hinge width scale", _settings.HingeWidthScale);
            _settings.BladeThickness = EditorGUILayout.FloatField("Thickness", _settings.BladeThickness);

            EditorGUILayout.LabelField("Sun core & dish", FrogletEditorPalette.SectionLabel);
            _settings.SunApparentDiameter = EditorGUILayout.FloatField(
                new GUIContent("Sun apparent diameter",
                    "What you SEE — the sphere the stella's corner spikes reach, √3 wider than its box."),
                _settings.SunApparentDiameter);
            _settings.DishRise = EditorGUILayout.FloatField("Dish rise", _settings.DishRise);
            _settings.DishPower = EditorGUILayout.FloatField("Dish power", _settings.DishPower);

            EditorGUILayout.LabelField("Preview", FrogletEditorPalette.SectionLabel);
            _showTiers = EditorGUILayout.Toggle("Colour by tier", _showTiers);
            _showRing = EditorGUILayout.Toggle("Draw the switch ring", _showRing);
            _showSectors = EditorGUILayout.Toggle("Draw sector boundaries", _showSectors);

            if (EditorGUI.EndChangeCheck()) Rebuild();
        }

        void DrawActions()
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.BeginHorizontal();
            if (FrogletEditorPalette.ColorButton("Reset to shipped", FrogletEditorPalette.Slate, 140f))
            {
                _settings = ScarabWingDaisSettings.Default;
                Say("Back to the shipped motif.", false);
                Rebuild();
            }
            if (FrogletEditorPalette.ColorButton("Load from asset", FrogletEditorPalette.Info, 140f))
            {
                LoadFromAsset(quiet: false);
                Rebuild();
            }
            using (new EditorGUI.DisabledScope(!_report.Clean))
            {
                if (FrogletEditorPalette.ColorButton("Write to PlaceSwitchAction", FrogletEditorPalette.Ok, 200f))
                    WriteToAsset();
            }
            EditorGUILayout.EndHorizontal();
            if (!_report.Clean)
                EditorGUILayout.LabelField(
                    "Writing is blocked while the rosette overlaps itself — that is the one property the " +
                    "dais promises the arena.", EditorStyles.miniLabel);
        }

        void LoadFromAsset(bool quiet)
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ActionAssetPath);
            if (so == null)
            {
                if (!quiet) Say($"No asset at {ActionAssetPath}.", true);
                return;
            }
            var prop = new SerializedObject(so).FindProperty("dais");
            if (prop == null)
            {
                if (!quiet) Say("That asset has no `dais` field.", true);
                return;
            }
            _settings = ReadSettings(prop);
            if (!quiet) Say("Loaded the shipped asset.", false);
        }

        static ScarabWingDaisSettings ReadSettings(SerializedProperty p)
        {
            var s = ScarabWingDaisSettings.Default;
            s.PairCount = p.FindPropertyRelative("PairCount").intValue;
            s.BladesPerWing = p.FindPropertyRelative("BladesPerWing").intValue;
            s.HingeEvery = p.FindPropertyRelative("HingeEvery").intValue;
            s.SunRadius = p.FindPropertyRelative("SunRadius").floatValue;
            s.WingHoleRadius = p.FindPropertyRelative("WingHoleRadius").floatValue;
            s.WingRootReach = p.FindPropertyRelative("WingRootReach").floatValue;
            s.WingHalfGapDeg = p.FindPropertyRelative("WingHalfGapDeg").floatValue;
            s.BladeGapDeg = p.FindPropertyRelative("BladeGapDeg").floatValue;
            s.SectorMargin = p.FindPropertyRelative("SectorMargin").floatValue;
            s.BladeTipLength = p.FindPropertyRelative("BladeTipLength").floatValue;
            s.BladeMinLength = p.FindPropertyRelative("BladeMinLength").floatValue;
            s.BladeTaper = p.FindPropertyRelative("BladeTaper").floatValue;
            s.BladeWidthStart = p.FindPropertyRelative("BladeWidthStart").floatValue;
            s.BladeWidthEnd = p.FindPropertyRelative("BladeWidthEnd").floatValue;
            s.BladeWidthShape = p.FindPropertyRelative("BladeWidthShape").floatValue;
            s.HingeWidthScale = p.FindPropertyRelative("HingeWidthScale").floatValue;
            s.BladeThickness = p.FindPropertyRelative("BladeThickness").floatValue;
            s.SunApparentDiameter = p.FindPropertyRelative("SunApparentDiameter").floatValue;
            s.DishRise = p.FindPropertyRelative("DishRise").floatValue;
            s.DishPower = p.FindPropertyRelative("DishPower").floatValue;
            return s;
        }

        void WriteToAsset()
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ActionAssetPath);
            if (so == null) { Say($"No asset at {ActionAssetPath}.", true); return; }

            var serialized = new SerializedObject(so);
            var p = serialized.FindProperty("dais");
            if (p == null) { Say("That asset has no `dais` field.", true); return; }

            p.FindPropertyRelative("PairCount").intValue = _settings.PairCount;
            p.FindPropertyRelative("BladesPerWing").intValue = _settings.BladesPerWing;
            p.FindPropertyRelative("HingeEvery").intValue = _settings.HingeEvery;
            p.FindPropertyRelative("SunRadius").floatValue = _settings.SunRadius;
            p.FindPropertyRelative("WingHoleRadius").floatValue = _settings.WingHoleRadius;
            p.FindPropertyRelative("WingRootReach").floatValue = _settings.WingRootReach;
            p.FindPropertyRelative("WingHalfGapDeg").floatValue = _settings.WingHalfGapDeg;
            p.FindPropertyRelative("BladeGapDeg").floatValue = _settings.BladeGapDeg;
            p.FindPropertyRelative("SectorMargin").floatValue = _settings.SectorMargin;
            p.FindPropertyRelative("BladeTipLength").floatValue = _settings.BladeTipLength;
            p.FindPropertyRelative("BladeMinLength").floatValue = _settings.BladeMinLength;
            p.FindPropertyRelative("BladeTaper").floatValue = _settings.BladeTaper;
            p.FindPropertyRelative("BladeWidthStart").floatValue = _settings.BladeWidthStart;
            p.FindPropertyRelative("BladeWidthEnd").floatValue = _settings.BladeWidthEnd;
            p.FindPropertyRelative("BladeWidthShape").floatValue = _settings.BladeWidthShape;
            p.FindPropertyRelative("HingeWidthScale").floatValue = _settings.HingeWidthScale;
            p.FindPropertyRelative("BladeThickness").floatValue = _settings.BladeThickness;
            p.FindPropertyRelative("SunApparentDiameter").floatValue = _settings.SunApparentDiameter;
            p.FindPropertyRelative("DishRise").floatValue = _settings.DishRise;
            p.FindPropertyRelative("DishPower").floatValue = _settings.DishPower;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, ActionAssetPath);
            Say($"Wrote {_report.Prisms} prisms' worth of dais into PlaceSwitchAction. " +
                "Ship it below — the asset is the deliverable, not this window.", false);
        }

        void Say(string message, bool isError)
        {
            _message = message;
            _messageIsError = isError;
        }
    }
}
