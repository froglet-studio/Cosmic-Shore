using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace StarWriter.Core
{
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

        [HideInInspector] public ResourceDisplay BoostDisplay;
        [HideInInspector] public ResourceDisplay LevelDisplay;
        [HideInInspector] public ResourceDisplay AmmoDisplay;
        [HideInInspector] public ResourceDisplay ChargeDisplay;
        [HideInInspector] public ResourceDisplay MassDisplay;
        [HideInInspector] public ResourceDisplay SpaceDisplay;
        [HideInInspector] public ResourceDisplay TimeDisplay;

        public static readonly float OneFuelUnit = 1 / 10f;
        ShipStatus shipData;

        void Start()
        {
            shipData = GetComponent<ShipStatus>();

            StartCoroutine(LateStart());
        }

        void Update()
        {
            if (shipData.ElevatedAmmoGain) ChangeAmmoAmount(Time.deltaTime * elevatedAmmoGainRate);
            else if (gainsAmmo) ChangeAmmoAmount(Time.deltaTime * ammoGainRate);
        }

        IEnumerator LateStart()
        {
            yield return new WaitForSeconds(.2f);

            Reset();
            
            BoostDisplay?.gameObject.SetActive(usesBoost);
            LevelDisplay?.gameObject.SetActive(usesLevels);
            AmmoDisplay?.gameObject.SetActive(usesAmmo);
            ChargeDisplay?.gameObject.SetActive(usesCharge);
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

        /*
        // TODO: P1 use these or lose these
        public void ResetMass()
        {
            CurrentCharge = initialCharge;
        }
        public void ResetSpace()
        {
            CurrentCharge = initialCharge;
        }
        public void ResetTime() // When we implement this for real, this name is pretty vague...
        {
            CurrentCharge = initialCharge;
        }
        */

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


        /*
         * TODO: we may want to move everything below this line to a new component
         */
        [HideInInspector] public ResourceDisplay ChargeLevelDisplay;
        [HideInInspector] public ResourceDisplay MassLevelDisplay;
        [HideInInspector] public ResourceDisplay SpaceLevelDisplay;
        [HideInInspector] public ResourceDisplay TimeLevelDisplay;
        const int MaxChargeLevel = 10;
        const int MaxMassLevel = 10;
        const int MaxSpaceLevel = 10;
        const int MaxTimeLevel = 10;
        public int InitialChargeLevel = 0;
        public int InitialMassLevel = 0;
        public int InitialSpaceLevel = 0;
        public int InitialTimeLevel = 0;
        int chargeLevel;
        int massLevel;
        int spaceLevel;
        int timeLevel;

        public int ChargeLevel
        {
            get => chargeLevel;
            private set
            {
                chargeLevel = value;

                if (ChargeLevelDisplay != null)
                    ChargeLevelDisplay.UpdateDisplay(chargeLevel);
            }
        }
        public int MassLevel
        {
            get => massLevel;
            private set
            {
                massLevel = value;

                if (MassLevelDisplay != null)
                    MassLevelDisplay.UpdateDisplay(massLevel);
            }
        }
        public int SpaceLevel
        {
            get => spaceLevel;
            private set
            {
                spaceLevel = value;

                if (SpaceLevelDisplay != null)
                    SpaceLevelDisplay.UpdateDisplay(spaceLevel);
            }
        }
        public int TimeLevel
        {
            get => timeLevel;
            private set
            {
                timeLevel = value;

                if (TimeLevelDisplay != null)
                    TimeLevelDisplay.UpdateDisplay(timeLevel);
            }
        }
        public void InitializeElementLevels()
        {
            chargeLevel = InitialChargeLevel;
            massLevel = InitialMassLevel;
            spaceLevel = InitialSpaceLevel;
            timeLevel = InitialTimeLevel;
        }
        public void IncrementChargeLevel()
        {
            chargeLevel = Math.Clamp(chargeLevel + 1, 0, MaxChargeLevel);
        }
        public void IncrementMassLevel()
        {
            massLevel = Math.Clamp(massLevel + 1, 0, MaxMassLevel);
        }
        public void IncrementSpaceLevel()
        {
            spaceLevel = Math.Clamp(spaceLevel + 1, 0, MaxSpaceLevel);
        }
        public void IncrementTimeLevel()
        {
            timeLevel = Math.Clamp(timeLevel + 1, 0, MaxTimeLevel);
        }
    }
}