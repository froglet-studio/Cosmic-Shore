using System;
using UnityEngine;

namespace CosmicShore.Utilities
{
    public abstract class GenericEventChannelWithReturnSO<T, Y> : ScriptableObject
    {
        public event Func<T, Y> OnEventReturn;

        public Y RaiseEvent(T item)
        {
            return OnEventReturn != null ? OnEventReturn.Invoke(item) : default(Y); // Return default value of Y if no listeners
        }
    }
}
