using CosmicShore.App.Systems.UserActions;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.CTA
{
    public class CallToActionSystem : SingletonPersistent<CallToActionSystem>
    {
        [SerializeField] bool testMode;
        List<CallToActionTargetType> ActiveTargets = new();
        Dictionary<CallToActionTargetType, int> ActiveDependencyTargets = new(); // 
        List<CallToAction> ActiveCallsToAction = new();
        Dictionary<CallToActionTargetType, Action> CallToActionActivatedCallbacks = new();
        Dictionary<CallToActionTargetType, Action> CallToActionDismissedCallbacks = new();

        void Start()
        {
            LoadCallsToAction();

            /*
            foreach (var call in ActiveCallsToAction)
            {
                ActiveTargets.Add(call.CallToActionTargetID);

            }*/

            UserActionSystem.Instance.OnUserActionCompleted += ResolveCallsToActionOnUserActionCompleted;
        }

        public void AddCallToAction(CallToAction call)
        {
            // Add to tracking lists
            ActiveCallsToAction.Add(call);
            ActiveTargets.Add(call.CallToActionTargetID);

            foreach (var targetId in call.DependencyTargetIDs)
            {
                // This one don't use TryGetValue because the value in the key-value pair should be incremented.
                if (ActiveDependencyTargets.ContainsKey(targetId))
                {
                    ActiveDependencyTargets[targetId]++;
                }
                else
                {
                    ActiveDependencyTargets.Add(targetId, 1);

                    // Notify anyone interested 
                    // TryGetValue is faster, reference: https://stackoverflow.com/questions/9382681/what-is-more-efficient-dictionary-trygetvalue-or-containskeyitem
                    if(CallToActionActivatedCallbacks.TryGetValue(targetId, out var callback))
                        callback?.Invoke();
                }
            }

            // Notify anyone interested
            if(CallToActionActivatedCallbacks.TryGetValue(call.CallToActionTargetID, out var target))
                target?.Invoke();
        }

        public void RegisterCallToActionTarget(CallToActionTargetType targetId, Action OnCallToActionActive, Action OnCallToActionDismissed)
        {
            // Reset Call to action callbacks if reissued
            if (CallToActionActivatedCallbacks.ContainsKey(targetId))
                CallToActionActivatedCallbacks.Remove(targetId);
            if (CallToActionDismissedCallbacks.ContainsKey(targetId))
                CallToActionDismissedCallbacks.Remove(targetId);

            CallToActionDismissedCallbacks.Add(targetId, OnCallToActionDismissed);
            CallToActionActivatedCallbacks.Add(targetId, OnCallToActionActive);
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

            // TODO: Go to server and get real calls to action
        }

        /// <summary>
        /// 1) Notify all CallToAction targets listening for this action to dismiss their indicators & remove from ActiveTargets list.
        /// 2) Decrement Dependency Counter if necessary and Notify DependencyTarget if counter reaches zero
        /// 3) Remove from ActiveCallsToAction
        /// </summary>
        /// <param name="action"></param>
        void ResolveCallsToActionOnUserActionCompleted(UserAction action)
        {
            Debug.Log($"{nameof(CallToActionSystem)} - {nameof(ResolveCallsToActionOnUserActionCompleted)}: {action}");
            List<CallToAction> matchingCalls = new();
            foreach (var call in ActiveCallsToAction)
            {
                if (call.CompletionUserAction == action.ActionType)
                {
                    matchingCalls.Add(call);

                    Debug.Log($"{nameof(CallToActionSystem)} - " +
                              $"{nameof(ResolveCallsToActionOnUserActionCompleted)}: " +
                              $"CompletionActionMatch - action:{action}, targetId:{call.CallToActionTargetID}");

                    // 1) Notify all CallToAction targets listening for this action to dismiss their indicators & remove from ActiveTargets list.
                    ActiveTargets.Remove(call.CallToActionTargetID);
                    if (!IsCallToActionTargetActive(call.CallToActionTargetID))
                    {
                        CallToActionDismissedCallbacks[call.CallToActionTargetID]?.Invoke();
                        CallToActionDismissedCallbacks.Remove(call.CallToActionTargetID);
                    }

                    // 2) Decrement Dependency Counter if necessary and Notify DependencyTarget if counter reaches zero
                    foreach (var targetId in call.DependencyTargetIDs)
                    {
                        Debug.Log($"{nameof(CallToActionSystem)} - {nameof(ResolveCallsToActionOnUserActionCompleted)}: " +
                                  $"DependencyActionMatch - action:{action}, targetId:{targetId}");

                        ActiveDependencyTargets[targetId]--;
                        if (ActiveDependencyTargets[targetId] == 0)
                        {
                            ActiveDependencyTargets.Remove(targetId);
                            if (!IsCallToActionTargetActive(targetId))
                            {
                                if(CallToActionDismissedCallbacks.TryGetValue(targetId,out var callback))
                                // CallToActionDismissedCallbacks[targetId]?.Invoke();
                                {
                                    callback?.Invoke();
                                }
                                else
                                {
                                    Debug.LogWarningFormat("{0} - {1} {2} is not registered in {3}",
                                        nameof(CallToActionSystem),
                                        nameof(ResolveCallsToActionOnUserActionCompleted),
                                        targetId, nameof(CallToActionDismissedCallbacks));
                                }
                                CallToActionDismissedCallbacks.Remove(targetId);
                            }
                        }
                    }
                }
            }

            // 3) Remove from ActiveCallsToAction
            // (in a separate loop so we're not modifying the list while iterating over it)
            foreach (var call in matchingCalls)
            {
                ActiveCallsToAction.Remove(call);
            }
        }
    }
}