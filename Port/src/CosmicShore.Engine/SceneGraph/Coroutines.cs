using System;
using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public abstract class YieldInstruction { }

    /// <summary>Suspends a coroutine for scaled game-time seconds.</summary>
    public sealed class WaitForSeconds : YieldInstruction
    {
        internal readonly float seconds;
        public WaitForSeconds(float seconds) { this.seconds = seconds; }
    }

    /// <summary>
    /// Suspends a coroutine for UNSCALED seconds (original contract: immune to
    /// timeScale — menu UI animates while the game is paused). The engine's
    /// unscaled clock advances per tick regardless of Time.timeScale, which is
    /// exactly the original's realtime-during-play semantics in this fixed-step
    /// harness.
    /// </summary>
    public sealed class WaitForSecondsRealtime : YieldInstruction
    {
        internal readonly float seconds;
        public WaitForSecondsRealtime(float seconds) { this.seconds = seconds; }
    }

    /// <summary>
    /// Suspends a coroutine until the predicate reports true (original contract:
    /// polled once per frame at the resume point; an already-true predicate still
    /// costs one frame of suspension, like the runner's other yields).
    /// </summary>
    public sealed class WaitUntil : YieldInstruction
    {
        internal readonly Func<bool> predicate;
        public WaitUntil(Func<bool> predicate)
            => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>Handle returned by StartCoroutine; yield it to await completion.</summary>
    /// <summary>
    /// Original contract: resume at the end of the current frame. The runner's
    /// default case resumes unknown yields NEXT frame — one tick later than the
    /// original, which every ported use (deferred modal launch, screenshot timing)
    /// tolerates; exact end-of-frame timing is available via GameTask.WaitForEndOfFrame.
    /// </summary>
    public sealed class WaitForEndOfFrame : YieldInstruction { }

    public sealed class Coroutine : YieldInstruction
    {
        internal bool Done;
    }

    /// <summary>
    /// Frame-driven coroutine execution with the original engine's contract:
    /// StartCoroutine runs the body synchronously to its first yield; `yield return null`
    /// resumes next frame; WaitForSeconds uses scaled time; nested IEnumerator/Coroutine
    /// yields suspend the parent; coroutines die with their owner (destroy or deactivate).
    /// Resumes after Update each frame (the classic coroutine timing point).
    /// </summary>
    public sealed class CoroutineRunner
    {
        sealed class Entry
        {
            public MonoBehaviour Owner;
            public readonly Stack<IEnumerator> Frames = new();
            public IEnumerator Root;
            public Coroutine Handle;
            public float WaitUntilTime = -1f;
            public float WaitUntilUnscaledTime = -1f;
            public Func<bool> WaitPredicate;
            public Coroutine WaitingOn;
        }

        readonly List<Entry> _entries = new();

        public Coroutine Start(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine is null) throw new ArgumentNullException(nameof(routine));
            var entry = new Entry { Owner = owner, Root = routine, Handle = new Coroutine() };
            entry.Frames.Push(routine);
            _entries.Add(entry);
            Step(entry); // synchronous run to first yield (original contract)
            return entry.Handle;
        }

        public void Stop(MonoBehaviour owner, Coroutine handle)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Handle == handle && _entries[i].Owner == owner)
                {
                    _entries[i].Handle.Done = true;
                    _entries.RemoveAt(i);
                    return;
                }
        }

        /// <summary>
        /// Original-engine StopCoroutine(IEnumerator) contract: stops the coroutine that
        /// was started with this exact enumerator instance. A freshly-created enumerator
        /// matches nothing and the call is a no-op (the documented original behavior).
        /// </summary>
        public void Stop(MonoBehaviour owner, IEnumerator routine)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_entries[i].Root, routine) && ReferenceEquals(_entries[i].Owner, owner))
                {
                    _entries[i].Handle.Done = true;
                    _entries.RemoveAt(i);
                    return;
                }
        }

        public void StopAll(MonoBehaviour owner)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_entries[i].Owner, owner))
                {
                    _entries[i].Handle.Done = true;
                    _entries.RemoveAt(i);
                }
        }

        internal void RunFrame()
        {
            // Index loop tolerant of StartCoroutine during stepping (appends).
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                // Owner died or its object went inactive: coroutine ends permanently.
                if (entry.Owner is null || entry.Owner.IsDestroyed || entry.Owner.gameObject is null
                    || entry.Owner.gameObject.IsDestroyed || !entry.Owner.gameObject.activeInHierarchy)
                {
                    entry.Handle.Done = true;
                    _entries.RemoveAt(i--);
                    continue;
                }

                if (entry.WaitUntilTime >= 0f)
                {
                    if (Time.time < entry.WaitUntilTime) continue;
                    entry.WaitUntilTime = -1f;
                }

                if (entry.WaitUntilUnscaledTime >= 0f)
                {
                    if (Time.unscaledTime < entry.WaitUntilUnscaledTime) continue;
                    entry.WaitUntilUnscaledTime = -1f;
                }

                if (entry.WaitPredicate is not null)
                {
                    if (!entry.WaitPredicate()) continue;
                    entry.WaitPredicate = null;
                }

                if (entry.WaitingOn is not null)
                {
                    if (!entry.WaitingOn.Done) continue;
                    entry.WaitingOn = null;
                }

                if (!Step(entry))
                    _entries.RemoveAt(i--);
            }
        }

        /// <summary>Advance one coroutine until it suspends or completes. False = completed.</summary>
        bool Step(Entry entry)
        {
            while (entry.Frames.Count > 0)
            {
                var frame = entry.Frames.Peek();
                bool moved;
                try { moved = frame.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e, entry.Owner);
                    entry.Handle.Done = true;
                    return false;
                }

                if (!moved)
                {
                    entry.Frames.Pop();
                    if (entry.Frames.Count == 0)
                    {
                        entry.Handle.Done = true;
                        return false;
                    }
                    continue; // resume parent immediately
                }

                switch (frame.Current)
                {
                    case null:
                        return true; // resume next frame
                    case WaitForSeconds wait:
                        entry.WaitUntilTime = Time.time + wait.seconds;
                        return true;
                    case WaitForSecondsRealtime waitRealtime:
                        entry.WaitUntilUnscaledTime = Time.unscaledTime + waitRealtime.seconds;
                        return true;
                    case WaitUntil waitUntil:
                        entry.WaitPredicate = waitUntil.predicate;
                        return true;
                    case IEnumerator nested:
                        entry.Frames.Push(nested); // child runs to its first yield this frame
                        continue;
                    case Coroutine other:
                        if (other.Done) continue;
                        entry.WaitingOn = other;
                        return true;
                    default:
                        return true; // unknown yield object: treat as next-frame
                }
            }
            entry.Handle.Done = true;
            return false;
        }
    }
}
