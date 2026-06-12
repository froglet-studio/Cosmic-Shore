using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Scripted behaviour with the original engine's lifecycle contract:
    /// Awake (once, on attach to an active object or first activation) →
    /// OnEnable → Start (once, before the first Update after enabling) →
    /// Update/FixedUpdate/LateUpdate while active+enabled → OnDisable → OnDestroy.
    /// Lifecycle methods are declared in subclasses with their original private
    /// zero-arg signatures and discovered reflectively (see <see cref="LifecycleHooks"/>).
    /// </summary>
    public abstract class MonoBehaviour : Behaviour
    {
        internal LifecycleHooks hooks;
        internal long sequence;            // insertion tiebreak for execution ordering
        internal bool awoken;
        internal bool enabledRun;          // OnEnable has run and OnDisable hasn't
        internal bool started;
        internal bool startQueued;

        internal int ExecutionOrder => hooks?.ExecutionOrder ?? 0;

        /// <summary>Called by GameObject.AddComponent after attachment.</summary>
        internal void HandleAttached()
        {
            hooks = LifecycleHooks.For(GetType());
            if (gameObject.activeInHierarchy)
            {
                WakeIfNeeded();
                if (enabled) EnableNow();
            }
        }

        internal void WakeIfNeeded()
        {
            if (awoken) return;
            awoken = true;
            InvokeGuarded(hooks.Awake);
        }

        internal override void OnEnabledChanged(bool value)
        {
            if (gameObject is null || !gameObject.activeInHierarchy || destroyedFlag) return;
            if (value) { WakeIfNeeded(); EnableNow(); }
            else DisableNow();
        }

        /// <summary>GameObject activation state changed for this behaviour's hierarchy.</summary>
        internal void HandleHierarchyActive(bool active)
        {
            if (destroyedFlag) return;
            if (active)
            {
                WakeIfNeeded();
                if (enabled) EnableNow();
            }
            else if (enabledRun)
            {
                DisableNow();
            }
        }

        void EnableNow()
        {
            if (enabledRun) return;
            enabledRun = true;
            InvokeGuarded(hooks.OnEnable);
            var loop = GameLoop.Current;
            loop.RegisterBehaviour(this);
            if (!started && !startQueued)
            {
                startQueued = true;
                loop.QueueStart(this);
            }
        }

        void DisableNow()
        {
            if (!enabledRun) return;
            enabledRun = false;
            InvokeGuarded(hooks.OnDisable);
            GameLoop.Current?.UnregisterBehaviour(this);
        }

        internal void RunStart()
        {
            startQueued = false;
            if (started || destroyedFlag) return;
            if (!isActiveAndEnabled) return; // re-queued on next enable
            started = true;
            InvokeGuarded(hooks.Start);
        }

        internal void RunUpdate() => InvokeGuarded(hooks.Update);
        internal void RunFixedUpdate() => InvokeGuarded(hooks.FixedUpdate);
        internal void RunLateUpdate() => InvokeGuarded(hooks.LateUpdate);

        internal bool HasUpdate => hooks.Update != null;
        internal bool HasFixedUpdate => hooks.FixedUpdate != null;
        internal bool HasLateUpdate => hooks.LateUpdate != null;

        public Coroutine StartCoroutine(System.Collections.IEnumerator routine)
            => GameLoop.Current.Coroutines.Start(this, routine);

        public void StopCoroutine(Coroutine routine)
            => GameLoop.Current?.Coroutines.Stop(this, routine);

        public void StopAllCoroutines()
            => GameLoop.Current?.Coroutines.StopAll(this);

        System.Threading.CancellationTokenSource _destroyCts;

        /// <summary>
        /// Cancelled when this behaviour is destroyed (same contract as the original
        /// engine's MonoBehaviour.destroyCancellationToken, added in its 2022.3 line).
        /// </summary>
        public System.Threading.CancellationToken destroyCancellationToken
            => (_destroyCts ??= new System.Threading.CancellationTokenSource()).Token;

        /// <summary>
        /// UniTask-era spelling of <see cref="destroyCancellationToken"/> (originally a
        /// `Cysharp.Threading.Tasks` extension method) so ported call sites stay verbatim.
        /// </summary>
        public System.Threading.CancellationToken GetCancellationTokenOnDestroy()
            => destroyCancellationToken;

        internal override void DestroyComponentNow()
        {
            if (destroyedFlag) return;
            if (enabledRun) DisableNow();
            if (awoken) InvokeGuarded(hooks.OnDestroy);
            if (_destroyCts is { IsCancellationRequested: false })
            {
                try { _destroyCts.Cancel(); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            base.DestroyComponentNow();
        }

        /// <summary>
        /// Exceptions in one behaviour's callback must not break the frame for everything
        /// else — same isolation contract as the original engine.
        /// </summary>
        void InvokeGuarded(Action<MonoBehaviour> hook)
        {
            if (hook is null) return;
            try { hook(this); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
