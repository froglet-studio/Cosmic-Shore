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
    public class WaitUntil : YieldInstruction
    {
        internal readonly Func<bool> predicate;
        public WaitUntil(Func<bool> predicate)
            => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>Suspends a coroutine while the predicate reports true (original: WaitWhile).</summary>
    public sealed class WaitWhile : WaitUntil
    {
        public WaitWhile(Func<bool> predicate) : base(Negate(predicate)) { }
        static Func<bool> Negate(Func<bool> p) { if (p == null) throw new ArgumentNullException(nameof(p)); return () => !p(); }
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
            public bool Removed;
        }

        // Every live coroutine in start order (the resume order), plus an index by owner.
        //
        // Both used to be one list with RemoveAt/linear scans, which was quadratic twice
        // over in a grown arena: every prism's pool reset calls StopAllCoroutines (a scan
        // of EVERY live coroutine, thousands of them), and every coroutine that finished
        // shifted the whole list down by one. A stop now flags the entry and unlinks it
        // from its owner's short list; the resume list is compacted once, in order, at the
        // end of the frame. Resume order and every Done transition are unchanged.
        readonly List<Entry> _entries = new();
        readonly Dictionary<MonoBehaviour, List<Entry>> _byOwner = new(ReferenceEqualityComparer.Instance);
        int _removed;

        public Coroutine Start(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine is null) throw new ArgumentNullException(nameof(routine));
            var entry = new Entry { Owner = owner, Root = routine, Handle = new Coroutine() };
            entry.Frames.Push(routine);
            _entries.Add(entry);
            if (owner is not null)
            {
                if (!_byOwner.TryGetValue(owner, out var list)) _byOwner[owner] = list = new List<Entry>(2);
                list.Add(entry);
            }
            if (!Step(entry)) Kill(entry); // synchronous run to first yield (original contract)
            // Started on an inactive object: it ran to its first yield and ends there (it would
            // never resume). Deactivation otherwise stops coroutines as it happens (StopAll from
            // MonoBehaviour.HandleHierarchyActive / DestroyComponentNow).
            else if (owner is not null && !owner.gameObject.activeInHierarchy) Kill(entry);
            return entry.Handle;
        }

        void Kill(Entry entry)
        {
            if (entry.Removed) return;
            entry.Removed = true;
            entry.Handle.Done = true;
            _removed++;
            if (entry.Owner is not null && _byOwner.TryGetValue(entry.Owner, out var list))
            {
                list.Remove(entry);
                if (list.Count == 0) _byOwner.Remove(entry.Owner);
            }
        }

        public void Stop(MonoBehaviour owner, Coroutine handle)
        {
            if (owner is null || !_byOwner.TryGetValue(owner, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Handle == handle) { Kill(list[i]); return; }
        }

        /// <summary>
        /// Original-engine StopCoroutine(IEnumerator) contract: stops the coroutine that
        /// was started with this exact enumerator instance. A freshly-created enumerator
        /// matches nothing and the call is a no-op (the documented original behavior).
        /// </summary>
        public void Stop(MonoBehaviour owner, IEnumerator routine)
        {
            if (owner is null || !_byOwner.TryGetValue(owner, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i].Root, routine)) { Kill(list[i]); return; }
        }

        public void StopAll(MonoBehaviour owner)
        {
            if (owner is null || !_byOwner.Remove(owner, out var list)) return;
            foreach (var entry in list)
            {
                if (entry.Removed) continue;
                entry.Removed = true;
                entry.Handle.Done = true;
                _removed++;
            }
        }

        // Diagnostics: CS_PORT_TRACE_CO=1 prints the live coroutine census and which
        // coroutine bodies the stepping time went to, every 300 frames.
        static readonly bool s_trace = Environment.GetEnvironmentVariable("CS_PORT_TRACE_CO") != null;
        readonly Dictionary<string, (long ticks, int steps)> _stepCost = new();

        void Account(Entry entry, long ticks)
        {
            string key = entry.Root?.GetType().Name ?? "?";
            _stepCost.TryGetValue(key, out var v);
            _stepCost[key] = (v.ticks + ticks, v.steps + 1);
        }

        void TraceCensus()
        {
            int sleeping = 0, predicate = 0, waiting = 0;
            foreach (var e in _entries)
            {
                if (e.Removed) continue;
                if (e.WaitUntilTime >= 0f || e.WaitUntilUnscaledTime >= 0f) sleeping++;
                else if (e.WaitPredicate != null) predicate++;
                else if (e.WaitingOn != null) waiting++;
            }
            var top = new List<KeyValuePair<string, (long ticks, int steps)>>(_stepCost);
            top.Sort((a, b) => b.Value.ticks.CompareTo(a.Value.ticks));
            Console.WriteLine($"[co] frame {Time.frameCount}: {_entries.Count} live ({sleeping} timed, {predicate} predicate, {waiting} awaiting); step ms/300f: " +
                string.Join(", ", top.GetRange(0, Math.Min(6, top.Count)).ConvertAll(
                    kv => $"{kv.Key} {kv.Value.ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F0}/{kv.Value.steps}")));
            _stepCost.Clear();
        }

        static readonly bool s_verify = Environment.GetEnvironmentVariable("COSMIC_SHORE_VERIFY_COROUTINES") == "1";

        /// <summary>Every live coroutine's owner must be active: anything else means a deactivation path skipped StopAll.</summary>
        void VerifyOwners()
        {
            int live = 0, bad = 0; string sample = null;
            foreach (var e in _entries)
            {
                if (e.Removed || e.Owner is null) continue;
                live++;
                if (!e.Owner.IsDestroyed && e.Owner.gameObject is { } go && !go.IsDestroyed && !go.activeInHierarchy)
                { bad++; sample ??= $"{e.Owner.GetType().Name} on '{go.name}' ({e.Root?.GetType().Name})"; }
            }
            Console.WriteLine($"[verify-coroutines] frame {Time.frameCount}: {live} live, {bad} with an inactive owner{(sample != null ? " e.g. " + sample : "")}");
        }

        internal void RunFrame()
        {
            // Index loop tolerant of StartCoroutine during stepping (appends).
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Removed) continue;

                // Owner died: coroutine ends permanently. (Deactivation and component destruction
                // stop coroutines as they happen, so no per-frame activity walk is needed here;
                // COSMIC_SHORE_VERIFY_COROUTINES=1 audits that.)
                if (entry.Owner is null || entry.Owner.IsDestroyed || entry.Owner.gameObject is null
                    || entry.Owner.gameObject.IsDestroyed)
                {
                    Kill(entry);
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

                long t0 = s_trace ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                bool alive = Step(entry);
                if (s_trace) Account(entry, System.Diagnostics.Stopwatch.GetTimestamp() - t0);
                if (!alive) Kill(entry);
            }
            if (s_trace && Time.frameCount % 300 == 0) TraceCensus();
            if (s_verify && Time.frameCount % 30 == 0) VerifyOwners();

            if (_removed > 0)
            {
                _entries.RemoveAll(static e => e.Removed);
                _removed = 0;
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
