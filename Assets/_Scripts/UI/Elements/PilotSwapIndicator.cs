using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arena pilot swap, made visible and tappable: in the top bar, between the goal stack
    /// (top left) and the domain score bar (top centre), the hull a swap would put you in each way
    /// round the team's ring - LEFT is D-pad left / key 1, RIGHT is D-pad right / key 2 - as that
    /// hull's class icon, with the control's keycap under it on a pad or keyboard. Clicking or
    /// tapping a tile is the same request the gesture makes (<see cref="Player.RequestPilotSwap"/>),
    /// so on a phone, which has no D-pad, this IS the swap control. Docs/GAME_MODE_TOPBAR.md §2.7.
    ///
    /// <para><b>What it shows is what a press does.</b> The targets come from
    /// <see cref="PilotSwap.ResolveSwapTargets"/>, the same ring pick the request makes, over the same
    /// team set (<see cref="PilotSwap.CollectTeamHulls"/>). One AI teammate shows one tile (both
    /// directions land on it); none, or any mode that is not an arena match, shows nothing.</para>
    ///
    /// <para><b>Built at runtime, never authored</b>, for the spectator badge's reason: it is ensured by
    /// <see cref="MiniGameHUD"/> under the HUD (whose <c>SafeAreaFitter</c> already keeps it inside the
    /// safe area), so every scene that instances the canvas gets it with no prefab or scene edit. Its
    /// sprites, sizes, colour tokens and words live in <see cref="PilotSwapIndicatorConfigSO"/>
    /// (<c>Resources/PilotSwapIndicatorConfig</c>).</para>
    ///
    /// <para><b>Polled at 4 Hz</b>, not evented: the roster grows late on a client, a teammate's hull can
    /// die, and the local pilot's own hull changes on every swap - three sources, none of which raises
    /// one event this could hold. A poll reuses its lists and redraws only when the answer changed, so
    /// it allocates nothing between changes.</para>
    ///
    /// <para>A touch that starts on a tile belongs to the tile, not to touch flight
    /// (<see cref="FlightTouchClaims"/>, as the ability cards do), while the tile is shown.</para>
    /// </summary>
    public class PilotSwapIndicator : MonoBehaviour
    {
        /// <summary>The config asset's path under Resources.</summary>
        public const string ConfigResourcePath = "PilotSwapIndicatorConfig";

        const float PollSeconds = 0.25f;
        const float FallbackDpi = 160f;
        const float MillimetresPerInch = 25.4f;
        const float ChipGap = 6f;
        const float CaptionHeight = 18f;

        [SerializeField] GameDataSO gameData;
        [SerializeField] PilotSwapIndicatorConfigSO config;
        [SerializeField] RectTransform leftNeighbour;
        [SerializeField] RectTransform rightNeighbour;

        enum ChipMode { None = 0, Pad = 1, Keyboard = 2 }

        sealed class Tile
        {
            public RectTransform Rect;
            public Image Plate;
            public Image Icon;
            public RectTransform Chip;
            public Image ChipPlate;
            public Image ChipGlyph;
            public TMP_Text ChipLabel;
            public bool Claimed;
        }

        readonly List<ulong> _teamHulls = new(8);
        readonly List<ulong> _ring = new(8);
        static readonly Vector3[] s_corners = new Vector3[4];

        RectTransform _rect;
        Canvas _rootCanvas;
        Tile _left;
        Tile _right;
        TMP_Text _caption;
        bool _built;
        float _nextPoll;

        // What is drawn now; a poll redraws only when one of these changes.
        int _shownCount = -1;
        ulong _shownLeft;
        ulong _shownRight;
        VesselClassType _shownLeftClass;
        VesselClassType _shownRightClass;
        ChipMode _shownChips;
        float _shownTile;

        /// <summary>
        /// Create (or find) the indicator under <paramref name="parent"/>, placed between
        /// <paramref name="left"/> (the goal stack) and <paramref name="right"/> (the score bar);
        /// either may be null. Idempotent, so a HUD that re-runs its ensure pass cannot stack two.
        /// </summary>
        public static PilotSwapIndicator Ensure(RectTransform parent, GameDataSO data,
                                                RectTransform left, RectTransform right)
        {
            if (!parent) return null;

            var existing = parent.GetComponentInChildren<PilotSwapIndicator>(true);
            if (existing)
            {
                existing.Bind(data, left, right);
                return existing;
            }

            var config = Resources.Load<PilotSwapIndicatorConfigSO>(ConfigResourcePath);
            if (!config)
            {
                CSDebug.LogWarning($"[PilotSwapIndicator] Resources/{ConfigResourcePath} is missing, so the " +
                                   "top bar shows no swap targets and a phone has no way to swap pilots.");
                return null;
            }

            var go = new GameObject("PilotSwapIndicator", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = Vector2.zero;

            var indicator = go.AddComponent<PilotSwapIndicator>();
            indicator.config = config;
            indicator.Bind(data, left, right);
            return indicator;
        }

        void Bind(GameDataSO data, RectTransform left, RectTransform right)
        {
            if (data) gameData = data;
            if (left) leftNeighbour = left;
            if (right) rightNeighbour = right;
            _nextPoll = 0f;
        }

        // ── pure parts (edit-mode tested) ───────────────────────────────────────────────────

        /// <summary>
        /// The tile edge in canvas units: the authored size, raised so it is at least
        /// <paramref name="minMillimetres"/> on the physical screen. <paramref name="dpi"/> at or below
        /// zero (Unity's "unknown") falls back to 160, the Android baseline density.
        /// </summary>
        public static float TileUnits(float authoredUnits, float minMillimetres, float dpi, float canvasScale)
        {
            float effectiveDpi = dpi > 0f ? dpi : FallbackDpi;
            float scale = canvasScale > 0f ? canvasScale : 1f;
            float minUnits = minMillimetres / MillimetresPerInch * effectiveDpi / scale;
            return Mathf.Max(authoredUnits, minUnits);
        }

        /// <summary>
        /// Centre x of a row <paramref name="width"/> wide placed in the gap between
        /// <paramref name="gapLeft"/> and <paramref name="gapRight"/>: centred when it fits, else
        /// hugging the right edge (the score bar), which keeps it clear of the score columns.
        /// </summary>
        public static float PlaceInGap(float gapLeft, float gapRight, float width)
        {
            if (gapRight - gapLeft >= width) return (gapLeft + gapRight) * 0.5f;
            return gapRight - width * 0.5f;
        }

        // ── lifecycle ───────────────────────────────────────────────────────────────────────

        void Awake() => _rect = (RectTransform)transform;

        void OnDisable() => ReleaseClaims();

        void OnDestroy() => ReleaseClaims();

        void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;
            Refresh();
        }

        void Refresh()
        {
            var pilot = ResolveLocalPilot();
            int count = 0;
            ulong left = 0, right = 0;

            if (pilot)
            {
                PilotSwap.CollectTeamHulls(pilot, gameData.Players, _teamHulls);
                count = PilotSwap.ResolveSwapTargets(_teamHulls, pilot.Vessel.VesselNetId, _ring,
                                                     out left, out right);
            }

            if (count > 0 && !_built) Build();
            if (!_built) return;

            var leftClass = count == 2 ? ClassOf(left) : VesselClassType.Random;
            var rightClass = count > 0 ? ClassOf(right) : VesselClassType.Random;
            var chips = count > 0 ? ResolveChipMode(pilot) : ChipMode.None;
            float tile = TileUnits(config.TileSize, config.MinTouchMillimetres, Screen.dpi, CanvasScale());

            if (count != _shownCount || left != _shownLeft || right != _shownRight ||
                leftClass != _shownLeftClass || rightClass != _shownRightClass ||
                chips != _shownChips || !Mathf.Approximately(tile, _shownTile))
            {
                _shownCount = count;
                _shownLeft = left;
                _shownRight = right;
                _shownLeftClass = leftClass;
                _shownRightClass = rightClass;
                _shownChips = chips;
                _shownTile = tile;
                Redraw(count, leftClass, rightClass, chips, tile);
            }

            if (count > 0) Place(count, chips, tile);
        }

        /// <summary>The local HUMAN pilot of an arena match whose turn is running and who has a live
        /// hull, or null - every other case shows nothing.</summary>
        Player ResolveLocalPilot()
        {
            if (!gameData || !config || !gameData.IsArenaMatch || !gameData.IsTurnRunning) return null;
            if (gameData.LocalPlayer is not Player p || !p || !p.IsSpawned || !p.IsLocalUser) return null;
            if (p.NetIsAI.Value) return null;
            return p.Vessel is Object hull && hull ? p : null;
        }

        VesselClassType ClassOf(ulong hullId)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is not Player p || !p) continue;
                if (p.Vessel is not Object hull || !hull) continue;
                if (p.Vessel.VesselNetId != hullId) continue;
                var status = p.Vessel.VesselStatus;
                return status != null ? status.VesselType : VesselClassType.Random;
            }
            return VesselClassType.Random;
        }

        static ChipMode ResolveChipMode(Player pilot)
        {
            // A phone or tablet with no pad names no controls (KitInputDevice) - the tiles are the control.
            if (!KitInputDevice.ShowsControlHints) return ChipMode.None;

            var input = pilot.InputController;
            var family = input ? input.ActiveDeviceFamily : InputDeviceFamily.None;
            if (family == InputDeviceFamily.None) family = InputDeviceActuation.DetectInitial();

            return family switch
            {
                InputDeviceFamily.Gamepad => ChipMode.Pad,
                InputDeviceFamily.KeyboardMouse => ChipMode.Keyboard,
                _ => ChipMode.None,
            };
        }

        float CanvasScale()
        {
            if (!_rootCanvas)
            {
                var canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas ? canvas.rootCanvas : null;
            }
            return _rootCanvas ? _rootCanvas.scaleFactor : 1f;
        }

        // ── clicks ──────────────────────────────────────────────────────────────────────────

        void OnLeftClicked() => RequestSwap(-1);

        // The one-teammate tile is the RIGHT tile; either direction lands on the same hull.
        void OnRightClicked() => RequestSwap(+1);

        void RequestSwap(int direction)
        {
            var pilot = ResolveLocalPilot();
            if (!pilot) return;
            pilot.RequestPilotSwap(direction);
            // The swap lands a round trip later; look again soon rather than a full poll away.
            _nextPoll = Mathf.Min(_nextPoll, Time.unscaledTime + PollSeconds * 0.5f);
        }

        // ── drawing ─────────────────────────────────────────────────────────────────────────

        void Redraw(int count, VesselClassType leftClass, VesselClassType rightClass, ChipMode chips, float tile)
        {
            // One teammate: the right tile alone (both directions land on that hull).
            DrawTile(_left, count == 2, leftClass, chips, HintBinding.PadDpadLeft, config.KeyboardLeftLabel, tile);
            DrawTile(_right, count >= 1, rightClass, chips, HintBinding.PadDpadRight, config.KeyboardRightLabel, tile);

            bool caption = count > 0 && !string.IsNullOrEmpty(config.Caption);
            _caption.gameObject.SetActive(caption);
            if (caption) _caption.text = config.Caption;
        }

        void DrawTile(Tile tile, bool shown, VesselClassType hull, ChipMode chips, HintBinding padBinding,
                      string keyboardLabel, float size)
        {
            tile.Rect.gameObject.SetActive(shown);
            SetClaim(tile, shown);
            if (!shown) return;

            tile.Rect.sizeDelta = new Vector2(size, size);

            Sprite icon = null;
            var vessels = config.Vessels;
            if (vessels && vessels.TryGetVesselByClass(hull, out var vessel, reportMissing: false))
                icon = vessel.IconActive ? vessel.IconActive : vessel.IconInactive;
            tile.Icon.sprite = icon;
            // No sprite would draw a solid quad in the tint; draw nothing instead.
            tile.Icon.enabled = icon;

            DrawChip(tile, chips, padBinding, keyboardLabel);
        }

        void DrawChip(Tile tile, ChipMode chips, HintBinding padBinding, string keyboardLabel)
        {
            string label = null;
            Sprite glyph = null;

            if (chips == ChipMode.Keyboard)
            {
                label = keyboardLabel;
            }
            else if (chips == ChipMode.Pad && config.GlyphSet)
            {
                var entry = config.GlyphSet.For(padBinding);
                if (entry != null)
                {
                    if (!string.IsNullOrEmpty(entry.padLabel)) label = entry.padLabel;
                    else glyph = entry.padGlyph;
                }
            }

            bool hasLabel = !string.IsNullOrEmpty(label);
            bool hasGlyph = !hasLabel && glyph;
            tile.Chip.gameObject.SetActive(hasLabel || hasGlyph);
            if (!hasLabel && !hasGlyph) return;

            float h = config.ChipHeight;
            float w = hasLabel ? Mathf.Max(h * 1.4f, tile.ChipLabel.GetPreferredValues(label).x + 16f) : h;
            tile.Chip.sizeDelta = new Vector2(w, h);
            tile.Chip.anchoredPosition = new Vector2(0f, -ChipGap);

            tile.ChipPlate.enabled = hasLabel;
            tile.ChipLabel.enabled = hasLabel;
            tile.ChipLabel.text = hasLabel ? label : string.Empty;
            tile.ChipGlyph.enabled = hasGlyph;
            tile.ChipGlyph.sprite = hasGlyph ? glyph : null;
        }

        /// <summary>Lay the tiles out and put the row in the gap between the two neighbours.</summary>
        void Place(int count, ChipMode chips, float tile)
        {
            float spacing = config.TileSpacing;
            float width = count == 2 ? tile * 2f + spacing : tile;

            if (count == 2)
            {
                SetPos(_left.Rect, new Vector2(-(tile + spacing) * 0.5f, 0f));
                SetPos(_right.Rect, new Vector2((tile + spacing) * 0.5f, 0f));
            }
            else
            {
                SetPos(_right.Rect, Vector2.zero);
            }

            var parent = _rect.parent as RectTransform;
            if (!parent) return;
            var bounds = parent.rect;
            float margin = config.NeighbourMargin;

            bool hasLeft = TryLocalEdges(leftNeighbour, parent, out var leftMin, out var leftMax);
            bool hasRight = TryLocalEdges(rightNeighbour, parent, out var rightMin, out _);

            float gapLeft = hasLeft ? leftMax.x + margin : bounds.xMin + margin;
            float gapRight = hasRight ? rightMin.x - margin : bounds.center.x - margin;
            float top = hasLeft ? leftMax.y : bounds.yMax - margin;

            // The caption sits above the tiles, inside the same top line.
            bool caption = _caption.gameObject.activeSelf;
            float tilesTop = caption ? top - CaptionHeight : top;
            SetPos(_caption.rectTransform, new Vector2(0f, CaptionHeight));

            var local = new Vector2(PlaceInGap(gapLeft, gapRight, width), tilesTop);
            var size = new Vector2(width, tile);
            SetPos(_rect, local - bounds.center);
            if (_rect.sizeDelta != size) _rect.sizeDelta = size;
        }

        static void SetPos(RectTransform rect, Vector2 pos)
        {
            if (rect.anchoredPosition != pos) rect.anchoredPosition = pos;
        }

        /// <summary>The neighbour's rect in <paramref name="space"/>'s local coordinates.</summary>
        static bool TryLocalEdges(RectTransform neighbour, RectTransform space, out Vector2 min, out Vector2 max)
        {
            min = max = Vector2.zero;
            if (!neighbour || !neighbour.gameObject.activeInHierarchy) return false;

            neighbour.GetWorldCorners(s_corners);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = space.InverseTransformPoint(s_corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return true;
        }

        // ── touch claims ────────────────────────────────────────────────────────────────────

        static void SetClaim(Tile tile, bool claim)
        {
            if (tile.Claimed == claim) return;
            tile.Claimed = claim;
            if (claim) FlightTouchClaims.Add(tile.Rect);
            else FlightTouchClaims.Remove(tile.Rect);
        }

        void ReleaseClaims()
        {
            if (_left != null) SetClaim(_left, false);
            if (_right != null) SetClaim(_right, false);
            // A re-enabled indicator redraws (and re-claims) on its next poll.
            _shownCount = -1;
        }

        // ── build ───────────────────────────────────────────────────────────────────────────

        void Build()
        {
            if (!_rect) _rect = (RectTransform)transform;
            var theme = config.Theme;

            _caption = NewText("Caption", _rect, config.CaptionFontSize, theme);
            var captionRect = _caption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 1f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.sizeDelta = new Vector2(0f, CaptionHeight);
            _caption.gameObject.SetActive(false);

            _left = BuildTile("Left", theme, OnLeftClicked);
            _right = BuildTile("Right", theme, OnRightClicked);
            _built = true;
        }

        Tile BuildTile(string name, UIThemeSO theme, UnityEngine.Events.UnityAction onClick)
        {
            var tile = new Tile();

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            tile.Rect = (RectTransform)go.transform;
            tile.Rect.SetParent(_rect, false);
            tile.Rect.anchorMin = tile.Rect.anchorMax = new Vector2(0.5f, 1f);
            tile.Rect.pivot = new Vector2(0.5f, 1f);

            tile.Plate = go.GetComponent<Image>();
            var plate = UITheme.Resolve(theme, config.PlateToken);
            plate.a *= config.PlateAlpha;
            tile.Plate.color = plate;
            tile.Plate.raycastTarget = true;   // the hit area: the whole tile
            var outline = go.AddComponent<Outline>();
            outline.effectColor = UITheme.Resolve(theme, config.OutlineToken);
            outline.effectDistance = new Vector2(1f, -1f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(tile.Rect, false);
            float inset = config.IconInset;
            iconRect.anchorMin = new Vector2(inset, inset);
            iconRect.anchorMax = new Vector2(1f - inset, 1f - inset);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            tile.Icon = iconGo.GetComponent<Image>();
            tile.Icon.preserveAspect = true;
            tile.Icon.raycastTarget = false;
            tile.Icon.color = UITheme.Resolve(theme, config.IconToken);

            var button = go.GetComponent<Button>();
            button.targetGraphic = tile.Icon;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = UITheme.Resolve(theme, config.PressedToken);
            colors.fadeDuration = UITheme.DurMicro;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var chipGo = new GameObject("Keycap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tile.Chip = (RectTransform)chipGo.transform;
            tile.Chip.SetParent(tile.Rect, false);
            tile.Chip.anchorMin = tile.Chip.anchorMax = new Vector2(0.5f, 0f);
            tile.Chip.pivot = new Vector2(0.5f, 1f);
            tile.ChipPlate = chipGo.GetComponent<Image>();
            tile.ChipPlate.color = UITheme.Resolve(theme, UIColorToken.SurfaceVeryDark);
            tile.ChipPlate.raycastTarget = false;
            var chipLine = chipGo.AddComponent<Outline>();
            chipLine.effectColor = UITheme.Resolve(theme, config.OutlineToken);
            chipLine.effectDistance = new Vector2(1f, -1f);

            var glyphGo = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var glyphRect = (RectTransform)glyphGo.transform;
            glyphRect.SetParent(tile.Chip, false);
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = glyphRect.offsetMax = Vector2.zero;
            tile.ChipGlyph = glyphGo.GetComponent<Image>();
            tile.ChipGlyph.preserveAspect = true;
            tile.ChipGlyph.raycastTarget = false;
            tile.ChipGlyph.color = config.GlyphSet ? config.GlyphSet.RestColor : UITheme.Resolve(theme, config.TextToken);
            tile.ChipGlyph.enabled = false;

            tile.ChipLabel = NewText("Label", tile.Chip, config.ChipFontSize, theme);
            var labelRect = tile.ChipLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            chipGo.SetActive(false);

            go.SetActive(false);
            return tile;
        }

        TMP_Text NewText(string name, RectTransform parent, float size, UIThemeSO theme)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)go.transform).SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (config.Font) text.font = config.Font;
            text.fontSize = size;
            text.fontStyle = FontStyles.UpperCase;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = UITheme.Resolve(theme, config.TextToken);
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }
    }
}
