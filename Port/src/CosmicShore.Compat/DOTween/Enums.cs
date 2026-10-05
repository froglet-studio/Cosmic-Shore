// First-party re-implementation of the DOTween (Demigiant) public API surface, written from
// the package's documented behavior (Assets/Plugins/Demigiant/DOTween/DOTween.XML) — no
// vendored source. Enum member ORDER is load-bearing: Unity serializes these enums as their
// integer values in authored assets (HUDAnimationSettings.asset stores OutBack as 27,
// OutQuad as 6; VesselElementalMorphConfig.asset stores InOutSine as 4), so every member
// keeps DOTween's implicit numbering.

namespace DG.Tweening
{
    /// <summary>Easing curves. Values match DOTween's serialized integers.</summary>
    public enum Ease
    {
        Unset,
        Linear,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce,
        Flash,
        InFlash,
        OutFlash,
        InOutFlash,
        /// <summary>Internal: used by zero-duration tweens (always evaluates to 1).</summary>
        INTERNAL_Zero,
        /// <summary>Internal: set when a custom ease (curve / function) is assigned.</summary>
        INTERNAL_Custom,
    }

    /// <summary>Behaviour of a tween when it loops.</summary>
    public enum LoopType
    {
        /// <summary>Each loop cycle restarts from the beginning.</summary>
        Restart,
        /// <summary>The tween moves forward and backwards at alternate cycles.</summary>
        Yoyo,
        /// <summary>Each cycle continues from where the previous one ended.</summary>
        Incremental,
    }

    /// <summary>Which engine update phase advances a tween.</summary>
    public enum UpdateType
    {
        Normal,
        Late,
        Fixed,
        Manual,
    }

    /// <summary>What a tween does when its linked GameObject changes state.</summary>
    public enum LinkBehaviour
    {
        PauseOnDisable,
        PauseOnDisablePlayOnEnable,
        PauseOnDisableRestartOnEnable,
        PlayOnEnable,
        RestartOnEnable,
        KillOnDisable,
        KillOnDestroy,
        CompleteOnDisable,
        CompleteAndKillOnDisable,
        RewindOnDisable,
        RewindAndKillOnDisable,
    }

    /// <summary>How rotation tweens interpolate.</summary>
    public enum RotateMode
    {
        /// <summary>Fastest way that never rotates beyond 360 degrees.</summary>
        Fast,
        /// <summary>Fastest way that may rotate beyond 360 degrees.</summary>
        FastBeyond360,
        /// <summary>Adds the given rotation in world axes.</summary>
        WorldAxisAdd,
        /// <summary>Adds the given rotation in the object's local axes.</summary>
        LocalAxisAdd,
    }

    public enum LogBehaviour
    {
        Default,
        Verbose,
        ErrorsOnly,
    }

    public enum AutoPlay
    {
        None,
        AutoPlaySequences,
        AutoPlayTweeners,
        All,
    }

    public enum ShakeRandomnessMode
    {
        /// <summary>Fully random direction changes.</summary>
        Full,
        /// <summary>Balanced direction changes (less erratic).</summary>
        Harmonic,
    }

    public delegate void TweenCallback();
    public delegate void TweenCallback<in T>(T value);
    public delegate T DOGetter<out T>();
    public delegate void DOSetter<in T>(T pNewValue);

    /// <summary>Custom ease signature: returns the eased 0..1 (may overshoot) value.</summary>
    public delegate float EaseFunction(float time, float duration, float overshootOrAmplitude, float period);
}
