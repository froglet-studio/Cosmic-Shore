using CosmicShore.Engine;
using DG.Tweening.Core.Easing;

namespace DG.Tweening
{
    /// <summary>
    /// Chainable settings, callbacks and Sequence building. Timing settings (loops, delay,
    /// speed-based) are ignored once a tween has started or been nested — the documented
    /// "has no effect if the tween has already started" rule.
    /// </summary>
    public static class TweenSettingsExtensions
    {
        static bool Editable(Tween t) => t != null && t.active;
        static bool TimingEditable(Tween t) => Editable(t) && !t.isSequenced && !t.startupDone;

        public static T SetAutoKill<T>(this T t) where T : Tween => t.SetAutoKill(true);
        public static T SetAutoKill<T>(this T t, bool autoKillOnCompletion) where T : Tween
        {
            if (Editable(t) && !t.isSequenced) t.autoKill = autoKillOnCompletion;
            return t;
        }

        public static T SetId<T>(this T t, object objectId) where T : Tween { if (Editable(t)) t.id = objectId; return t; }
        public static T SetId<T>(this T t, string stringId) where T : Tween { if (Editable(t)) t.stringId = stringId; return t; }
        public static T SetId<T>(this T t, int intId) where T : Tween { if (Editable(t)) t.intId = intId; return t; }

        public static T SetLink<T>(this T t, GameObject gameObject) where T : Tween
            => t.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        public static T SetLink<T>(this T t, GameObject gameObject, LinkBehaviour behaviour) where T : Tween
        {
            if (Editable(t)) t.SetLinkTarget(gameObject, behaviour);
            return t;
        }

        public static T SetTarget<T>(this T t, object target) where T : Tween { if (Editable(t)) t.target = target; return t; }

        public static T SetLoops<T>(this T t, int loops) where T : Tween
        {
            if (!TimingEditable(t)) return t;
            t.loops = loops < 0 ? -1 : (loops == 0 ? 1 : loops);
            return t;
        }

        public static T SetLoops<T>(this T t, int loops, LoopType loopType) where T : Tween
        {
            if (!TimingEditable(t)) return t;
            t.loops = loops < 0 ? -1 : (loops == 0 ? 1 : loops);
            t.loopType = loopType;
            return t;
        }

        public static T SetEase<T>(this T t, Ease ease) where T : Tween
        {
            if (!Editable(t)) return t;
            t.easeType = ease;
            t.customEase = null;
            if (EaseManager.IsFlashEase(ease)) t.easeOvershootOrAmplitude = (int)t.easeOvershootOrAmplitude;
            return t;
        }

        public static T SetEase<T>(this T t, Ease ease, float overshoot) where T : Tween
        {
            if (!Editable(t)) return t;
            t.easeType = ease;
            t.customEase = null;
            t.easeOvershootOrAmplitude = EaseManager.IsFlashEase(ease) ? (int)overshoot : overshoot;
            return t;
        }

        public static T SetEase<T>(this T t, Ease ease, float amplitude, float period) where T : Tween
        {
            if (!Editable(t)) return t;
            t.easeType = ease;
            t.customEase = null;
            t.easeOvershootOrAmplitude = EaseManager.IsFlashEase(ease) ? (int)amplitude : amplitude;
            t.easePeriod = period;
            return t;
        }

        public static T SetEase<T>(this T t, AnimationCurve animCurve) where T : Tween
        {
            if (!Editable(t) || animCurve == null) return t;
            t.easeType = Ease.INTERNAL_Custom;
            t.customEase = EaseManager.ToEaseFunction(animCurve);
            return t;
        }

        public static T SetEase<T>(this T t, EaseFunction customEase) where T : Tween
        {
            if (!Editable(t) || customEase == null) return t;
            t.easeType = Ease.INTERNAL_Custom;
            t.customEase = customEase;
            return t;
        }

        public static T SetRecyclable<T>(this T t) where T : Tween => t.SetRecyclable(true);
        public static T SetRecyclable<T>(this T t, bool recyclable) where T : Tween
        {
            if (Editable(t)) t.isRecyclable = recyclable; // pooling is not implemented: advisory only
            return t;
        }

        /// <summary>TRUE = ignore Time.timeScale (advance on unscaled time).</summary>
        public static T SetUpdate<T>(this T t, bool isIndependentUpdate) where T : Tween
            => t.SetUpdate(DOTween.defaultUpdateType, isIndependentUpdate);

        public static T SetUpdate<T>(this T t, UpdateType updateType) where T : Tween
            => t.SetUpdate(updateType, DOTween.defaultTimeScaleIndependent);

        public static T SetUpdate<T>(this T t, UpdateType updateType, bool isIndependentUpdate) where T : Tween
        {
            if (!Editable(t) || t.isSequenced) return t;
            t.updateType = updateType;
            t.isIndependentUpdate = isIndependentUpdate;
            return t;
        }

        public static T SetInverted<T>(this T t) where T : Tween => t.SetInverted(true);
        public static T SetInverted<T>(this T t, bool inverted) where T : Tween
        {
            if (Editable(t)) t.isInverted = inverted;
            return t;
        }

        // ── Callbacks ──

        public static T OnStart<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onStart = action; return t; }
        public static T OnPlay<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onPlay = action; return t; }
        public static T OnPause<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onPause = action; return t; }
        public static T OnRewind<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onRewind = action; return t; }
        public static T OnUpdate<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onUpdate = action; return t; }
        public static T OnStepComplete<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onStepComplete = action; return t; }
        public static T OnComplete<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onComplete = action; return t; }
        public static T OnKill<T>(this T t, TweenCallback action) where T : Tween { if (Editable(t)) t.onKill = action; return t; }
        public static T OnWaypointChange<T>(this T t, TweenCallback<int> action) where T : Tween { if (Editable(t)) t.onWaypointChange = action; return t; }

        /// <summary>Copies settings (id, target, ease, loops, callbacks, update type…) from another tween.</summary>
        public static T SetAs<T>(this T t, Tween asTween) where T : Tween
        {
            if (!Editable(t) || asTween == null) return t;
            t.timeScale = asTween.timeScale;
            t.isBackwards = asTween.isBackwards;
            t.updateType = asTween.updateType;
            t.isIndependentUpdate = asTween.isIndependentUpdate;
            t.id = asTween.id;
            t.stringId = asTween.stringId;
            t.intId = asTween.intId;
            t.target = asTween.target;
            t.onStart = asTween.onStart;
            t.onPlay = asTween.onPlay;
            t.onRewind = asTween.onRewind;
            t.onUpdate = asTween.onUpdate;
            t.onStepComplete = asTween.onStepComplete;
            t.onComplete = asTween.onComplete;
            t.onKill = asTween.onKill;
            t.onWaypointChange = asTween.onWaypointChange;
            t.isRecyclable = asTween.isRecyclable;
            t.isSpeedBased = asTween.isSpeedBased;
            t.autoKill = asTween.autoKill;
            if (!t.startupDone)
            {
                t.loops = asTween.loops;
                t.loopType = asTween.loopType;
            }
            t.easeType = asTween.easeType;
            t.customEase = asTween.customEase;
            t.easeOvershootOrAmplitude = asTween.easeOvershootOrAmplitude;
            t.easePeriod = asTween.easePeriod;
            return t;
        }

        public static T SetDelay<T>(this T t, float delay) where T : Tween
        {
            if (!TimingEditable(t)) return t;
            t.delay = delay < 0f ? 0f : delay;
            t.elapsedDelay = 0f;
            t.delayComplete = t.delay <= 0f;
            return t;
        }

        public static T SetDelay<T>(this T t, float delay, bool asPrependedIntervalIfSequence) where T : Tween
        {
            if (asPrependedIntervalIfSequence && t is Sequence s && TimingEditable(t))
            {
                s.DoPrependInterval(delay);
                return t;
            }
            return t.SetDelay(delay);
        }

        public static T SetRelative<T>(this T t) where T : Tween => t.SetRelative(true);
        public static T SetRelative<T>(this T t, bool isRelative) where T : Tween
        {
            if (TimingEditable(t) && !t.isFrom) t.isRelative = isRelative;
            return t;
        }

        /// <summary>Treat the duration argument as speed (units per second) instead.</summary>
        public static T SetSpeedBased<T>(this T t) where T : Tween => t.SetSpeedBased(true);
        public static T SetSpeedBased<T>(this T t, bool isSpeedBased) where T : Tween
        {
            if (TimingEditable(t) && t is Tweener) t.isSpeedBased = isSpeedBased;
            return t;
        }

        // ── From ──

        /// <summary>Turn into a FROM tween: jump to the end value now, then tween back to the current value.</summary>
        public static TweenerCore<T> From<T>(this TweenerCore<T> t) => t.From(true, false);
        public static TweenerCore<T> From<T>(this TweenerCore<T> t, bool isRelative) => t.From(true, isRelative);

        public static TweenerCore<T> From<T>(this TweenerCore<T> t, bool setImmediately, bool isRelative)
        {
            if (TimingEditable(t)) t.SetFrom(t.endValue, setImmediately, isRelative);
            return t;
        }

        public static TweenerCore<T> From<T>(this TweenerCore<T> t, T fromValue, bool setImmediately = true, bool isRelative = false)
        {
            if (TimingEditable(t)) t.SetFrom(fromValue, setImmediately, isRelative);
            return t;
        }

        public static TweenerCore<float> SetOptions(this TweenerCore<float> t, bool snapping) { if (Editable(t)) t.snapping = snapping; return t; }
        public static TweenerCore<Vector2> SetOptions(this TweenerCore<Vector2> t, bool snapping) { if (Editable(t)) t.snapping = snapping; return t; }
        public static TweenerCore<Vector3> SetOptions(this TweenerCore<Vector3> t, bool snapping) { if (Editable(t)) t.snapping = snapping; return t; }
        public static TweenerCore<Vector4> SetOptions(this TweenerCore<Vector4> t, bool snapping) { if (Editable(t)) t.snapping = snapping; return t; }
        public static WaypointTweener SetOptions(this WaypointTweener t, bool snapping) { if (Editable(t)) t.snapping = snapping; return t; }

        // ── Sequence building ──

        public static Sequence Append(this Sequence s, Tween t)
        {
            if (s != null && s.active) s.DoInsert(s.duration, t);
            return s;
        }

        public static Sequence Prepend(this Sequence s, Tween t)
        {
            if (s != null && s.active) s.DoPrepend(t);
            return s;
        }

        public static Sequence Join(this Sequence s, Tween t)
        {
            if (s != null && s.active) s.DoInsert(s.lastInsertTime, t);
            return s;
        }

        public static Sequence Insert(this Sequence s, float atPosition, Tween t)
        {
            if (s != null && s.active) s.DoInsert(atPosition, t);
            return s;
        }

        public static Sequence AppendInterval(this Sequence s, float interval)
        {
            if (s != null && s.active) s.DoAppendInterval(interval);
            return s;
        }

        public static Sequence PrependInterval(this Sequence s, float interval)
        {
            if (s != null && s.active) s.DoPrependInterval(interval);
            return s;
        }

        public static Sequence AppendCallback(this Sequence s, TweenCallback callback)
        {
            if (s != null && s.active) s.DoInsertCallback(s.duration, callback);
            return s;
        }

        public static Sequence PrependCallback(this Sequence s, TweenCallback callback)
        {
            if (s != null && s.active) s.DoPrependCallback(callback);
            return s;
        }

        public static Sequence JoinCallback(this Sequence s, TweenCallback callback)
        {
            if (s != null && s.active) s.DoInsertCallback(s.lastInsertTime, callback);
            return s;
        }

        public static Sequence InsertCallback(this Sequence s, float atPosition, TweenCallback callback)
        {
            if (s != null && s.active) s.DoInsertCallback(atPosition, callback);
            return s;
        }
    }
}
