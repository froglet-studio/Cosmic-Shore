using System;
using System.Collections.Generic;
using DG.Tweening.Core.Easing;

namespace DG.Tweening
{
    /// <summary>
    /// A timeline of nested tweens, intervals and callbacks. Nested tweens are owned by the
    /// sequence (removed from the global update, autoKill forced off, their delay converted
    /// into a leading gap, their own SetUpdate/SetLink ignored); the sequence drives them by
    /// playhead position, so a nested tween captures its start value only when the sequence
    /// playhead first reaches it. A sequence's default ease is Linear; a non-linear ease remaps
    /// the whole timeline.
    /// </summary>
    public sealed class Sequence : Tween
    {
        internal struct Item
        {
            public Tween tween;
            public TweenCallback callback;
            public float start;
            public float end;
        }

        internal readonly List<Item> items = new();
        internal float lastInsertTime;
        bool _zeroCallbacksFired;

        internal Sequence()
        {
            easeType = Ease.Linear;
            duration = 0f;
        }

        bool Locked => startupDone || !active;

        internal override void Startup() { }

        internal void ResetZeroCallbacks() => _zeroCallbacksFired = false;

        // ── Building ──

        internal void DoInsert(float at, Tween t)
        {
            if (Locked || t == null || !t.active || t.isSequenced || ReferenceEquals(t, this)) return;
            TweenManager.Remove(t);
            if (at < 0f) at = 0f;
            float insertAt = at + t.delay;
            t.delay = 0f;
            t.elapsedDelay = 0f;
            t.delayComplete = true;
            t.isSequenced = true;
            t.sequenceParent = this;
            t.autoKill = false;
            t.isPlaying = false;
            t.hasLink = false;
            if (t.isSpeedBased)
            {
                // Speed-based tweens need their start value to know their duration, which a
                // sequence must know at build time: treat the "speed" as a duration.
                t.isSpeedBased = false;
            }
            if (t.loops < 0) t.loops = 1;
            float length = t.duration * Math.Max(t.loops, 1);
            items.Add(new Item { tween = t, start = insertAt, end = insertAt + length });
            lastInsertTime = at;
            if (insertAt + length > duration) duration = insertAt + length;
        }

        internal void DoInsertCallback(float at, TweenCallback cb)
        {
            if (Locked || cb == null) return;
            if (at < 0f) at = 0f;
            items.Add(new Item { callback = cb, start = at, end = at });
            lastInsertTime = at;
            if (at > duration) duration = at;
        }

        internal void DoAppendInterval(float interval)
        {
            if (Locked) return;
            lastInsertTime = duration;
            duration += Math.Max(0f, interval);
        }

        internal void DoPrependInterval(float interval)
        {
            if (Locked || interval <= 0f) return;
            Shift(interval);
            lastInsertTime = 0f;
        }

        internal void DoPrepend(Tween t)
        {
            if (Locked || t == null || !t.active || t.isSequenced || ReferenceEquals(t, this)) return;
            float length = t.delay + t.duration * Math.Max(t.loops < 0 ? 1 : t.loops, 1);
            Shift(length);
            DoInsert(0f, t);
            lastInsertTime = 0f;
        }

        internal void DoPrependCallback(TweenCallback cb)
        {
            if (Locked || cb == null) return;
            items.Insert(0, new Item { callback = cb, start = 0f, end = 0f });
            lastInsertTime = 0f;
        }

        void Shift(float by)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                it.start += by;
                it.end += by;
                items[i] = it;
            }
            duration += by;
        }

        internal void TerminateNested()
        {
            foreach (var it in items)
            {
                if (it.tween == null || !it.tween.active) continue;
                it.tween.active = false;
                it.tween.isPlaying = false;
                it.tween.Fire(it.tween.onKill);
            }
        }

        // ── Playback ──

        float Effective(int loop, float pos)
        {
            bool mirrored = loopType == LoopType.Yoyo && (loop & 1) == 1;
            float t = mirrored ? duration - pos : pos;
            if (duration <= 0f || easeType == Ease.Linear) return t;
            return duration * EaseManager.Evaluate(easeType, customEase, t, duration, easeOvershootOrAmplitude, easePeriod);
        }

        internal override void ApplyAt(int loopIndex, float loopPosition)
            => Range(Effective(loopIndex, loopPosition), Effective(loopIndex, loopPosition), false);

        internal override void ApplyTransition(int prevLoop, float prevPos, int loop, float pos, bool silent)
        {
            if (loop == prevLoop)
            {
                Range(Effective(prevLoop, prevPos), Effective(loop, pos), silent);
                return;
            }
            bool yoyo = loopType == LoopType.Yoyo;
            int guard = 0;
            if (loop > prevLoop)
            {
                Range(Effective(prevLoop, prevPos), Effective(prevLoop, duration), silent);
                for (int k = prevLoop + 1; k <= loop && guard++ < 10000 && active; k++)
                {
                    if (!yoyo)
                    {
                        Range(duration, 0f, true); // silent restart of the nested timeline
                        _zeroCallbacksFired = false;
                    }
                    float endPos = k == loop ? pos : duration;
                    Range(Effective(k, 0f), Effective(k, endPos), silent);
                }
            }
            else
            {
                Range(Effective(prevLoop, prevPos), Effective(prevLoop, 0f), silent);
                for (int k = prevLoop - 1; k >= loop && guard++ < 10000 && active; k--)
                {
                    if (!yoyo) Range(0f, duration, true);
                    float endPos = k == loop ? pos : 0f;
                    Range(Effective(k, duration), Effective(k, endPos), silent);
                }
            }
        }

        /// <summary>Walk the timeline from <paramref name="from"/> to <paramref name="to"/>.</summary>
        void Range(float from, float to, bool silent)
        {
            if (to >= from)
            {
                bool includeFrom = !_zeroCallbacksFired && from <= 0f;
                for (int i = 0; i < items.Count && active; i++)
                {
                    var it = items[i];
                    if (it.tween == null)
                    {
                        if (silent) continue;
                        bool passed = (it.start > from || (includeFrom && it.start <= from)) && it.start <= to;
                        if (passed) Fire(it.callback);
                        continue;
                    }
                    var t = it.tween;
                    if (!t.active || to < it.start) continue;
                    if (from > it.end && t.startupDone && t.isComplete) continue;
                    t.GotoNested(Math.Min(to - it.start, it.end - it.start), !silent);
                }
                if (from <= 0f) _zeroCallbacksFired = true;
            }
            else
            {
                for (int i = items.Count - 1; i >= 0 && active; i--)
                {
                    var it = items[i];
                    if (it.tween == null)
                    {
                        if (!silent && it.start < from && it.start >= to) Fire(it.callback);
                        continue;
                    }
                    var t = it.tween;
                    if (!t.active || !t.startupDone) continue;
                    if (from < it.start && t.FullPosition <= 0f) continue;
                    t.GotoNested(Math.Max(to - it.start, 0f), !silent);
                }
                if (to <= 0f) _zeroCallbacksFired = true;
            }
        }
    }
}
