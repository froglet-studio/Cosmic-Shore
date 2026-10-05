using System;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc D part 2 — navigation + the used interactive controls, headless: the
// automatic navigation graph (nearest-in-direction by rect position, original
// dot ÷ distance² scoring), explicit links, the sendNavigationEvents gate the
// menu flips for freestyle input ownership, Submit driving Button through the
// selection, Toggle's isOn/notify/checkmark-alpha model, Slider's clamp/round/
// notify value model, and ScrollRect riding the proven drag pipeline (clamped
// pan, wheel sensitivity, velocity fling decaying in LateUpdate, normalized
// travel mapping).
// ─────────────────────────────────────────────────────────────────────────────

public class EngineUiNavigationTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiNavigationTests));
    readonly int _savedWidth = Screen.width;
    readonly int _savedHeight = Screen.height;

    readonly EventSystem eventSystem;
    readonly StandaloneInputModule module;
    readonly RectTransform canvasRoot;

    public EngineUiNavigationTests()
    {
        Screen.width = 1280;
        Screen.height = 720;

        var esGo = new GameObject("EventSystem");
        eventSystem = esGo.AddComponent<EventSystem>();
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

    Image MakeImage(string name, float x, float y, float w, float h, RectTransform parent = null)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent != null ? parent : canvasRoot, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return rt.gameObject.AddComponent<Image>();
    }

    Button MakeButton(string name, float x, float y) =>
        MakeImage(name, x, y, 100f, 50f).gameObject.AddComponent<Button>();

    // ── the navigation graph ─────────────────────────────────────────────

    [Fact]
    public void Move_StepsSelection_AlongTheAutomaticGraph()
    {
        var left = MakeButton("left", 100f, 300f);
        var mid = MakeButton("mid", 300f, 300f);
        var right = MakeButton("right", 500f, 300f);

        eventSystem.SetSelectedGameObject(left.gameObject);

        module.Move(MoveDirection.Right);
        Assert.Same(mid.gameObject, eventSystem.currentSelectedGameObject);

        module.Move(MoveDirection.Right);
        Assert.Same(right.gameObject, eventSystem.currentSelectedGameObject);

        module.Move(MoveDirection.Left);
        Assert.Same(mid.gameObject, eventSystem.currentSelectedGameObject);

        module.Move(MoveDirection.Right);
        module.Move(MoveDirection.Right);                          // off the end — stays put
        Assert.Same(right.gameObject, eventSystem.currentSelectedGameObject);
    }

    [Fact]
    public void Move_IsGatedOn_SendNavigationEvents()
    {
        var left = MakeButton("left", 100f, 300f);
        MakeButton("right", 300f, 300f);

        eventSystem.SetSelectedGameObject(left.gameObject);
        eventSystem.sendNavigationEvents = false;                  // freestyle owns the pad

        module.Move(MoveDirection.Right);
        Assert.Same(left.gameObject, eventSystem.currentSelectedGameObject);

        eventSystem.sendNavigationEvents = true;
        module.Move(MoveDirection.Right);
        Assert.NotSame(left.gameObject, eventSystem.currentSelectedGameObject);
    }

    [Fact]
    public void Move_ExplicitLinks_OverridePositions()
    {
        var a = MakeButton("a", 100f, 300f);
        MakeButton("near", 300f, 300f);                            // positionally next
        var far = MakeButton("far", 900f, 100f);

        a.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = far };
        eventSystem.SetSelectedGameObject(a.gameObject);

        module.Move(MoveDirection.Right);
        Assert.Same(far.gameObject, eventSystem.currentSelectedGameObject); // authored link wins
    }

    [Fact]
    public void Move_SkipsNonInteractableCandidates()
    {
        var left = MakeButton("left", 100f, 300f);
        var mid = MakeButton("mid", 300f, 300f);
        var right = MakeButton("right", 500f, 300f);
        mid.interactable = false;

        eventSystem.SetSelectedGameObject(left.gameObject);
        module.Move(MoveDirection.Right);
        Assert.Same(right.gameObject, eventSystem.currentSelectedGameObject); // hops the disabled one
    }

    [Fact]
    public void Submit_DrivesTheSelectedButton_NavGated()
    {
        var button = MakeButton("button", 100f, 300f);
        int clicks = 0;
        button.onClick.AddListener(() => clicks++);

        eventSystem.SetSelectedGameObject(button.gameObject);
        module.Submit();
        Assert.Equal(1, clicks);

        eventSystem.sendNavigationEvents = false;
        module.Submit();
        Assert.Equal(1, clicks);                                   // gate closed
    }

    // ── Toggle ───────────────────────────────────────────────────────────

    [Fact]
    public void Toggle_Click_FlipsState_NotifiesAndShowsCheckmark()
    {
        var image = MakeImage("toggle", 100f, 100f, 200f, 100f);
        var toggle = image.gameObject.AddComponent<Toggle>();
        var checkmark = MakeImage("check", 10f, 10f, 20f, 20f, (RectTransform)image.transform);
        toggle.graphic = checkmark;

        bool? reported = null;
        toggle.onValueChanged.AddListener(v => reported = v);

        Assert.True(toggle.isOn);                                  // original default

        var p = new Vector2(150f, 150f);
        module.PointerDown(p);
        module.PointerUp(p);                                       // click → off
        Assert.False(toggle.isOn);
        Assert.False(reported!.Value);
        Assert.Equal(0f, checkmark.color.a);                       // checkmark hidden

        reported = null;
        toggle.SetIsOnWithoutNotify(true);                         // silent write
        Assert.True(toggle.isOn);
        Assert.Null(reported);
        Assert.Equal(1f, checkmark.color.a);

        toggle.interactable = false;
        module.PointerDown(p);
        module.PointerUp(p);
        Assert.True(toggle.isOn);                                  // gate closed
    }

    // ── Slider ───────────────────────────────────────────────────────────

    [Fact]
    public void Slider_ValueModel_ClampsRoundsAndNotifies()
    {
        var slider = new GameObject("slider", typeof(RectTransform)).AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 10f;

        float? reported = null;
        slider.onValueChanged.AddListener(v => reported = v);

        slider.value = 25f;                                        // clamps to max
        Assert.Equal(10f, slider.value);
        Assert.Equal(10f, reported!.Value);

        slider.wholeNumbers = true;
        slider.value = 4.6f;                                       // rounds
        Assert.Equal(5f, slider.value);

        reported = null;
        slider.SetValueWithoutNotify(2f);                          // silent write
        Assert.Equal(2f, slider.value);
        Assert.Null(reported);

        Assert.Equal(0.2f, slider.normalizedValue, 3);
        slider.normalizedValue = 1f;
        Assert.Equal(10f, slider.value);
    }

    // ── ScrollRect over the drag pipeline ────────────────────────────────

    (ScrollRect scroll, RectTransform content) MakeScrollView()
    {
        var view = MakeImage("view", 100f, 100f, 200f, 200f);      // viewport + hit target
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var content = (RectTransform)new GameObject("content", typeof(RectTransform)).transform;
        content.SetParent(view.transform, worldPositionStays: false);
        content.anchorMin = content.anchorMax = new Vector2(0f, 1f); // top-left anchored list
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(400f, 600f);
        scroll.content = content;
        return (scroll, content);
    }

    [Fact]
    public void ScrollRect_DragPansContent_WithinClampedBounds()
    {
        var (_, content) = MakeScrollView();

        module.PointerDown(new Vector2(200f, 200f));               // inside the view
        module.PointerMove(new Vector2(150f, 260f));               // past threshold — drag ANCHORS here
        Assert.Equal(Vector2.zero, content.anchoredPosition);      // (original: threshold distance is eaten)

        module.PointerMove(new Vector2(100f, 320f));               // now left 50, up 60 from the anchor
        Assert.Equal(new Vector2(-50f, 60f), content.anchoredPosition);

        module.PointerMove(new Vector2(-800f, 900f));              // way past the slack
        Assert.Equal(new Vector2(-200f, 400f), content.anchoredPosition); // clamped to slack (400-200, 600-200)
        module.PointerUp(new Vector2(-800f, 900f));
    }

    [Fact]
    public void ScrollRect_WheelScrolls_BySensitivity()
    {
        var (scroll, content) = MakeScrollView();
        scroll.scrollSensitivity = 20f;

        module.Scroll(new Vector2(0f, -1.5f), new Vector2(200f, 200f)); // wheel down → content up
        Assert.Equal(new Vector2(0f, 30f), content.anchoredPosition);
    }

    [Fact]
    public void ScrollRect_VelocityFling_DecaysInLateUpdate()
    {
        var (scroll, content) = MakeScrollView();
        loop.Tick(1f / 60f);                                       // Start runs — LateUpdate live

        scroll.velocity = new Vector2(0f, 300f);                   // the GameEventFeed fling
        loop.Tick(1f / 60f);
        Assert.Equal(5f, content.anchoredPosition.y, 2);           // 300 × 1/60
        Assert.True(scroll.velocity.y < 300f);                     // decaying

        loop.Run(600, 1f / 60f);                                   // long tail
        Assert.Equal(Vector2.zero, scroll.velocity);               // fully decayed
        Assert.True(content.anchoredPosition.y <= 400f);           // never past the slack
    }

    [Fact]
    public void ScrollRect_NormalizedPositions_MapTravel()
    {
        var (scroll, content) = MakeScrollView();

        Assert.Equal(0f, scroll.verticalNormalizedPosition);

        scroll.verticalNormalizedPosition = 0.5f;                  // half the 400 slack
        Assert.Equal(200f, content.anchoredPosition.y, 2);
        Assert.Equal(0.5f, scroll.verticalNormalizedPosition, 3);

        scroll.horizontalNormalizedPosition = 1f;                  // full 200 slack left
        Assert.Equal(-200f, content.anchoredPosition.x, 2);
        Assert.Equal(1f, scroll.horizontalNormalizedPosition, 3);
    }
}
