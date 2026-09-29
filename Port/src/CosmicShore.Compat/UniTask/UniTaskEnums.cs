namespace Cysharp.Threading.Tasks
{
    /// <summary>
    /// Original contract: the player-loop phase a UniTask continuation resumes in. The port
    /// engine has two task resume points per frame (see <c>GameLoop.Tick</c>): the scheduler
    /// phase (after Update, before LateUpdate) and end-of-frame (after LateUpdate). Timings map
    /// onto them as follows:
    ///   Initialization … Update (+ Last*), TimeUpdate, FixedUpdate → scheduler phase of a LATER frame
    ///   PreLateUpdate / LastPreLateUpdate                          → scheduler phase (same frame when
    ///                                                                 the caller is still in Update, as upstream)
    ///   PostLateUpdate / LastPostLateUpdate                        → end-of-frame
    /// FixedUpdate timings have no dedicated resume point in the engine and resume like Update.
    /// </summary>
    public enum PlayerLoopTiming
    {
        Initialization = 0,
        LastInitialization = 1,
        EarlyUpdate = 2,
        LastEarlyUpdate = 3,
        FixedUpdate = 4,
        LastFixedUpdate = 5,
        PreUpdate = 6,
        LastPreUpdate = 7,
        Update = 8,
        LastUpdate = 9,
        PreLateUpdate = 10,
        LastPreLateUpdate = 11,
        PostLateUpdate = 12,
        LastPostLateUpdate = 13,
        TimeUpdate = 14,
        LastTimeUpdate = 15,
    }

    /// <summary>Which clock a UniTask delay counts.</summary>
    public enum DelayType
    {
        /// <summary>Scaled game time (<c>Time.time</c>) — pauses with <c>Time.timeScale = 0</c>.</summary>
        DeltaTime = 0,
        /// <summary>Unscaled game time (<c>Time.unscaledTime</c>).</summary>
        UnscaledDeltaTime = 1,
        /// <summary>Wall-clock time (a stopwatch), independent of frame deltas.</summary>
        Realtime = 2,
    }

    public enum UniTaskStatus
    {
        Pending = 0,
        Succeeded = 1,
        Faulted = 2,
        Canceled = 3,
    }

    public static class UniTaskStatusExtensions
    {
        public static bool IsCompleted(this UniTaskStatus status) => status != UniTaskStatus.Pending;
        public static bool IsCompletedSuccessfully(this UniTaskStatus status) => status == UniTaskStatus.Succeeded;
        public static bool IsCanceled(this UniTaskStatus status) => status == UniTaskStatus.Canceled;
        public static bool IsFaulted(this UniTaskStatus status) => status == UniTaskStatus.Faulted;
    }

    /// <summary>The unit value (UniTask's <c>void</c> stand-in for generic contexts).</summary>
    public readonly struct AsyncUnit : System.IEquatable<AsyncUnit>
    {
        public static readonly AsyncUnit Default = default;
        public bool Equals(AsyncUnit other) => true;
        public override bool Equals(object obj) => obj is AsyncUnit;
        public override int GetHashCode() => 0;
        public override string ToString() => "()";
    }
}
