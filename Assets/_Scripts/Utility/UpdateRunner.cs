using System;
using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Utilities
{
    /// <summary>
    /// Some objects might need to be on a slower update loop than the usual MonoBehaviour Update and without precise timing.
    /// e.g. to refresh data from services such as lobby data.
    /// Some might also not want to be coupled to a Unity object at all but still need an Update loop.
    /// </summary>
    public class UpdateRunner : MonoBehaviour
    {
        internal class SubscriberData
        {
            public float Period;
            public float NextCallTime;
            public float LastCallTime;
        }

        private readonly Queue<Action> _pendingHandlersQueue = new Queue<Action>();
        private readonly HashSet<Action<float>> _subscribersHashSet = new HashSet<Action<float>>();
        private readonly Dictionary<Action<float>, SubscriberData> _subscriberDataDictionary = new Dictionary<Action<float>, SubscriberData>();

        /// <summary>
        /// Each frame, iterate through all subscribers.
        /// Any subscriber that have hit their period should be called, though if they take too long they can be removed from the queue.
        /// </summary>
        private void Update()
        {
            while (_pendingHandlersQueue.Count > 0)
            {
                _pendingHandlersQueue.Dequeue()?.Invoke();
            }

            foreach (Action<float> subscriberAction in _subscribersHashSet)
            {
                SubscriberData subscriberData = _subscriberDataDictionary[subscriberAction];

                if (Time.time >= subscriberData.NextCallTime)
                {
                    subscriberAction.Invoke(Time.time - subscriberData.LastCallTime);
                    subscriberData.LastCallTime = Time.time;
                    subscriberData.NextCallTime = Time.time + subscriberData.Period;
                }
            }
        }

        private void OnDestroy()
        {
            _pendingHandlersQueue.Clear();
            _subscribersHashSet.Clear();
            _subscriberDataDictionary.Clear();
        }

        /// <summary>
        /// Subscribe in order to be called approximately every period in seconds. (or every frame, if period <= 0)
        /// Don't assume, that the period is exact. It might be off by a few milliseconds.
        /// Don't assume that the onUpdate will be called in any particular order compared to other subscribers (as we are using a HashSet internally to store all subscribers)
        /// </summary>
        /// <param name="onUpdate">The action need to be subscribed</param>
        /// <param name="updatePeriod">interval in seconds between invoking onUpdate</param>
        public void Subscribe(Action<float> onUpdate, float updatePeriod)
        {
            if (onUpdate == null)
            {
                return; //  throw new ArgumentNullException(nameof(onUpdate));
            }

            if (onUpdate.Target == null)    // Detect a local function that cannot be Unsubscribed since it could go out of scope.
            {
                return; // throw new Exception("Cannot subscribe a local function that cannot be Unsubscribed");
            }

            if (onUpdate.Method.ToString().Contains("<"))   // Detect
            {
                Debug.LogError("Can't subscribe with an anonymous method that cannot be Unsubscribed, by checking for a character" +
                    "that can't exist in a declared method name.");
                return;
            }

            if (!_subscribersHashSet.Contains(onUpdate))
            {
                _pendingHandlersQueue.Enqueue(
                    () =>
                    {
                        if (_subscribersHashSet.Add(onUpdate))
                        {
                            _subscriberDataDictionary.Add(
                                onUpdate, 
                                new SubscriberData { Period = updatePeriod, NextCallTime = 0, LastCallTime = Time.time }
                            );
                        }
                    }
                );
            }
        }


        /// <summary>
        /// Safe to call even if onUpdate was not previously subscribed.
        /// </summary>
        /// <param name="onUpdate"></param>
        public void Unsubscribe(Action<float> onUpdate)
        {
            _pendingHandlersQueue.Enqueue(
                () =>
                {
                    _subscribersHashSet.Remove(onUpdate);
                    _subscriberDataDictionary.Remove(onUpdate);
                }
            );
        }
    }
}

