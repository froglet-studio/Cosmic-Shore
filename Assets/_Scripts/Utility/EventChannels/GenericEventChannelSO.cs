using System;
using UnityEngine;

namespace CosmicShore.Utilities
{
    public abstract class GenericEventChannelSO<T> : ScriptableObject
    {
        public event Action<T> OnEventRaised;

        public void RaiseEvent(T item) => 
            OnEventRaised?.Invoke(item);
    }
}
