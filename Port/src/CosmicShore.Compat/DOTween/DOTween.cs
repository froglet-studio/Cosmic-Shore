using System;
using System.Collections.Generic;
using CosmicShore.Engine;

namespace DG.Tweening
{
    /// <summary>Returned by <see cref="DOTween.Init"/> (capacity is advisory in the port).</summary>
    public interface IDOTweenInit
    {
        IDOTweenInit SetCapacity(int tweenersCapacity, int sequencesCapacity);
    }

    /// <summary>Main DOTween entry point: global settings, factories and filtered control.</summary>
    public static class DOTween
    {
        public static readonly string Version = "1.2.765-port";

        // ── Global settings (documented defaults) ──
        public static bool useSafeMode = true;
        public static LogBehaviour logBehaviour = LogBehaviour.ErrorsOnly;
        public static bool showUnityEditorReport;
        public static float timeScale = 1f;
        public static float unscaledTimeScale = 1f;
        public static bool useSmoothDeltaTime;
        public static float maxSmoothUnscaledTime = 0.15f;
        public static bool drawGizmos = true;
        public static bool debugMode;
        public static bool debugStoreTargetId = true;
        public static AutoPlay defaultAutoPlay = AutoPlay.All;
        public static bool defaultAutoKill = true;
        public static LoopType defaultLoopType = LoopType.Restart;
        public static bool defaultRecyclable;
        public static Ease defaultEaseType = Ease.OutQuad;
        public static float defaultEaseOvershootOrAmplitude = 1.70158f;
        public static float defaultEasePeriod;
        public static UpdateType defaultUpdateType = UpdateType.Normal;
        public static bool defaultTimeScaleIndependent;

        sealed class InitResult : IDOTweenInit
        {
            public IDOTweenInit SetCapacity(int tweenersCapacity, int sequencesCapacity) => this;
        }

        static readonly InitResult _init = new();

        public static IDOTweenInit Init(bool? recycleAllByDefault = null, bool? useSafeMode = null, LogBehaviour? logBehaviour = null)
        {
            if (recycleAllByDefault.HasValue) defaultRecyclable = recycleAllByDefault.Value;
            if (useSafeMode.HasValue) DOTween.useSafeMode = useSafeMode.Value;
            if (logBehaviour.HasValue) DOTween.logBehaviour = logBehaviour.Value;
            return _init;
        }

        public static void SetTweensCapacity(int tweenersCapacity, int sequencesCapacity) { }

        /// <summary>Kill every tween and restore the documented defaults.</summary>
        public static void Clear(bool destroy = false)
        {
            TweenManager.Clear();
            if (!destroy) return;
            useSafeMode = true;
            logBehaviour = LogBehaviour.ErrorsOnly;
            timeScale = 1f;
            unscaledTimeScale = 1f;
            defaultAutoPlay = AutoPlay.All;
            defaultAutoKill = true;
            defaultLoopType = LoopType.Restart;
            defaultRecyclable = false;
            defaultEaseType = Ease.OutQuad;
            defaultEaseOvershootOrAmplitude = 1.70158f;
            defaultEasePeriod = 0f;
            defaultUpdateType = UpdateType.Normal;
            defaultTimeScaleIndependent = false;
        }

        public static void ClearCachedTweens() { }
        public static int Validate() => 0;

        /// <summary>Advance tweens set to <see cref="UpdateType.Manual"/>.</summary>
        public static void ManualUpdate(float deltaTime, float unscaledDeltaTime)
            => TweenManager.Update(UpdateType.Manual, deltaTime, unscaledDeltaTime);

        // ── Factories ──

        internal static T Register<T>(T t) where T : Tween
        {
            bool autoPlay = t is Sequence
                ? defaultAutoPlay is AutoPlay.All or AutoPlay.AutoPlaySequences
                : defaultAutoPlay is AutoPlay.All or AutoPlay.AutoPlayTweeners;
            t.isPlaying = autoPlay;
            TweenManager.Add(t);
            return t;
        }

        internal static TweenerCore<T> Make<T>(DOGetter<T> getter, DOSetter<T> setter, T endValue, float duration, ValuePlugin<T> plugin)
            => Register(new TweenerCore<T>(getter, setter, endValue, duration, plugin));

        public static Sequence Sequence() => Register(new Sequence());

        public static Sequence Sequence(object target)
        {
            var s = Sequence();
            s.target = target;
            return s;
        }

        public static TweenerCore<float> To(DOGetter<float> getter, DOSetter<float> setter, float endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Float);
        public static TweenerCore<double> To(DOGetter<double> getter, DOSetter<double> setter, double endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Double);
        public static TweenerCore<int> To(DOGetter<int> getter, DOSetter<int> setter, int endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Int);
        public static TweenerCore<uint> To(DOGetter<uint> getter, DOSetter<uint> setter, uint endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.UInt);
        public static TweenerCore<long> To(DOGetter<long> getter, DOSetter<long> setter, long endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Long);
        public static TweenerCore<ulong> To(DOGetter<ulong> getter, DOSetter<ulong> setter, ulong endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.ULong);
        public static TweenerCore<Vector2> To(DOGetter<Vector2> getter, DOSetter<Vector2> setter, Vector2 endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Vector2);
        public static TweenerCore<Vector3> To(DOGetter<Vector3> getter, DOSetter<Vector3> setter, Vector3 endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Vector3);
        public static TweenerCore<Vector4> To(DOGetter<Vector4> getter, DOSetter<Vector4> setter, Vector4 endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Vector4);
        public static TweenerCore<Color> To(DOGetter<Color> getter, DOSetter<Color> setter, Color endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Color);
        public static TweenerCore<Rect> To(DOGetter<Rect> getter, DOSetter<Rect> setter, Rect endValue, float duration)
            => Make(getter, setter, endValue, duration, Plugins.Rect);

        /// <summary>Quaternion tween toward an euler end value (shortest path per axis).</summary>
        public static TweenerCore<Vector3> To(DOGetter<Quaternion> getter, DOSetter<Quaternion> setter, Vector3 endValue, float duration)
            => Make(() => getter().eulerAngles, v => setter(Quaternion.Euler(v)), endValue, duration, new EulerPlugin(true));

        /// <summary>Virtual tween: calls <paramref name="setter"/> with a value moving from start to end.</summary>
        public static Tweener To(DOSetter<float> setter, float startValue, float endValue, float duration)
        {
            float v = startValue;
            var t = To(() => v, x => { v = x; setter(x); }, endValue, duration);
            t.startValue = startValue;
            t.startValueSet = true;
            return t;
        }

        /// <summary>Tweens only the alpha channel of a color.</summary>
        public static TweenerCore<float> ToAlpha(DOGetter<Color> getter, DOSetter<Color> setter, float endValue, float duration)
            => AlphaTween(getter, setter, endValue, duration);

        internal static TweenerCore<float> AlphaTween(DOGetter<Color> getter, DOSetter<Color> setter, float endValue, float duration)
            => Make(() => getter().a, a => { var c = getter(); c.a = a; setter(c); }, endValue, duration, Plugins.Float);

        public static WaypointTweener Punch(DOGetter<Vector3> getter, DOSetter<Vector3> setter, Vector3 direction, float duration, int vibrato = 10, float elasticity = 1f)
        {
            WaypointTweener.BuildPunch(direction, duration, vibrato, elasticity, out var offsets, out var segments);
            return Register(new WaypointTweener(getter, setter, offsets, segments, duration));
        }

        public static WaypointTweener Shake(DOGetter<Vector3> getter, DOSetter<Vector3> setter, float duration, float strength = 3f,
                                           int vibrato = 10, float randomness = 90f, bool ignoreZAxis = true, bool fadeOut = true,
                                           ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => Shake(getter, setter, duration, new Vector3(strength, strength, strength), vibrato, randomness, fadeOut, randomnessMode, ignoreZAxis);

        public static WaypointTweener Shake(DOGetter<Vector3> getter, DOSetter<Vector3> setter, float duration, Vector3 strength,
                                           int vibrato = 10, float randomness = 90f, bool fadeOut = true,
                                           ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => Shake(getter, setter, duration, strength, vibrato, randomness, fadeOut, randomnessMode, false);

        internal static WaypointTweener Shake(DOGetter<Vector3> getter, DOSetter<Vector3> setter, float duration, Vector3 strength,
                                             int vibrato, float randomness, bool fadeOut, ShakeRandomnessMode mode, bool ignoreZ)
        {
            WaypointTweener.BuildShake(strength, duration, vibrato, randomness, fadeOut, ignoreZ, mode, out var offsets, out var segments);
            return Register(new WaypointTweener(getter, setter, offsets, segments, duration));
        }

        // ── Filtered control ──

        public static int Kill(object targetOrId, bool complete = false)
            => TweenManager.Filtered(targetOrId, t => { t.DoKill(complete); return true; });

        public static int Kill(object target, object id, bool complete = false)
            => TweenManager.Filtered(target, t =>
            {
                if (!TweenManager.Matches(t, id)) return false;
                t.DoKill(complete);
                return true;
            });

        public static int KillAll(bool complete = false)
            => TweenManager.Filtered(null, t => { t.DoKill(complete); return true; }, matchAll: true);

        public static int KillAll(bool complete, params object[] idsOrTargetsToExclude)
            => TweenManager.Filtered(null, t =>
            {
                if (idsOrTargetsToExclude != null)
                    foreach (var ex in idsOrTargetsToExclude)
                        if (TweenManager.Matches(t, ex)) return false;
                t.DoKill(complete);
                return true;
            }, matchAll: true);

        public static int Complete(object targetOrId, bool withCallbacks = false)
            => TweenManager.Filtered(targetOrId, t => { bool was = t.isComplete; t.DoComplete(withCallbacks); return !was; });
        public static int CompleteAll(bool withCallbacks = false)
            => TweenManager.Filtered(null, t => { bool was = t.isComplete; t.DoComplete(withCallbacks); return !was; }, matchAll: true);

        public static int Pause(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { bool was = t.isPlaying; t.DoPause(); return was; });
        public static int PauseAll()
            => TweenManager.Filtered(null, t => { bool was = t.isPlaying; t.DoPause(); return was; }, matchAll: true);

        public static int Play(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { bool was = t.isPlaying; t.DoPlay(); return !was && t.isPlaying; });
        public static int PlayAll()
            => TweenManager.Filtered(null, t => { bool was = t.isPlaying; t.DoPlay(); return !was && t.isPlaying; }, matchAll: true);

        public static int PlayForward(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { t.isBackwards = false; t.DoPlay(); return true; });
        public static int PlayBackwards(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { t.isBackwards = true; t.DoPlay(); return true; });

        public static int Restart(object targetOrId, bool includeDelay = true, float changeDelayTo = -1f)
            => TweenManager.Filtered(targetOrId, t => { t.DoRestart(includeDelay, changeDelayTo); return true; });
        public static int RestartAll(bool includeDelay = true)
            => TweenManager.Filtered(null, t => { t.DoRestart(includeDelay, -1f); return true; }, matchAll: true);

        public static int Rewind(object targetOrId, bool includeDelay = true)
            => TweenManager.Filtered(targetOrId, t => { t.DoRewind(includeDelay, true); return true; });
        public static int RewindAll(bool includeDelay = true)
            => TweenManager.Filtered(null, t => { t.DoRewind(includeDelay, true); return true; }, matchAll: true);

        public static int TogglePause(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { if (t.isPlaying) t.DoPause(); else t.DoPlay(); return true; });

        public static int Goto(object targetOrId, float to, bool andPlay = false)
            => TweenManager.Filtered(targetOrId, t => { t.DoGoto(to, andPlay); return true; });

        public static int Flip(object targetOrId)
            => TweenManager.Filtered(targetOrId, t => { t.isBackwards = !t.isBackwards; return true; });

        // ── Queries ──

        public static bool IsTweening(object targetOrId, bool alsoCheckIfIsPlaying = false)
        {
            foreach (var t in TweenManager.Active)
            {
                if (!t.active || !TweenManager.Matches(t, targetOrId)) continue;
                if (!alsoCheckIfIsPlaying || t.isPlaying) return true;
            }
            return false;
        }

        public static int TotalActiveTweens() => TweenManager.ActiveCount;

        public static int TotalPlayingTweens()
        {
            int n = 0;
            foreach (var t in TweenManager.Active) if (t.active && t.isPlaying) n++;
            return n;
        }

        public static List<Tween> PlayingTweens(List<Tween> fillableList = null)
        {
            var list = fillableList ?? new List<Tween>();
            foreach (var t in TweenManager.Active) if (t.active && t.isPlaying) list.Add(t);
            return list;
        }

        public static List<Tween> PausedTweens(List<Tween> fillableList = null)
        {
            var list = fillableList ?? new List<Tween>();
            foreach (var t in TweenManager.Active) if (t.active && !t.isPlaying) list.Add(t);
            return list;
        }

        public static List<Tween> TweensById(object id, bool playingOnly = false, List<Tween> fillableList = null)
        {
            var list = fillableList ?? new List<Tween>();
            foreach (var t in TweenManager.Active)
                if (t.active && (!playingOnly || t.isPlaying) && TweenManager.Matches(t, id)) list.Add(t);
            return list;
        }

        public static List<Tween> TweensByTarget(object target, bool playingOnly = false, List<Tween> fillableList = null)
        {
            var list = fillableList ?? new List<Tween>();
            foreach (var t in TweenManager.Active)
                if (t.active && (!playingOnly || t.isPlaying) && ReferenceEquals(t.target, target)) list.Add(t);
            return list;
        }
    }
}
