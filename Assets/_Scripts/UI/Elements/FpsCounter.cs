using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The player's FPS counter: one line in a screen corner, over every scene, in EVERY build (release included),
    /// shown while Settings > Display > Show FPS is on. It is not the diagnostics overlay
    /// (<c>DiagnosticsHUD</c>), which is stripped from release builds and shows far more than a player needs.
    ///
    /// Self-installing on a persistent root at startup, like <c>DisplayGraphicsSettings</c>; its words and look come
    /// from <c>Resources/FpsCounterConfig.asset</c> (<see cref="FpsCounterConfigSO"/>). The choice is device-local
    /// (PlayerPrefs), off by default. While it is off the counter does no work: the canvas and this component are
    /// disabled. While it is on the text is rebuilt once per sample window, never per frame.
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        const string ConfigResourcePath = "FpsCounterConfig";
        const string PrefKey = "gfx.ShowFps";

        static FpsCounter _instance;

        /// <summary>Raised when the Show FPS choice changes.</summary>
        public static event Action<bool> OnShownChanged;

        /// <summary>The player's Show FPS choice. Setting it saves it and shows or hides the counter at once.</summary>
        public static bool Shown
        {
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
            set
            {
                if (Shown == value) return;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_instance) _instance.ApplyShown(value);
                OnShownChanged?.Invoke(value);
            }
        }

        FpsCounterConfigSO _config;
        Canvas _canvas;
        RectTransform _safeArea;
        TMP_Text _text;
        Rect _appliedSafeArea;
        Vector2Int _appliedScreen;
        float _elapsed;
        int _frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance) return;
            var config = Resources.Load<FpsCounterConfigSO>(ConfigResourcePath);
            if (!config)
            {
                CSDebug.LogWarning($"[FpsCounter] No config at Resources/{ConfigResourcePath} - Show FPS will do nothing.");
                return;
            }

            var host = new GameObject(nameof(FpsCounter));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<FpsCounter>();
            _instance._config = config;
            _instance.BuildUI();
            _instance.ApplyShown(Shown);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _config.SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // A stretched rect that follows Screen.safeArea, so a notch or rounded corner never covers the number.
            var safeGo = new GameObject("SafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(canvasGo.transform, false);
            _safeArea = (RectTransform)safeGo.transform;
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_safeArea, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = _config.Corner;
            rect.anchoredPosition = new Vector2(
                Mathf.Lerp(_config.Margin.x, -_config.Margin.x, _config.Corner.x),
                Mathf.Lerp(_config.Margin.y, -_config.Margin.y, _config.Corner.y));
            rect.sizeDelta = new Vector2(_config.FontSize * 8f, _config.FontSize * 1.4f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (_config.Font) text.font = _config.Font;
            text.fontSize = _config.FontSize;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = _config.Corner.x > 0.5f ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            var colour = UITheme.Resolve(_config.Theme, _config.Token);
            colour.a *= _config.Alpha;
            text.color = colour;
            text.text = string.Empty;
            _text = text;
            FitSafeArea();
        }

        void ApplyShown(bool shown)
        {
            if (_canvas) _canvas.enabled = shown;
            enabled = shown;
            _elapsed = 0f;
            _frames = 0;
            if (_text) _text.text = string.Empty;
        }

        void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < _config.SampleSeconds) return;

            int fps = Mathf.RoundToInt(_frames / _elapsed);
            _frames = 0;
            _elapsed = 0f;
            _text.text = TemplateText.Fill(_config.Template, "fps", fps.ToString());
            FitSafeArea();
        }

        void FitSafeArea()
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == _appliedSafeArea && screen == _appliedScreen) return;
            _appliedSafeArea = area;
            _appliedScreen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;
            _safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            _safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
        }
    }
}
