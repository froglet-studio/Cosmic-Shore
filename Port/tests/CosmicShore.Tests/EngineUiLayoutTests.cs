using System;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc B — the UGUI layout core, fully headless: Horizontal/Vertical layout
// groups (the canonical two-branch solve: min→preferred lerp, flexible
// distribution, surplus alignment, controlled vs free child sizes),
// GridLayoutGroup (constraints, corners), LayoutElement opinions + ignoreLayout,
// ContentSizeFitter self-sizing, nested groups solved in ONE rebuild, and the
// queued rebuild flushing in the GameLoop's canvas-update slot. Assertions read
// in a top-left-origin, y-down frame via RectInParent.
// ─────────────────────────────────────────────────────────────────────────────

public class EngineUiLayoutTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiLayoutTests));

    public void Dispose() => loop.Dispose();

    static RectTransform MakeRect(string name, RectTransform parent = null, float width = 0f, float height = 0f)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rt.SetParent(parent, worldPositionStays: false);
        rt.sizeDelta = new Vector2(width, height);
        return rt;
    }

    static LayoutElement WithPreferred(RectTransform rt, float width = -1f, float height = -1f,
        float flexW = -1f, float flexH = -1f, float minW = -1f, float minH = -1f)
    {
        var el = rt.gameObject.AddComponent<LayoutElement>();
        el.preferredWidth = width;
        el.preferredHeight = height;
        el.flexibleWidth = flexW;
        el.flexibleHeight = flexH;
        el.minWidth = minW;
        el.minHeight = minH;
        return el;
    }

    /// <summary>The child's rect expressed in its parent's top-left-origin, y-DOWN frame.</summary>
    static Rect RectInParent(RectTransform child)
    {
        var parent = (RectTransform)child.parent;
        Rect parentRect = parent.rect;
        Rect childRect = child.rect;
        Vector3 local = child.localPosition;
        float x = (local.x + childRect.xMin) - parentRect.xMin;
        float yFromTop = parentRect.yMax - (local.y + childRect.yMax);
        return new Rect(x, yFromTop, childRect.width, childRect.height);
    }

    static void AssertRect(float x, float y, float w, float h, RectTransform child, float tol = 1e-3f)
    {
        Rect actual = RectInParent(child);
        Assert.True(
            Math.Abs(actual.x - x) <= tol && Math.Abs(actual.y - y) <= tol &&
            Math.Abs(actual.width - w) <= tol && Math.Abs(actual.height - h) <= tol,
            $"expected (x:{x}, y:{y}, w:{w}, h:{h}), got {actual}");
    }

    // ── Vertical group: the main-axis branches ───────────────────────────

    [Fact]
    public void VerticalGroup_StacksPreferredHeights_AndStretchesWidths()
    {
        var group = MakeRect("group", width: 300f, height: 400f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 5f;
        v.childForceExpandHeight = false; // heights hug preferred; widths force-expand (default)

        var a = MakeRect("a", group);
        WithPreferred(a, height: 50f);
        var b = MakeRect("b", group);
        WithPreferred(b, height: 70f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        AssertRect(10f, 10f, 280f, 50f, a);      // inner width 280 (force-expanded)
        AssertRect(10f, 65f, 280f, 70f, b);      // 10 + 50 + 5 spacing
    }

    [Fact]
    public void VerticalGroup_DistributesSurplus_ByFlexibleWeight()
    {
        var group = MakeRect("group", width: 100f, height: 400f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childForceExpandHeight = false;

        var fixedChild = MakeRect("fixed", group);
        WithPreferred(fixedChild, height: 50f);
        var flexChild = MakeRect("flex", group);
        WithPreferred(flexChild, height: 50f, flexH: 1f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        // Surplus 300 goes entirely to the one flexible child.
        AssertRect(0f, 0f, 100f, 50f, fixedChild);
        AssertRect(0f, 50f, 100f, 350f, flexChild);
    }

    [Fact]
    public void VerticalGroup_ShrinksBetweenMinAndPreferred_WhenSpaceIsShort()
    {
        var group = MakeRect("group", width: 100f, height: 120f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childForceExpandHeight = false;

        var a = MakeRect("a", group);
        WithPreferred(a, height: 100f, minH: 20f);
        var b = MakeRect("b", group);
        WithPreferred(b, height: 100f, minH: 20f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        // totalMin 40, totalPreferred 200 → lerp = (120−40)/160 = 0.5 → each 20+80·0.5 = 60.
        AssertRect(0f, 0f, 100f, 60f, a);
        AssertRect(0f, 60f, 100f, 60f, b);
    }

    [Fact]
    public void VerticalGroup_AlignsTheRun_WhenNothingFlexes()
    {
        var group = MakeRect("group", width: 200f, height: 300f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        v.childControlWidth = false; // children keep their own widths

        var a = MakeRect("a", group, width: 80f, height: 0f);
        WithPreferred(a, height: 50f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        // Height run (50) centres in 300 → starts at 125; free width (80) centres in 200 → 60.
        AssertRect(60f, 125f, 80f, 50f, a);
    }

    [Fact]
    public void HorizontalGroup_MirrorsTheSolveOnX()
    {
        var group = MakeRect("group", width: 400f, height: 100f);
        var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 4, 4);
        h.spacing = 6f;
        h.childForceExpandWidth = false;

        var a = MakeRect("a", group);
        WithPreferred(a, width: 60f);
        var b = MakeRect("b", group);
        WithPreferred(b, width: 90f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        AssertRect(8f, 4f, 60f, 92f, a);          // inner height 92 (force-expanded)
        AssertRect(8f + 60f + 6f, 4f, 90f, 92f, b);
    }

    [Fact]
    public void IgnoreLayout_ChildIsSkippedEntirely()
    {
        var group = MakeRect("group", width: 100f, height: 200f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childForceExpandHeight = false;

        var ignored = MakeRect("ignored", group, width: 33f, height: 44f);
        var el = WithPreferred(ignored, height: 44f);
        el.ignoreLayout = true;
        ignored.anchoredPosition = new Vector2(7f, 7f);

        var laidOut = MakeRect("laidOut", group);
        WithPreferred(laidOut, height: 50f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        AssertRect(0f, 0f, 100f, 50f, laidOut);          // starts at the top: no gap for `ignored`
        Assert.Equal(new Vector2(7f, 7f), ignored.anchoredPosition); // untouched by the group
    }

    // ── Grid ─────────────────────────────────────────────────────────────

    [Fact]
    public void Grid_FixedColumns_FillsRowMajor_WithPaddingAndSpacing()
    {
        var group = MakeRect("grid", width: 300f, height: 300f);
        var grid = group.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(50f, 40f);
        grid.spacing = new Vector2(5f, 5f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var cells = new RectTransform[5];
        for (int i = 0; i < 5; i++) cells[i] = MakeRect($"cell{i}", group);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        AssertRect(10f, 10f, 50f, 40f, cells[0]);
        AssertRect(65f, 10f, 50f, 40f, cells[1]);
        AssertRect(10f, 55f, 50f, 40f, cells[2]);
        AssertRect(65f, 55f, 50f, 40f, cells[3]);
        AssertRect(10f, 100f, 50f, 40f, cells[4]);
    }

    [Fact]
    public void Grid_UpperRightCorner_MirrorsColumns()
    {
        var group = MakeRect("grid", width: 300f, height: 300f);
        var grid = group.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(50f, 40f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startCorner = GridLayoutGroup.Corner.UpperRight;

        var first = MakeRect("first", group);
        var second = MakeRect("second", group);
        var third = MakeRect("third", group);

        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        // Column index mirrors: first fills the RIGHT cell of row 0.
        AssertRect(50f, 0f, 50f, 40f, first);
        AssertRect(0f, 0f, 50f, 40f, second);
        AssertRect(50f, 40f, 50f, 40f, third);
    }

    // ── Fitters + nesting ────────────────────────────────────────────────

    [Fact]
    public void ContentSizeFitter_HugsTheGroupsPreferredSize()
    {
        var root = MakeRect("root", width: 500f, height: 500f);
        var panel = MakeRect("panel", root);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(5, 5, 5, 5);
        v.spacing = 10f;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        WithPreferred(MakeRect("a", panel), width: 100f, height: 30f);
        WithPreferred(MakeRect("b", panel), width: 80f, height: 40f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        // Width hugs the widest child + padding; height the stacked run + spacing + padding.
        Assert.Equal(110f, panel.rect.width, 3);
        Assert.Equal(5f + 30f + 10f + 40f + 5f, panel.rect.height, 3);
    }

    [Fact]
    public void NestedGroups_SolveInOneRebuild()
    {
        var outer = MakeRect("outer", width: 400f, height: 300f);
        var vOuter = outer.gameObject.AddComponent<VerticalLayoutGroup>();
        vOuter.childForceExpandHeight = false;

        // The inner group is an element of the outer one: its preferred height is the
        // sum of ITS children, aggregated during the same bottom-up input pass.
        var inner = MakeRect("inner", outer);
        var hInner = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
        hInner.childForceExpandWidth = false;
        hInner.childForceExpandHeight = false;

        var leafA = MakeRect("leafA", inner);
        WithPreferred(leafA, width: 50f, height: 60f);
        var leafB = MakeRect("leafB", inner);
        WithPreferred(leafB, width: 70f, height: 40f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(outer);

        AssertRect(0f, 0f, 400f, 60f, inner);   // outer stretched width, preferred height (max leaf)
        AssertRect(0f, 0f, 50f, 60f, leafA);    // inner laid its own children in the same pass
        AssertRect(50f, 0f, 70f, 40f, leafB);
    }

    // ── The queued rebuild path ──────────────────────────────────────────

    [Fact]
    public void MarkedLayout_RebuildsInTheGameLoopCanvasSlot()
    {
        var group = MakeRect("group", width: 200f, height: 200f);
        var v = group.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childForceExpandHeight = false;

        var child = MakeRect("child", group);
        WithPreferred(child, height: 80f);

        // Property change marks the layout root; nothing moves until the tick flushes it.
        v.spacing = 1f;
        v.spacing = 0f;
        loop.Tick(1f / 60f);

        AssertRect(0f, 0f, 200f, 80f, child);
    }
}
