using CosmicShore.Utility;
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    public class CallToActionSystem : SingletonPersistent<CallToActionSystem>
    {
        [SerializeField] bool testMode;
        List<CallToActionTargetType> ActiveTargets = new();
        Dictionary<CallToActionTargetType, int> ActiveDependencyTargets = new();
        List<CallToAction> ActiveCallsToAction = new();
        Dictionary<CallToActionTargetType, Action> CallToActionActivatedCallbacks = new();
        Dictionary<CallToActionTargetType, Action> CallToActionDismissedCallbacks = new();

        void Start()
        {
            LoadCallsToAction();

            if (UserActionSystem.Instance != null)
                UserActionSystem.Instance.OnUserActionCompleted += ResolveCallsToActionOnUserActionCompleted;
            else
                Debug.LogWarning($"{nameof(CallToActionSystem)}: UserActionSystem.Instance is null - skipping event subscription.");
        }

        /// <summary>
        /// Lights a breadcrumb: marks its target active, raises the active dependency path, and
        /// notifies any registered <see cref="CallToActionTarget"/> so its indicator blooms (C3).
        /// </summary>
        public void AddCallToAction(CallToAction call)
        {
            if (call == null) return;

            // Only bloom the main target if it wasn't already lit by another call/dependency —
            // re-firing the activate callback would replay the bloom on an already-visible indicator.
            bool wasActive = IsCallToActionTargetActive(call.CallToActionTargetID);

            ActiveCallsToAction.Add(call);
            ActiveTargets.Add(call.CallToActionTargetID);

            foreach (var targetId in call.DependencyTargetIDs)
                IncrementDependency(targetId);

            if (!wasActive)
                RaiseActivated(call.CallToActionTargetID);
        }

        /// <summary>
        /// Retracts a previously-added breadcrumb (the progression-driven frontier swap path).
        /// Fades the target's indicator when nothing else keeps it active, and unwinds the
        /// dependency path. No-op if the call is not currently active. Mirrors the cleanup the
        /// user-action resolver performs, so a frontier can be re-lit later.
        /// </summary>
        public void RemoveCallToAction(CallToAction call)
        {
            if (call == null || !ActiveCallsToAction.Remove(call)) return;

            ActiveTargets.Remove(call.CallToActionTargetID);
            if (!IsCallToActionTargetActive(call.CallToActionTargetID))
                RaiseDismissed(call.CallToActionTargetID);

            foreach (var targetId in call.DependencyTargetIDs)
                DecrementDependency(targetId);
        }

        public void RegisterCallToActionTarget(CallToActionTargetType targetId, Action OnCallToActionActive, Action OnCallToActionDismissed)
        {
            // Reset Call to action callbacks if reissued (e.g. the target's GameObject respawns on
            // a scene reload). Both callbacks live for the target's registered lifetime — they are
            // NOT discarded on dismissal, so a target can be re-lit and re-dismissed repeatedly.
            CallToActionDismissedCallbacks[targetId] = OnCallToActionDismissed;
            CallToActionActivatedCallbacks[targetId] = OnCallToActionActive;
        }

        public bool IsCallToActionTargetActive(CallToActionTargetType targetId)
        {
            return ActiveTargets.Contains(targetId) || ActiveDependencyTargets.ContainsKey(targetId);
        }

        void LoadCallsToAction()
        {
            if (testMode)
            {
                // Add in a test notification for the arcade menu
                //AddCallToAction(new CallToAction(CallToActionTargetType.HangarMenu, UserActionType.ViewHangarMenu));
                //AddCallToAction(new CallToAction(CallToActionTargetType.ArcadeLoadoutMenu, UserActionType.ViewArcadeLoadoutMenu, new List<CallToActionTargetType>() { CallToActionTargetType.ArcadeMenu }));
            }

            // Breadcrumbs are now driven by GameModeProgressionService (the active-frontier wire),
            // not loaded from a server here.
        }

        /// <summary>
        /// Dismisses every breadcrumb whose CompletionUserAction matches the just-completed action.
        /// </summary>
        void ResolveCallsToActionOnUserActionCompleted(UserAction action)
        {
            CSDebug.Log($"{nameof(CallToActionSystem)} - {nameof(ResolveCallsToActionOnUserActionCompleted)}: {action}");

            List<CallToAction> matchingCalls = new();
            foreach (var call in ActiveCallsToAction)
                if (call.CompletionUserAction == action.ActionType)
                    matchingCalls.Add(call);

            // Separate pass so we are not mutating ActiveCallsToAction while iterating it.
            foreach (var call in matchingCalls)
                RemoveCallToAction(call);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        void IncrementDependency(CallToActionTargetType targetId)
        {
            if (ActiveDependencyTargets.TryGetValue(targetId, out var count))
            {
                ActiveDependencyTargets[targetId] = count + 1;
            }
            else
            {
                ActiveDependencyTargets[targetId] = 1;
                RaiseActivated(targetId);
            }
        }

        void DecrementDependency(CallToActionTargetType targetId)
        {
            if (!ActiveDependencyTargets.TryGetValue(targetId, out var count)) return;

            count--;
            if (count > 0)
            {
                ActiveDependencyTargets[targetId] = count;
                return;
            }

            ActiveDependencyTargets.Remove(targetId);
            if (!IsCallToActionTargetActive(targetId))
                RaiseDismissed(targetId);
        }

        void RaiseActivated(CallToActionTargetType targetId)
        {
            if (CallToActionActivatedCallbacks.TryGetValue(targetId, out var callback))
                callback?.Invoke();
        }

        void RaiseDismissed(CallToActionTargetType targetId)
        {
            if (CallToActionDismissedCallbacks.TryGetValue(targetId, out var callback))
                callback?.Invoke();
        }
    }
}
