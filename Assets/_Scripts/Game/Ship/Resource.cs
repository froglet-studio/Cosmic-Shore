using System;
using UnityEngine;

namespace CosmicShore.Core
{
    [Serializable]
    public class Resource
    {
        public delegate void ResourceUpdateDelegate(float currentResource);
        public event ResourceUpdateDelegate OnResourceChange;

        [SerializeField] public string Name;

        [HideInInspector] public float initialResourceGainRate;
        [SerializeField] public float resourceGainRate;

        [SerializeField, Range(0, 1)] float maxAmount = 1f;
        public float MaxAmount => maxAmount;

        [SerializeField, Range(0, 1)] float initialAmount = 1f;
        public float InitialAmount => initialAmount;

        float currentAmount;
        public float CurrentAmount
        {
            get => currentAmount;
            set
            {
                currentAmount = Mathf.Clamp01(value);
                OnResourceChange?.Invoke(currentAmount);  
            }
        }
    }
}