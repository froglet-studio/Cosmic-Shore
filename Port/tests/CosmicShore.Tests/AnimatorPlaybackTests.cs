using CosmicShore.Engine;
using Xunit;

public class AnimatorPlaybackTests : System.IDisposable
{
    readonly GameLoop _loop = new();
    public void Dispose() => _loop.Dispose();

    static AnimationClip AlphaClip(float from, float to, float length)
    {
        var clip = new AnimationClip { name = $"{from}->{to}", length = length };
        clip.Bindings.Add(new ClipBinding
        {
            Path = "", ClassId = 225, Attribute = "m_Alpha",
            Curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(length, to)),
        });
        return clip;
    }

    /// <summary>The modal shape: default "Start", Play("Window In") fades up, "Window Out" fades down.</summary>
    static (Animator, CanvasGroup) Modal()
    {
        var layer = new AnimatorLayerData();
        layer.States.Add(new AnimatorStateData { Name = "Start", NameHash = Animator.StringToHash("Start"), Clip = AlphaClip(0f, 0f, 0.1f) });
        layer.States.Add(new AnimatorStateData { Name = "Window In", NameHash = Animator.StringToHash("Window In"), Clip = AlphaClip(0f, 1f, 0.25f) });
        layer.States.Add(new AnimatorStateData { Name = "Window Out", NameHash = Animator.StringToHash("Window Out"), Clip = AlphaClip(1f, 0f, 0.25f) });
        var controller = new AnimatorController();
        controller.Layers.Add(layer);

        var go = new GameObject("modal");
        var group = go.AddComponent<CanvasGroup>();
        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return (animator, group);
    }

    [Fact]
    public void PlayedState_WritesItsCurve_AndTheDefaultStateHoldsUntilThen()
    {
        var (animator, group) = Modal();
        _loop.Run(3, 1f / 60f);
        Assert.Equal(0f, group.alpha, 3);          // default state "Start" holds alpha at 0

        animator.Play("Window In");
        _loop.Run(30, 1f / 60f);                   // 0.5 s: past the 0.25 s clip
        Assert.Equal(1f, group.alpha, 3);

        animator.Play("Window Out");
        _loop.Run(30, 1f / 60f);
        Assert.Equal(0f, group.alpha, 3);
        Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Window Out"));
    }

    [Fact]
    public void ExitTimeTransition_MovesToTheNextState()
    {
        var (animator, _) = Modal();
        var layer = ((AnimatorController)animator.runtimeAnimatorController).Layers[0];
        layer.States[1].Transitions.Add(new AnimatorTransitionData { Destination = 2, HasExitTime = true, ExitTime = 1f, Duration = 0f });
        animator.Play("Window In");
        _loop.Run(30, 1f / 60f);
        Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Window Out"));
    }

    [Fact]
    public void TriggerCondition_IsConsumedByTheTransitionThatTakesIt()
    {
        var (animator, _) = Modal();
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        controller.Parameters = new[] { new AnimatorControllerParameter { name = "Open", type = AnimatorControllerParameterType.Trigger } };
        var t = new AnimatorTransitionData { Destination = 1, Duration = 0f };
        t.Conditions.Add(new AnimatorCondition { Mode = AnimatorConditionMode.If, Parameter = "Open" });
        controller.Layers[0].States[0].Transitions.Add(t);

        _loop.Run(2, 1f / 60f);
        Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Start"));
        animator.SetTrigger("Open");
        _loop.Run(2, 1f / 60f);
        Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Window In"));
    }

    [Fact]
    public void InfiniteTangent_IsAStep()
    {
        var curve = new AnimationCurve(new Keyframe(0f, 0f, 0f, float.PositiveInfinity), new Keyframe(1f, 1f, float.PositiveInfinity, 0f));
        Assert.Equal(0f, curve.Evaluate(0.5f));
    }

    static AnimationClip PosClip(float x)
    {
        var clip = new AnimationClip { name = $"x={x}", length = 1f / 24f };
        clip.Bindings.Add(new ClipBinding
        {
            Path = "part", ClassId = 4, Attribute = "m_LocalPosition.x",
            Curve = new AnimationCurve(new Keyframe(0f, x), new Keyframe(1f / 24f, x)),
        });
        return clip;
    }

    [Fact]
    public void Simple1D_InterpolatesBetweenBracketingThresholds()
    {
        var tree = new BlendTree { Type = BlendTreeType.Simple1D, ParameterX = "s" };
        tree.Children.Add(new BlendTreeChild { Clip = PosClip(0f), Threshold = 0f });
        tree.Children.Add(new BlendTreeChild { Clip = PosClip(10f), Threshold = 1f });
        tree.Children.Add(new BlendTreeChild { Clip = PosClip(30f), Threshold = 2f });
        var w = new float[3];
        BlendTreeWeights.Compute(tree, 1.25f, 0f, _ => 0f, w);
        Assert.Equal(new[] { 0f, 0.75f, 0.25f }, w);
        BlendTreeWeights.Compute(tree, 9f, 0f, _ => 0f, w);
        Assert.Equal(new[] { 0f, 0f, 1f }, w);
    }

    [Fact]
    public void FreeformCartesian_IsExactAtEachChild_AndSumsToOne()
    {
        var tree = new BlendTree { Type = BlendTreeType.FreeformCartesian2D };
        var pts = new[] { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };
        foreach (var p in pts) tree.Children.Add(new BlendTreeChild { Clip = PosClip(0f), Position = p });
        var w = new float[4];
        for (int i = 0; i < pts.Length; i++)
        {
            BlendTreeWeights.Compute(tree, pts[i].x, pts[i].y, _ => 0f, w);
            Assert.Equal(1f, w[i], 4);
        }
        BlendTreeWeights.Compute(tree, 0.3f, 0.2f, _ => 0f, w);
        Assert.Equal(1f, w[0] + w[1] + w[2] + w[3], 4);
        Assert.True(w[2] > w[3] && w[0] > w[1]);
    }

    [Fact]
    public void DirectTree_WeightsByParameter_AndFillsTheRestFromTheDefault()
    {
        var inner = new BlendTree { Type = BlendTreeType.Simple1D, ParameterX = "Pitch" };
        inner.Children.Add(new BlendTreeChild { Clip = PosClip(-4f), Threshold = -1f });
        inner.Children.Add(new BlendTreeChild { Clip = PosClip(4f), Threshold = 1f });
        var direct = new BlendTree { Type = BlendTreeType.Direct };
        direct.Children.Add(new BlendTreeChild { Tree = inner, DirectParameter = "Blend" });
        var layer = new AnimatorLayerData();
        layer.States.Add(new AnimatorStateData { Name = "Blend Tree", NameHash = Animator.StringToHash("Blend Tree"), Tree = direct, TimeParameter = "Blend" });
        var controller = new AnimatorController();
        controller.Layers.Add(layer);

        var go = new GameObject("vessel");
        var part = new GameObject("part");
        part.transform.SetParent(go.transform, false);
        part.transform.localPosition = new Vector3(2f, 0f, 0f);   // the rest pose
        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        animator.SetFloat("Pitch", 1f);
        _loop.Run(2, 1f / 60f);
        Assert.Equal(2f, part.transform.localPosition.x, 3);    // Blend = 0: the tree carries no weight

        animator.SetFloat("Blend", 1f);
        _loop.Run(2, 1f / 60f);
        Assert.Equal(4f, part.transform.localPosition.x, 3);

        animator.SetFloat("Blend", 0.5f);
        animator.SetFloat("Pitch", 0f);
        _loop.Run(2, 1f / 60f);
        Assert.Equal(1f, part.transform.localPosition.x, 3);    // 0.5 * 0 + 0.5 * rest
    }

    /// <summary>
    /// AnimationClip.SampleAnimation (the model viewer's takes): every binding the hierarchy has is
    /// written at the time asked, a child by its path, rotation written a component at a time comes
    /// out unit length, and a binding with no target is skipped rather than thrown on.
    /// </summary>
    [Fact]
    public void SampleAnimation_PosesTheHierarchyAtTheTimeAsked()
    {
        var root = new GameObject("rig");
        var bone = new GameObject("bone");
        bone.transform.SetParent(root.transform, false);
        var clip = new AnimationClip { name = "take", length = 2f };
        void Bind(string path, string attr, float a, float b) => clip.Bindings.Add(new ClipBinding
        {
            Path = path, ClassId = 4, Attribute = attr,
            Curve = new AnimationCurve(new Keyframe(0f, a), new Keyframe(2f, b)),
        });
        Bind("bone", "m_LocalPosition.y", 0f, 4f);
        // A quarter turn about Z, a component at a time (sampled halfway, the raw sum is not unit length).
        Bind("bone", "m_LocalRotation.z", 0f, 0.7071068f);
        Bind("bone", "m_LocalRotation.w", 1f, 0.7071068f);
        Bind("missing/child", "m_LocalPosition.x", 0f, 1f);

        clip.SampleAnimation(root, 1f);
        Assert.Equal(2f, bone.transform.localPosition.y, 4);
        var q = bone.transform.localRotation;
        Assert.Equal(1f, System.MathF.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w), 4);
        Assert.True(q.z > 0.3f && q.z < 0.45f, $"halfway is about 22.5 degrees, z = {q.z}");

        clip.SampleAnimation(root, 2f);
        Assert.Equal(4f, bone.transform.localPosition.y, 4);
        Assert.Equal(0.7071068f, bone.transform.localRotation.z, 4);
    }
}
