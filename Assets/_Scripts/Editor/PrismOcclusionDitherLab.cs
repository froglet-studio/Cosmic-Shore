using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>FrogletTools ▸ Ecology ▸ Prism Animation ▸ Occlusion Dither Lab.</b>
    ///
    /// <para>The design surface for the camera↔vessel occlusion corridor's dither — the
    /// unit shape the player sees thousands of times right next to their ship
    /// (Docs/PRISM_ANIMATION.md §4.7). Every dial in <c>PrismOcclusionCorridor.hlsl</c> is a
    /// compile-time constant, which is right for shipping and useless for CHOOSING: you
    /// cannot slide a <c>#define</c> while flying. This window promotes the whole dial set
    /// to two global uniforms and drives them live — <b>including in play mode</b>, so the
    /// look can be judged in motion against real trail mass, which is the only place a
    /// dither can actually be judged.</para>
    ///
    /// <para><b>Three things make it a lab rather than a slider panel:</b></para>
    /// <list type="number">
    /// <item><b>The preview is the shipped GPU code.</b> It renders through
    /// <c>Hidden/CosmicShore/PrismOcclusionDitherPreview</c>, which includes the corridor's
    /// own HLSL and calls the same <c>PrismOcclusionDitherThreshold</c> dispatch reading the
    /// same globals. A preview that re-implemented the kernels in C# could drift from the
    /// game; this one cannot, and a kernel added to the corridor appears here for free.</item>
    /// <item><b>Measure runs the real metric.</b> The corridor's admission rule is
    /// |coverage − alpha| under ~0.01 — the number that decides whether the SHORT gradient
    /// band reads as a fade or as an edge. Measure renders the raw threshold+alpha pair to a
    /// float target, reads it back, and computes that metric over actual rendered output —
    /// the same methodology as the in-situ numbers in the shader's comments. It measures the
    /// shipped Worley baseline in the same pass, so the verdict is a RATIO and cannot be
    /// fooled by anything about this harness. Sliders that let you silently break a platform
    /// law would be a worse tool than no sliders.</item>
    /// <item><b>Bake closes the loop.</b> Design mode is not free — it compiles all five
    /// kernels into every prism shader and allocates registers for the largest. Bake writes
    /// the chosen values into the constants and flips <c>PRISM_OCCLUSION_LIVE_TUNING</c> to
    /// 0, so the cost lasts exactly as long as the design session and nobody has to
    /// hand-transcribe numbers out of a screenshot.</item>
    /// </list>
    ///
    /// <para>This is a KEEPER tool (a re-runnable design surface, not a one-off migration),
    /// so it declares no <c>ToolScriptPaths</c> and the ship panel's Retire button hides
    /// itself. It still draws the panel, because Bake WRITES an asset —
    /// <c>PrismOcclusionCorridor.hlsl</c> — and per Docs/TOOLING.md a tool's output is the
    /// deliverable.</para>
    /// </summary>
    public class PrismOcclusionDitherLab : EditorWindow
    {
        const string ToolName = "Occlusion Dither Lab";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl";
        const string PreviewShader = "Hidden/CosmicShore/PrismOcclusionDitherPreview";

        // ── The kernels, mirrored from the HLSL's #defines ────────────────────────
        static readonly string[] KernelNames = { "IGN (dissolve)", "Spiral (iris)", "Worley (round flecks)", "Shard (triangles)", "Shatter (cracked lattice)", "Shatter3D (volumetric lattice)" };
        static readonly string[] KernelTokens = { "PRISM_OCCLUSION_KERNEL_IGN", "PRISM_OCCLUSION_KERNEL_SPIRAL", "PRISM_OCCLUSION_KERNEL_WORLEY", "PRISM_OCCLUSION_KERNEL_SHARD", "PRISM_OCCLUSION_KERNEL_SHATTER", "PRISM_OCCLUSION_KERNEL_SHATTER3D" };
        const int KernelIgn = 0, KernelSpiral = 1, KernelWorley = 2, KernelShard = 3, KernelShatter = 4, KernelShatter3D = 5;

        static readonly string[] OrientNames = { "Fixed (one heading)", "Flip (up + down)", "Spin (free rotation)" };
        static readonly string[] OrientTokens = { "PRISM_OCCLUSION_SHARD_FIXED", "PRISM_OCCLUSION_SHARD_FLIP", "PRISM_OCCLUSION_SHARD_SPIN" };

        // The measured windows from Docs/PRISM_ANIMATION.md §4.7. Sliders span a little
        // WIDER than the window on purpose — the failure has to be reachable and visible,
        // or "inside the window" means nothing — and everything outside is flagged.
        const float CellMin = 2.5f, CellMax = 14f, CellGoodLo = 4.5f, CellGoodHi = 11f;
        const float ShatterCellMin = 4f, ShatterCellMax = 24f, ShatterCellGoodLo = 8f, ShatterCellGoodHi = 18f;
        const float ShatterWallMin = 2f, ShatterWallMax = 28f;

        // The wall window is RELATIVE to the polygon, not absolute. The Lab shipped with a
        // flat 4–11 px band lifted from a sweep taken at a FIXED 11 px polygon, and it
        // promptly flagged a perfectly good setting (16.26 px polygon / 20 px wall) as out
        // of window — that combination measures 0.0102 corridor, better than the shipped
        // Worley baseline's 0.0117. What actually fails is a wall wide relative to its own
        // polygon (11 px polygon / 18 px wall = 1.64× → 0.0173), because there is no
        // lattice left to crack. Measured: 0.75× → 0.0063, 1.00× → 0.0094, 1.23× → 0.0102,
        // 1.30× → 0.0162. Flag past 1.25× and say "measure it", rather than "it is wrong".
        const float ShatterWallRatioMax = 1.25f;
        const float MorphMax = 0.35f, MorphCeiling = 0.25f;

        // The two layered-beat dials. Depth phase is capped low on purpose: the measured
        // conflict (see PrismOcclusionCorridor.hlsl) puts useful decorrelation ~50x above
        // the speed budget, so the slider's job is to let you SEE that, not to invite a
        // shipped value. Back-face power runs to 6, past which interiors are gone before
        // the exterior fade has really started.
        const float DepthPhaseMax = 0.05f, DepthPhaseCeiling = 0.002f;
        const float BackFacePowerMin = 1f, BackFacePowerMax = 6f;

        // ── Live dial state (EditorWindow fields survive domain reloads) ──────────
        [SerializeField] int _kernel = KernelShatter;
        [SerializeField] float _cellSize = 6f;
        [SerializeField] int _shardOrient = 0;
        [SerializeField] float _morphRate = 0.12f;
        [SerializeField] float _shatterCell = 12f;
        [SerializeField] float _shatterWall = 9f;
        [SerializeField] float _shatter3dCell = 12f;
        [SerializeField] float _shatter3dWall = 1.2f;
        [SerializeField] float _spiralRings = 9f;
        [SerializeField] int _spiralArms = 3;
        [SerializeField] float _depthPhase = 0f;
        [SerializeField] float _backFacePower = 3f;
        [SerializeField] bool _driveGame = true;
        [SerializeField] bool _animatePreview = true;

        // ── Preview / measurement state ──────────────────────────────────────────
        [SerializeField] float _previewInner = 0.25f, _previewOuter = 1f, _previewCore = 0f, _previewExtent = 1.15f;
        [SerializeField] float _rampLo = 0.04f, _rampHi = 0.8f;
        [SerializeField] Vector2 _scroll;

        Material _mat;
        RenderTexture _rtCorridor, _rtRamp;
        double _lastPublish, _previewClock;
        string _measureLine = string.Empty;
        string _measureDetail = string.Empty;
        Color _measureColor = Color.gray;
        string _message = string.Empty;
        bool _messageIsError;

        static readonly int DitherA = Shader.PropertyToID("_PrismOcclusionDitherA");
        static readonly int DitherB = Shader.PropertyToID("_PrismOcclusionDitherB");
        static readonly int DitherC = Shader.PropertyToID("_PrismOcclusionDitherC");

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            CommitType = "feat",
            CommitScope = "prisms",
            CommitSubject = _ => "feat(prisms): bake the occlusion corridor's dither look",
            Validate = ValidateSource,
        };

        [MenuItem("FrogletTools/Ecology/Prism Animation/Occlusion Dither Lab", false, 34)]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Slide the occlusion corridor's dither shape live (works in play mode), " +
                          "measure its coverage fidelity against the shipped baseline, then bake it into the shader.")]
        public static void Open()
        {
            var w = GetWindow<PrismOcclusionDitherLab>("Dither Lab");
            w.minSize = new Vector2(440f, 560f);
            w.Show();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────────
        void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Publish();
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            // Closing the Lab must hand the look back to the shader's own constants — a
            // stale global outliving the window would silently misrepresent what the
            // committed source says, which is exactly the confusion this tool exists to end.
            Unpublish();
            ReleaseResources();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Entering play mode re-creates the render pipeline; re-publish so the dials the
            // window is showing are the dials the running game is using.
            if (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode)
                Publish();
        }

        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            // Re-publish on a slow heartbeat rather than every frame: shader globals persist
            // until overwritten, so this only has to beat scene loads and pipeline resets.
            if (_driveGame && now - _lastPublish > 0.25)
            {
                Publish();
                _lastPublish = now;
            }

            if (_animatePreview && _morphRate > 0f)
            {
                _previewClock = now;
                Repaint();
            }
        }

        void ReleaseResources()
        {
            if (_mat != null) { DestroyImmediate(_mat); _mat = null; }
            if (_rtCorridor != null) { _rtCorridor.Release(); DestroyImmediate(_rtCorridor); _rtCorridor = null; }
            if (_rtRamp != null) { _rtRamp.Release(); DestroyImmediate(_rtRamp); _rtRamp = null; }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Publishing — the same two-globals-per-frame shape the corridor itself uses
        // ─────────────────────────────────────────────────────────────────────────
        Vector4 PackA() => new Vector4(_kernel + 1, _cellSize, _shardOrient, _morphRate);
        Vector4 PackB() => new Vector4(_shatterCell, _shatterWall, _spiralRings, _spiralArms);
        Vector4 PackC() => new Vector4(_depthPhase, _backFacePower, _shatter3dCell, _shatter3dWall);

        void Publish()
        {
            // x carries kernel+1 so that an unpublished global (all zeros) reads as "nobody
            // is driving" and every dial in the shader falls back to its constant.
            Shader.SetGlobalVector(DitherA, PackA());
            Shader.SetGlobalVector(DitherB, PackB());
            Shader.SetGlobalVector(DitherC, PackC());
        }

        static void Unpublish() => Shader.SetGlobalVector(DitherA, Vector4.zero);

        Material Mat
        {
            get
            {
                if (_mat == null)
                {
                    var shader = Shader.Find(PreviewShader);
                    if (shader == null) return null;
                    _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return _mat;
            }
        }

        RenderTexture EnsureRT(ref RenderTexture rt, int w, int h)
        {
            if (rt != null && (rt.width != w || rt.height != h))
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }
            if (rt == null)
            {
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                rt.Create();
            }
            return rt;
        }

        void RenderPreview(RenderTexture rt, int mode)
        {
            var m = Mat;
            if (m == null || rt == null) return;

            // The preview reads the SAME globals the game does, so it is showing the dials,
            // not a copy of them.
            Publish();

            m.SetVector("_PreviewRect", new Vector4(rt.width, rt.height, mode,
                _animatePreview ? (float)_previewClock : 0f));
            m.SetVector("_PreviewProfile", new Vector4(_previewInner, _previewOuter, _previewCore, _previewExtent));
            m.SetVector("_PreviewRamp", new Vector4(_rampLo, _rampHi, 0f, 0f));

            var prev = RenderTexture.active;
            Graphics.Blit(Texture2D.whiteTexture, rt, m);
            RenderTexture.active = prev;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            FrogletEditorPalette.Banner("Occlusion Dither Lab",
                "The unit shape of the corridor's dissolve — live, measured, bakeable.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Ecology));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSourceState();
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            DrawDials();
            if (EditorGUI.EndChangeCheck())
            {
                Publish();
                _measureLine = string.Empty;   // any dial change invalidates the last measurement
                _measureDetail = string.Empty;
            }

            EditorGUILayout.Space(6f);
            DrawPreviews();
            EditorGUILayout.Space(6f);
            DrawMeasure();
            EditorGUILayout.Space(6f);
            DrawBake();

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(_message, _messageIsError ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.Space(8f);
            FrogletToolShipPanel.Draw(Ship, this);

            EditorGUILayout.EndScrollView();
        }

        void DrawSourceState()
        {
            if (!TryReadTuningFlag(out bool live))
            {
                EditorGUILayout.HelpBox(
                    "Could not read PRISM_OCCLUSION_LIVE_TUNING from the shader, so the Lab does not know " +
                    "whether the dials below are driving anything. Run Validate & Push's checks — if the " +
                    "anchors have drifted, fix them in PrismOcclusionDitherLab.cs.", MessageType.Error);
                return;
            }

            if (!live)
            {
                EditorGUILayout.HelpBox(
                    "PRISM_OCCLUSION_LIVE_TUNING is 0 in the shader, so the dials below do nothing — " +
                    "the corridor is compiled against its constants (which is the correct SHIPPED state). " +
                    "Set it to 1 to design, and Bake to return here.", MessageType.Warning);
                if (GUILayout.Button("Enable design mode in the shader"))
                    SetTuningFlag(true);
                return;
            }

            EditorGUILayout.HelpBox(
                "Design mode is ON: all five kernels are compiled into every prism shader and the dials " +
                "below drive the game live. That costs GPU occupancy — bake and turn it off before shipping.",
                MessageType.Info);

            _driveGame = EditorGUILayout.ToggleLeft(
                new GUIContent("Drive the running game",
                    "Publishes the dials as shader globals every quarter second, so play mode shows them live. " +
                    "Off leaves the game on the shader's own constants while you keep previewing here."),
                _driveGame);
        }

        void DrawDials()
        {
            EditorGUILayout.LabelField("Kernel", FrogletEditorPalette.SectionHeader);
            _kernel = EditorGUILayout.Popup(new GUIContent("Shape"), _kernel, KernelNames);

            EditorGUILayout.Space(2f);
            switch (_kernel)
            {
                case KernelShard:
                    _shardOrient = EditorGUILayout.Popup(new GUIContent("Orientation",
                        "Fixed is the most legible AS a triangle; Flip and Spin scatter the heading and " +
                        "read progressively more as generic angular splinters."), _shardOrient, OrientNames);
                    DialSlider("Triangle pitch", ref _cellSize, CellMin, CellMax, CellGoodLo, CellGoodHi, "px",
                        $"equal-area triangle ≈ {(2f * 0.77756f * _cellSize):0.#} px tall",
                        "Lattice pitch, which IS the triangle size. Below ~4.5 px the shape falls under the " +
                        "pixel floor; past ~11 px too few cells span the gradient band and the fade reads as " +
                        "a chunky edge. The CDF fit is scale-invariant, so nothing needs re-fitting in between.");
                    break;

                case KernelWorley:
                    DialSlider("Fleck pitch", ref _cellSize, CellMin, CellMax, CellGoodLo, CellGoodHi, "px",
                        $"round fleck ≈ {_cellSize:0.#} px across", "Same lattice as Shard, round metric.");
                    break;

                case KernelShatter:
                    DialSlider("Polygon size", ref _shatterCell, ShatterCellMin, ShatterCellMax,
                        ShatterCellGoodLo, ShatterCellGoodHi, "px", null,
                        "The Voronoi cell size — how big each facet of the cracked lattice is.");

                    // The wall window is RELATIVE, not absolute — see ShatterWallRatioMax.
                    _shatterWall = EditorGUILayout.Slider(new GUIContent("Wall period",
                        "The band repeat. This is the one kernel whose wall thickness is authorable " +
                        "independently of its cell size — but only within a ratio of it: a wall wider " +
                        "than its own polygon has no lattice left to crack."),
                        _shatterWall, ShatterWallMin, ShatterWallMax);
                    float ratio = _shatterWall / Mathf.Max(_shatterCell, 1e-3f);
                    bool wallOutside = ratio > ShatterWallRatioMax || _shatterWall < ShatterWallMin;
                    string wallNote = wallOutside
                        ? "past " + ShatterWallRatioMax.ToString("0.00") + "x, measure before trusting it"
                        : "in window";
                    EditorGUILayout.LabelField(" ",
                        ratio.ToString("0.00") + "x the polygon - " + wallNote,
                        Warn(wallOutside));
                    EditorGUILayout.LabelField(" ", "at alpha a the dark wall is (1−a) × period wide",
                        EditorStyles.miniLabel);
                    break;

                case KernelShatter3D:
                    EditorGUILayout.HelpBox("REJECTED ON LOOK (2026-08-10, the day it shipped): a volumetric " +
                        "crack plane lying near-parallel to a viewed surface makes a face-sized plate share one " +
                        "threshold — plate-flashes that read as glitchy clipping around the vessel. Carried for " +
                        "a possible 3D-SHARD successor; do not re-ship as-is.", MessageType.Warning);
                    DialSlider("Cell size (angular)", ref _shatter3dCell, ShatterCellMin, ShatterCellMax,
                        ShatterCellGoodLo, ShatterCellGoodHi, "px", null,
                        "The IDEAL on-screen size of a volumetric cell. The lattice lives on a power-of-two " +
                        "ladder of WORLD sizes and each fragment picks the rung nearest this angular target, " +
                        "so rendered cells sweep roughly 0.7-1.4x this value across depth. Coverage is exact " +
                        "by construction at any scale; the window is about legibility, same as 2D Shatter.");
                    _shatter3dWall = EditorGUILayout.Slider(new GUIContent("Wall period (x cell)",
                        "The band repeat as a RATIO of the cell — the same relative window as 2D Shatter: " +
                        "past ~1.25x there is no lattice left to crack."),
                        _shatter3dWall, 0.3f, 1.6f);
                    EditorGUILayout.LabelField(" ",
                        _shatter3dWall > ShatterWallRatioMax
                            ? $"past {ShatterWallRatioMax:0.00}x — measure before trusting it"
                            : "in window",
                        Warn(_shatter3dWall > ShatterWallRatioMax));
                    EditorGUILayout.LabelField(" ",
                        "world-anchored: no strobe at speed, true parallax between stacked layers; " +
                        "the preview shows its z = 0 slice",
                        EditorStyles.miniLabel);
                    break;

                case KernelSpiral:
                    _spiralRings = EditorGUILayout.Slider(new GUIContent("Bands across radius"), _spiralRings, 2f, 24f);
                    _spiralArms = EditorGUILayout.IntSlider(new GUIContent("Arms (turns/rev)",
                        "MUST stay an integer: atan2's ±π seam jumps the phase by exactly one turn, which " +
                        "frac() erases only for an integer count. A fractional count leaves a radial scar."),
                        _spiralArms, 1, 8);
                    break;

                case KernelIgn:
                    EditorGUILayout.HelpBox("IGN has no shape dials — it is a low-discrepancy hash. It also " +
                                            "cannot morph: advancing a hash resamples it per pixel rather than " +
                                            "moving it, which is full-amplitude shimmer.", MessageType.None);
                    break;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Motion", FrogletEditorPalette.SectionHeader);
            using (new EditorGUILayout.HorizontalScope())
            {
                _morphRate = EditorGUILayout.Slider(new GUIContent("Morph rate",
                    "Pattern evolution in cycles/sec. 0 freezes it. Past ~0.25 it reads as noise rather " +
                    "than flow. Coverage fidelity is independent of this, so it cannot break the fade."),
                    _morphRate, 0f, MorphMax);
                GUILayout.Label(_morphRate > MorphCeiling ? "reads as noise" :
                                _morphRate <= 0f ? "frozen" : $"1 cycle / {(1f / Mathf.Max(_morphRate, 1e-4f)):0} s",
                    Warn(_morphRate > MorphCeiling), GUILayout.Width(120f));
            }
            if (_kernel == KernelIgn && _morphRate > 0f)
                EditorGUILayout.HelpBox("IGN ignores the morph rate by design.", MessageType.None);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Layered beat", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                "Two surfaces stacked along one camera ray read the SAME screen-anchored threshold, so " +
                "their clip contours sit a pixel apart and moire-beat. These two dials attack the two " +
                "preconditions: decorrelate the pattern between layers, or stop both layers being " +
                "mid-fade at once.", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _depthPhase = EditorGUILayout.Slider(new GUIContent("Depth band phase",
                    "Shatter only. Shifts each cell's WALL by view depth (the lattice itself stays put). " +
                    "Coverage-neutral. Measured dead end: separating a prism's own two faces needs ~50x " +
                    "the rate the speed budget allows, so it ships at 0 — the slider is here to show you " +
                    "that, not to be turned up."),
                    _depthPhase, 0f, DepthPhaseMax);
                GUILayout.Label(_depthPhase <= 0f ? "off" :
                                _depthPhase > DepthPhaseCeiling ? "flickers at speed" : "within budget",
                    Warn(_depthPhase > DepthPhaseCeiling), GUILayout.Width(130f));
            }
            if (_depthPhase > DepthPhaseCeiling)
                EditorGUILayout.LabelField(" ",
                    $"~{_depthPhase * 5f / 0.02f * 17.9f:0.#}% of band pixels flip per frame at 300 u/s " +
                    "(ceiling ~1.45%)", Warn(true));
            if (_kernel != KernelShatter && _depthPhase > 0f)
                EditorGUILayout.HelpBox("Only the Shatter kernel reads the depth band phase.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                _backFacePower = EditorGUILayout.Slider(new GUIContent("Back-face sharpen",
                    "alpha^power on surfaces facing AWAY from the camera — prisms render two-sided, so " +
                    "the beat's usual second layer is a prism's own interior. Sharpening it drops the " +
                    "interior out of the gradient band while the exterior is still dissolving, which " +
                    "removes the interference rather than scrambling it. No temporal cost. 1 = off. " +
                    "The trade is a look change: interiors vanish earlier, so a mid-fade prism reads as " +
                    "a thinner shell."),
                    _backFacePower, BackFacePowerMin, BackFacePowerMax);
                GUILayout.Label(_backFacePower <= 1.001f ? "off" : $"interior gone by a={ApproxInteriorGone(_backFacePower):0.00}",
                    Warn(false), GUILayout.Width(160f));
            }
            EditorGUILayout.LabelField(" ",
                "both dials only show on real stacked mass in play mode — the preview is a single flat layer",
                EditorStyles.miniLabel);
        }

        /// <summary>Alpha at which a back face has fully left the gradient band (alpha^p &lt;= 0.08),
        /// the readout that makes the look trade concrete while dragging.</summary>
        static float ApproxInteriorGone(float power) => Mathf.Pow(0.08f, 1f / Mathf.Max(power, 1e-3f));

        void DialSlider(string label, ref float value, float min, float max, float goodLo, float goodHi,
                        string unit, string readout, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.Slider(new GUIContent(label, tooltip), value, min, max);
                bool outside = value < goodLo || value > goodHi;
                GUILayout.Label(outside ? $"outside {goodLo:0.#}–{goodHi:0.#} {unit}" : $"in window ({goodLo:0.#}–{goodHi:0.#})",
                    Warn(outside), GUILayout.Width(150f));
            }
            if (!string.IsNullOrEmpty(readout))
                EditorGUILayout.LabelField(" ", readout, EditorStyles.miniLabel);
        }

        static GUIStyle _warnStyle, _mutedStyle, _verdictStyle;

        // Cached rather than rebuilt: OnGUI runs many times a second while a slider is
        // dragged, and a fresh GUIStyle per label is pure allocation churn.
        static GUIStyle Warn(bool bad)
        {
            if (_warnStyle == null)
            {
                _warnStyle = new GUIStyle(EditorStyles.miniLabel);
                _warnStyle.normal.textColor = FrogletEditorPalette.Warn;
                _mutedStyle = new GUIStyle(EditorStyles.miniLabel);
                _mutedStyle.normal.textColor = FrogletEditorPalette.Muted;
            }
            return bad ? _warnStyle : _mutedStyle;
        }

        void DrawPreviews()
        {
            EditorGUILayout.LabelField("Preview — the shipped GPU code, at 1:1 pixels",
                FrogletEditorPalette.SectionHeader);

            float width = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 40f);
            int side = Mathf.Clamp((int)width, 160, 420);

            var corridor = EnsureRT(ref _rtCorridor, side, side);
            RenderPreview(corridor, 0);
            var r = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(r, corridor);
            EditorGUILayout.LabelField("corridor cross-section — the slice the ship flies inside",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(3f);
            var ramp = EnsureRT(ref _rtRamp, side, Mathf.Max(48, side / 4));
            RenderPreview(ramp, 1);
            var rr = GUILayoutUtility.GetRect(ramp.width, ramp.height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(rr, ramp);
            EditorGUILayout.LabelField($"alpha ramp {_rampLo:0.00} → {_rampHi:0.00} — where the unit shape reads",
                EditorStyles.miniLabel);

            _animatePreview = EditorGUILayout.ToggleLeft("Animate the preview clock", _animatePreview);
        }

        void DrawMeasure()
        {
            EditorGUILayout.LabelField("Fidelity", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                "|coverage − alpha| decides whether the corridor's SHORT gradient band reads as a fade or " +
                "as an edge. Measured against the shipped Worley baseline in the same pass, so the verdict " +
                "is a ratio and does not depend on this harness.", EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button("Measure this setting", GUILayout.Height(24f)))
                Measure();

            if (!string.IsNullOrEmpty(_measureLine))
            {
                if (_verdictStyle == null) _verdictStyle = new GUIStyle(EditorStyles.boldLabel);
                _verdictStyle.normal.textColor = _measureColor;
                EditorGUILayout.LabelField(_measureLine, _verdictStyle);
                EditorGUILayout.LabelField(_measureDetail, EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawBake()
        {
            EditorGUILayout.LabelField("Bake", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                "Writes these values into PrismOcclusionCorridor.hlsl as its compile-time constants and turns " +
                "design mode off, so the shipped shader carries one kernel, no branch and no uniforms.",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Bake to source + ship mode", FrogletEditorPalette.Ok, 210f, 26f))
                    Bake(disableTuning: true);
                if (FrogletEditorPalette.ColorButton("Bake, stay in design mode", FrogletEditorPalette.Info, 190f, 26f))
                    Bake(disableTuning: false);
            }
            if (GUILayout.Button("Copy the constants to the clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = ConstantBlock();
                Say("Copied. Paste over the matching lines in PrismOcclusionCorridor.hlsl.", false);
            }
        }

        void Say(string message, bool error)
        {
            _message = message;
            _messageIsError = error;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Measurement — the corridor's own admission rule, run on rendered output
        // ─────────────────────────────────────────────────────────────────────────
        void Measure()
        {
            if (Mat == null)
            {
                Say($"Preview shader '{PreviewShader}' not found — cannot measure.", true);
                return;
            }

            const int Size = 512;
            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGBFloat,
                                                RenderTextureReadWrite.Linear);
            Texture2D tex = null;
            try
            {
                // The setting under test.
                RenderPreview(rt, 2);
                var mine = Readback(rt, ref tex);

                // The shipped baseline, measured through the identical path so the two
                // numbers are directly comparable no matter what this harness does.
                var savedA = PackA();
                var savedB = PackB();
                Shader.SetGlobalVector(DitherA, new Vector4(KernelWorley + 1, 6f, 0f, _morphRate));
                Shader.SetGlobalVector(DitherB, savedB);
                RenderPreviewRaw(rt);
                var baseline = Readback(rt, ref tex);
                Shader.SetGlobalVector(DitherA, savedA);
                Shader.SetGlobalVector(DitherB, savedB);

                float ratio = baseline.corridor > 1e-6f ? mine.corridor / baseline.corridor : 99f;
                string verdict;
                if (ratio <= 1.35f) { verdict = "PASS"; _measureColor = FrogletEditorPalette.Ok; }
                else if (ratio <= 2f) { verdict = "MARGINAL"; _measureColor = FrogletEditorPalette.Warn; }
                else { verdict = "BREAKS THE FADE"; _measureColor = FrogletEditorPalette.Error; }

                _measureLine = $"{verdict} — corridor {mine.corridor:0.0000}, uniform {mine.uniform:0.0000} " +
                               $"({ratio:0.00}× the shipped baseline)";
                _measureDetail =
                    $"Baseline (Worley @ 6 px, measured just now through the same path): corridor " +
                    $"{baseline.corridor:0.0000}, uniform {baseline.uniform:0.0000}. " +
                    "Up to ~1.35× is the band the admitted kernels already sit in; past ~2× the gradient " +
                    "band stops reading as a fade. Judge in play mode too — the number is necessary, not sufficient.";
                Say(string.Empty, false);
            }
            catch (Exception e)
            {
                Say("Measurement failed: " + e.Message, true);
            }
            finally
            {
                if (tex != null) DestroyImmediate(tex);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // Renders raw mode WITHOUT re-publishing, so a caller can measure a config other
        // than the window's own (the baseline pass depends on this).
        void RenderPreviewRaw(RenderTexture rt)
        {
            var m = Mat;
            if (m == null || rt == null) return;
            m.SetVector("_PreviewRect", new Vector4(rt.width, rt.height, 2f,
                _animatePreview ? (float)_previewClock : 0f));
            m.SetVector("_PreviewProfile", new Vector4(_previewInner, _previewOuter, _previewCore, _previewExtent));
            m.SetVector("_PreviewRamp", new Vector4(_rampLo, _rampHi, 0f, 0f));
            var prev = RenderTexture.active;
            Graphics.Blit(Texture2D.whiteTexture, rt, m);
            RenderTexture.active = prev;
        }

        struct Fidelity
        {
            public float uniform;
            public float corridor;
        }

        Fidelity Readback(RenderTexture rt, ref Texture2D tex)
        {
            if (tex == null)
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false)
                { hideFlags = HideFlags.HideAndDontSave };

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;

            var px = tex.GetPixels();   // R = threshold, G = alpha
            return ComputeFidelity(px);
        }

        /// <summary>
        /// Two numbers, both |coverage − alpha|:
        /// <b>uniform</b> sweeps alpha over the whole tile and asks how closely the kept
        /// fraction tracks it — catches a kernel whose threshold distribution is skewed.
        /// <b>corridor</b> bins by the LOCAL alpha of the real radial profile and compares
        /// within each bin — catches a spatially correlated kernel that the global sweep
        /// flatters, which is the failure mode that actually breaks a short gradient band.
        /// </summary>
        static Fidelity ComputeFidelity(Color[] px)
        {
            const int Bins = 40;
            var binKept = new int[Bins];
            var binCount = new int[Bins];
            var binAlphaSum = new double[Bins];

            // The uniform sweep runs off a threshold histogram (one pass, no sort). The
            // histogram is deliberately far finer than the 101 alphas it is evaluated at:
            // a bucket only has to be small enough that quantisation cannot show up in the
            // fourth decimal, which is the digit the admission rule is argued in.
            const int Steps = 101;
            const int HistBuckets = 4096;
            var thresholdHist = new int[HistBuckets];
            int total = 0;

            foreach (var p in px)
            {
                float threshold = p.r;
                float alpha = p.g;

                int slot = Mathf.Clamp((int)(threshold * HistBuckets), 0, HistBuckets - 1);
                thresholdHist[slot]++;
                total++;

                // Only the gradient band can show a pattern at all — the core clips whatever
                // the threshold is and the exterior clips nothing, so measuring there would
                // dilute the number with pixels that cannot be wrong.
                if (alpha <= 0.02f || alpha >= 0.98f) continue;
                int bin = Mathf.Clamp((int)(alpha * Bins), 0, Bins - 1);
                binCount[bin]++;
                binAlphaSum[bin] += alpha;
                if (threshold <= alpha) binKept[bin]++;
            }

            double uniformErr = 0.0;
            int cumulative = 0;
            int bucket = 0;
            for (int i = 0; i < Steps; i++)
            {
                double alpha = i / (double)(Steps - 1);
                int upTo = Mathf.Clamp((int)(alpha * HistBuckets), 0, HistBuckets);
                while (bucket < upTo) cumulative += thresholdHist[bucket++];
                double coverage = total > 0 ? cumulative / (double)total : 0.0;
                uniformErr += Math.Abs(coverage - alpha);
            }
            uniformErr /= Steps;

            double corridorErr = 0.0;
            long weight = 0;
            for (int b = 0; b < Bins; b++)
            {
                if (binCount[b] < 200) continue;   // too few samples to mean anything
                double coverage = binKept[b] / (double)binCount[b];
                double meanAlpha = binAlphaSum[b] / binCount[b];
                corridorErr += Math.Abs(coverage - meanAlpha) * binCount[b];
                weight += binCount[b];
            }
            corridorErr = weight > 0 ? corridorErr / weight : double.NaN;

            return new Fidelity { uniform = (float)uniformErr, corridor = (float)corridorErr };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Source rewriting
        // ─────────────────────────────────────────────────────────────────────────
        static string F(float v) => v.ToString("0.0####", CultureInfo.InvariantCulture);

        string ConstantBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"#define PRISM_OCCLUSION_KERNEL {KernelTokens[_kernel]}");
            sb.AppendLine($"#define PRISM_OCCLUSION_SHARD_ORIENT {OrientTokens[_shardOrient]}");
            sb.AppendLine($"static const float PRISM_OCCLUSION_CELL_SIZE = {F(_cellSize)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_MORPH_RATE = {F(_morphRate)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SHATTER_CELL = {F(_shatterCell)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SHATTER_WALL = {F(_shatterWall)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SPIRAL_RINGS = {F(_spiralRings)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SPIRAL_ARMS = {F(_spiralArms)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SHATTER_DEPTH_PHASE = {F(_depthPhase)};");
            sb.AppendLine($"static const float PRISM_BACKFACE_POWER = {F(_backFacePower)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SHATTER3D_CELL = {F(_shatter3dCell)};");
            sb.AppendLine($"static const float PRISM_OCCLUSION_SHATTER3D_WALL = {F(_shatter3dWall)};");
            return sb.ToString();
        }

        // Every edit is anchored and must match EXACTLY once. A tool that rewrites source is
        // only safe if it refuses on the first surprise: a partial rewrite of this file would
        // be a broken shader that still compiles into something nobody chose.
        //
        // `tail` records whether the pattern has a third group to preserve. It is explicit
        // rather than inferred because .NET does NOT throw on a replacement that names a
        // group the pattern lacks — it emits the literal text "${3}" into the file. Getting
        // that wrong writes a shader that fails to compile, from a tool whose whole job is
        // to be safer than editing by hand.
        //
        // EVERY line-anchored pattern captures `(\r?)` as that third group. In .NET `$` in
        // multiline mode matches before the '\n' but AFTER any '\r', so `...SHARD$` simply
        // does not match on a CRLF checkout — which is every Windows clone of this repo.
        // Shipped without it, the three #define anchors matched 0 times: the Lab reported
        // design mode as OFF when the shader said 1, and refused to bake. Capturing the
        // carriage return and re-emitting it also means a bake cannot silently convert a
        // line's ending, which would show up as noise in the diff.
        readonly struct Anchor
        {
            public readonly string Key;
            public readonly string Pattern;
            public readonly bool Tail;
            public Anchor(string key, string pattern, bool tail) { Key = key; Pattern = pattern; Tail = tail; }
        }

        static readonly Anchor[] Anchors =
        {
            new Anchor("kernel",      @"(?m)^(#define PRISM_OCCLUSION_KERNEL )(PRISM_OCCLUSION_KERNEL_\w+)(\r?)$", true),
            new Anchor("orient",      @"(?m)^(#define PRISM_OCCLUSION_SHARD_ORIENT )(PRISM_OCCLUSION_SHARD_\w+)(\r?)$", true),
            new Anchor("tuning",      @"(?m)^(#define PRISM_OCCLUSION_LIVE_TUNING )(\d+)(\r?)$", true),
            new Anchor("cell",        @"(?m)^(static const float PRISM_OCCLUSION_CELL_SIZE = )([^;]+)(;)", true),
            new Anchor("morph",       @"(?m)^(static const float PRISM_OCCLUSION_MORPH_RATE = )([^;]+)(;)", true),
            new Anchor("shatterCell", @"(?m)^(static const float PRISM_OCCLUSION_SHATTER_CELL = )([^;]+)(;)", true),
            new Anchor("shatterWall", @"(?m)^(static const float PRISM_OCCLUSION_SHATTER_WALL = )([^;]+)(;)", true),
            new Anchor("spiralRings", @"(?m)^(static const float PRISM_OCCLUSION_SPIRAL_RINGS = )([^;]+)(;)", true),
            new Anchor("spiralArms",  @"(?m)^(static const float PRISM_OCCLUSION_SPIRAL_ARMS = )([^;]+)(;)", true),
            new Anchor("depthPhase",  @"(?m)^(static const float PRISM_OCCLUSION_SHATTER_DEPTH_PHASE = )([^;]+)(;)", true),
            new Anchor("backFace",    @"(?m)^(static const float PRISM_BACKFACE_POWER = )([^;]+)(;)", true),
            new Anchor("s3dCell",     @"(?m)^(static const float PRISM_OCCLUSION_SHATTER3D_CELL = )([^;]+)(;)", true),
            new Anchor("s3dWall",     @"(?m)^(static const float PRISM_OCCLUSION_SHATTER3D_WALL = )([^;]+)(;)", true),
        };

        static FrogletToolValidation ValidateSource()
        {
            if (!File.Exists(HlslPath))
                return FrogletToolValidation.Fail("The corridor shader is missing.", HlslPath + " not found.");

            string text = File.ReadAllText(HlslPath);
            var problems = new List<string>();
            foreach (var anchor in Anchors)
            {
                int n = Regex.Matches(text, anchor.Pattern).Count;
                if (n != 1)
                    problems.Add($"'{anchor.Key}' anchor matched {n} times (expected exactly 1) — the Lab can no " +
                                 "longer safely rewrite this file. Fix the anchor in PrismOcclusionDitherLab.cs.");
            }
            return problems.Count == 0
                ? FrogletToolValidation.Pass("Every constant the Lab writes is anchored exactly once.")
                : FrogletToolValidation.Fail("PrismOcclusionCorridor.hlsl no longer matches the Lab's anchors.", problems);
        }

        // Returns false only when the flag could not be READ. That distinction is
        // load-bearing: the first version folded "unreadable" into "off" and told the human
        // the shader said 0 when it said 1, which is the worst thing a tool can do — report
        // a state the source does not hold.
        bool TryReadTuningFlag(out bool live)
        {
            live = false;
            if (!File.Exists(HlslPath)) return false;
            var m = Regex.Match(File.ReadAllText(HlslPath), @"(?m)^#define PRISM_OCCLUSION_LIVE_TUNING (\d+)\r?$");
            if (!m.Success) return false;
            live = m.Groups[1].Value != "0";
            return true;
        }

        void SetTuningFlag(bool live)
        {
            if (!File.Exists(HlslPath)) { Say("Corridor shader not found.", true); return; }
            string text = File.ReadAllText(HlslPath);
            // `(\r?)$` and the ${3} tail, exactly like the Anchor table above — NOT a bare `$`.
            // In .NET multiline mode `$` matches before the `\n` but AFTER any `\r`, so on a
            // Windows checkout a bare `$` here matches ZERO times: the replace is a no-op,
            // `updated == text`, and this method reports "could not find the line" and refuses
            // to toggle design mode. Capturing and re-emitting the ending also keeps the write
            // from silently converting the file's line endings into diff noise.
            string updated = Regex.Replace(text, @"(?m)^(#define PRISM_OCCLUSION_LIVE_TUNING )(\d+)(\r?)$",
                                           "${1}" + (live ? "1" : "0") + "${3}");
            if (updated == text) { Say("Could not find the PRISM_OCCLUSION_LIVE_TUNING line.", true); return; }
            WriteHlsl(updated);
            Say(live ? "Design mode enabled — the shader is recompiling." : "Design mode off.", false);
        }

        void Bake(bool disableTuning)
        {
            var validation = ValidateSource();
            if (!validation.Passed)
            {
                Say(validation.Summary + "\n" + string.Join("\n", validation.Problems), true);
                return;
            }

            string text = File.ReadAllText(HlslPath);
            var values = new Dictionary<string, string>
            {
                { "kernel", KernelTokens[_kernel] },
                { "orient", OrientTokens[_shardOrient] },
                { "cell", F(_cellSize) },
                { "morph", F(_morphRate) },
                { "shatterCell", F(_shatterCell) },
                { "shatterWall", F(_shatterWall) },
                { "spiralRings", F(_spiralRings) },
                { "spiralArms", F(_spiralArms) },
                { "depthPhase", F(_depthPhase) },
                { "backFace", F(_backFacePower) },
                { "s3dCell", F(_shatter3dCell) },
                { "s3dWall", F(_shatter3dWall) },
                { "tuning", disableTuning ? "0" : "1" },
            };

            foreach (var anchor in Anchors)
            {
                // ${1} keeps the declaration, ${3} (where present) keeps the ';' and the
                // trailing comment — those comments carry the measured windows and must
                // survive a bake.
                string replacement = "${1}" + values[anchor.Key].Replace("$", "$$")
                                   + (anchor.Tail ? "${3}" : string.Empty);
                text = Regex.Replace(text, anchor.Pattern, replacement);
            }

            WriteHlsl(text);

            // Baking hands authority back to the source, so the live globals must stop
            // driving — otherwise the window would keep overriding the values it just wrote
            // and "what I see" would stop being "what is committed".
            if (disableTuning)
            {
                _driveGame = false;
                Unpublish();
            }

            Say(disableTuning
                ? "Baked, and design mode is off. The shader now carries one kernel, no branch and no " +
                  "tuning uniforms. Use Validate & Push below to commit it."
                : "Baked. Design mode is still on, so the dials keep driving — bake again with ship mode " +
                  "when the look is settled.", false);
        }

        void WriteHlsl(string text)
        {
            File.WriteAllText(HlslPath, text);
            // Record in the same block that writes, per Docs/TOOLING.md: a path recorded
            // beside the write cannot be missed by an early return or an exception.
            FrogletToolChangeLedger.Record(ToolName, HlslPath);
            AssetDatabase.ImportAsset(HlslPath, ImportAssetOptions.ForceUpdate);
            Repaint();
        }
    }
}
