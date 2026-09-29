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
}
