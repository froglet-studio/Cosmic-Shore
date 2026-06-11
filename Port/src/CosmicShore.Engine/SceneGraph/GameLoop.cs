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
    ///   FixedUpdate (accumulator) → Update → task scheduler (Yield/Delay/WaitUntil) →
    ///   LateUpdate → end-of-frame continuations → destroy queue.
    ///
    /// Headless by design: tests call <see cref="Tick"/> directly; the CLI/realtime
    /// hosts call <see cref="Run"/> with wall-clock deltas.
    /// </summary>
    public sealed class GameLoop : IDisposable
    {
        public static GameLoop Current { get; private set; }

        public Scene Scene { get; }
        public GameTaskScheduler Scheduler { get; }
        public GameSynchronizationContext SyncContext { get; }

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

        internal void UnregisterBehaviour(MonoBehaviour mb) => _behaviours.Remove(mb);

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
                Scheduler.RunFrame();
                RunPhase(static mb => mb.HasLateUpdate, static mb => mb.RunLateUpdate());
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
                try { RunPhase(static mb => mb.HasFixedUpdate, static mb => mb.RunFixedUpdate()); }
                finally { Time.ExitFixedPhase(); }
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
            if (Current == this) Current = null;
        }
    }
}
