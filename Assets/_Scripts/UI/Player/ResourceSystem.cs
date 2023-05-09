using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

        [SerializeField] List<ResourceType> Resources;
        [SerializeField] bool usesBoost;
        [SerializeField] bool usesLevels;
        [SerializeField] bool usesAmmo;
        [SerializeField] bool usesCharge;

        [SerializeField] bool gainsAmmo = false;
        [SerializeField] float ammoGainRate = .01f;
        [SerializeField] float elevatedAmmoGainRate = .03f;


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

        [Tooltip("Max ammo level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float maxCharge = 1f;

        [Tooltip("Initial boost level from 0-1")]
        [SerializeField]
        [Range(0, 1)]
        float initialCharge = 1f;

        float currentCharge;

        public float MaxCharge { get { return maxCharge; } }

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

        public ChargeDisplay BoostDisplay;
        public ChargeDisplay LevelDisplay;
        public ChargeDisplay AmmoDisplay;
        public ChargeDisplay ChargeDisplay;

        ShipData shipData;

        void Start()
        {
            StartCoroutine(LateStart());
            shipData = GetComponent<ShipData>();
        }

        private void Update()
        {
            if (shipData.ElevatedAmmoGain) ChangeAmmoAmount(Time.deltaTime * elevatedAmmoGainRate);
            else if (gainsAmmo) ChangeAmmoAmount(Time.deltaTime * ammoGainRate);
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
            if (usesCharge) ResetAmmo();
            else ChargeDisplay?.gameObject.SetActive(false);
        }

        public void Reset()
        {
            ResetBoost();
            ResetLevel();
            ResetAmmo();
            ResetCharge();
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

         public void ResetCharge()
        {
            CurrentCharge = initialCharge;
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
        
        public void ChangeChargeAmount(float amount)
        {
            CurrentCharge = Mathf.Clamp(currentCharge + amount, 0, maxCharge);
        }
    }
}