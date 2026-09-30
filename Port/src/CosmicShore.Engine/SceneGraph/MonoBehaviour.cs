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
            // Original contract: deactivating the object stops its coroutines (disabling the
            // component alone does not). The runner relies on this instead of re-testing every
            // coroutine's owner every frame.
            if (!active) GameLoop.Current?.Coroutines.StopAll(this);
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

        /// <summary>Diagnostics: CS_PORT_TRACE_DISABLE=&lt;type-name fragment&gt; prints a stack for matching OnDisable calls.</summary>
        static readonly string s_traceDisable = Environment.GetEnvironmentVariable("CS_PORT_TRACE_DISABLE");

        void DisableNow()
        {
            if (!enabledRun) return;
            enabledRun = false;
            if (s_traceDisable != null && GetType().Name.Contains(s_traceDisable, StringComparison.Ordinal))
                Console.WriteLine($"[trace] OnDisable {GetType().Name} on '{name}' (activeInHierarchy={gameObject.activeInHierarchy}, destroyed={destroyedFlag})\n{Environment.StackTrace}");
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

        // Trigger messages (TriggerPass): delivered regardless of the per-behaviour
        // `enabled` flag — the original engine's physics messages bypass it — with the
        // same exception isolation as the frame phases.
        internal void RunTriggerEnter(Collider other)
        {
            if (hooks?.TriggerEnter is not { } hook) return;
            try { hook(this, other); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        internal void RunTriggerExit(Collider other)
        {
            if (hooks?.TriggerExit is not { } hook) return;
            try { hook(this, other); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        internal bool HasUpdate => hooks.Update != null;
        internal bool HasFixedUpdate => hooks.FixedUpdate != null;
        internal bool HasLateUpdate => hooks.LateUpdate != null;

        public Coroutine StartCoroutine(System.Collections.IEnumerator routine)
            => GameLoop.Current.Coroutines.Start(this, routine);

        public void StopCoroutine(Coroutine routine)
            => GameLoop.Current?.Coroutines.Stop(this, routine);

        public void StopCoroutine(System.Collections.IEnumerator routine)
            => GameLoop.Current?.Coroutines.Stop(this, routine);

        public void StopAllCoroutines()
            => GameLoop.Current?.Coroutines.StopAll(this);

        /// <summary>
        /// Schedules a zero-argument method by name after <paramref name="time"/> seconds
        /// of scaled time (original contract: UnityEngine.MonoBehaviour.Invoke — any
        /// visibility, resolved by reflection; rides the coroutine runner, so it stops
        /// with the behaviour like the original's destroy semantics).
        /// </summary>
        public void Invoke(string methodName, float time)
        {
            var method = GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
            {
                Debug.LogError($"Invoke: no zero-argument method '{methodName}' on {GetType().Name}.", this);
                return;
            }
            var routine = InvokeRoutine(method, time);
            (_invokes ??= new()).Add((methodName, routine));
            StartCoroutine(routine);
        }

        System.Collections.IEnumerator InvokeRoutine(System.Reflection.MethodInfo method, float time)
        {
            yield return new WaitForSeconds(time);
            method.Invoke(this, null);
            RemoveInvoke(method.Name, null);
        }

        System.Collections.Generic.List<(string name, System.Collections.IEnumerator routine)> _invokes;

        void RemoveInvoke(string name, System.Collections.IEnumerator routine)
        {
            if (_invokes == null) return;
            for (int i = _invokes.Count - 1; i >= 0; i--)
                if (_invokes[i].name == name && (routine == null || _invokes[i].routine == routine)) { _invokes.RemoveAt(i); if (routine != null) return; }
        }

        /// <summary>Calls a zero-argument method after <paramref name="time"/> and then every <paramref name="repeatRate"/> seconds (original contract).</summary>
        public void InvokeRepeating(string methodName, float time, float repeatRate)
        {
            var method = GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
            {
                Debug.LogError($"InvokeRepeating: no zero-argument method '{methodName}' on {GetType().Name}.", this);
                return;
            }
            var routine = RepeatRoutine(method, time, repeatRate);
            (_invokes ??= new()).Add((methodName, routine));
            StartCoroutine(routine);
        }

        System.Collections.IEnumerator RepeatRoutine(System.Reflection.MethodInfo method, float time, float repeatRate)
        {
            yield return new WaitForSeconds(time);
            while (true)
            {
                method.Invoke(this, null);
                yield return repeatRate > 0f ? new WaitForSeconds(repeatRate) : null;
            }
        }

        /// <summary>Cancels every pending Invoke / InvokeRepeating on this behaviour (or only <paramref name="methodName"/>).</summary>
        public void CancelInvoke(string methodName = null)
        {
            if (_invokes == null) return;
            for (int i = _invokes.Count - 1; i >= 0; i--)
            {
                if (methodName != null && _invokes[i].name != methodName) continue;
                if (_invokes[i].routine != null) StopCoroutine(_invokes[i].routine);
                _invokes.RemoveAt(i);
            }
        }

        public bool IsInvoking(string methodName = null)
        {
            if (_invokes == null) return false;
            foreach (var i in _invokes) if (methodName == null || i.name == methodName) return true;
            return false;
        }

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
            GameLoop.Current?.Coroutines.StopAll(this);
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
