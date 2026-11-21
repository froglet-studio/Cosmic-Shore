using System.Linq;
using CosmicShore.Cli;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using Silk.NET.Input;
// The engine's UI Button wins — Silk.NET.Input also declares a Button.
using Button = CosmicShore.Engine.UI.Button;
using Vector2 = CosmicShore.Engine.Vector2;
using Vector3 = CosmicShore.Engine.Vector3;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-I: the REAL in-round UI — an engine-UI canvas built INSIDE the round's
    /// world (it ticks with the round's GameLoop and renders through the same
    /// UiCanvasBridge walk the menu uses). Two elements:
    ///
    ///   READY — the domain-game controllers raise `_onToggleReadyButton` expecting a
    ///   UI button; when the host disables the driver's AutoReady, this button shows
    ///   while <see cref="IRoundDriver.ReadyPending"/> and its press calls
    ///   <see cref="IRoundDriver.ClickReady"/> through the full raycast → Button →
    ///   onClick stack (hardware mouse in interactive runs; a scripted synthetic
    ///   click in `--ready manual` screenshot verifies, deterministically).
    ///
    ///   SCOREBOARD — the end-game standings as a real centered panel, populated
    ///   from <see cref="IRoundDriver.StandingRows"/> the frame the round finishes.
    /// </summary>
    public sealed class RoundUiOverlay
    {
        readonly IRoundDriver _round;
        readonly StandaloneInputModule _module;
        readonly GameObject _readyGo;
        readonly Button _readyButton;
        readonly CanvasGroup _scoreboardGroup;
        readonly TextMeshProUGUI _scoreboardTitle;
        readonly TextMeshProUGUI[] _scoreboardRows;

        bool _scoreboardPopulated;
        bool _prevMouseDown;

        public bool ScoreboardShown => _scoreboardPopulated;

        public RoundUiOverlay(IRoundDriver round)
        {
            _round = round;

            // Event plumbing — the round world's own EventSystem (the menu's died
            // with the menu loop; fresh-world statics demand a fresh one here).
            var esGo = new GameObject("RoundEventSystem");
            esGo.AddComponent<EventSystem>();
            _module = esGo.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject("RoundCanvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasGo.transform;

            // ── READY (lower center — the DomainGames ready seam) ──────────────
            var ready = MakeChild("ReadyButton", canvasRect);
            ready.anchorMin = ready.anchorMax = new Vector2(0.5f, 0f);
            ready.pivot = new Vector2(0.5f, 0f);
            ready.anchoredPosition = new Vector2(0f, 180f);
            ready.sizeDelta = new Vector2(340f, 96f);
            var readyImage = ready.gameObject.AddComponent<Image>();
            readyImage.color = new Color(0.16f, 0.42f, 0.28f, 0.96f);
            _readyButton = ready.gameObject.AddComponent<Button>();
            var colors = _readyButton.colors;
            colors.normalColor = new Color(0.16f, 0.42f, 0.28f, 0.96f);
            colors.highlightedColor = new Color(0.22f, 0.55f, 0.36f, 1f);
            colors.pressedColor = new Color(0.4f, 0.85f, 0.55f, 1f);
            _readyButton.colors = colors;
            _readyButton.onClick.AddListener(() => _round.ClickReady());
            MakeFullStretchText(ready, "READY", 34f, new Color(0.92f, 1f, 0.95f, 1f));
            _readyGo = ready.gameObject;
            _readyGo.SetActive(false);

            // ── SCOREBOARD (centered panel, hidden until the round ends) ───────
            var panel = MakeChild("ScoreboardPanel", canvasRect);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(860f, 520f);
            _scoreboardGroup = panel.gameObject.AddComponent<CanvasGroup>();
            _scoreboardGroup.alpha = 0f;
            _scoreboardGroup.blocksRaycasts = false;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.04f, 0.15f, 0.96f);
            panelImage.raycastTarget = false;

            var title = MakeChild("Title", panel);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -30f);
            title.sizeDelta = new Vector2(0f, 60f);
            _scoreboardTitle = title.gameObject.AddComponent<TextMeshProUGUI>();
            _scoreboardTitle.fontSize = 40f;
            _scoreboardTitle.color = new Color(0.4f, 1f, 0.6f, 1f);
            _scoreboardTitle.alignment = TextAlignmentOptions.Center;

            _scoreboardRows = new TextMeshProUGUI[13]; // 12 players max + scoring blurb
            for (int i = 0; i < _scoreboardRows.Length; i++)
            {
                var row = MakeChild($"Row_{i}", panel);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -110f - i * 30f);
                row.sizeDelta = new Vector2(-64f, 28f);
                var text = row.gameObject.AddComponent<TextMeshProUGUI>();
                text.fontSize = 20f;
                text.color = new Color(0.9f, 0.95f, 1f, 1f);
                text.alignment = TextAlignmentOptions.Center;
                _scoreboardRows[i] = text;
            }
        }

        /// <summary>Per-frame state sync (call before the round steps).</summary>
        public void Update()
        {
            _readyGo.SetActive(_round.ReadyPending);

            if (_round.Finished && !_scoreboardPopulated)
            {
                _scoreboardPopulated = true;
                _scoreboardTitle.text = $"WINNER  {_round.WinnerName} ({_round.WinnerDomain})";
                int i = 0;
                foreach (var row in _round.StandingRows)
                {
                    if (i >= _scoreboardRows.Length - 1) break;
                    _scoreboardRows[i].text =
                        $"#{row.Rank}  {row.Name,-8} {row.Domain,-5} {row.Crystals,3}   {row.ScoreText}";
                    i++;
                }
                _scoreboardRows[i].text = $"({_round.ScoringLabel})";
                _scoreboardGroup.alpha = 1f;
            }
        }

        /// <summary>Hardware mouse → the round world's input module (interactive runs).</summary>
        public void DriveMouse(IInputContext inputContext)
        {
            var mouse = inputContext.Mice.FirstOrDefault();
            if (mouse == null) return;

            // Silk mouse Y is top-down; the UI event space is y-up screen pixels.
            var position = new Vector2(mouse.Position.X, Screen.height - mouse.Position.Y);
            _module.PointerMove(position);

            bool down = mouse.IsButtonPressed(MouseButton.Left);
            if (down && !_prevMouseDown) _module.PointerDown(position);
            if (!down && _prevMouseDown) _module.PointerUp(position);
            _prevMouseDown = down;
        }

        /// <summary>
        /// Scripted press for deterministic verifies: a synthetic pointer click at the
        /// READY button's center — the full raycast → Button → onClick path.
        /// </summary>
        public bool ClickReadySynthetic()
        {
            if (!_readyGo.activeSelf) return false;
            var corners = new Vector3[4];
            ((RectTransform)_readyButton.transform).GetWorldCorners(corners);
            var centre = new Vector2(
                (corners[0].x + corners[2].x) * 0.5f,
                (corners[0].y + corners[2].y) * 0.5f);
            _module.PointerDown(centre);
            _module.PointerUp(centre);
            return true;
        }

        static RectTransform MakeChild(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, worldPositionStays: false);
            return rt;
        }

        static TextMeshProUGUI MakeFullStretchText(RectTransform parent, string content, float size, Color color)
        {
            var label = MakeChild("Label", parent);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }
    }
}
