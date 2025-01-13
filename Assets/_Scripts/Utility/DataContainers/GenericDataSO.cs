using System;
using UnityEngine;


namespace CosmicShore.Utilities
{
    /// <summary>
    /// This is a generic data scriptable object that can be used to store any type of data.
    /// It has an event that can be subscribed to, to get notified when the value changes.
    /// </summary>
    /// <typeparam name="T">Type of the stored data</typeparam>
    public abstract class GenericDataSO<T> : ScriptableObject
    {
        private T m_Value;

        public event Action OnValueChanged;

        public T Value
        {
            get => m_Value;
            set
            {
                m_Value = value;
                OnValueChanged?.Invoke();
            }
        }

        // Create implicit conversion from IntDataSO to int
        public static implicit operator T(GenericDataSO<T> data) => data.Value;
    }
}