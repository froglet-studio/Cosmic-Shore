using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Vector2 = CosmicShore.Engine.Vector2;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-C verification host (`--mode uidemo`): builds a REAL engine canvas —
    /// CanvasScaler (1920×1080 reference), a fitter-hugged VerticalLayoutGroup menu
    /// panel, Images, TMP labels with alignment, Buttons on the full event stack, a
    /// half-alpha CanvasGroup — ticks the GameLoop so Arc-B layout solves in the
    /// canvas slot, clicks a button SYNTHETICALLY through StandaloneInputModule at
    /// frame 30 (its selected tint must show), renders every frame through
    /// UiCanvasBridge, and screenshots. Two runs must be byte-identical — the Arc-C
    /// gate. This is the whole milestone stack (A layout → B components → D events →
    /// C pixels) in one image.
    /// </summary>
    public sealed class UiDemoWindow
    {
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        UiRenderer _ui;
        GameLoop _loop;
        StandaloneInputModule _module;
        Button _highlightButton;
        int _clicks;
        int _frameIndex;
        int _graphicCount, _textCount;

        public UiDemoWindow(string screenshotPath, int screenshotFrame)
        {
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — UI demo (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/3] creating window (GLFW)...");
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _loop?.Dispose();
        }

        void OnLoad()
        {
            Console.WriteLine("[2/3] window open — initializing GL/canvas...");
            _gl = GL.GetApi(_window);
            _gl.ClearColor(0.02f, 0.015f, 0.06f, 1f);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _ui = new UiRenderer(_gl);

            Screen.width = _window.FramebufferSize.X;
            Screen.height = _window.FramebufferSize.Y;

            _loop = new GameLoop("UiDemo");
            BuildCanvas();
            Console.WriteLine("[3/3] ready — ui demo.");
        }

        void BuildCanvas()
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            _module = esGo.AddComponent<StandaloneInputModule>();

            // Root canvas — authored at 1920×1080 like the real menu; the scaler maps
            // it onto the actual framebuffer (2/3 at 1280×720).
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasGo.transform;

            // Full-screen backdrop.
            var backdrop = MakeChild("Backdrop", canvasRect);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0.045f, 0.03f, 0.11f, 1f);
            backdropImage.raycastTarget = false;

            // Centre menu panel: fitter-hugged vertical group — Arc B's marquee shape.
            var panel = MakeChild("Panel", canvasRect);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.10f, 0.07f, 0.22f, 0.92f);
            panelImage.raycastTarget = false;
            var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(36, 36, 28, 28);
            group.spacing = 16f;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MakeLabel(panel, "COSMIC SHORE", 56f, new Color(0.55f, 0.95f, 1f, 1f), 640f);
            MakeLabel(panel, "MENU-UI FOUNDATION - ARC C", 22f, new Color(1f, 0.45f, 0.85f, 0.9f), 640f);

            string[] entries = { "PLAY", "HANGAR", "SETTINGS" };
            for (int i = 0; i < entries.Length; i++)
            {
                var row = MakeChild($"Row_{entries[i]}", panel);
                var rowImage = row.gameObject.AddComponent<Image>();
                rowImage.color = new Color(0.16f, 0.20f, 0.45f, 1f);
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 560f;
                element.preferredHeight = 76f;
                var button = row.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = new Color(0.16f, 0.20f, 0.45f, 1f);
                colors.selectedColor = new Color(0.30f, 0.55f, 0.95f, 1f);
                colors.pressedColor = new Color(0.55f, 0.85f, 1f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => _clicks++);
                if (i == 1) _highlightButton = button; // HANGAR gets the synthetic click

                var label = MakeChild("Label", row);
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = Vector2.zero;
                label.offsetMax = Vector2.zero;
                var text = label.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = entries[i];
                text.fontSize = 30f;
                text.color = new Color(0.9f, 0.97f, 1f, 1f);
                text.alignment = TextAlignmentOptions.Center;
            }

            // Half-alpha CanvasGroup badge, bottom-left — alpha inheritance proof.
            var badge = MakeChild("Badge", canvasRect);
            badge.anchorMin = badge.anchorMax = Vector2.zero;
            badge.pivot = Vector2.zero;
            badge.anchoredPosition = new Vector2(36f, 36f);
            badge.sizeDelta = new Vector2(420f, 64f);
            badge.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var badgeImage = badge.gameObject.AddComponent<Image>();
            badgeImage.color = new Color(1f, 0.45f, 0.85f, 1f);
            badgeImage.raycastTarget = false;
            var badgeLabel = MakeChild("BadgeLabel", badge);
            badgeLabel.anchorMin = Vector2.zero;
            badgeLabel.anchorMax = Vector2.one;
            badgeLabel.offsetMin = Vector2.zero;
            badgeLabel.offsetMax = Vector2.zero;
            var badgeText = badgeLabel.gameObject.AddComponent<TextMeshProUGUI>();
            badgeText.text = "CANVASGROUP ALPHA 0.5";
            badgeText.fontSize = 24f;
            badgeText.color = Color.white;
            badgeText.alignment = TextAlignmentOptions.Center;
        }

        static RectTransform MakeChild(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, worldPositionStays: false);
            return rt;
        }

        static void MakeLabel(RectTransform parent, string content, float size, Color color, float width)
        {
            var rt = MakeChild($"Label_{content}", parent);
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = size * 1.3f;
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
        }

        void OnUpdate(double dt)
        {
            _loop.Tick((float)dt > 0.25f ? 0.25f : (float)dt);
            _frameIndex++;

            // Frame 30: click HANGAR through the REAL event stack — raycast at its
            // world-corner centre, press, release. Its selected tint must survive
            // into the screenshot.
            if (_frameIndex == 30 && _highlightButton != null)
            {
                var corners = new Vector3[4];
                ((RectTransform)_highlightButton.transform).GetWorldCorners(corners);
                var centre = new Vector2(
                    (corners[0].x + corners[2].x) * 0.5f,
                    (corners[0].y + corners[2].y) * 0.5f);
                _module.PointerDown(centre);
                _module.PointerUp(centre);
            }
        }

        unsafe void OnRender(double dt)
        {
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            CountDrawSurface();
            UiCanvasBridge.Render(_ui, _window.FramebufferSize.X, _window.FramebufferSize.Y);

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        void CountDrawSurface()
        {
            _graphicCount = 0;
            _textCount = 0;
            foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                if (graphic.isActiveAndEnabled) _graphicCount++;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text is Behaviour { isActiveAndEnabled: true }) _textCount++;
        }

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)[0];
            var panel = (RectTransform)canvas.transform.Find("Panel");
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"mode uidemo, scale {canvas.scaleFactor:F4}, graphics {_graphicCount}, texts {_textCount}, " +
                $"panel {panel.rect.width:F0}x{panel.rect.height:F0}, clicks {_clicks}, " +
                $"selected {(EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "none")}");
        }
    }
}
