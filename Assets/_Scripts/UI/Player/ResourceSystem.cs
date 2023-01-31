using System;
using UnityEngine;
using StarWriter.Core.Input;
using UnityEngine.Serialization;

namespace StarWriter.Core
{
    public enum ResourceType
    {
        Charge,
        Ammunition,
    }

    public class ResourceSystem : MonoBehaviour
    {
        [Tooltip("Max charge level from 0-1")]
        [FormerlySerializedAs("maxFuel")]
        [SerializeField]
        [Range(0, 1)]
        static float maxCharge = 1f;

        [Tooltip("Initial charge level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        static float initialCharge = 1f;

        float currentCharge;

        public float CurrentCharge
        {
            get => currentCharge;
            private set
            {
                currentCharge = value;

                if (ChargeDisplay != null)
                    ChargeDisplay.UpdateDisplay(currentCharge);
            }
        }

        public ChargeDisplay ChargeDisplay;

        // WIP
        // TODO: we can't use events like this anymore because each ship has a resource system - we need to communicate with the ship or resource system directly instead
        void OnEnable()
        {
            Skimmer.OnSkim += ChangeChargeAmount;
            InputController.OnBoost += ChangeChargeAmount;
        }

        void OnDisable()
        {
            Skimmer.OnSkim -= ChangeChargeAmount;
            InputController.OnBoost -= ChangeChargeAmount;
        }

        void Start()
        {
            ResetCharge();
        }

        public void ResetCharge()
        {
            CurrentCharge = initialCharge;
        }

        public void ChangeChargeAmount(string uuid, float amount)
        {
            CurrentCharge = Mathf.Clamp(currentCharge + amount, 0, maxCharge);
        }
    }
}