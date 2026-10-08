using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Engine.Tasks;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The frame driver. One per process (matching the original engine's single player
    /// loop). Tick order per frame:
    ///
    ///   Time.Advance → pump external sync-context posts → Start queue →
    ///   FixedUpdate (accumulator) → Update → trigger pass (OnTriggerEnter/Exit) →
    ///   coroutines → task scheduler (Yield/Delay/WaitUntil) →
    ///   LateUpdate → end-of-frame continuations → destroy queue.
    ///
    /// The trigger pass runs once per frame after Update (see <see cref="TriggerPass"/>
    /// for the timing rationale and semantics).
    ///
    /// Headless by design: tests call <see cref="Tick"/> directly; the CLI/realtime
    /// hosts call <see cref="Run"/> with wall-clock deltas.
    /// </summary>
    public sealed class GameLoop : IDisposable
    {
        public static GameLoop Current { get; private set; }

        public Scene Scene { get; }
        public GameTaskScheduler Scheduler { get; }
        internal CoroutineRunner Coroutines { get; } = new();
        public GameSynchronizationContext SyncContext { get; }

        /// <summary>Phase-2 trigger physics: collider registry + per-frame enter/exit dispatch.</summary>
        public TriggerPass Triggers { get; } = new();

        // E18: dynamic rigidbodies, integrated once per fixed step after the FixedUpdate
        // phase (the original engine's "callbacks, then simulation" order). Registration
        // order = creation order (deterministic, same convention as the trigger pass).
        readonly List<Rigidbody> _rigidbodies = new();

        internal void RegisterRigidbody(Rigidbody rb)
        {
            if (!_rigidbodies.Contains(rb)) _rigidbodies.Add(rb);
        }

        internal void UnregisterRigidbody(Rigidbody rb) => _rigidbodies.Remove(rb);

        int _loopThreadId = -1;
        public bool IsOnLoopThread => Environment.CurrentManagedThreadId == _loopThreadId;

        // One list per per-frame hook, each sorted by (ExecutionOrder, sequence). A
        // behaviour is filed only in the lists whose hook its type declares, so a phase
        // walks exactly the behaviours it will invoke. The former single list walked
        // every enabled behaviour for every phase AND every fixed step, which at a grown
        // arena's ~20k prisms (most of which declare no per-frame hook at all) was the
        // largest self-time in the frame. Relative order inside each phase is unchanged:
        // the same total order, restricted to the behaviours that run in it.
        readonly List<MonoBehaviour> _update = new();
        readonly List<MonoBehaviour> _lateUpdate = new();
        readonly List<MonoBehaviour> _fixedUpdate = new();
        readonly Queue<MonoBehaviour> _startQueue = new();
        readonly List<Object> _destroyQueue = new();
        readonly HashSet<Object> _destroyQueued = new(ReferenceEqualityComparer.Instance);
        MonoBehaviour[] _scratch = new MonoBehaviour[64];
        long _sequenceCounter;
        float _fixedAccumulator;

        public GameLoop(string sceneName = "Main")
        {
            if (Current != null)
                throw new InvalidOperationException(
                    "A GameLoop already exists. The engine runs exactly one loop per process — dispose the old one first.");
            Current = this;
            Time.Reset();
            Physics.ResetSettings();
            // Fresh-world reset for static UI state (same rationale as Time.Reset):
            // loop disposal skips OnDisable, so the old world's registrations and
            // queued marks would otherwise leak into this one.
            UI.BaseRaycaster.ResetRegistry();
            UI.Selectable.ResetRegistry();
            UI.LayoutRebuilder.ResetQueue();
            UI.EventSystem.current = null;
            Scene = new Scene(sceneName);
            Scheduler = new GameTaskScheduler();
            SyncContext = new GameSynchronizationContext(this);
            _loopThreadId = Environment.CurrentManagedThreadId;
        }

        internal long NextSequence() => _sequenceCounter++;

        // ── Behaviour registry ───────────────────────────────────────

        internal void RegisterBehaviour(MonoBehaviour mb)
        {
            if (mb.HasUpdate) Insert(_update, mb);
            if (mb.HasLateUpdate) Insert(_lateUpdate, mb);
            if (mb.HasFixedUpdate) Insert(_fixedUpdate, mb);
        }

        internal void UnregisterBehaviour(MonoBehaviour mb)
        {
            if (mb.HasUpdate) Remove(_update, mb);
            if (mb.HasLateUpdate) Remove(_lateUpdate, mb);
            if (mb.HasFixedUpdate) Remove(_fixedUpdate, mb);
        }

        static void Insert(List<MonoBehaviour> list, MonoBehaviour mb)
        {
            int index = list.BinarySearch(mb, BehaviourOrderComparer.Instance);
            if (index < 0) index = ~index;
            list.Insert(index, mb);
        }

        static void Remove(List<MonoBehaviour> list, MonoBehaviour mb)
        {
            // Binary search over the (ExecutionOrder, sequence) total order — `sequence` is
            // unique per behaviour, so an exact comparer hit can only be this behaviour.
            // A linear List.Remove made mass teardown (wiping a race's accumulated prism
            // field on restart) quadratic in scene size.
            int index = list.BinarySearch(mb, BehaviourOrderComparer.Instance);
            if (index >= 0 && ReferenceEquals(list[index], mb))
                list.RemoveAt(index);
            else
                list.Remove(mb); // defensive fallback (should not happen)
        }

        internal void QueueStart(MonoBehaviour mb) => _startQueue.Enqueue(mb);

        internal void QueueDestroy(Object obj)
        {
            if (_destroyQueued.Add(obj)) _destroyQueue.Add(obj);
        }

        readonly List<TrailRenderer> _trailScratch = new();

        void SampleTrails()
        {
            Renderer.CollectLiveTrails(_trailScratch);
            foreach (var trail in _trailScratch)
                if (trail.enabled) trail.Sample();
        }

        sealed class BehaviourOrderComparer : IComparer<MonoBehaviour>
        {
            public static readonly BehaviourOrderComparer Instance = new();
            public int Compare(MonoBehaviour a, MonoBehaviour b)
            {
                int byOrder = a.ExecutionOrder.CompareTo(b.ExecutionOrder);
                return byOrder != 0 ? byOrder : a.sequence.CompareTo(b.sequence);
            }
        }

        // ── Frame ────────────────────────────────────────────────────

        public void Tick(float deltaTime)
        {
            _loopThreadId = Environment.CurrentManagedThreadId;
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(SyncContext);
            try
            {
                long mark = PhaseTiming ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                if (PhaseTiming) _allocMark = GC.GetAllocatedBytesForCurrentThread();
                Time.Advance(deltaTime);
                InputSystem.InputSystem.Update(); // commit device state + evaluate actions before any script runs
                SyncContext.Pump();
                Networking.NetDriver.EarlyUpdate(); // the transport's receive slot: before any script runs
                DrainStartQueue();
                if (PhaseTiming) Lap(ref mark, "start");
                RunFixedSteps();
                if (PhaseTiming) Lap(ref mark, "fixed");
                RunPhase(_update, static mb => mb.RunUpdate());
                if (PhaseTiming) Lap(ref mark, "update");
                Coroutines.RunFrame();
                if (PhaseTiming) Lap(ref mark, "coroutines");
                Scheduler.RunFrame();
                if (PhaseTiming) Lap(ref mark, "tasks");
                Animator.TickAll(); // the animation slot: after Update and coroutines, before LateUpdate
                ParticleSystem.TickAll(); // particles move after Update has placed their emitters
                ProceduralLines.RunFrame(); // code-drawn ribbons (approximate VFX Graphs)
                if (PhaseTiming) Lap(ref mark, "animator");
                RunPhase(_lateUpdate, static mb => mb.RunLateUpdate());
                if (PhaseTiming) Lap(ref mark, "late");
                Networking.NetDriver.PostLateUpdate(); // the send slot: dirty variables, transforms
                SampleTrails(); // render-time slot: trails record where their transform ended the frame
                UI.LayoutRebuilder.FlushQueuedRebuilds(); // canvas-update slot: queued UI layout solves after LateUpdate
                Scheduler.RunEndOfFrame();
                FlushDestroyQueue();
                if (PhaseTiming) { Lap(ref mark, "destroy"); TimedFrames++; }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        /// <summary>
        /// Diagnostics: when set, <see cref="Tick"/> accumulates wall time per loop phase
        /// (start, fixed, update, triggers, coroutines, tasks, animator, late, destroy) until
        /// <see cref="TakePhaseReport"/> drains it. Off by default and branch-only when off.
        /// </summary>
        public static bool PhaseTiming;
        readonly Dictionary<string, long> _phaseTicks = new();
        readonly Dictionary<string, long> _phaseTotals = new();

        readonly Dictionary<string, long> _phaseAlloc = new();
        long _allocMark;

        /// <summary>Every phase's accumulated ticks since the loop started timing (never drained; the session report reads it).</summary>
        public IReadOnlyDictionary<string, long> PhaseTotals => _phaseTotals;

        /// <summary>Bytes the loop thread allocated in each top-level phase while timing was on (the session report reads it).</summary>
        public IReadOnlyDictionary<string, long> PhaseAllocations => _phaseAlloc;

        /// <summary>Frames ticked while <see cref="PhaseTiming"/> was on.</summary>
        public int TimedFrames { get; private set; }

        /// <summary>Diagnostics: add a sub-phase's wall time to the current report (no-op unless <see cref="PhaseTiming"/>).</summary>
        internal static void AddPhase(string phase, long ticks)
        {
            if (!PhaseTiming || Current is not { } loop) return;
            loop._phaseTicks.TryGetValue(phase, out long sum);
            loop._phaseTicks[phase] = sum + ticks;
            loop._phaseTotals.TryGetValue(phase, out long total);
            loop._phaseTotals[phase] = total + ticks;
        }

        void Lap(ref long mark, string phase)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            _phaseTicks.TryGetValue(phase, out long sum);
            _phaseTicks[phase] = sum + (now - mark);
            _phaseTotals.TryGetValue(phase, out long total);
            _phaseTotals[phase] = total + (now - mark);
            mark = now;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            _phaseAlloc.TryGetValue(phase, out long bytes);
            _phaseAlloc[phase] = bytes + (allocated - _allocMark);
            _allocMark = allocated;
        }

        /// <summary>Per-phase milliseconds since the last call, averaged over <paramref name="frames"/>.</summary>
        public string TakePhaseReport(int frames)
        {
            var parts = new List<string>();
            foreach (var kv in _phaseTicks)
            {
                double ms = kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frames);
                if (ms >= 0.05) parts.Add($"{kv.Key} {ms:F1}");
            }
            _phaseTicks.Clear();
            return string.Join(", ", parts);
        }

        /// <summary>Tick a fixed number of frames at a fixed delta (deterministic harness driving).</summary>
        public void Run(int frames, float deltaTime)
        {
            for (int i = 0; i < frames; i++) Tick(deltaTime);
        }

        readonly List<MonoBehaviour> _startBatch = new();

        void DrainStartQueue()
        {
            // Start runs in script execution order (enable order within one order), as the
            // per-frame phases do. Behaviours enabled during Start callbacks form the next batch
            // of the same drain.
            while (_startQueue.Count > 0)
            {
                _startBatch.Clear();
                bool ordered = false;
                while (_startQueue.Count > 0)
                {
                    var mb = _startQueue.Dequeue();
                    ordered |= mb.ExecutionOrder != 0;
                    _startBatch.Add(mb);
                }
                if (ordered)
                {
                    var indexed = new List<(MonoBehaviour mb, int i)>(_startBatch.Count);
                    for (int i = 0; i < _startBatch.Count; i++) indexed.Add((_startBatch[i], i));
                    indexed.Sort((a, b) => a.mb.ExecutionOrder != b.mb.ExecutionOrder ? a.mb.ExecutionOrder.CompareTo(b.mb.ExecutionOrder) : a.i.CompareTo(b.i));
                    for (int i = 0; i < indexed.Count; i++) _startBatch[i] = indexed[i].mb;
                }
                foreach (var mb in _startBatch) mb.RunStart();
            }
        }

        void RunFixedSteps()
        {
            _fixedAccumulator += Time.deltaTime;
            while (_fixedAccumulator >= Time.fixedDeltaTime)
            {
                _fixedAccumulator -= Time.fixedDeltaTime;
                Time.EnterFixedPhase();
                try
                {
                    RunPhase(_fixedUpdate, static mb => mb.RunFixedUpdate());
                    IntegrateRigidbodies(Time.fixedDeltaTime);
                    // The physics step's contact pass (Unity: FixedUpdate → Physics.Simulate, which
                    // sends OnTrigger* messages). Triggers are a SAMPLE at the fixed rate — 25 Hz in
                    // this project — not once per rendered frame: a frame with no fixed step fires
                    // no trigger message at all, and a fast body can cross a thin trigger between two.
                    long tmark = PhaseTiming ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    Triggers.RunFrame();
                    if (PhaseTiming) AddPhase("triggers", System.Diagnostics.Stopwatch.GetTimestamp() - tmark);
                }
                finally { Time.ExitFixedPhase(); }
            }
        }

        /// <summary>
        /// E18: ballistic integration of non-kinematic rigidbodies, once per fixed step
        /// after the FixedUpdate callbacks — the original engine's physics-simulation
        /// slot inside the step. Snapshot iteration: callbacks/destroys during
        /// integration affect the NEXT step.
        /// </summary>
        void IntegrateRigidbodies(float dt)
        {
            if (_rigidbodies.Count == 0) return;
            var bodies = _rigidbodies.ToArray();
            foreach (var rb in bodies)
            {
                if (rb.destroyedFlag || rb.gameObject is null || rb.gameObject.IsDestroyed || !rb.gameObject.activeInHierarchy)
                    continue;
                rb.Integrate(dt);
            }
        }

        void RunPhase(List<MonoBehaviour> phase, Action<MonoBehaviour> invoke)
        {
            // Snapshot: callbacks may register/unregister behaviours mid-phase.
            int count = phase.Count;
            if (count == 0) return;
            if (_scratch.Length < count) _scratch = new MonoBehaviour[Math.Max(count, _scratch.Length * 2)];
            phase.CopyTo(_scratch, 0);

            for (int i = 0; i < count; i++)
            {
                var mb = _scratch[i];
                if (mb.destroyedFlag || !mb.isActiveAndEnabled || !mb.started) continue;
                invoke(mb);
            }
            Array.Clear(_scratch, 0, count);
        }

        void FlushDestroyQueue()
        {
            if (_destroyQueue.Count == 0) return;
            var toDestroy = _destroyQueue.ToArray();
            _destroyQueue.Clear();
            _destroyQueued.Clear();
            foreach (var obj in toDestroy)
            {
                switch (obj)
                {
                    case GameObject go: go.DestroyNow(); break;
                    case Component c: c.DestroyComponentNow(); break;
                    default: obj.destroyedFlag = true; break;
                }
            }
        }

        public void Dispose()
        {
            Scene.isLoaded = false; // teardown probes (e.g. Spindle) skip unloading-scene work
            if (Current == this) Current = null;
        }
    }
}
