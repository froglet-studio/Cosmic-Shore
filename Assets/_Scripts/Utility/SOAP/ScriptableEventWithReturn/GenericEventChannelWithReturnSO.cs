using System;
using UnityEngine;

namespace CosmicShore.Utilities
{
    public abstract class GenericEventChannelWithReturnSO<T, Y> : ScriptableObject
    {
        public event Func<T, Y> OnEventReturn;

        public Y RaiseEvent(T item)
        {
            if (OnEventReturn != null)
            {
                return OnEventReturn.Invoke(item);
            }
            else
            {
                Debug.LogWarning($"No listeners for event {name} with item {item}");
                return default(Y); // Return default value of Y if no listeners
            }
        }
    }
}