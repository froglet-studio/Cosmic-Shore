using System;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc B part 2 — the Graphic/Image render-state family, headless. What matters
// headless is the REAL half: Image's ILayoutElement face (sprite pixels ÷
// pixelsPerUnit drive preferred size, so panels and fitters size themselves
// around icons), the sprite ppu ÷ canvas reference-ppu division, the sliced
// border minimum, priority resolution against an explicit LayoutElement, the
// dirty→canvas-slot re-solve on sprite swap, SetNativeSize on both image kinds,
// RawImage's deliberate ABSENCE of a layout opinion (faithful to the original),
// and the RequireComponent-equivalent Transform→RectTransform conversion on
// first rectTransform read. Pixel-facing state (fill, aspect, mask flags) is
// asserted as data until Arc C rasterizes it.
// ─────────────────────────────────────────────────────────────────────────────

public class EngineUiGraphicTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiGraphicTests));

    public void Dispose() => loop.Dispose();

    static Sprite MakeSprite(float width, float height, float pixelsPerUnit = 100f, Vector4 border = default)
        => Sprite.Create(new Texture2D((int)width, (int)height), new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, border);

    static RectTransform MakeRect(string name, RectTransform parent = null, float width = 0f, float height = 0f)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rt.SetParent(parent, worldPositionStays: false);
        rt.sizeDelta = new Vector2(width, height);
        return rt;
    }

    // ── the layout face: sprite pixels → preferred size ─────────────────

    [Fact]
    public void Image_PreferredSize_IsSpriteRectOverPixelsPerUnit()
    {
        var image = MakeRect("image").gameObject.AddComponent<Image>();

        Assert.Equal(0f, image.preferredWidth);   // no sprite → no opinion
        Assert.Equal(0f, image.preferredHeight);

        image.sprite = MakeSprite(64f, 32f);       // ppu 100 vs reference 100 → 1:1
        Assert.Equal(64f, image.preferredWidth);
        Assert.Equal(32f, image.preferredHeight);

        image.sprite = MakeSprite(64f, 32f, pixelsPerUnit: 200f); // denser sprite → half size
        Assert.Equal(32f, image.preferredWidth);
        Assert.Equal(16f, image.preferredHeight);

        Assert.Equal(0f, image.minWidth);          // Image never imposes a minimum
        Assert.Equal(-1f, image.flexibleWidth);
        Assert.Equal(0, image.layoutPriority);
    }

    [Fact]
    public void Image_PixelsPerUnitMultiplier_ScalesPreferredSize()
    {
        var image = MakeRect("image").gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);

        image.pixelsPerUnitMultiplier = 2f;        // denser mapping → half the units
        Assert.Equal(32f, image.preferredWidth);
        Assert.Equal(16f, image.preferredHeight);
    }

    [Fact]
    public void Image_SlicedType_ReportsBorderMinAsPreferred()
    {
        var image = MakeRect("image").gameObject.AddComponent<Image>();
        // Border layout is (left, bottom, right, top) — original convention.
        image.sprite = MakeSprite(64f, 32f, border: new Vector4(10f, 6f, 14f, 8f));

        image.type = Image.Type.Sliced;            // smallest unsquashed 9-slice
        Assert.Equal(24f, image.preferredWidth);   // left + right
        Assert.Equal(14f, image.preferredHeight);  // bottom + top

        image.type = Image.Type.Simple;            // back to the full rect
        Assert.Equal(64f, image.preferredWidth);
    }

    [Fact]
    public void Image_PixelsPerUnit_HonorsCanvasReferencePPU()
    {
        var canvasRt = MakeRect("canvas");
        canvasRt.gameObject.AddComponent<Canvas>();
        var scaler = canvasRt.gameObject.AddComponent<CanvasScaler>();
        scaler.referencePixelsPerUnit = 200f;

        var image = MakeRect("image", canvasRt).gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);       // sprite ppu 100 ÷ reference 200 = 0.5

        Assert.Equal(0.5f, image.pixelsPerUnit);
        Assert.Equal(128f, image.preferredWidth);  // fewer pixels per unit → bigger in units
        Assert.Equal(64f, image.preferredHeight);
    }

    [Fact]
    public void Image_LayoutElementOverride_WinsByPriority()
    {
        var rt = MakeRect("image");
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);

        var element = rt.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 200f;             // priority 1 beats Image's priority 0

        Assert.Equal(200f, LayoutUtility.GetPreferredSize(rt, 0));
        Assert.Equal(32f, LayoutUtility.GetPreferredSize(rt, 1)); // element has no height opinion → Image's stands
    }

    // ── the marquee: an icon sizes its panel through fitter + group ──────

    [Fact]
    public void Image_InFitterHuggedPanel_SizesThePanel()
    {
        var panel = MakeRect("panel");
        var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(10, 10, 10, 10);
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var image = MakeRect("icon", panel).gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        Assert.Equal(84f, panel.rect.width, 3);    // 64 + 10 + 10
        Assert.Equal(52f, panel.rect.height, 3);   // 32 + 10 + 10
        Assert.Equal(64f, image.rectTransform.rect.width, 3);
        Assert.Equal(32f, image.rectTransform.rect.height, 3);
    }

    [Fact]
    public void Image_SpriteSwap_ReSolvesLayout_InTheCanvasSlot()
    {
        var panel = MakeRect("panel");
        var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(10, 10, 10, 10);
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var image = MakeRect("icon", panel).gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);
        loop.Tick(1f / 60f);                       // canvas slot solves the initial marks
        Assert.Equal(84f, panel.rect.width, 3);

        image.sprite = MakeSprite(100f, 40f);      // SetAllDirty → queued for the canvas slot
        Assert.Equal(84f, panel.rect.width, 3);    // not yet — solves at end of tick, not inline
        loop.Tick(1f / 60f);
        Assert.Equal(120f, panel.rect.width, 3);   // 100 + padding
        Assert.Equal(60f, panel.rect.height, 3);   // 40 + padding
    }

    // ── native sizing ────────────────────────────────────────────────────

    [Fact]
    public void Image_SetNativeSize_SetsRectToSpritePixels()
    {
        var image = MakeRect("image", MakeRect("parent", width: 500f, height: 500f))
            .gameObject.AddComponent<Image>();
        image.sprite = MakeSprite(64f, 32f);

        image.SetNativeSize();

        Assert.Equal(64f, image.rectTransform.rect.width, 3);
        Assert.Equal(32f, image.rectTransform.rect.height, 3);
        Assert.Equal(image.rectTransform.anchorMin, image.rectTransform.anchorMax); // anchors collapsed
    }

    [Fact]
    public void RawImage_NoLayoutOpinion_ButSetNativeSizeUsesUvRect()
    {
        var rt = MakeRect("raw");
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = new Texture2D(256, 128);
        raw.uvRect = new Rect(0f, 0f, 0.5f, 0.25f);

        // Faithful: RawImage is NOT an ILayoutElement — no size opinion to layout.
        Assert.Equal(0f, LayoutUtility.GetPreferredSize(rt, 0));
        Assert.Equal(0f, LayoutUtility.GetPreferredSize(rt, 1));

        raw.SetNativeSize();                       // texture pixels × uv span
        Assert.Equal(128f, rt.rect.width, 3);
        Assert.Equal(32f, rt.rect.height, 3);
    }

    // ── state surface + conversion ───────────────────────────────────────

    [Fact]
    public void Graphic_RectTransform_ConvertsPlainTransformInPlace()
    {
        var go = new GameObject("plain");          // plain Transform host
        var image = go.AddComponent<Image>();

        var rt = image.rectTransform;              // RequireComponent-equivalent conversion
        Assert.IsType<RectTransform>(go.transform);
        Assert.Same(go.transform, rt);
    }

    [Fact]
    public void Image_OverrideSprite_FallsBackToAuthoredSprite()
    {
        var image = MakeRect("image").gameObject.AddComponent<Image>();
        var authored = MakeSprite(64f, 32f);
        var swapped = MakeSprite(100f, 40f);

        image.sprite = authored;
        Assert.Equal(64f, image.preferredWidth);

        image.overrideSprite = swapped;            // runtime swap wins while set
        Assert.Equal(100f, image.preferredWidth);

        image.overrideSprite = null;               // clearing falls back to authored
        Assert.Equal(64f, image.preferredWidth);
    }

    [Fact]
    public void GraphicState_Defaults_And_FillClamp()
    {
        var go = new GameObject("stateful", typeof(RectTransform));
        var image = go.AddComponent<Image>();

        Assert.Equal(Color.white, image.color);
        Assert.True(image.raycastTarget);
        Assert.True(image.maskable);
        Assert.Equal(1f, image.fillAmount);

        image.fillAmount = 1.7f;                   // boost-bar writes clamp (original contract)
        Assert.Equal(1f, image.fillAmount);
        image.fillAmount = -0.3f;
        Assert.Equal(0f, image.fillAmount);

        // Clipper + raycaster data surface round-trips (real behavior lands Arc C/D).
        var mask = go.AddComponent<Mask>();
        Assert.True(mask.showMaskGraphic);
        var rectMask = go.AddComponent<RectMask2D>();
        rectMask.softness = new Vector2Int(-5, 8); // negatives clamp to 0
        Assert.Equal(new Vector2Int(0, 8), rectMask.softness);
        var raycaster = go.AddComponent<GraphicRaycaster>();
        Assert.True(raycaster.ignoreReversedGraphics);
        Assert.Equal(GraphicRaycaster.BlockingObjects.None, raycaster.blockingObjects);
    }
}
