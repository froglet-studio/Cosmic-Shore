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

        readonly List<MonoBehaviour> _behaviours = new();   // sorted by (ExecutionOrder, sequence)
        readonly Queue<MonoBehaviour> _startQueue = new();
        readonly List<Object> _destroyQueue = new();
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
            int index = _behaviours.BinarySearch(mb, BehaviourOrderComparer.Instance);
            if (index < 0) index = ~index;
            _behaviours.Insert(index, mb);
        }

        internal void UnregisterBehaviour(MonoBehaviour mb)
        {
            // Binary search over the (ExecutionOrder, sequence) total order — `sequence` is
            // unique per behaviour, so an exact comparer hit can only be this behaviour.
            // Replaces the former List.Remove linear scan, which made mass teardown
            // (e.g. wiping a race's accumulated prism field on restart) quadratic in
            // scene size. Behavior-preserving: same element removed, same list order kept.
            int index = _behaviours.BinarySearch(mb, BehaviourOrderComparer.Instance);
            if (index >= 0 && ReferenceEquals(_behaviours[index], mb))
                _behaviours.RemoveAt(index);
            else
                _behaviours.Remove(mb); // defensive fallback (should not happen)
        }

        internal void QueueStart(MonoBehaviour mb) => _startQueue.Enqueue(mb);

        internal void QueueDestroy(Object obj)
        {
            if (!_destroyQueue.Contains(obj)) _destroyQueue.Add(obj);
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
                Time.Advance(deltaTime);
                SyncContext.Pump();
                DrainStartQueue();
                RunFixedSteps();
                RunPhase(static mb => mb.HasUpdate, static mb => mb.RunUpdate());
                Triggers.RunFrame();
                Coroutines.RunFrame();
                Scheduler.RunFrame();
                RunPhase(static mb => mb.HasLateUpdate, static mb => mb.RunLateUpdate());
                UI.LayoutRebuilder.FlushQueuedRebuilds(); // canvas-update slot: queued UI layout solves after LateUpdate
                Scheduler.RunEndOfFrame();
                FlushDestroyQueue();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        /// <summary>Tick a fixed number of frames at a fixed delta (deterministic harness driving).</summary>
        public void Run(int frames, float deltaTime)
        {
            for (int i = 0; i < frames; i++) Tick(deltaTime);
        }

        void DrainStartQueue()
        {
            // Behaviours enabled during Start callbacks queue for the same drain.
            while (_startQueue.Count > 0)
                _startQueue.Dequeue().RunStart();
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
                    RunPhase(static mb => mb.HasFixedUpdate, static mb => mb.RunFixedUpdate());
                    IntegrateRigidbodies(Time.fixedDeltaTime);
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

        void RunPhase(Func<MonoBehaviour, bool> hasHook, Action<MonoBehaviour> invoke)
        {
            // Snapshot: callbacks may register/unregister behaviours mid-phase.
            int count = _behaviours.Count;
            if (_scratch.Length < count) _scratch = new MonoBehaviour[Math.Max(count, _scratch.Length * 2)];
            _behaviours.CopyTo(_scratch, 0);

            for (int i = 0; i < count; i++)
            {
                var mb = _scratch[i];
                if (mb.destroyedFlag || !mb.isActiveAndEnabled || !mb.started) continue;
                if (hasHook(mb)) invoke(mb);
            }
            Array.Clear(_scratch, 0, count);
        }

        void FlushDestroyQueue()
        {
            if (_destroyQueue.Count == 0) return;
            var toDestroy = _destroyQueue.ToArray();
            _destroyQueue.Clear();
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
