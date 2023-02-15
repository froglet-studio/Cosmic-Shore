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
        [Tooltip("Max boost level from 0-1")]
        [FormerlySerializedAs("maxBoost")]
        [SerializeField]
        [Range(0, 1)]
        float maxBoost = 1f;

        [Tooltip("Initial boost level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float initialBoost = 1f;

        float currentBoost;

        public float CurrentBoost
        {
            get => currentBoost;
            private set
            {
                currentBoost = value;

                if (BoostDisplay != null)
                    BoostDisplay.UpdateDisplay(currentBoost);
            }
        }

        [Tooltip("Max level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float maxLevel = 1f;

        [Tooltip("Initial level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float initialLevel = 0f;

        float currentLevel;

        public float MaxLevel {  get { return maxLevel; } }

        public float CurrentLevel
        {
            get => currentLevel;
            private set
            {
                currentLevel = value;

                if (LevelDisplay != null)
                    LevelDisplay.UpdateDisplay(currentLevel);
            }
        }

        [Tooltip("Max ammo level from 0-1")]
        [FormerlySerializedAs("maxAmmo")]
        [SerializeField]
        [Range(0, 1)]
        float maxAmmo = 1f;

        [Tooltip("Initial boost level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float initialAmmo = 1f;

        float currentAmmo;

        public float CurrentAmmo
        {
            get => currentAmmo;
            private set
            {
                currentAmmo = value;

                if (AmmoDisplay != null)
                    AmmoDisplay.UpdateDisplay(currentAmmo);
            }
        }

        public ChargeDisplay BoostDisplay;
        public ChargeDisplay LevelDisplay;
        public ChargeDisplay AmmoDisplay;

        // WIP
        // TODO: we can't use events like this anymore because each ship has a resource system - we need to communicate with the ship or resource system directly instead
        void OnEnable()
        {
            //Skimmer.OnSkim += ChangeBoostAmount;
            ShipController.OnBoost += ChangeBoostAmount;
        }

        void OnDisable()
        {
            //Skimmer.OnSkim -= ChangeBoostAmount;
            ShipController.OnBoost -= ChangeBoostAmount;
        }

        void Start()
        {
            ResetBoost();
            ResetLevel();
            ResetAmmo();
        }

        public void ResetBoost()
        {
            CurrentBoost = initialBoost;
        }
        public void ResetLevel()
        {
            CurrentLevel = initialLevel;
        }
        public void ResetAmmo()
        {
            CurrentAmmo = initialAmmo;
        }

        public void ChangeBoostAmount(string uuid, float amount)
        {
            CurrentBoost = Mathf.Clamp(currentBoost + amount, 0, maxBoost);
        }

        public void ChangeLevel(string uuid, float amount)
        {
            CurrentLevel = Mathf.Clamp(currentLevel + amount, 0, maxLevel);
        }

        public void ChangeAmmoAmount(string uuid, float amount)
        {
            CurrentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
        }
    }
}