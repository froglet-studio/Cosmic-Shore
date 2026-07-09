using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using EngineVector3 = CosmicShore.Engine.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace CosmicShore.Client
{
    /// <summary>
    /// The Arc-C canvas bridge: renders the live engine canvas tree through
    /// <see cref="UiRenderer"/>. The walk IS the GraphicRaycaster's — hierarchy order
    /// = draw order, later siblings on top, nested canvases own their subtrees — so
    /// what you hit is exactly what you see. Screen-space world corners are already
    /// pixels (the Arc-A canvas-driven solve), so geometry lands pixel-exact with no
    /// per-frame math beyond the corner reads. CanvasGroup alpha multiplies down the
    /// tree (the menu's fade surfaces). Draw surface today: Image/RawImage → tinted
    /// rect at the world corners (sprite pixels arrive with the content pipeline),
    /// TMP_Text → atlas text with H/V alignment inside the rect.
    /// </summary>
    public static class UiCanvasBridge
    {
        public static void Render(UiRenderer ui, float screenWidth, float screenHeight)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length == 0) return;

            // Root screen-space canvases only, back-to-front by sorting order.
            var roots = new List<Canvas>();
            foreach (var canvas in canvases)
                if (canvas.isActiveAndEnabled && canvas.isRootCanvas
                    && canvas.renderMode != RenderMode.WorldSpace
                    && canvas.gameObject.activeInHierarchy)
                    roots.Add(canvas);
            roots.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            if (roots.Count == 0) return;

            ui.Begin(screenWidth, screenHeight);
            foreach (var canvas in roots)
                Walk(canvas.transform, ui, inheritedAlpha: 1f, isRoot: true);
            ui.End();
        }

        static readonly EngineVector3[] s_Corners = new EngineVector3[4];

        static void Walk(Transform node, UiRenderer ui, float inheritedAlpha, bool isRoot)
        {
            if (!node.gameObject.activeInHierarchy) return;
            if (!isRoot && node.gameObject.GetComponent<Canvas>() != null) return;

            var group = node.gameObject.GetComponent<CanvasGroup>();
            if (group != null && group.isActiveAndEnabled) inheritedAlpha *= group.alpha;
            if (inheritedAlpha <= 0.001f) return; // fully faded — nothing below shows

            if (node is RectTransform rect)
            {
                foreach (var graphic in node.gameObject.GetComponents<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    rect.GetWorldCorners(s_Corners); // BL, TL, TR, BR — screen pixels
                    float x = s_Corners[0].x, y = s_Corners[0].y;
                    float w = s_Corners[3].x - s_Corners[0].x;
                    float h = s_Corners[1].y - s_Corners[0].y;
                    var c = graphic.color;
                    ui.DrawRect(x, y, w, h, new Vector4(c.r, c.g, c.b, c.a * inheritedAlpha));
                }

                foreach (var text in node.gameObject.GetComponents<TMP_Text>())
                {
                    if (text is not Behaviour { isActiveAndEnabled: true }) continue;
                    if (string.IsNullOrEmpty(text.text)) continue;
                    rect.GetWorldCorners(s_Corners);
                    // fontSize is in CANVAS UNITS; the world scale (canvas scaleFactor
                    // through the parent chain) converts it to pixels, exactly as the
                    // corners were converted.
                    DrawAlignedText(ui, text, inheritedAlpha, rect.lossyScale.x,
                        s_Corners[0].x, s_Corners[0].y,
                        s_Corners[3].x - s_Corners[0].x, s_Corners[1].y - s_Corners[0].y);
                }
            }

            for (int i = 0; i < node.childCount; i++)
                Walk(node.GetChild(i), ui, inheritedAlpha, isRoot: false);
        }

        static void DrawAlignedText(UiRenderer ui, TMP_Text text, float inheritedAlpha,
            float worldScale, float x, float y, float w, float h)
        {
            float size = text.fontSize * worldScale;
            float textWidth = UiRenderer.MeasureText(text.text, size);

            // TMP alignment bit layout: low byte = horizontal (1 left, 2 center, 4 right),
            // high byte = vertical (1 top, 2 middle, 4 bottom).
            int alignment = (int)text.alignment;
            int horizontal = alignment & 0xFF;
            int vertical = alignment >> 8;

            float penX = horizontal switch
            {
                2 => x + (w - textWidth) * 0.5f,
                4 => x + w - textWidth,
                _ => x,
            };
            float penY = vertical switch
            {
                2 => y + (h - size) * 0.5f,
                4 => y,
                _ => y + h - size, // top (TMP default)
            };

            var c = text.color;
            ui.DrawText(text.text, penX, penY, size, new Vector4(c.r, c.g, c.b, c.a * inheritedAlpha));
        }
    }
}
