using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;

namespace CosmicShore.Gameplay
{
    public abstract class ImpactorBase : MonoBehaviour, IImpactor
    {
        [Inject] Container _diContainer;
        public Container DIContainer => _diContainer;

        protected virtual bool isInitialized => true;

        public Transform Transform => transform;
        public abstract Domains OwnDomain { get; }
        
        protected abstract void AcceptImpactee(IImpactor impactee);

        protected bool DoesEffectExist(ImpactEffectSO[] effects) => effects is { Length: > 0 };

        // Empty serialized effect slots already reported, keyed by (container, field, index) so
        // one authoring hole logs once rather than once per contact.
        static readonly HashSet<long> s_reportedEmptyEffectSlots = new();

        // Warn-once means once per PLAY SESSION, and instance IDs get reused across sessions
        // with domain reload disabled — clear all three report keys per play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarnOnceKeys()
        {
            s_reportedEmptyEffectSlots.Clear();
            s_reportedThrowingEffects.Clear();
            s_reportedMissingContainers.Clear();
        }

        // Impactors whose whole effect container is unassigned, keyed by impactor instance so
        // an unwired prefab names itself once rather than once per physics contact.
        static readonly HashSet<int> s_reportedMissingContainers = new();

        /// <summary>
        /// True when the impactor's serialized effect CONTAINER is unassigned — the level above
        /// <see cref="IsEffectSlotEmpty"/>, same doctrine. Dispatch sites deref the container
        /// directly, so an unwired prefab used to surface as a bare
        /// <see cref="NullReferenceException"/> inside a PhysX callback, once per contact for as
        /// long as anything touched the collider — thousands of console exceptions that read as
        /// the editor freezing (the 2026-08-21 'Run managed callbacks' hang reports carried
        /// exactly this storm from the legacy Components/Skimmer.prefab, whose serialized data
        /// predates the container refactor on six vessels). Name the hole ONCE with the full
        /// hierarchy path and skip dispatch: the missing container cannot be invented, but the
        /// editor must stay alive to say so.
        /// </summary>
        protected bool IsEffectContainerMissing(UnityEngine.Object container, string containerField)
        {
            if (container) return false;

            if (s_reportedMissingContainers.Add(GetInstanceID()))
            {
                var t = transform;
                string path = t.name;
                while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
                CSDebug.LogError(
                    $"[{GetType().Name}] '{path}'.{containerField} is not assigned — this impactor " +
                    "dispatches no effects until it is wired (reported once; contacts are skipped, " +
                    "not thrown). Run FrogletTools > Vessels > Audit Vessel Skimmers to see every " +
                    "unwired skimmer.", this);
            }
            return true;
        }

        /// <summary>
        /// True when a serialized effect slot holds nothing runnable — never assigned in the
        /// inspector, or its asset failed to load (missing script, unresolved GUID).
        ///
        /// Dispatch sites deref the slot directly, so a hole used to surface as a bare
        /// <see cref="NullReferenceException"/> at the call site, naming neither the container
        /// nor the index — and because <see cref="PrismShellContactManager"/> dispatches from
        /// Update rather than a PhysX callback, once per frame for as long as the contact
        /// lasted, which also aborted the rest of that frame's shell contacts. Name the hole
        /// ONCE and let the sibling effects in the list still run: the missing effect cannot be
        /// invented, but everything around it can still do its job.
        /// </summary>
        protected bool IsEffectSlotEmpty(ImpactEffectSO effect, UnityEngine.Object container, string field, int index)
        {
            if (effect) return false;

            int containerId = container ? container.GetInstanceID() : 0;
            long key = ((long)containerId << 32) ^ (uint)(field.GetHashCode() * 397 ^ index);
            if (s_reportedEmptyEffectSlots.Add(key))
            {
                CSDebug.LogError(
                    $"[{GetType().Name}] '{(container ? container.name : "<missing container>")}'." +
                    $"{field}[{index}] is empty — that slot dispatches nothing and the rest of the " +
                    "list still runs. Assign the effect asset, or remove the slot.", container);
            }
            return true;
        }

        // Effect instances whose Execute already threw, keyed by (effect, impactor type) so a
        // broken effect names itself once rather than once per contact.
        static readonly HashSet<long> s_reportedThrowingEffects = new();

        /// <summary>
        /// Runs one effect with its siblings' survival guaranteed: an exception inside
        /// <c>Execute</c> is reported ONCE per (effect, impactor type) with its stack - loud,
        /// named, actionable - and the rest of the effect list still runs.
        ///
        /// The companion of <see cref="IsEffectSlotEmpty"/>, and the same doctrine: this is not
        /// a fail-soft blanket, it is fail-loud with the offender's address. Before it, one
        /// throwing effect (an unwired SOAP event on a prism variant, a listener assuming
        /// in-game state from the menu) silently killed every LATER effect in the list for that
        /// contact - on the Urchin spike container that meant an aborted steal also swallowed
        /// the chain volley, and the whole weapon read as dead with nothing in the console
        /// naming why.
        /// </summary>
        protected void RunEffectIsolated(System.Action execute, UnityEngine.Object effect)
        {
            try
            {
                execute();
            }
            catch (System.Exception ex)
            {
                int effectId = effect ? effect.GetInstanceID() : 0;
                long key = ((long)effectId << 32) ^ (uint)GetType().Name.GetHashCode();
                if (s_reportedThrowingEffects.Add(key))
                {
                    CSDebug.LogError(
                        $"[{GetType().Name}] Effect '{(effect ? effect.name : "<null>")}' threw during " +
                        $"Execute - reported once; the rest of this contact's effect list still runs.\n{ex}",
                        effect);
                }
            }
        }

        // Per-concrete-type profiler marker so an impact storm shows up in captures as
        // e.g. 'SkimmerImpactor.AcceptImpactee' with real timings instead of vanishing
        // into Physics.SendEvents self-time. Lazily created (one string per component
        // lifetime) — no Awake added here, since most subclasses declare their own.
        ProfilerMarker _acceptMarker;
        bool _acceptMarkerInit;

        /// <summary>
        /// True while <see cref="AcceptImpactee"/> is running for a shell contact
        /// dispatched by <see cref="PrismShellContactManager"/> (the analytic
        /// shielded-prism tier) rather than a PhysX trigger. Lets the shielded-prism
        /// suppression check in subclasses pass shell dispatches through while
        /// short-circuiting the box-trigger path for the same prism.
        /// </summary>
        protected bool IsShellDispatch { get; private set; }

        /// <summary>
        /// Exposes the trigger gate to <see cref="PrismShellContactManager"/> so the
        /// shell tier honors the exact same initialization gating as OnTriggerEnter
        /// (e.g. a skimmer goes inert when its Player deactivates mid-scene).
        /// </summary>
        internal bool IsInitializedForImpact => isInitialized;

        /// <summary>
        /// Shell-tier entry point: dispatches an impactee through the same
        /// isInitialized gate, profiler marker, and AcceptImpactee chain as the
        /// trigger path, with <see cref="IsShellDispatch"/> raised so subclass
        /// suppression checks let it through.
        /// </summary>
        internal void AcceptImpacteeFromShellContact(IImpactor impactee)
        {
            if (!isInitialized)
                return;

            EnsureAcceptMarker();
            using (_acceptMarker.Auto())
            {
                IsShellDispatch = true;
                try
                {
                    AcceptImpactee(impactee);
                }
                finally
                {
                    IsShellDispatch = false;
                }
            }
        }

        /// <summary>
        /// Shell-tier exit notification — the analogue of OnTriggerExit for a shell
        /// contact ending. Base does nothing; SkimmerImpactor overrides to mirror its
        /// trigger-exit skim bookkeeping.
        /// </summary>
        internal virtual void NotifyShellContactExit(PrismImpactor prismImpactor) { }

        /// <summary>
        /// True while <see cref="AcceptImpactee"/> is running for a SWEPT contact — one
        /// found by querying the segment an object crossed this frame rather than by a
        /// PhysX trigger overlap at its landing point. Lets a subclass suppress the
        /// trigger path for a contact class the sweep has taken ownership of, without
        /// suppressing the sweep's own dispatch of it.
        /// </summary>
        protected bool IsSweepDispatch { get; private set; }

        /// <summary>
        /// Swept-tier entry point, the exact analogue of
        /// <see cref="AcceptImpacteeFromShellContact"/>: same isInitialized gate, same
        /// profiler marker, same AcceptImpactee chain, with <see cref="IsSweepDispatch"/>
        /// raised. A discrete trigger moved by transform writes only ever tests the points
        /// it lands on; this is how the path BETWEEN them gets to land impacts too.
        /// </summary>
        internal void AcceptImpacteeFromSweep(IImpactor impactee)
        {
            if (!isInitialized)
                return;

            EnsureAcceptMarker();
            using (_acceptMarker.Auto())
            {
                IsSweepDispatch = true;
                try
                {
                    AcceptImpactee(impactee);
                }
                finally
                {
                    IsSweepDispatch = false;
                }
            }
        }

        void EnsureAcceptMarker()
        {
            if (_acceptMarkerInit)
                return;
            _acceptMarkerInit = true;
            _acceptMarker = new ProfilerMarker(GetType().Name + ".AcceptImpactee");
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!isInitialized)
                return;

            // Use the concrete ImpactCollider (the sole IImpactCollider implementer)
            // rather than TryGetComponent<IImpactCollider>. An interface-typed
            // GetComponent forces Unity to iterate and type-check every component on
            // the GameObject; this runs per prism-enter across dense trails and was
            // ~26% self-time inside Physics.SendEvents. The concrete type uses Unity's
            // native typed-lookup fast path.
            if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
                return;

            EnsureAcceptMarker();
            using (_acceptMarker.Auto())
                AcceptImpactee(impacteeCollider.Impactor);
        }
    }
}