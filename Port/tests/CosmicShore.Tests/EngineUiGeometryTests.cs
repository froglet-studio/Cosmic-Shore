using System;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc A — the engine UI geometry core, fully headless: RectTransform's anchor
// solve (anchors together/apart, pivot, anchoredPosition, sizeDelta,
// offsetMin/Max, nested stretch chains, world corners), the Transform→
// RectTransform conversion (the `new GameObject(name, typeof(RectTransform))`
// idiom), Canvas driving the root rect from Screen/scaleFactor, and
// CanvasScaler's reference-resolution scaling. No renderer anywhere.
// ─────────────────────────────────────────────────────────────────────────────

public class EngineUiGeometryTests : IDisposable
{
    readonly GameLoop loop = new(nameof(EngineUiGeometryTests));
    readonly int _savedWidth = Screen.width;
    readonly int _savedHeight = Screen.height;

    public EngineUiGeometryTests()
    {
        Screen.width = 1280;
        Screen.height = 720;
    }

    public void Dispose()
    {
        Screen.width = _savedWidth;
        Screen.height = _savedHeight;
        loop.Dispose();
    }

    static void AssertVec(Vector2 expected, Vector2 actual, float tolerance = 1e-3f)
    {
        Assert.True((expected - actual).magnitude <= tolerance,
            $"expected {expected}, got {actual}");
    }

    static RectTransform MakeRect(string name, RectTransform parent = null)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rt.SetParent(parent, worldPositionStays: false);
        return rt;
    }

    /// <summary>A 1280×720-equivalent plain parent rect: a root RectTransform sized explicitly.</summary>
    RectTransform MakeRoot(float width = 800f, float height = 600f)
    {
        var root = MakeRect("root");
        root.sizeDelta = new Vector2(width, height); // no parent rect → size == sizeDelta
        return root;
    }

    // ── The anchor solve ─────────────────────────────────────────────────

    [Fact]
    public void PointAnchors_SizeIsSizeDelta_AndAnchoredPositionOffsetsThePivot()
    {
        var root = MakeRoot(800f, 600f);
        var child = MakeRect("child", root);
        child.sizeDelta = new Vector2(100f, 50f);
        child.anchoredPosition = new Vector2(10f, -20f);

        // Centre anchors + centre pivot: rect is pivot-relative local space.
        Assert.Equal(100f, child.rect.width);
        Assert.Equal(50f, child.rect.height);
        AssertVec(new Vector2(-50f, -25f), child.rect.min);

        // The pivot rests at the parent-rect centre (anchor ref = (0,0) in parent-local
        // space for a centred parent pivot) plus the anchored offset.
        AssertVec(new Vector2(10f, -20f), new Vector2(child.localPosition.x, child.localPosition.y));
    }

    [Fact]
    public void CornerAnchors_PositionAgainstThatCorner()
    {
        var root = MakeRoot(800f, 600f);
        var child = MakeRect("child", root);
        child.anchorMin = child.anchorMax = new Vector2(0f, 1f); // top-left of the parent
        child.pivot = new Vector2(0f, 1f);                       // by its own top-left corner
        child.sizeDelta = new Vector2(200f, 80f);
        child.anchoredPosition = new Vector2(24f, -12f);         // 24 right, 12 down from the corner

        // Parent rect (centre pivot): min (-400,-300), max (400,300). Top-left = (-400, 300).
        AssertVec(new Vector2(-400f + 24f, 300f - 12f),
            new Vector2(child.localPosition.x, child.localPosition.y));

        // Own rect hangs right/down from the pivot corner.
        Assert.Equal(0f, child.rect.xMin);
        Assert.Equal(-80f, child.rect.yMin);
    }

    [Fact]
    public void StretchAnchors_SizeFollowsParent_WithSizeDeltaAsMargins()
    {
        var root = MakeRoot(800f, 600f);
        var child = MakeRect("child", root);
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;
        child.offsetMin = new Vector2(10f, 20f);   // left/bottom insets
        child.offsetMax = new Vector2(-30f, -40f); // right/top insets

        Assert.Equal(800f - 10f - 30f, child.rect.width);
        Assert.Equal(600f - 20f - 40f, child.rect.height);

        // Resizing the parent re-solves the child on the next read — pull-based, no pass.
        root.sizeDelta = new Vector2(1000f, 500f);
        Assert.Equal(960f, child.rect.width);
        Assert.Equal(440f, child.rect.height);

        // The offsets are preserved views over (anchoredPosition, sizeDelta).
        AssertVec(new Vector2(10f, 20f), child.offsetMin);
        AssertVec(new Vector2(-30f, -40f), child.offsetMax);
    }

    [Fact]
    public void OffsetViews_RoundTrip_ThroughAnchoredPositionAndSizeDelta()
    {
        var root = MakeRoot();
        var child = MakeRect("child", root);
        child.pivot = new Vector2(0.25f, 0.75f); // asymmetric pivot exercises the solve
        child.offsetMin = new Vector2(5f, 10f);
        child.offsetMax = new Vector2(65f, 90f);

        AssertVec(new Vector2(60f, 80f), child.sizeDelta);
        AssertVec(new Vector2(5f, 10f), child.offsetMin);
        AssertVec(new Vector2(65f, 90f), child.offsetMax);
        // anchoredPosition = lerp(offsetMin, offsetMax, pivot).
        AssertVec(new Vector2(20f, 70f), child.anchoredPosition);
    }

    [Fact]
    public void LocalPosition_BacksSolvesAnchoredPosition_AndRoundTrips()
    {
        var root = MakeRoot(800f, 600f);
        var child = MakeRect("child", root);
        child.anchorMin = child.anchorMax = new Vector2(1f, 0f); // bottom-right anchor
        child.pivot = new Vector2(0.5f, 0.5f);

        child.localPosition = new Vector3(350f, -250f, 7f);

        // Anchor ref is the parent's bottom-right corner (400, -300).
        AssertVec(new Vector2(-50f, 50f), child.anchoredPosition);
        Assert.Equal(350f, child.localPosition.x);
        Assert.Equal(-250f, child.localPosition.y);
        Assert.Equal(7f, child.localPosition.z);
    }

    [Fact]
    public void Reparenting_KeepsWorldPosition_ByBackSolvingAnchors()
    {
        var rootA = MakeRoot(800f, 600f);
        var rootB = MakeRoot(400f, 400f);
        rootB.localPosition = new Vector3(1000f, 0f, 0f);

        var child = MakeRect("child", rootA);
        child.anchoredPosition = new Vector2(100f, 50f);
        Vector3 worldBefore = child.position;

        child.SetParent(rootB, worldPositionStays: true);

        Assert.True((child.position - worldBefore).magnitude < 1e-3f,
            $"world pose should survive reparenting: {worldBefore} → {child.position}");
        // And the anchored state now expresses that pose relative to the NEW parent.
        AssertVec(new Vector2(100f - 1000f, 50f), child.anchoredPosition);
    }

    [Fact]
    public void NestedStretchChain_ResolvesLeafWorldCorners()
    {
        var root = MakeRoot(1000f, 800f);

        var middle = MakeRect("middle", root);   // full-stretch with 50-unit insets all round
        middle.anchorMin = Vector2.zero;
        middle.anchorMax = Vector2.one;
        middle.offsetMin = new Vector2(50f, 50f);
        middle.offsetMax = new Vector2(-50f, -50f);

        var leaf = MakeRect("leaf", middle);     // fixed 100×100 centred in the middle
        leaf.sizeDelta = new Vector2(100f, 100f);

        Assert.Equal(900f, middle.rect.width);
        Assert.Equal(700f, middle.rect.height);

        var corners = new Vector3[4];
        leaf.GetWorldCorners(corners);
        // Root centre is the origin; every rect here is centre-pivoted and centred, so the
        // leaf spans ±50 about the origin. Corner order: BL, TL, TR, BR.
        AssertVec(new Vector2(-50f, -50f), new Vector2(corners[0].x, corners[0].y));
        AssertVec(new Vector2(-50f, 50f), new Vector2(corners[1].x, corners[1].y));
        AssertVec(new Vector2(50f, 50f), new Vector2(corners[2].x, corners[2].y));
        AssertVec(new Vector2(50f, -50f), new Vector2(corners[3].x, corners[3].y));
    }

    [Fact]
    public void SizingHelpers_SetSizeAndParentEdgeInsets()
    {
        var root = MakeRoot(800f, 600f);
        var bar = MakeRect("bar", root);
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = Vector2.one;

        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500f);
        Assert.Equal(500f, bar.rect.width);

        var panel = MakeRect("panel", root);
        panel.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 30f, 200f);
        panel.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 10f, 60f);
        Assert.Equal(200f, panel.rect.width);
        Assert.Equal(60f, panel.rect.height);
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        // 30 in from the parent's left edge (-400), 10 down from its top edge (+300).
        AssertVec(new Vector2(-400f + 30f, 300f - 10f), new Vector2(corners[1].x, corners[1].y)); // top-left
    }

    // ── Transform → RectTransform conversion ────────────────────────────

    [Fact]
    public void AddComponent_ConvertsTheTransform_PreservingHierarchyAndPose()
    {
        var parent = new GameObject("parent");
        var sibling0 = new GameObject("sibling0");
        sibling0.transform.SetParent(parent.transform, false);
        var target = new GameObject("target");
        target.transform.SetParent(parent.transform, false);
        target.transform.localPosition = new Vector3(3f, 4f, 5f);
        var child = new GameObject("child");
        child.transform.SetParent(target.transform, false);

        var rt = target.AddComponent<RectTransform>();

        Assert.Same(rt, target.transform);                 // the transform IS the RectTransform now
        Assert.True(target.transform is RectTransform);
        Assert.Same(parent.transform, rt.parent);          // same parent…
        Assert.Same(rt, parent.transform.GetChild(1));     // …same sibling slot
        Assert.Same(child.transform, rt.GetChild(0));      // children came along
        Assert.Same(rt, child.transform.parent);
        Assert.Equal(new Vector3(3f, 4f, 5f), rt.localPosition); // pose preserved
        Assert.Same(rt, target.GetComponent<Transform>()); // component list swapped in place
    }

    [Fact]
    public void ConstructorTypeList_CreatesWithARectTransform()
    {
        var go = new GameObject("Label", typeof(RectTransform));
        Assert.True(go.transform is RectTransform);
        Assert.Single(go.GetComponents<Transform>()); // one transform — not a Transform plus a RectTransform

        // Adding again is a no-op returning the existing one.
        Assert.Same(go.transform, go.AddComponent<RectTransform>());
    }

    // ── Canvas + CanvasScaler ────────────────────────────────────────────

    static (Canvas canvas, RectTransform rt) MakeCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        return (go.GetComponent<Canvas>(), (RectTransform)go.transform);
    }

    [Fact]
    public void RootOverlayCanvas_DrivesItsRect_FromScreenAndScaleFactor()
    {
        var (canvas, rt) = MakeCanvas();

        Assert.True(canvas.isRootCanvas);
        Assert.Equal(1280f, rt.rect.width);
        Assert.Equal(720f, rt.rect.height);
        AssertVec(new Vector2(640f, 360f), new Vector2(rt.localPosition.x, rt.localPosition.y));

        canvas.scaleFactor = 2f;
        Assert.Equal(640f, rt.rect.width);   // canvas units shrink…
        Assert.Equal(360f, rt.rect.height);
        Assert.Equal(2f, rt.localScale.x);   // …but the transform scales back up,

        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);         // so world corners stay the full screen in pixels.
        AssertVec(Vector2.zero, new Vector2(corners[0].x, corners[0].y));
        AssertVec(new Vector2(1280f, 720f), new Vector2(corners[2].x, corners[2].y));
    }

    [Fact]
    public void FullStretchChildOfCanvas_CoversTheScreen_AtAnyScaleFactor()
    {
        var (canvas, rt) = MakeCanvas();
        canvas.scaleFactor = 1.5f;

        var screenRt = MakeRect("screen", rt);
        screenRt.anchorMin = Vector2.zero;
        screenRt.anchorMax = Vector2.one;
        screenRt.offsetMin = Vector2.zero;
        screenRt.offsetMax = Vector2.zero;

        AssertVec(rt.rect.size, screenRt.rect.size); // canvas units
        var corners = new Vector3[4];
        screenRt.GetWorldCorners(corners);           // pixels
        AssertVec(Vector2.zero, new Vector2(corners[0].x, corners[0].y));
        AssertVec(new Vector2(1280f, 720f), new Vector2(corners[2].x, corners[2].y));
    }

    [Fact]
    public void ScreenResize_PropagatesThroughTheWholeChain_Immediately()
    {
        var (_, rt) = MakeCanvas();
        var child = MakeRect("child", rt);
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;

        Assert.Equal(1280f, child.rect.width);
        Screen.width = 1920;
        Screen.height = 1080;
        Assert.Equal(1920f, child.rect.width); // pull-based: no layout pass needed
        Assert.Equal(1080f, child.rect.height);
    }

    [Fact]
    public void CanvasScaler_ScaleWithScreenSize_MatchesWidthHeightOrBlends()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f); // screen 1280×720 → ratios 1.6 / 1.2

        scaler.matchWidthOrHeight = 0f;
        Assert.Equal(1.6f, canvas.scaleFactor, 3);
        scaler.matchWidthOrHeight = 1f;
        Assert.Equal(1.2f, canvas.scaleFactor, 3);
        scaler.matchWidthOrHeight = 0.5f;
        Assert.Equal(MathF.Sqrt(1.6f * 1.2f), canvas.scaleFactor, 3); // geometric mean (log-space lerp)

        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Assert.Equal(1.2f, canvas.scaleFactor, 3);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        Assert.Equal(1.6f, canvas.scaleFactor, 3);

        // ConstantPixelSize ignores the screen entirely.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 3f;
        Assert.Equal(3f, canvas.scaleFactor, 3);

        // The reference-resolution canvas rect: 1280/1.6 = 800 canvas units wide at match 0.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.matchWidthOrHeight = 0f;
        var rt = (RectTransform)go.transform;
        Assert.Equal(800f, rt.rect.width, 3);
        Assert.Equal(450f, rt.rect.height, 3);
    }

    [Fact]
    public void NestedCanvas_InheritsTheRootScaleFactor()
    {
        var (rootCanvas, rootRt) = MakeCanvas();
        rootCanvas.scaleFactor = 2f;

        var nestedGo = new GameObject("Nested", typeof(RectTransform), typeof(Canvas));
        ((RectTransform)nestedGo.transform).SetParent(rootRt, false);
        var nested = nestedGo.GetComponent<Canvas>();

        Assert.False(nested.isRootCanvas);
        Assert.Same(rootCanvas, nested.rootCanvas);
        Assert.Equal(2f, nested.scaleFactor);

        // A nested canvas does NOT drive its rect — it anchors like any element.
        var nestedRt = (RectTransform)nestedGo.transform;
        nestedRt.sizeDelta = new Vector2(100f, 100f);
        Assert.Equal(100f, nestedRt.rect.width);
    }
}
