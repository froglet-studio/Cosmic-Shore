using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The BLACK HOLE TOOL (Docs/BLACK_HOLE.md §6.1): a runtime panel for spawning and tuning black
    /// holes from any scene, opened from the DiagnosticsHUD console with <c>blackhole tool on</c>
    /// (<c>off</c> closes it; <c>blackhole config</c> opens it on the config view).
    ///
    /// <para><b>The values live in the config ASSET, not in the tool.</b> The Spawn rows edit
    /// <c>BlackHoleConfig</c>'s Spawn section — strength (the pull), size (the event-horizon
    /// radius; 0 derives it from the strength), world position, velocity, spin axis — and
    /// <b>Spawn</b> spawns from exactly those
    /// (<see cref="BlackHoleRegistry.SpawnFromConfig"/>), so what is on the asset is what you get,
    /// from the tool or from <c>blackhole spawn</c> with no strength. <b>Config</b> opens every
    /// other field of the asset — physics, budgets, vessels, warp, lens — drawn from the SO's own
    /// <c>[Header]</c> / <c>[Range]</c> / <c>[Tooltip]</c> attributes (<see cref="BlackHoleToolModel"/>),
    /// so a field added to the config appears here with no edit to this file. Edits apply live;
    /// in the Editor they mark the asset dirty and <b>Save asset</b> writes it to disk.</para>
    ///
    /// Code-built uGUI in the DiagnosticsHUD / PrismGridExplosionHarness idiom. Editor and
    /// development builds only: the class compiles empty in a release player.
    /// </summary>
    public class BlackHoleTool : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static BlackHoleTool s_instance;

        /// <summary>True while the tool's panel is showing.</summary>
        public static bool IsOpen => s_instance != null && s_instance._open;

        /// <summary>True while the panel is showing with its config view expanded.</summary>
        public static bool IsConfigShown => IsOpen && s_instance._showConfig;

        /// <summary>Config fields the open tool has a live control for (spawn rows + config view).</summary>
        public static int BoundFieldCount => s_instance != null ? s_instance._bindings.Count : 0;

        /// <summary>Open or close the tool; <paramref name="showConfig"/> also expands the config view.</summary>
        public static void SetOpen(bool open, bool showConfig = false)
        {
            if (!open)
            {
                if (s_instance != null) s_instance.Show(false);
                return;
            }
            if (s_instance == null)
            {
                var go = new GameObject("[BlackHoleTool]");
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);   // edit mode: the edit-mode test builds it
                s_instance = go.AddComponent<BlackHoleTool>();
            }
            s_instance.Show(true);
            if (showConfig) s_instance.SetConfigShown(true);
        }

        // Play-mode re-entry with domain reload off: the last session's instance is destroyed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        // ── layout ──
        const float PanelWidth = 470f, Pad = 10f, RowH = 26f, TitleH = 30f;
        const float LabelW = 150f, SliderW = 180f, InputW = 70f, VecW = 62f;
        const float ConfigViewportH = 330f, HintH = 46f;
        const int LiveRows = 4;   // = BlackHoleConfigSO.maxBlackHoles' ceiling

        // palette (the DiagnosticsHUD's)
        static readonly Color PanelBg = new(0f, 0f, 0f, 0.8f);
        static readonly Color TitleBg = new(0.14f, 0.18f, 0.26f, 0.97f);
        static readonly Color ButtonBg = new(0.25f, 0.3f, 0.4f, 0.95f);
        static readonly Color SpawnBg = new(0.18f, 0.42f, 0.28f, 0.95f);
        static readonly Color DangerBg = new(0.45f, 0.2f, 0.2f, 0.95f);
        static readonly Color FieldBg = new(0.12f, 0.15f, 0.2f, 0.95f);
        static readonly Color Accent = new(0.49f, 0.76f, 1f, 1f);
        static readonly Color LabelColor = new(0.72f, 0.77f, 0.84f, 1f);
        static readonly Color Dim = new(0.55f, 0.6f, 0.68f, 1f);

        bool _open, _showConfig, _built;
        float _nextRefresh;
        Font _font;
        Canvas _canvas;
        GameObject _canvasGO;
        RectTransform _panel, _configRoot;
        Text _configButtonLabel, _caption, _status, _hint;
        string _statusText = "ready — values come from the BlackHoleConfig asset";
        float _spawnSectionBottom;
        readonly List<Binding> _bindings = new();
        readonly LiveRow[] _live = new LiveRow[LiveRows];

        BlackHoleConfigSO Config => BlackHoleRegistry.Config;

        void Show(bool open)
        {
            if (open && !_built) BuildUI();
            _open = open;
            if (_canvasGO != null) _canvasGO.SetActive(open);
            if (open) Refresh();
        }

        void SetConfigShown(bool shown)
        {
            _showConfig = shown;
            if (_configRoot != null) _configRoot.gameObject.SetActive(shown);
            if (_configButtonLabel != null) _configButtonLabel.text = shown ? "Config ▴" : "Config ▾";
            Relayout();
        }

        void Update()
        {
            if (!_open || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        void Refresh()
        {
            var config = Config;
            foreach (var b in _bindings) b.Pull(config);

            float strength = config.SpawnStrength;
            float size = config.SpawnHorizonRadius;
            float rs = config.HorizonRadius(strength, size);
            if (_caption != null)
                _caption.text =
                    $"size {(size > 0f ? "set" : "from strength")}: horizon r_s {rs:F1} u · shadow ≈ {2.6f * rs:F0} u · " +
                    $"lens {config.LensRadiusMultiplier * rs:F0} u\n" +
                    $"spawns at {Fmt(config.SpawnPosition)}{FromCamera(config.SpawnPosition)} · pull GM {config.GM(strength):N0} · " +
                    $"influence {config.InfluenceRadius(strength, rs):F0} u";

            var holes = BlackHoleRegistry.Holes;
            var cam = Camera.main;
            int row = 0;
            for (int i = 0; i < holes.Count && row < LiveRows; i++)
            {
                var h = holes[i];
                if (h == null || h.IsDespawning) continue;
                string away = cam != null ? $" · {Vector3.Distance(cam.transform.position, h.transform.position):F0} u away" : "";
                _live[row].Bind(h, $"#{h.Id}  strength {h.Strength:F1}  r_s {h.HorizonRadius:F1}{away}");
                row++;
            }
            for (; row < LiveRows; row++) _live[row].Bind(null, row == 0 && BlackHoleRegistry.Count == 0 ? "no black holes live" : "");

            if (_status != null)
            {
                string sane = config.IsSane ? "" : "  ⚠ config is not sane — spawns are refused (BlackHoleConfigSO.IsSane)";
                _status.text = $"{BlackHoleRegistry.Count}/{config.MaxBlackHoles} live · {BlackHoleGravityField.BodyCount:N0} bodies · " +
                               $"{BlackHoleGravityField.CapturedTotal:N0} captured\n{_statusText}{sane}";
            }
        }

        static string Fmt(Vector3 v) => $"({v.x:0.#}, {v.y:0.#}, {v.z:0.#})";

        /// <summary>" · N u from the camera", or nothing without a main camera.</summary>
        static string FromCamera(Vector3 p)
        {
            var cam = Camera.main;
            return cam != null ? $" · {Vector3.Distance(cam.transform.position, p):F0} u from the camera" : "";
        }

        // ── actions ──
        void Spawn()
        {
            var hole = BlackHoleRegistry.SpawnFromConfig();
            _statusText = hole != null
                ? $"spawned #{hole.Id}: strength {hole.Strength:F1}, r_s {hole.HorizonRadius:F1} u, at {Fmt(hole.transform.position)}"
                : $"spawn refused — {BlackHoleRegistry.Count}/{Config.MaxBlackHoles} live, or the config is not sane";
            Refresh();
        }

        void DespawnAll()
        {
            int n = BlackHoleRegistry.Count;
            BlackHoleRegistry.DespawnAll();
            _statusText = $"despawning {n} black hole(s)";
            Refresh();
        }

        void Retune(BlackHole hole)
        {
            if (hole == null) return;
            var config = Config;
            hole.SetStrength(config.SpawnStrength);
            hole.SetSize(config.SpawnHorizonRadius);
            _statusText = $"#{hole.Id} retuned to strength {hole.Strength:F1}, r_s {hole.HorizonRadius:F1} u";
            Refresh();
        }

        void Despawn(BlackHole hole)
        {
            if (hole == null) return;
            BlackHoleRegistry.Despawn(hole.Id);
            _statusText = $"despawning #{hole.Id}";
            Refresh();
        }

        void Edited()
        {
#if UNITY_EDITOR
            // Live edits are edits to the ASSET: mark it so Save (or the project's next save) keeps them.
            var config = Config;
            if (UnityEditor.AssetDatabase.Contains(config)) UnityEditor.EditorUtility.SetDirty(config);
#endif
            Refresh();
        }

#if UNITY_EDITOR
        void SaveAsset()
        {
            var config = Config;
            if (!UnityEditor.AssetDatabase.Contains(config))
            {
                _statusText = $"no asset to save — create Resources/{BlackHoleRegistry.ConfigResourcePath}.asset";
                Refresh();
                return;
            }
            UnityEditor.EditorUtility.SetDirty(config);
            UnityEditor.AssetDatabase.SaveAssets();   // the repo's convention (SetDirty + SaveAssets)
            _statusText = "saved " + UnityEditor.AssetDatabase.GetAssetPath(config);
            Refresh();
        }

        void SelectAsset()
        {
            var config = Config;
            if (!UnityEditor.AssetDatabase.Contains(config))
            {
                _statusText = "the config is a runtime default — there is no asset to select";
                Refresh();
                return;
            }
            UnityEditor.Selection.activeObject = config;
            UnityEditor.EditorGUIUtility.PingObject(config);
            _statusText = "selected " + UnityEditor.AssetDatabase.GetAssetPath(config) + " in the Inspector";
            Refresh();
        }
#endif

        // ── UI construction ──
        void BuildUI()
        {
            _built = true;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(es);
            }

            _canvasGO = new GameObject("BlackHoleToolCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGO.transform.SetParent(transform, false);
            _canvas = _canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32700;   // above scene tools, below the DiagnosticsHUD (32760)
            _canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Panel, top-right. Height is set by Relayout().
            _panel = Rect("Panel", _canvasGO.transform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(PanelWidth, 100));
            _panel.pivot = new Vector2(1, 1);
            _panel.gameObject.AddComponent<Image>().color = PanelBg;

            // Title bar — drag it to move the panel.
            var title = Rect("Title", _panel, new Vector2(0, 1), Vector2.zero, new Vector2(PanelWidth, TitleH));
            title.gameObject.AddComponent<Image>().color = TitleBg;
            title.gameObject.AddComponent<DragHandle>().Init(_panel, _canvas);
            var titleText = Label(title, "BLACK HOLE TOOL", new Vector2(Pad, 0), PanelWidth - 200, TitleH, 14, Color.white);
            titleText.fontStyle = FontStyle.Bold;
            _configButtonLabel = Button(title, "Config ▾", new Vector2(PanelWidth - 140, -3), 96, ButtonBg,
                () => SetConfigShown(!_showConfig)).GetComponentInChildren<Text>();
            Button(title, "×", new Vector2(PanelWidth - 38, -3), 30, DangerBg, () => SetOpen(false));

            float y = -TitleH - 8f;

            // ── SPAWN ──
            Label(_panel, "SPAWN — these values live in the BlackHoleConfig asset", new Vector2(Pad, y), PanelWidth - 2 * Pad, 20, 12, Accent);
            y -= 22f;
            var specs = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));
            foreach (var name in BlackHoleToolModel.SpawnFieldNames)
            {
                var spec = specs.Find(s => s.Name == name);
                if (spec == null) continue;
                FieldRow(_panel, spec, ref y);
            }
            _caption = Label(_panel, "", new Vector2(Pad, y), PanelWidth - 2 * Pad, 34, 12, Dim);
            y -= 38f;

            Button(_panel, "Spawn", new Vector2(Pad, y), 200, SpawnBg, Spawn);
            Button(_panel, "Despawn all", new Vector2(Pad + 206, y), 110, DangerBg, DespawnAll);
#if UNITY_EDITOR
            Button(_panel, "Save asset", new Vector2(Pad + 322, y), 118, ButtonBg, SaveAsset);
#endif
            y -= RowH + 8f;

            // ── LIVE ──
            Label(_panel, "LIVE HOLES — Retune applies the spawn strength and size", new Vector2(Pad, y), PanelWidth - 2 * Pad, 20, 12, Accent);
            y -= 22f;
            for (int i = 0; i < LiveRows; i++)
            {
                var row = new LiveRow();
                row.Text = Label(_panel, "", new Vector2(Pad, y), PanelWidth - 2 * Pad - 156, RowH, 12, Color.white);
                row.Retune = Button(_panel, "Retune", new Vector2(PanelWidth - Pad - 150, y), 70, ButtonBg, () => Retune(row.Hole));
                row.Despawn = Button(_panel, "Despawn", new Vector2(PanelWidth - Pad - 76, y), 76, DangerBg, () => Despawn(row.Hole));
                _live[i] = row;
                y -= RowH;
            }
            _status = Label(_panel, "", new Vector2(Pad, y - 4), PanelWidth - 2 * Pad, 34, 12, LabelColor);
            y -= 42f;
            _spawnSectionBottom = y;

            // ── CONFIG: a second panel docked to the LEFT of this one (a child, so dragging the title
            // bar moves both), the same height — the two side by side fit a 1366×768 game view. ──
            _configRoot = Rect("Config", _panel, new Vector2(0, 1), new Vector2(-8, 0), new Vector2(PanelWidth, 10));
            _configRoot.pivot = new Vector2(1, 1);
            _configRoot.gameObject.AddComponent<Image>().color = PanelBg;
            float cy = -8f;
            Label(_configRoot, "CONFIG — every field of the asset, live", new Vector2(Pad, cy), 280, 22, 12, Accent);
#if UNITY_EDITOR
            Button(_configRoot, "Select asset", new Vector2(PanelWidth - Pad - 110, cy), 110, ButtonBg, SelectAsset);
#endif
            cy -= 28f;

            var viewport = Rect("Viewport", _configRoot, new Vector2(0, 1), new Vector2(Pad, cy), new Vector2(PanelWidth - 2 * Pad, ConfigViewportH));
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.zero, new Vector2(PanelWidth - 2 * Pad, 10));
            float contentY = -4f;
            foreach (var spec in specs)
            {
                if (Array.IndexOf(BlackHoleToolModel.SpawnFieldNames, spec.Name) >= 0) continue;   // shown above
                if (!string.IsNullOrEmpty(spec.Header))
                {
                    contentY -= 6f;
                    Label(content, spec.Header.ToUpperInvariant(), new Vector2(4, contentY), PanelWidth - 4 * Pad, 20, 12, Accent);
                    contentY -= 22f;
                }
                FieldRow(content, spec, ref contentY, indent: 4f);
            }
            content.sizeDelta = new Vector2(PanelWidth - 2 * Pad, -contentY + 6f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            cy -= ConfigViewportH + 6f;
            _hint = Label(_configRoot, "hover a row for what it does", new Vector2(Pad, cy), PanelWidth - 2 * Pad, HintH, 12, Dim);
            _hint.verticalOverflow = VerticalWrapMode.Truncate;
            cy -= HintH + 6f;
            _configRoot.sizeDelta = new Vector2(PanelWidth, -cy);

            SetConfigShown(_showConfig);
        }

        void Relayout()
        {
            if (_panel == null) return;
            _panel.sizeDelta = new Vector2(PanelWidth, -_spawnSectionBottom + 4f);
        }

        /// <summary>One row for one config field, bound to it.</summary>
        void FieldRow(RectTransform parent, BlackHoleToolField spec, ref float y, float indent = Pad)
        {
            var binding = new Binding { Spec = spec, Tool = this };
            var labelText = Label(parent, spec.Label, new Vector2(indent, y), LabelW, RowH, 12, LabelColor);
            // The longest names ("Influence Acceleration Floor", ~190 px at 12 pt) outrun the column:
            // shrink to fit rather than wrap into the next row.
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 9;
            labelText.resizeTextMaxSize = 12;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            var hover = labelText.gameObject.AddComponent<HoverHint>();
            hover.Init(this, spec);
            labelText.raycastTarget = true;
            float x = indent + LabelW + 4f;

            switch (spec.Kind)
            {
                case BlackHoleToolFieldKind.Float:
                case BlackHoleToolFieldKind.Int:
                    if (spec.HasRange)
                    {
                        binding.Slider = Slider(parent, new Vector2(x, y), SliderW, spec.Min, spec.Max,
                            spec.Kind == BlackHoleToolFieldKind.Int, v => binding.FromSlider(v));
                        x += SliderW + 6f;
                        binding.Input = Input(parent, new Vector2(x, y), InputW, s => binding.FromInput(s));
                    }
                    else
                    {
                        binding.Input = Input(parent, new Vector2(x, y), InputW + SliderW * 0.5f, s => binding.FromInput(s));
                    }
                    break;
                case BlackHoleToolFieldKind.Bool:
                    binding.Toggle = Toggle(parent, new Vector2(x, y), v => binding.FromToggle(v));
                    break;
                case BlackHoleToolFieldKind.Vector3:
                    binding.Axes = new InputField[3];
                    for (int a = 0; a < 3; a++)
                    {
                        int axis = a;
                        Label(parent, a == 0 ? "x" : a == 1 ? "y" : "z", new Vector2(x, y), 10, RowH, 11, Dim);
                        binding.Axes[a] = Input(parent, new Vector2(x + 11f, y), VecW, s => binding.FromAxis(axis, s));
                        x += VecW + 16f;
                    }
                    break;
                default:
                    Label(parent, $"({spec.Field.FieldType.Name} — edit on the asset)", new Vector2(x, y), 240, RowH, 11, Dim);
                    break;
            }
            _bindings.Add(binding);
            y -= RowH;
        }

        internal void ShowHint(BlackHoleToolField spec)
        {
            if (_hint == null) return;
            string range = spec.HasRange ? $"  [{BlackHoleToolModel.Format(spec.Min, spec.Kind)} – {BlackHoleToolModel.Format(spec.Max, spec.Kind)}]" : "";
            _hint.text = $"{spec.Label}{range}: {spec.Tooltip ?? "(no tooltip on the field)"}";
        }

        // ── widgets ──
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Fill(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        Text Label(Transform parent, string text, Vector2 pos, float width, float height, int size, Color colour)
        {
            var rt = Rect("Label", parent, new Vector2(0, 1), pos, new Vector2(width, height));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = colour;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        Button Button(Transform parent, string label, Vector2 pos, float width, Color colour, Action onClick)
        {
            var rt = Rect("Btn_" + label, parent, new Vector2(0, 1), pos, new Vector2(width, RowH - 2f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = Fill("Label", rt).gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.text = label;
            return btn;
        }

        InputField Input(Transform parent, Vector2 pos, float width, Action<string> onEndEdit)
        {
            var rt = Rect("Input", parent, new Vector2(0, 1), pos, new Vector2(width, RowH - 4f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = FieldBg;
            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = img;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = InputField.ContentType.DecimalNumber;
            var textRT = Fill("Text", rt);
            textRT.offsetMin = new Vector2(5, 0);
            textRT.offsetMax = new Vector2(-5, 0);
            var t = textRT.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            field.textComponent = t;
            field.onEndEdit.AddListener(v => onEndEdit(v));
            return field;
        }

        Slider Slider(Transform parent, Vector2 pos, float width, float min, float max, bool whole, Action<float> onChanged)
        {
            var rt = Rect("Slider", parent, new Vector2(0, 1), pos, new Vector2(width, RowH - 4f));
            var slider = rt.gameObject.AddComponent<Slider>();
            var track = Fill("Track", rt);
            track.anchorMin = new Vector2(0, 0.38f);
            track.anchorMax = new Vector2(1, 0.62f);
            track.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.17f, 0.23f, 0.95f);
            var fillArea = Fill("FillArea", rt);
            fillArea.anchorMin = new Vector2(0, 0.38f);
            fillArea.anchorMax = new Vector2(1, 0.62f);
            var fill = Fill("Fill", fillArea);
            fill.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.55f, 0.85f, 0.95f);
            var handleArea = Fill("HandleArea", rt);
            handleArea.offsetMin = new Vector2(6, 0);
            handleArea.offsetMax = new Vector2(-6, 0);
            var handle = Fill("Handle", handleArea);
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(12, 0);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        Toggle Toggle(Transform parent, Vector2 pos, Action<bool> onChanged)
        {
            var rt = Rect("Toggle", parent, new Vector2(0, 1), pos + new Vector2(0, -2), new Vector2(20, 20));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = FieldBg;
            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            var check = Fill("Check", rt);
            check.offsetMin = new Vector2(4, 4);
            check.offsetMax = new Vector2(-4, -4);
            var checkImg = check.gameObject.AddComponent<Image>();
            checkImg.color = Accent;
            toggle.graphic = checkImg;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        // ── bindings: one config field ⇄ its controls ──
        sealed class Binding
        {
            public BlackHoleToolField Spec;
            public BlackHoleTool Tool;
            public Slider Slider;
            public InputField Input;
            public Toggle Toggle;
            public InputField[] Axes;

            /// <summary>Asset → controls. Never overwrites a field the user is typing in.</summary>
            public void Pull(BlackHoleConfigSO config)
            {
                switch (Spec.Kind)
                {
                    case BlackHoleToolFieldKind.Float:
                    case BlackHoleToolFieldKind.Int:
                    {
                        float v = Spec.GetNumber(config);
                        if (Slider != null) Slider.SetValueWithoutNotify(v);
                        if (Input != null && !Input.isFocused) Input.SetTextWithoutNotify(BlackHoleToolModel.Format(v, Spec.Kind));
                        break;
                    }
                    case BlackHoleToolFieldKind.Bool:
                        if (Toggle != null) Toggle.SetIsOnWithoutNotify(Spec.GetBool(config));
                        break;
                    case BlackHoleToolFieldKind.Vector3:
                    {
                        var v = Spec.GetVector(config);
                        for (int a = 0; a < 3 && Axes != null; a++)
                            if (!Axes[a].isFocused) Axes[a].SetTextWithoutNotify(BlackHoleToolModel.Format(v[a], BlackHoleToolFieldKind.Float));
                        break;
                    }
                }
            }

            public void FromSlider(float v)
            {
                Spec.SetNumber(Tool.Config, v);
                Tool.Edited();
            }

            public void FromInput(string text)
            {
                if (BlackHoleToolModel.TryParse(text, out float v)) Spec.SetNumber(Tool.Config, v);
                Tool.Edited();
            }

            public void FromToggle(bool v)
            {
                Spec.SetBool(Tool.Config, v);
                Tool.Edited();
            }

            public void FromAxis(int axis, string text)
            {
                if (BlackHoleToolModel.TryParse(text, out float v))
                {
                    var vec = Spec.GetVector(Tool.Config);
                    vec[axis] = v;
                    Spec.SetVector(Tool.Config, vec);
                }
                Tool.Edited();
            }
        }

        sealed class LiveRow
        {
            public Text Text;
            public Button Retune, Despawn;
            public BlackHole Hole;

            public void Bind(BlackHole hole, string text)
            {
                Hole = hole;
                Text.text = text;
                Retune.gameObject.SetActive(hole != null);
                Despawn.gameObject.SetActive(hole != null);
            }
        }

        /// <summary>Drag the title bar to move the panel.</summary>
        sealed class DragHandle : MonoBehaviour, IDragHandler
        {
            RectTransform _panel;
            Canvas _canvas;

            public void Init(RectTransform panel, Canvas canvas)
            {
                _panel = panel;
                _canvas = canvas;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_panel == null) return;
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                _panel.anchoredPosition += eventData.delta / scale;
            }
        }

        /// <summary>Hovering a row's label shows the field's tooltip in the config view's hint line.</summary>
        sealed class HoverHint : MonoBehaviour, IPointerEnterHandler
        {
            BlackHoleTool _tool;
            BlackHoleToolField _spec;

            public void Init(BlackHoleTool tool, BlackHoleToolField spec)
            {
                _tool = tool;
                _spec = spec;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (_tool != null) _tool.ShowHint(_spec);
            }
        }
#endif
    }
}
