using System;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// The uGUI behaviour the menus showed broken in Amoebius on 2026-10-13 (Profile, Toybox, the card
// grids, scrolling), each held to what Unity does: the scroll view's wheel, spring and fling, the
// slider and scroll bar driving their fill and handle, a cloned card keeping its children on their
// anchors, canvas text staying orthographic, SetParent keeping world scale, and the
// OnRectTransformDimensionsChange message a root layout group re-lays out on.
public class EngineUiParityTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiParityTests));
    readonly int _savedWidth = Screen.width;
    readonly int _savedHeight = Screen.height;
    readonly StandaloneInputModule module;
    readonly RectTransform canvasRoot;

    public EngineUiParityTests()
    {
        Screen.width = 1280;
        Screen.height = 720;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        module = esGo.AddComponent<StandaloneInputModule>();
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.AddComponent<Canvas>();
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGo.transform;
    }

    public void Dispose()
    {
        Screen.width = _savedWidth;
        Screen.height = _savedHeight;
        loop.Dispose();
    }

    RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 pivot = default)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent ?? canvasRoot, worldPositionStays: false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        return rt;
    }

    /// <summary>A 200x200 view at (100,100) over a 400x600 top-left anchored list.</summary>
    (ScrollRect scroll, RectTransform content) MakeScrollView(ScrollRect.MovementType movement, bool horizontal = true, bool vertical = true)
    {
        var view = MakeRect("view", null, Vector2.zero, Vector2.zero, new Vector2(200f, 200f));
        view.anchoredPosition = new Vector2(100f, 100f);
        view.gameObject.AddComponent<Image>();
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.movementType = movement;
        scroll.horizontal = horizontal;
        scroll.vertical = vertical;
        var content = MakeRect("content", view, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, 600f), new Vector2(0f, 1f));
        scroll.content = content;
        return (scroll, content);
    }

    // ── ScrollRect ───────────────────────────────────────────────────────

    [Fact]
    public void AHorizontalOnlyTrack_TakesTheVerticalWheel()
    {
        // The Profile quest track: the wheel does nothing on it unless its larger axis is remapped.
        var (scroll, content) = MakeScrollView(ScrollRect.MovementType.Clamped, horizontal: true, vertical: false);
        scroll.scrollSensitivity = 20f;
        module.Scroll(new Vector2(0f, 1.5f), new Vector2(200f, 200f)); // wheel up → scrolls forward
        Assert.Equal(new Vector2(-30f, 0f), content.anchoredPosition);
    }

    [Fact]
    public void TheUiModule_TakesSixUnitsPerWheelNotch()
    {
        var input = new GameObject("ui").AddComponent<CosmicShore.Engine.InputSystem.UI.InputSystemUIInputModule>();
        Assert.Equal(6f, input.scrollDeltaPerTick);
    }

    [Fact]
    public void ADrag_LeavesInertia_AndAPressStopsIt()
    {
        var (scroll, content) = MakeScrollView(ScrollRect.MovementType.Clamped);
        loop.Tick(1f / 60f);
        module.PointerDown(new Vector2(200f, 200f));
        module.PointerMove(new Vector2(200f, 220f));                  // past the threshold: the drag anchors here
        loop.Tick(1f / 60f);
        for (int i = 1; i <= 3; i++)
        {
            module.PointerMove(new Vector2(200f, 220f + 20f * i));
            loop.Tick(1f / 60f);
        }
        module.PointerUp(new Vector2(200f, 280f));
        Assert.Equal(60f, content.anchoredPosition.y, 2);
        Assert.True(scroll.velocity.y > 100f, $"velocity {scroll.velocity.y}");   // the content's motion, carried on release

        loop.Tick(1f / 60f);
        Assert.True(content.anchoredPosition.y > 60f);                // coasting

        module.PointerDown(new Vector2(200f, 200f));                  // a press over the view catches it
        Assert.Equal(Vector2.zero, scroll.velocity);
        module.PointerUp(new Vector2(200f, 200f));
    }

    [Fact]
    public void ElasticContent_RubberBandsPastTheEdge_AndSpringsBack()
    {
        var (_, content) = MakeScrollView(ScrollRect.MovementType.Elastic);
        loop.Tick(1f / 60f);
        module.PointerDown(new Vector2(200f, 200f));
        module.PointerMove(new Vector2(200f, 180f));                  // threshold
        module.PointerMove(new Vector2(200f, 80f));                   // 100 down, past the top edge
        float rubber = (1f - 1f / (100f * 0.55f / 200f + 1f)) * 200f;
        Assert.Equal(-rubber, content.anchoredPosition.y, 2);
        module.PointerUp(new Vector2(200f, 80f));

        loop.Run(120, 1f / 60f);
        Assert.True(Math.Abs(content.anchoredPosition.y) < 0.5f, $"y {content.anchoredPosition.y}");
    }

    [Fact]
    public void AScrollView_DrivesItsScrollbar_AndTheScrollbarDrivesTheView()
    {
        var (scroll, content) = MakeScrollView(ScrollRect.MovementType.Clamped);
        var bar = MakeRect("bar", null, Vector2.zero, Vector2.zero, new Vector2(20f, 200f));
        var track = MakeRect("track", bar, Vector2.zero, Vector2.one, Vector2.zero);
        var handle = MakeRect("handle", track, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scroll.verticalScrollbar = scrollbar;

        loop.Tick(1f / 60f);
        Assert.Equal(200f / 600f, scrollbar.size, 3);                 // the view's share of the content
        Assert.Equal(1f, scrollbar.value, 3);                         // at the top
        Assert.Equal(1f - scrollbar.size, handle.anchorMin.y, 3);     // the handle sits at the top of its track
        Assert.Equal(1f, handle.anchorMax.y, 3);

        scrollbar.value = 0f;                                         // drag the bar to the bottom
        Assert.Equal(400f, content.anchoredPosition.y, 2);
    }

    // ── Slider ───────────────────────────────────────────────────────────

    (Slider slider, RectTransform fill, RectTransform handle) MakeSlider(bool filledImage)
    {
        var root = MakeRect("slider", null, Vector2.zero, Vector2.zero, new Vector2(200f, 20f));
        var fillArea = MakeRect("Fill Area", root, Vector2.zero, Vector2.one, Vector2.zero);
        var fill = MakeRect("Fill", fillArea, Vector2.zero, Vector2.zero, Vector2.zero);  // Unity saves driven anchors as zero
        var image = fill.gameObject.AddComponent<Image>();
        if (filledImage) image.type = Image.Type.Filled;
        var handleArea = MakeRect("Handle Slide Area", root, Vector2.zero, Vector2.one, Vector2.zero);
        var handle = MakeRect("Handle", handleArea, Vector2.zero, Vector2.zero, new Vector2(20f, 0f));
        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        return (slider, fill, handle);
    }

    [Fact]
    public void ASlider_DrivesItsFillAndHandle()
    {
        var (slider, fill, handle) = MakeSlider(filledImage: false);
        slider.value = 0.25f;
        Assert.Equal(Vector2.zero, fill.anchorMin);
        Assert.Equal(new Vector2(0.25f, 1f), fill.anchorMax);
        Assert.Equal(new Vector2(0.25f, 0f), handle.anchorMin);
        Assert.Equal(new Vector2(0.25f, 1f), handle.anchorMax);
        Assert.Equal(50f, fill.rect.width, 3);

        slider.direction = Slider.Direction.RightToLeft;
        Assert.Equal(new Vector2(0.75f, 0f), fill.anchorMin);
        Assert.Equal(Vector2.one, fill.anchorMax);
    }

    [Fact]
    public void AFilledImageFill_SpansTheTrack_AndFillsByAmount()
    {
        var (slider, fill, _) = MakeSlider(filledImage: true);
        slider.maxValue = 7f;
        slider.wholeNumbers = true;
        slider.value = 2f;
        Assert.Equal(Vector2.zero, fill.anchorMin);
        Assert.Equal(Vector2.one, fill.anchorMax);
        Assert.Equal(2f / 7f, fill.GetComponent<Image>().fillAmount, 4);
    }

    [Fact]
    public void ASliderDisabledBeforeItsOnEnable_StillDrawsItsValue()
    {
        // The quest track's display-only bar: its owner disables it during scene load, which may
        // come before the slider's own OnEnable.
        var root = MakeRect("bar", null, Vector2.zero, Vector2.zero, new Vector2(300f, 20f));
        root.gameObject.SetActive(false);
        var fillArea = MakeRect("Fill Area", root, Vector2.zero, Vector2.one, Vector2.zero);
        var fill = MakeRect("Fill", fillArea, Vector2.zero, Vector2.zero, Vector2.zero);
        fill.gameObject.AddComponent<Image>().type = Image.Type.Filled;
        var slider = root.gameObject.AddComponent<Slider>();
        var field = typeof(Slider).GetField("m_FillRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(slider, fill);                                 // as the scene loads it
        slider.enabled = false;
        root.gameObject.SetActive(true);

        slider.maxValue = 4f;
        slider.value = 1f;
        Assert.Equal(Vector2.one, fill.anchorMax);
        Assert.Equal(0.25f, fill.GetComponent<Image>().fillAmount, 4);
    }

    // ── cloning, text, parenting ─────────────────────────────────────────

    [Fact]
    public void ACloneOfASceneCard_KeepsItsChildrenOnTheirAnchors()
    {
        // A 275x203 template inside a grid, cloned and resized to 400x250 by the grid: the title
        // stays on its stretched anchors (the Toybox labels fell below their cards).
        var grid = MakeRect("grid", null, Vector2.zero, Vector2.one, Vector2.zero);
        var template = MakeRect("ToyCardTemplate", grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(275.03f, 202.72f), new Vector2(0.5f, 0.5f));
        template.anchoredPosition = new Vector2(159.51f, -171.36f);
        var title = MakeRect("GameTitle", template, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.34f), Vector2.zero, new Vector2(0.5f, 0.5f));

        var clone = Object.Instantiate(template.gameObject, grid);
        var cloneRect = (RectTransform)clone.transform;
        var cloneTitle = (RectTransform)cloneRect.Find("GameTitle");
        Assert.Equal(template.anchoredPosition, cloneRect.anchoredPosition);
        Assert.Equal(Vector2.zero, cloneTitle.anchoredPosition);
        Assert.Equal(title.anchorMin, cloneTitle.anchorMin);

        cloneRect.sizeDelta = new Vector2(400f, 250f);
        var r = cloneTitle.rect;
        Assert.Equal(0.88f * 400f, r.width, 2);
        Assert.Equal(Vector2.zero, cloneTitle.anchoredPosition);      // still centred on its anchors
    }

    [Fact]
    public void CanvasText_IsOrthographic_WhateverTheSceneSaved()
    {
        var go = new GameObject("label", typeof(RectTransform));
        go.SetActive(false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.isOrthographic = false;                                  // as a scene saved with m_isOrthographic: 0
        go.SetActive(true);                                           // Awake
        Assert.True(text.isOrthographic);

        var world = new GameObject("sign").AddComponent<TextMeshPro>();
        Assert.False(world.isOrthographic);                           // 3D text keeps its own mode
    }

    [Fact]
    public void SetParent_KeepingWorldPosition_AlsoKeepsWorldScale()
    {
        var parent = new GameObject("parent").transform;
        parent.localScale = new Vector3(2f, 2f, 2f);
        var child = new GameObject("child").transform;
        child.position = new Vector3(4f, 0f, 0f);

        child.SetParent(parent, worldPositionStays: true);
        Assert.Equal(new Vector3(0.5f, 0.5f, 0.5f), child.localScale);
        Assert.Equal(Vector3.one, child.lossyScale);
        Assert.Equal(4f, child.position.x, 4);

        var other = new GameObject("other").transform;
        other.SetParent(parent, worldPositionStays: false);           // local values kept
        Assert.Equal(Vector3.one, other.localScale);
    }

    // ── OnRectTransformDimensionsChange ──────────────────────────────────

    sealed class DimensionCounter : MonoBehaviour
    {
        public int Count;
        void OnRectTransformDimensionsChange() => Count++;
    }

    [Fact]
    public void ARectThatChangesSize_IsToldOnce()
    {
        var panel = MakeRect("panel", null, Vector2.zero, Vector2.zero, new Vector2(100f, 100f));
        var stretched = MakeRect("stretched", panel, Vector2.zero, Vector2.one, Vector2.zero);
        var counter = stretched.gameObject.AddComponent<DimensionCounter>();

        loop.Tick(1f / 60f);
        Assert.Equal(0, counter.Count);                               // first sight is not a change

        panel.sizeDelta = new Vector2(250f, 100f);                    // the parent stretches it
        loop.Tick(1f / 60f);
        Assert.Equal(1, counter.Count);
        loop.Tick(1f / 60f);
        Assert.Equal(1, counter.Count);

        counter.enabled = false;
        panel.sizeDelta = new Vector2(300f, 100f);
        loop.Tick(1f / 60f);
        Assert.Equal(1, counter.Count);                               // disabled behaviours hear nothing
    }

    [Fact]
    public void ARootLayoutGroup_ReLaysOut_WhenAScriptResizesItsParent()
    {
        var panel = MakeRect("panel", null, Vector2.zero, Vector2.zero, new Vector2(300f, 300f));
        var list = MakeRect("list", panel, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0.5f, 0.5f));
        var group = list.gameObject.AddComponent<VerticalLayoutGroup>();
        group.childControlWidth = true;
        group.childForceExpandWidth = true;
        var item = MakeRect("item", list, Vector2.zero, Vector2.zero, new Vector2(10f, 40f));

        loop.Tick(1f / 60f);
        Assert.Equal(300f, item.rect.width, 2);

        panel.sizeDelta = new Vector2(500f, 300f);                    // no screen change: only the message re-lays it out
        loop.Tick(1f / 60f);
        Assert.Equal(500f, item.rect.width, 2);
    }
}
