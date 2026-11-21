using System.Threading.Tasks;
using CosmicShore.Engine;

namespace DG.Tweening
{
    /// <summary>
    /// Control and query methods on tweens. Every method is null-safe (a null or killed tween
    /// is a no-op / reports inactive), and nested tweens cannot be controlled individually —
    /// their Sequence owns them (documented DOTween behavior).
    /// </summary>
    public static class TweenExtensions
    {
        public static void Complete(this Tween t) => t?.DoComplete(false);
        public static void Complete(this Tween t, bool withCallbacks) => t?.DoComplete(withCallbacks);

        public static void Flip(this Tween t)
        {
            if (t == null || !t.active || t.isSequenced) return;
            t.isBackwards = !t.isBackwards;
        }

        /// <summary>Forces the tween to capture its start values now.</summary>
        public static void ForceInit(this Tween t)
        {
            if (t == null || !t.active || t.isSequenced || t.startupDone) return;
            t.DoStartup();
        }

        public static void Goto(this Tween t, float to, bool andPlay = false) => t?.DoGoto(to, andPlay);

        public static void Kill(this Tween t, bool complete = false) => t?.DoKill(complete);

        public static void ManualUpdate(this Tween t, float deltaTime, float unscaledDeltaTime)
        {
            if (t == null || !t.active || t.isSequenced) return;
            t.Advance((t.isIndependentUpdate ? unscaledDeltaTime : deltaTime) * DOTween.timeScale * t.timeScale);
        }

        public static T Pause<T>(this T t) where T : Tween { t?.DoPause(); return t; }
        public static T Play<T>(this T t) where T : Tween { t?.DoPlay(); return t; }

        public static void PlayBackwards(this Tween t)
        {
            if (t == null || !t.active || t.isSequenced) return;
            t.isBackwards = true;
            t.DoPlay();
        }

        public static void PlayForward(this Tween t)
        {
            if (t == null || !t.active || t.isSequenced) return;
            t.isBackwards = false;
            t.DoPlay();
        }

        public static void Restart(this Tween t, bool includeDelay = true, float changeDelayTo = -1f)
            => t?.DoRestart(includeDelay, changeDelayTo);

        public static void Rewind(this Tween t, bool includeDelay = true) => t?.DoRewind(includeDelay, true);

        /// <summary>Plays the tween backwards from its current position (instead of snapping to start).</summary>
        public static void SmoothRewind(this Tween t) => t.PlayBackwards();

        public static void TogglePause(this Tween t)
        {
            if (t == null || !t.active || t.isSequenced) return;
            if (t.isPlaying) t.DoPause(); else t.DoPlay();
        }

        // ── Queries ──

        public static int CompletedLoops(this Tween t) => t == null || !t.active ? 0 : (t.isComplete ? t.loops : t.completedLoops);
        public static float Delay(this Tween t) => t == null || !t.active ? 0f : t.delay;
        public static float ElapsedDelay(this Tween t) => t == null || !t.active ? 0f : t.elapsedDelay;

        public static float Duration(this Tween t, bool includeLoops = true)
        {
            if (t == null || !t.active) return 0f;
            if (!includeLoops) return t.duration;
            return t.loops < 0 ? float.PositiveInfinity : t.duration * t.loops;
        }

        public static float Elapsed(this Tween t, bool includeLoops = true)
        {
            if (t == null || !t.active) return 0f;
            if (includeLoops) return t.FullPosition;
            return t.isComplete ? t.duration : t.position;
        }

        public static float ElapsedPercentage(this Tween t, bool includeLoops = true)
        {
            if (t == null || !t.active) return 0f;
            if (includeLoops)
            {
                if (t.loops < 0) return t.duration <= 0f ? 0f : t.position / t.duration;
                float full = t.duration * t.loops;
                return full <= 0f ? 1f : t.FullPosition / full;
            }
            return t.duration <= 0f ? 1f : (t.isComplete ? 1f : t.position / t.duration);
        }

        public static float ElapsedDirectionalPercentage(this Tween t)
        {
            float p = t.ElapsedPercentage(false);
            if (t == null || !t.active) return 0f;
            bool mirrored = t.loopType == LoopType.Yoyo && (t.CompletedLoops() & 1) == 1 && !t.isComplete;
            return mirrored ? 1f - p : p;
        }

        public static bool IsActive(this Tween t) => t != null && t.active;
        public static bool IsBackwards(this Tween t) => t != null && t.active && t.isBackwards;
        public static bool IsComplete(this Tween t) => t != null && t.active && t.isComplete;
        public static bool IsInitialized(this Tween t) => t != null && t.active && t.startupDone;
        public static bool IsPlaying(this Tween t) => t != null && t.active && t.isPlaying;
        public static int Loops(this Tween t) => t == null || !t.active ? 0 : t.loops;

        // ── Coroutine / async waits ──

        /// <summary>Yield in a coroutine until the tween completes or is killed.</summary>
        public static YieldInstruction WaitForCompletion(this Tween t, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active || t.isComplete);

        public static YieldInstruction WaitForRewind(this Tween t, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active || (!t.isPlaying && t.FullPosition <= 0f));

        public static YieldInstruction WaitForKill(this Tween t, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active);

        public static YieldInstruction WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active || t.CompletedLoops() >= elapsedLoops);

        public static YieldInstruction WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active || t.FullPosition >= position);

        public static YieldInstruction WaitForStart(this Tween t, bool returnCustomYieldInstruction = false)
            => new WaitUntil(() => t == null || !t.active || t.startFired);

        /// <summary>Completes when the tween completes or is killed.</summary>
        public static Task AsyncWaitForCompletion(this Tween t)
        {
            if (t == null || !t.active || t.isComplete) return Task.CompletedTask;
            t.completionSource ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return t.completionSource.Task;
        }

        public static Task AsyncWaitForKill(this Tween t)
        {
            if (t == null || !t.active) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var prev = t.onKill;
            t.onKill = () => { prev?.Invoke(); tcs.TrySetResult(true); };
            return tcs.Task;
        }
    }
}
