using System;
using UnityEngine;
using System.Collections;

namespace StarWriter.Core
{
    // TODO: P1 move to enum folder
    public enum ResourceType
    {
        Charge,
        Ammunition,
        Boost,
        Level
    }

    public class ResourceSystem : MonoBehaviour
    {
        [SerializeField] bool usesBoost;
        [SerializeField] bool usesLevels;
        [SerializeField] bool usesAmmo;

        [Tooltip("Max boost level from 0-1")]
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
        [SerializeField]
        [Range(0, 1)]
        float maxAmmo = 1f;

        [Tooltip("Initial boost level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float initialAmmo = 1f;

        float currentAmmo;

        public float MaxAmmo { get { return maxAmmo; } }

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

        void Start()
        {
            StartCoroutine(LateStart());
        }

        IEnumerator LateStart()
        {
            yield return new WaitForSeconds(.2f);

            if (usesBoost) ResetBoost();
            else BoostDisplay?.gameObject.SetActive(false);
            if (usesLevels) ResetLevel();
            else LevelDisplay?.gameObject.SetActive(false);
            if (usesAmmo) ResetAmmo();
            else AmmoDisplay?.gameObject.SetActive(false);
        }

        public void Reset()
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

        public void ChangeBoostAmount(float amount)
        {
            CurrentBoost = Mathf.Clamp(currentBoost + amount, 0, maxBoost);
        }

        public void ChangeLevel(float amount)
        {
            CurrentLevel = Mathf.Clamp(currentLevel + amount, 0, maxLevel);
        }

        public void ChangeAmmoAmount(float amount)
        {
            CurrentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
        }
    }
}