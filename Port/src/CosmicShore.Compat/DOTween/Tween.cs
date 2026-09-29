using System;
using CosmicShore.Engine;
using DG.Tweening.Core.Easing;

namespace DG.Tweening
{
    /// <summary>
    /// Base of every tween and sequence. Time model (documented DOTween behavior):
    /// <list type="bullet">
    /// <item>A tween's life is <c>delay</c> (elapsed only while playing) followed by
    /// <c>loops × duration</c> of playhead time (<c>loops == -1</c> = infinite).</item>
    /// <item>The playhead is a (completedLoops, position-in-loop) pair. Yoyo mirrors the
    /// in-loop time on odd loops (so the ease is replayed in reverse); Incremental offsets
    /// each loop's start by the tween's full change.</item>
    /// <item>Start values are captured lazily on the tween's first update after its delay
    /// (its "startup"), so a tween appended to a Sequence starts from wherever the previous
    /// tween left the target.</item>
    /// <item>Callbacks per advance: OnStart (once) → OnPlay → [apply] → OnUpdate →
    /// OnStepComplete (per loop completed) → OnComplete → (autoKill) OnKill.</item>
    /// </list>
    /// </summary>
    public abstract class Tween
    {
        // ── Public surface (DOTween exposes these as public fields) ──
        public float timeScale = 1f;
        public bool isBackwards;
        public object id;
        public string stringId;
        public int intId = -999;
        public object target;
        public TweenCallback onPlay, onPause, onRewind, onUpdate, onStepComplete, onComplete, onKill;
        public TweenCallback<int> onWaypointChange;

        /// <summary>True for tweens created via DOTween.Sequence().</summary>
        public bool isSequence => this is Sequence;

        // ── Settings ──
        internal Ease easeType;
        internal EaseFunction customEase;
        internal float easeOvershootOrAmplitude;
        internal float easePeriod;
        internal bool autoKill;
        internal int loops = 1;
        internal LoopType loopType;
        internal float delay;
        internal float elapsedDelay;
        internal bool delayComplete = true;
        internal UpdateType updateType;
        internal bool isIndependentUpdate;
        internal bool isRelative;
        internal bool isSpeedBased;
        internal bool isInverted;
        internal bool isFrom;
        internal bool isRecyclable;

        // ── Playhead / state ──
        internal float duration;
        internal int completedLoops;
        internal float position;
        internal bool active = true;
        internal bool isPlaying;
        internal bool isComplete;
        internal bool startupDone;
        internal bool startFired;
        internal bool playFired;

        // ── Sequencing ──
        internal bool isSequenced;
        internal Sequence sequenceParent;

        // ── Link ──
        internal bool hasLink;
        internal GameObject linkTarget;
        internal LinkBehaviour linkBehaviour;
        internal bool linkWasActive;

        // ── Async completion ──
        internal System.Threading.Tasks.TaskCompletionSource<bool> completionSource;

        protected Tween()
        {
            easeType = DOTween.defaultEaseType;
            easeOvershootOrAmplitude = DOTween.defaultEaseOvershootOrAmplitude;
            easePeriod = DOTween.defaultEasePeriod;
            autoKill = DOTween.defaultAutoKill;
            loopType = DOTween.defaultLoopType;
            updateType = DOTween.defaultUpdateType;
            isIndependentUpdate = DOTween.defaultTimeScaleIndependent;
            isRecyclable = DOTween.defaultRecyclable;
        }

        internal float FullDuration => loops < 0 ? float.PositiveInfinity : duration * loops;

        /// <summary>Current playhead in full-timeline seconds (delay excluded).</summary>
        internal float FullPosition => isComplete ? FullDuration : completedLoops * duration + position;

        internal void GetLoopState(out int loopIndex, out float loopPosition)
        {
            if (isComplete)
            {
                loopIndex = Math.Max(loops - 1, 0);
                loopPosition = duration;
            }
            else
            {
                loopIndex = completedLoops;
                loopPosition = position;
            }
        }

        // ── Subclass contract ──

        /// <summary>Capture start values (first update after the delay). Throw to fail safely.</summary>
        internal abstract void Startup();

        /// <summary>Apply the target state for the given loop index and in-loop position.</summary>
        internal abstract void ApplyAt(int loopIndex, float loopPosition);

        /// <summary>
        /// Move from one playhead state to another. Tweeners are stateless (just apply the
        /// destination); Sequences walk their timeline so nested tweens/callbacks see the pass.
        /// </summary>
        internal virtual void ApplyTransition(int prevLoop, float prevPos, int loop, float pos, bool silent)
            => ApplyAt(loop, pos);

        /// <summary>Eased 0..1 progress for an in-loop position (Yoyo mirroring applied).</summary>
        internal float EasedProgress(int loopIndex, float loopPosition)
        {
            bool mirrored = loopType == LoopType.Yoyo && (loopIndex & 1) == 1;
            float t = mirrored ? duration - loopPosition : loopPosition;
            if (duration <= 0f) return mirrored ? 0f : 1f;
            if (isInverted) t = duration - t;
            float e = EaseManager.Evaluate(easeType, customEase, t, duration, easeOvershootOrAmplitude, easePeriod);
            return isInverted ? 1f - e : e;
        }

        // ── Engine-facing lifecycle ──

        internal bool TargetIsDead()
            => target is CosmicShore.Engine.Object o && o.IsDestroyed;

        internal bool DoStartup()
        {
            startupDone = true;
            if (TargetIsDead()) { Terminate(); return false; }
            try
            {
                Startup();
            }
            catch (Exception ex)
            {
                SafeModeFail(ex);
                return false;
            }
            return true;
        }

        void SafeModeFail(Exception ex)
        {
            if (!DOTween.useSafeMode) throw ex;
            if (DOTween.logBehaviour != LogBehaviour.ErrorsOnly)
                Debug.LogWarning($"[DOTween] Tween killed by safe mode: {ex.GetType().Name}: {ex.Message}");
            if (isSequenced) { active = false; return; }
            Terminate();
        }

        bool SafeApply(int prevLoop, float prevPos, int loop, float pos, bool silent)
        {
            if (TargetIsDead())
            {
                if (isSequenced) active = false; else Terminate();
                return false;
            }
            try
            {
                ApplyTransition(prevLoop, prevPos, loop, pos, silent);
            }
            catch (Exception ex)
            {
                SafeModeFail(ex);
                return false;
            }
            return active;
        }

        /// <summary>
        /// Move the playhead to <paramref name="to"/> (full-timeline seconds, clamped),
        /// applying values and (optionally) firing OnUpdate / OnStepComplete / OnComplete.
        /// </summary>
        internal void GotoInternal(float to, bool fireCallbacks)
        {
            if (!active) return;
            if (!startupDone && !DoStartup()) return;

            float full = FullDuration;
            if (float.IsNaN(to) || to < 0f) to = 0f;
            if (to > full) to = full;

            bool wasComplete = isComplete;
            int prevCompleted = isComplete ? loops : completedLoops;
            GetLoopState(out int prevLoop, out float prevPos);

            if (duration <= 0f)
            {
                completedLoops = loops < 0 ? 1 : loops;
                position = 0f;
                isComplete = loops >= 0;
            }
            else
            {
                double ratio = to / (double)duration;
                int cl = ratio >= int.MaxValue ? int.MaxValue - 1 : (int)Math.Floor(ratio);
                float pos = (float)(to - cl * (double)duration);
                if (pos < 0f) pos = 0f;
                if (pos > duration) pos = duration;
                if (loops >= 0 && cl >= loops)
                {
                    completedLoops = loops;
                    position = 0f;
                    isComplete = true;
                }
                else
                {
                    completedLoops = cl;
                    position = pos;
                    isComplete = false;
                }
            }

            GetLoopState(out int loop, out float loopPos);
            if (!SafeApply(prevLoop, prevPos, loop, loopPos, !fireCallbacks)) return;

            if (!fireCallbacks) return;
            Fire(onUpdate);
            int newCompleted = isComplete ? loops : completedLoops;
            int steps = Math.Min(Math.Abs(newCompleted - prevCompleted), 10000);
            for (int i = 0; i < steps && active; i++) Fire(onStepComplete);
            if (active && isComplete && !wasComplete)
            {
                Fire(onComplete);
                completionSource?.TrySetResult(true);
            }
        }

        /// <summary>Per-frame advance for a top-level (manager-owned) tween.</summary>
        internal void Advance(float dt)
        {
            if (!active || !isPlaying) return;
            if (!delayComplete)
            {
                elapsedDelay += dt;
                if (elapsedDelay < delay) return;
                dt = elapsedDelay - delay;
                elapsedDelay = delay;
                delayComplete = true;
            }
            if (!startupDone && !DoStartup()) return;
            if (!startFired) { startFired = true; Fire(onStart); }
            if (!active) return;
            if (!playFired) { playFired = true; Fire(onPlay); }
            if (!active || !isPlaying) return;

            float to = FullPosition + (isBackwards ? -dt : dt);
            GotoInternal(to, true);
            if (!active) return;

            if (isBackwards && to <= 0f)
            {
                isPlaying = false;
                Fire(onRewind);
            }
            else if (isComplete && !isBackwards)
            {
                isPlaying = false;
                if (autoKill) Terminate();
            }
        }

        /// <summary>Playhead positioning for a tween nested in a Sequence.</summary>
        internal void GotoNested(float localTo, bool fireCallbacks)
        {
            if (!active) return;
            if (!startupDone && !DoStartup()) return;
            if (fireCallbacks && !startFired) { startFired = true; Fire(onStart); }
            GotoInternal(localTo, fireCallbacks);
        }

        internal TweenCallback onStart;

        internal void Fire(TweenCallback cb)
        {
            if (cb == null) return;
            if (!DOTween.useSafeMode) { cb(); return; }
            try { cb(); }
            catch (Exception ex)
            {
                Debug.LogError($"[DOTween] Exception in tween callback: {ex}");
            }
        }

        // ── Control (called by TweenExtensions / DOTween statics) ──

        internal void Terminate()
        {
            if (!active) return;
            active = false;
            isPlaying = false;
            TweenManager.Remove(this);
            if (this is Sequence seq) seq.TerminateNested();
            Fire(onKill);
            completionSource?.TrySetResult(isComplete);
        }

        internal void DoKill(bool complete)
        {
            if (!active || isSequenced) return;
            if (complete) DoComplete(true);
            if (active) Terminate();
        }

        internal void DoComplete(bool withCallbacks)
        {
            if (!active || isSequenced) return;
            delayComplete = true;
            if (loops < 0)
            {
                // Infinite loops: jump to the end of the current loop cycle (no completion).
                if (!startupDone && !DoStartup()) return;
                GetLoopState(out int li, out float lp);
                SafeApply(li, lp, li, duration, !withCallbacks);
                return;
            }
            if (!startupDone && !DoStartup()) return;
            if (withCallbacks && !startFired) { startFired = true; Fire(onStart); }
            if (!active) return;
            isBackwards = false;
            GotoInternal(FullDuration, withCallbacks);
            if (!active) return;
            isPlaying = false;
            if (autoKill) Terminate();
        }

        internal void DoRewind(bool includeDelay, bool fireRewind)
        {
            if (!active || isSequenced) return;
            bool wasAtStart = FullPosition <= 0f && !isComplete;
            if (startupDone) GotoInternal(0f, false);
            completedLoops = 0;
            position = 0f;
            isComplete = false;
            isPlaying = false;
            playFired = false;
            if (includeDelay)
            {
                elapsedDelay = 0f;
                delayComplete = delay <= 0f;
            }
            if (this is Sequence seq) seq.ResetZeroCallbacks();
            if (fireRewind && !wasAtStart) Fire(onRewind);
        }

        internal void DoRestart(bool includeDelay, float changeDelayTo)
        {
            if (!active || isSequenced) return;
            DoRewind(includeDelay, false);
            if (changeDelayTo >= 0f)
            {
                delay = changeDelayTo;
                elapsedDelay = 0f;
                delayComplete = delay <= 0f;
            }
            isBackwards = false;
            DoPlay();
        }

        internal void DoPlay()
        {
            if (!active || isSequenced) return;
            if (isComplete && !isBackwards) return;
            if (isBackwards && FullPosition <= 0f && startupDone) return;
            isPlaying = true;
            TweenManager.EnsureRunning(this);
        }

        internal void DoPause()
        {
            if (!active || isSequenced || !isPlaying) return;
            isPlaying = false;
            playFired = false;
            Fire(onPause);
        }

        internal void DoGoto(float to, bool andPlay)
        {
            if (!active || isSequenced) return;
            delayComplete = true;
            elapsedDelay = delay;
            GotoInternal(to, true);
            if (!active) return;
            if (andPlay) DoPlay(); else DoPause();
        }

        // ── Link ──

        internal void SetLinkTarget(GameObject go, LinkBehaviour behaviour)
        {
            if (isSequenced) return;
            hasLink = go is not null;
            linkTarget = go;
            linkBehaviour = behaviour;
            linkWasActive = go is not null && !go.IsDestroyed && go.activeInHierarchy;
        }

        /// <summary>Evaluate the link; returns false if the tween was killed.</summary>
        internal bool CheckLink()
        {
            if (!hasLink) return true;
            if (linkTarget is null || linkTarget.IsDestroyed)
            {
                DoKill(false);
                return false;
            }
            bool now = linkTarget.activeInHierarchy;
            if (now == linkWasActive) return active;
            linkWasActive = now;
            if (!now)
            {
                switch (linkBehaviour)
                {
                    case LinkBehaviour.PauseOnDisable:
                    case LinkBehaviour.PauseOnDisablePlayOnEnable:
                    case LinkBehaviour.PauseOnDisableRestartOnEnable:
                        DoPause();
                        break;
                    case LinkBehaviour.KillOnDisable:
                        DoKill(false);
                        break;
                    case LinkBehaviour.CompleteOnDisable:
                        DoComplete(true);
                        break;
                    case LinkBehaviour.CompleteAndKillOnDisable:
                        DoKill(true);
                        break;
                    case LinkBehaviour.RewindOnDisable:
                        DoRewind(true, true);
                        break;
                    case LinkBehaviour.RewindAndKillOnDisable:
                        DoRewind(true, true);
                        DoKill(false);
                        break;
                }
            }
            else
            {
                switch (linkBehaviour)
                {
                    case LinkBehaviour.PauseOnDisablePlayOnEnable:
                    case LinkBehaviour.PlayOnEnable:
                        DoPlay();
                        break;
                    case LinkBehaviour.PauseOnDisableRestartOnEnable:
                    case LinkBehaviour.RestartOnEnable:
                        DoRestart(true, -1f);
                        break;
                }
            }
            return active;
        }
    }

    /// <summary>A tween that animates a single value from a start to an end.</summary>
    public abstract class Tweener : Tween
    {
        internal Tweener() { }
    }
}
