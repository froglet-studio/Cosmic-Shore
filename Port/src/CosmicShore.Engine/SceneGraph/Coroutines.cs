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

    /// <summary>Handle returned by StartCoroutine; yield it to await completion.</summary>
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
            public Coroutine Handle;
            public float WaitUntilTime = -1f;
            public Coroutine WaitingOn;
        }

        readonly List<Entry> _entries = new();

        public Coroutine Start(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine is null) throw new ArgumentNullException(nameof(routine));
            var entry = new Entry { Owner = owner, Handle = new Coroutine() };
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
