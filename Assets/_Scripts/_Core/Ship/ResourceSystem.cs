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
            AmmoDisplay?.gameObject.SetActive(usesAmmo);
            ChargeDisplay?.gameObject.SetActive(usesCharge);
        }

        public void Reset()
        {
            ResetBoost();
            ResetAmmo();
            ResetCharge();
        }

        public void ResetBoost()
        {
            CurrentBoost = initialBoost;
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

        public void ChangeAmmoAmount(float amount)
        {
            CurrentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
            if (CurrentAmmo >= maxAmmo * .75f)
            {
                GetComponent<Ship>().StopClassResourceActions(ResourceEvents.AboveHalfAmmo);
                GetComponent<Ship>().PerformClassResourceActions(ResourceEvents.AboveThreeQuartersAmmo);
            }
            else if (CurrentAmmo >= maxAmmo * .5f)
            {
                GetComponent<Ship>().StopClassResourceActions(ResourceEvents.AboveThreeQuartersAmmo);
                GetComponent<Ship>().PerformClassResourceActions(ResourceEvents.AboveHalfAmmo);
            }
            else
            {
                GetComponent<Ship>().StopClassResourceActions(ResourceEvents.AboveThreeQuartersAmmo);
                GetComponent<Ship>().StopClassResourceActions(ResourceEvents.AboveHalfAmmo);
            }
        }
        
        public void ChangeChargeAmount(float amount)
        {
            CurrentCharge = Mathf.Clamp(currentCharge + amount, 0, maxCharge);
        }

        /*
         * TODO: we may want to move everything below this line to a new component
         */

        public delegate void OnChargeLevelChange();
        public event OnChargeLevelChange onChargeLevelChange;

        [HideInInspector] public ResourceDisplay ChargeLevelDisplay;
        [HideInInspector] public ResourceDisplay MassLevelDisplay;
        [HideInInspector] public ResourceDisplay SpaceLevelDisplay;
        [HideInInspector] public ResourceDisplay TimeLevelDisplay;
        const float MaxChargeLevel = 1;
        const float MaxMassLevel = 1;
        const float MaxSpaceLevel = 1;
        const float MaxTimeLevel = 1;
        public float InitialChargeLevel = 0;
        public float InitialMassLevel = 0;
        public float InitialSpaceLevel = 0;
        public float InitialTimeLevel = 0;
        float chargeLevel;
        float massLevel;
        float spaceLevel;
        float timeLevel;

        public float ChargeLevel
        {
            get => chargeLevel;
            private set
            {
                chargeLevel = value;

                if (ChargeLevelDisplay != null)
                    ChargeLevelDisplay.UpdateDisplay(chargeLevel);

                onChargeLevelChange?.Invoke();
            }
        }
        public float MassLevel
        {
            get => massLevel;
            private set
            {
                massLevel = value;

                if (MassLevelDisplay != null)
                    MassLevelDisplay.UpdateDisplay(massLevel);
            }
        }
        public float SpaceLevel
        {
            get => spaceLevel;
            private set
            {
                spaceLevel = value;

                if (SpaceLevelDisplay != null)
                    SpaceLevelDisplay.UpdateDisplay(spaceLevel);
            }
        }
        public float TimeLevel
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
        public void AdjustChargeLevel(float amount)
        {
            chargeLevel = Math.Clamp(chargeLevel + amount, 0, MaxChargeLevel);
        }
        public void AdjustMassLevel(float amount)
        {
            massLevel = Math.Clamp(massLevel + amount, 0, MaxMassLevel);
        }
        public void AdjustSpaceLevel(float amount)
        {
            spaceLevel = Math.Clamp(spaceLevel + amount, 0, MaxSpaceLevel);
        }
        public void AdjustTimeLevel(float amount)
        {
            timeLevel = Math.Clamp(timeLevel + amount, 0, MaxTimeLevel);
        }

        public void IncrementLevel(Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    IncrementChargeLevel();
                    break;
                case Element.Mass: 
                    IncrementMassLevel();
                    break;
                case Element.Space:
                    IncrementSpaceLevel();
                    break;
                case Element.Time:
                    IncrementTimeLevel();
                    break;
            }
        }

        public void ChangeLevel(Element element, float amount)
        {
            switch (element)
            {
                case Element.Charge:
                    AdjustChargeLevel(amount);
                    break;
                case Element.Mass:
                    AdjustMassLevel(amount);
                    break;
                case Element.Space:
                    AdjustSpaceLevel(amount);
                    break;
                case Element.Time:
                    AdjustTimeLevel(amount);
                    break;
            }
        }
    }
}