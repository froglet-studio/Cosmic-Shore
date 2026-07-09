using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc D — the event-system core, fully headless: the REAL GraphicRaycaster walk
// (draw order = hierarchy order, later siblings on top, raycastTarget + active
// gating, containment via the Arc-A world corners), the pointer state machine
// (enter/exit to the common hover root, press/click pairing through the
// ancestor-handler walk, drag threshold + press-release on cross-object drags),
// selection (SetSelectedGameObject select/deselect, pointer-down self-select /
// outside-press deselect), and Selectable/Button riding it all (state tints,
// onClick from a synthetic raycast click). Tests inject through
// StandaloneInputModule's synthetic API — no window anywhere.
// ─────────────────────────────────────────────────────────────────────────────

public class EngineUiEventTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiEventTests));
    readonly int _savedWidth = Screen.width;
    readonly int _savedHeight = Screen.height;

    readonly EventSystem eventSystem;
    readonly StandaloneInputModule module;
    readonly RectTransform canvasRoot;

    public EngineUiEventTests()
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

    /// <summary>A full-stretch, graphic-less container covering the whole canvas.</summary>
    RectTransform MakeHolder(string name)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(canvasRoot, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>An Image occupying pixels [x, x+w) × [y, y+h) (bottom-left origin).</summary>
    Image MakeImage(string name, float x, float y, float w, float h, RectTransform parent = null)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent != null ? parent : canvasRoot, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // anchor at parent's bottom-left
        rt.pivot = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return rt.gameObject.AddComponent<Image>();
    }

    sealed class Recorder : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        ISelectHandler, IDeselectHandler
    {
        public readonly List<string> events = new();
        public Vector2 lastPosition;

        void Record(string name, PointerEventData e = null)
        {
            events.Add(name);
            if (e != null) lastPosition = e.position;
        }

        public void OnPointerEnter(PointerEventData e) => Record("enter", e);
        public void OnPointerExit(PointerEventData e) => Record("exit", e);
        public void OnPointerDown(PointerEventData e) => Record("down", e);
        public void OnPointerUp(PointerEventData e) => Record("up", e);
        public void OnPointerClick(PointerEventData e) => Record("click", e);
        public void OnBeginDrag(PointerEventData e) => Record("beginDrag", e);
        public void OnDrag(PointerEventData e) => Record("drag", e);
        public void OnEndDrag(PointerEventData e) => Record("endDrag", e);
        public void OnSelect(BaseEventData e) => Record("select");
        public void OnDeselect(BaseEventData e) => Record("deselect");
    }

    /// <summary>Enter/exit-only recorder — safe on hover parents (no click/drag opinions).</summary>
    sealed class HoverRecorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public readonly List<string> events = new();
        public void OnPointerEnter(PointerEventData e) => events.Add("enter");
        public void OnPointerExit(PointerEventData e) => events.Add("exit");
    }

    // ── raycast walk ─────────────────────────────────────────────────────

    [Fact]
    public void RaycastAll_TopmostSiblingWins()
    {
        var a = MakeImage("a", 100f, 100f, 200f, 100f);           // [100,300)×[100,200)
        var b = MakeImage("b", 200f, 100f, 200f, 100f);           // [200,400)×[100,200) — later sibling, on top

        var results = new List<RaycastResult>();
        var probe = new PointerEventData(eventSystem) { position = new Vector2(250f, 150f) }; // overlap
        eventSystem.RaycastAll(probe, results);

        Assert.Equal(2, results.Count);
        Assert.Same(b.gameObject, results[0].gameObject);          // topmost first
        Assert.Same(a.gameObject, results[1].gameObject);

        probe.position = new Vector2(150f, 150f);                  // a-only region
        eventSystem.RaycastAll(probe, results);
        Assert.Single(results);
        Assert.Same(a.gameObject, results[0].gameObject);
    }

    [Fact]
    public void Raycast_RespectsTargetFlag_ActiveState_AndBounds()
    {
        var a = MakeImage("a", 100f, 100f, 200f, 100f);
        var results = new List<RaycastResult>();
        var probe = new PointerEventData(eventSystem) { position = new Vector2(150f, 150f) };

        a.raycastTarget = false;
        eventSystem.RaycastAll(probe, results);
        Assert.Empty(results);

        a.raycastTarget = true;
        a.gameObject.SetActive(false);
        eventSystem.RaycastAll(probe, results);
        Assert.Empty(results);

        a.gameObject.SetActive(true);
        probe.position = new Vector2(500f, 500f);                  // outside
        eventSystem.RaycastAll(probe, results);
        Assert.Empty(results);
    }

    // ── press/click pairing ──────────────────────────────────────────────

    [Fact]
    public void Click_DownUpOnSameObject_FiresDownUpClick()
    {
        var a = MakeImage("a", 100f, 100f, 200f, 100f);
        var recorder = a.gameObject.AddComponent<Recorder>();

        var p = new Vector2(150f, 150f);
        module.PointerDown(p);
        module.PointerUp(p);

        Assert.Equal(new[] { "enter", "down", "up", "click" }, recorder.events);
        Assert.Equal(p, recorder.lastPosition);                    // eventData.position reaches handlers
    }

    [Fact]
    public void Click_LandsOnAncestorHandler_WhenChildIsHit()
    {
        var holder = MakeHolder("holder");
        var recorder = holder.gameObject.AddComponent<Recorder>(); // handler lives on the parent
        MakeImage("icon", 100f, 100f, 200f, 100f, holder);         // hit target is the child

        var p = new Vector2(150f, 150f);
        module.PointerDown(p);
        module.PointerUp(p);

        Assert.Contains("click", recorder.events);                 // walked up from the icon
        Assert.Contains("down", recorder.events);
    }

    [Fact]
    public void Click_Suppressed_WhenReleasedOverAnotherObject()
    {
        var a = MakeImage("a", 100f, 100f, 100f, 100f);            // [100,200)
        var b = MakeImage("b", 300f, 100f, 100f, 100f);            // [300,400)
        var ra = a.gameObject.AddComponent<Recorder>();
        var rb = b.gameObject.AddComponent<Recorder>();

        module.PointerDown(new Vector2(150f, 150f));
        module.PointerUp(new Vector2(350f, 150f));

        Assert.Contains("down", ra.events);
        Assert.Contains("up", ra.events);                          // press target still gets the release
        Assert.DoesNotContain("click", ra.events);
        Assert.DoesNotContain("click", rb.events);
        Assert.DoesNotContain("up", rb.events);
    }

    // ── drag ─────────────────────────────────────────────────────────────

    [Fact]
    public void Drag_StartsPastThreshold_ThenEnds()
    {
        var a = MakeImage("a", 100f, 100f, 400f, 200f);
        var recorder = a.gameObject.AddComponent<Recorder>();

        module.PointerDown(new Vector2(150f, 150f));
        module.PointerMove(new Vector2(155f, 150f));               // 5px < threshold 10
        Assert.DoesNotContain("beginDrag", recorder.events);

        module.PointerMove(new Vector2(170f, 150f));               // 20px — drag starts + first drag
        Assert.Contains("beginDrag", recorder.events);
        Assert.Contains("drag", recorder.events);

        module.PointerUp(new Vector2(170f, 150f));
        Assert.Contains("endDrag", recorder.events);
    }

    [Fact]
    public void Drag_OnAnotherObject_ReleasesPressAndKillsClick()
    {
        // Press/click handlers on the child; the drag handler on the parent — the
        // ScreenSwitcher shape (panels drag while buttons live inside them).
        var holder = MakeHolder("holder");
        var dragRecorder = holder.gameObject.AddComponent<DragOnlyRecorder>();

        var child = MakeImage("child", 100f, 100f, 200f, 100f, holder);
        var clickRecorder = child.gameObject.AddComponent<ClickOnlyRecorder>();

        module.PointerDown(new Vector2(150f, 150f));
        module.PointerMove(new Vector2(180f, 150f));               // past threshold — drag begins on holder
        Assert.Contains("beginDrag", dragRecorder.events);
        Assert.Contains("up", clickRecorder.events);               // press released when the drag took over

        module.PointerUp(new Vector2(150f, 150f));                 // back over the child
        Assert.DoesNotContain("click", clickRecorder.events);      // the gesture was a drag, not a click
        Assert.Contains("endDrag", dragRecorder.events);
    }

    sealed class DragOnlyRecorder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public readonly List<string> events = new();
        public void OnBeginDrag(PointerEventData e) => events.Add("beginDrag");
        public void OnDrag(PointerEventData e) => events.Add("drag");
        public void OnEndDrag(PointerEventData e) => events.Add("endDrag");
    }

    sealed class ClickOnlyRecorder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public readonly List<string> events = new();
        public void OnPointerDown(PointerEventData e) => events.Add("down");
        public void OnPointerUp(PointerEventData e) => events.Add("up");
        public void OnPointerClick(PointerEventData e) => events.Add("click");
    }

    // ── enter/exit to the common root ────────────────────────────────────

    [Fact]
    public void EnterExit_WalksToTheCommonHoverRoot()
    {
        var holder = MakeHolder("holder");
        var pr = holder.gameObject.AddComponent<HoverRecorder>();

        var a = MakeImage("a", 100f, 100f, 100f, 100f, holder);
        var b = MakeImage("b", 300f, 100f, 100f, 100f, holder);
        var ar = a.gameObject.AddComponent<HoverRecorder>();
        var br = b.gameObject.AddComponent<HoverRecorder>();

        module.PointerMove(new Vector2(150f, 150f));               // over a
        Assert.Equal(new[] { "enter" }, ar.events);
        Assert.Equal(new[] { "enter" }, pr.events);                // ancestors enter too

        module.PointerMove(new Vector2(350f, 150f));               // a → b, same parent
        Assert.Equal(new[] { "enter", "exit" }, ar.events);
        Assert.Equal(new[] { "enter" }, br.events);
        Assert.Equal(new[] { "enter" }, pr.events);                // common root does NOT re-enter

        module.PointerMove(new Vector2(700f, 600f));               // off everything
        Assert.Equal(new[] { "enter", "exit" }, br.events);
        Assert.Equal(new[] { "enter", "exit" }, pr.events);
    }

    // ── selection ────────────────────────────────────────────────────────

    [Fact]
    public void Selection_FiresSelectAndDeselect()
    {
        var a = new GameObject("a").AddComponent<Recorder>();
        var b = new GameObject("b").AddComponent<Recorder>();

        eventSystem.SetSelectedGameObject(a.gameObject);
        Assert.Equal(new[] { "select" }, a.events);
        Assert.Same(a.gameObject, eventSystem.currentSelectedGameObject);

        eventSystem.SetSelectedGameObject(b.gameObject);
        Assert.Equal(new[] { "select", "deselect" }, a.events);
        Assert.Equal(new[] { "select" }, b.events);

        eventSystem.SetSelectedGameObject(null);
        Assert.Equal(new[] { "select", "deselect" }, b.events);
        Assert.Null(eventSystem.currentSelectedGameObject);
    }

    [Fact]
    public void Selectable_PointerDownSelects_OutsidePressDeselects()
    {
        var image = MakeImage("sel", 100f, 100f, 200f, 100f);
        var selectable = image.gameObject.AddComponent<Selectable>();

        module.PointerDown(new Vector2(150f, 150f));
        module.PointerUp(new Vector2(150f, 150f));
        Assert.Same(selectable.gameObject, eventSystem.currentSelectedGameObject);

        module.PointerDown(new Vector2(700f, 600f));               // empty space
        module.PointerUp(new Vector2(700f, 600f));
        Assert.Null(eventSystem.currentSelectedGameObject);
    }

    // ── Button on the full stack ─────────────────────────────────────────

    [Fact]
    public void Button_SyntheticClick_InvokesOnClick_AndTintsStates()
    {
        var image = MakeImage("button", 100f, 100f, 200f, 100f);
        var button = image.gameObject.AddComponent<Button>();      // adopts the Image as targetGraphic

        int clicks = 0;
        button.onClick.AddListener(() => clicks++);

        var p = new Vector2(150f, 150f);
        module.PointerDown(p);
        Assert.Equal(button.colors.pressedColor, image.color);     // ColorTint while held

        module.PointerUp(p);
        Assert.Equal(1, clicks);
        Assert.Equal(button.colors.selectedColor, image.color);    // pressing selected it

        button.interactable = false;                               // gate closes
        module.PointerDown(p);
        module.PointerUp(p);
        Assert.Equal(1, clicks);
        Assert.Equal(button.colors.disabledColor, image.color);
    }

    [Fact]
    public void EventSystem_Current_And_PointerOverTracking()
    {
        Assert.Same(eventSystem, EventSystem.current);

        MakeImage("a", 100f, 100f, 200f, 100f);
        module.PointerMove(new Vector2(150f, 150f));
        Assert.True(eventSystem.IsPointerOverGameObject());

        module.PointerMove(new Vector2(700f, 600f));
        Assert.False(eventSystem.IsPointerOverGameObject());
    }
}
