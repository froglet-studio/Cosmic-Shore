using System.Threading;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base class for a freestyle <b>Toy</b>: a world-space station the LOCAL player's vessel
    /// flies into to activate. Toys have no score and no end condition; they re-arm after each
    /// pass so they can be played with indefinitely.
    ///
    /// Modelled on the existing menu world-triggers (<c>FreestyleSign</c> / <c>ModeSelectTrigger</c>
    /// / <c>ShapeSign</c>): a trigger collider + <c>GetComponentInParent&lt;VesselStatus&gt;</c>
    /// detection. Adds local-user gating, freestyle-only gating, continuity-law bloom-in, and
    /// re-arm on exit so the toy persists instead of being consumed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Toy : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds for the bloom-in (scale-from-zero) on spawn.")]
        float bloomDuration = 1.2f;

        [SerializeField, Tooltip("Seconds after the local vessel leaves before the toy re-arms.")]
        float rearmDelaySeconds = 0.35f;

        protected ToyDefinitionSO Definition { get; private set; }
        protected ToyContext Context { get; private set; }

        Vector3 _targetScale = Vector3.one;
        bool _armed;
        bool _activating;
        bool _blooming;

        /// <summary>One-line label used in logs.</summary>
        public string DisplayName => Definition ? Definition.DisplayName : name;

        /// <summary>
        /// Wire up the toy. Called by the <see cref="ToyDefinitionSO"/> factory right after the
        /// GameObject + collider + visuals are created. Starts the bloom-in.
        /// </summary>
        public void Initialize(ToyDefinitionSO definition, ToyContext context, ToyPlacement placement)
        {
            Definition = definition;
            Context = context;
            _targetScale = transform.localScale;

            if (TryGetComponent(out Collider col)) col.isTrigger = true;

            OnInitialized();
            BloomIn(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Hook for subclasses to do extra setup after <see cref="Initialize"/>.</summary>
        protected virtual void OnInitialized() { }

        /// <summary>Re-run the bloom (a scale-pop) — used to signal an in-place visual change (e.g. a flip).</summary>
        public void Rebloom()
        {
            if (_blooming) return;
            BloomIn(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid BloomIn(CancellationToken ct)
        {
            _blooming = true;
            _armed = false;
            transform.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < bloomDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bloomDuration);
                float eased = t * t * (3f - 2f * t); // smoothstep
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, _targetScale, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            transform.localScale = _targetScale;
            _blooming = false;
            _armed = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_armed || _activating) return;
            if (!TryGetLocalVessel(other, out var vessel)) return;

            // Only respond while the player is actually flying freestyle. In the menu
            // (autopilot) the vessel drifts through the lava lamp; toys are visible but inert.
            if (Context?.IsFreestyleActive != null && !Context.IsFreestyleActive()) return;

            _armed = false;
            _activating = true;
            ActivateDeferred(vessel).Forget();
        }

        /// <summary>
        /// Toy effects run on the next Update tick, NOT inside the physics trigger callback:
        /// they reach deep (domain RPC → vessel re-theme → HUD pool rebuilds, networked vessel
        /// swaps), and a swath of engine APIs (DestroyImmediate among them) is illegal during
        /// physics/animation/render callbacks. One frame of deferral is imperceptible.
        /// </summary>
        async UniTaskVoid ActivateDeferred(IVesselStatus vessel)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            try { OnActivated(vessel); }
            finally { _activating = false; }
        }

        void OnTriggerExit(Collider other)
        {
            if (_armed) return; // already armed — nothing to do
            if (!TryGetLocalVessel(other, out _)) return;
            Rearm(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid Rearm(CancellationToken ct)
        {
            if (rearmDelaySeconds > 0f)
                await UniTask.Delay((int)(rearmDelaySeconds * 1000f), ignoreTimeScale: true, cancellationToken: ct);
            _armed = true;
        }

        /// <summary>
        /// Resolve the colliding object to the LOCAL player's vessel. Never lets a remote (or
        /// AI/autopilot in a party) vessel trip this client's toy.
        /// </summary>
        static bool TryGetLocalVessel(Collider other, out IVesselStatus vessel)
        {
            vessel = null;
            var status = other.GetComponentInParent<VesselStatus>();
            if (!status) return false;
            IVesselStatus iv = status;
            if (!iv.IsLocalUser) return false;
            vessel = iv;
            return true;
        }

        /// <summary>Called once per local-vessel pass while in freestyle. Implement the toy's effect.</summary>
        protected abstract void OnActivated(IVesselStatus localVessel);
    }
}
