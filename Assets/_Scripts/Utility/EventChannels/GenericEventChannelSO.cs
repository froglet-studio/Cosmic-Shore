using System;
using UnityEngine;

namespace CosmicShore.Utilities
{
    public abstract class GenericEventChannelSO<T> : ScriptableObject
    {
        public event Action<T> OnEventRaised;

        public void RaiseEvent(T item)
        {
            if (OnEventRaised != null)
            {
                OnEventRaised.Invoke(item);
            }
            else
            {
                Debug.LogWarning($"No listeners for event {name} with item {item}");
            }
        }
    }
}
