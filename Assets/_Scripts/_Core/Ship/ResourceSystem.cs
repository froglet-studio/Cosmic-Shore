using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

namespace StarWriter.Core
{
    public class ResourceSystem : ElementalShipComponent
    {
        [Header("Boost")]
        [SerializeField] bool usesBoost;
        [SerializeField] [Range(0, 1)] float initialBoost = 1f;
        [SerializeField] [Range(0, 1)] float maxBoost = 1f;
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

        [Header("Ammo")]
        [SerializeField] bool usesAmmo;
        [SerializeField] bool gainsAmmo;
        [FormerlySerializedAs("AmmoGainRate2")]
        [SerializeField] ElementalFloat ammoGainRate = new ElementalFloat(0.01f);
        [SerializeField] float elevatedAmmoGainRate = .03f;
        [SerializeField] [Range(0, 1)] float initialAmmo = 1f;
        [SerializeField] [Range(0, 1)] float maxAmmo = 1f;
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
        public float MaxAmmo { get { return maxAmmo; } }

        [Header("Energy")]
        [SerializeField] bool usesEnergy;
        [SerializeField] [Range(0, 1)] float maxEnergy = 1f;
        [SerializeField] [Range(0, 1)] float initialEnergy = 1f;
        float currentEnergy;
        public float CurrentEnergy
        {
            get => currentEnergy;
            private set
            {
                currentEnergy = value;

                if (EnergyDisplay != null)
                    EnergyDisplay.UpdateDisplay(currentEnergy);
            }
        }
        public float MaxEnergy { get { return maxEnergy; } }

        [HideInInspector] public ResourceDisplay ChargeDisplay;
        [HideInInspector] public ResourceDisplay MassDisplay;
        [HideInInspector] public ResourceDisplay SpaceDisplay;
        [HideInInspector] public ResourceDisplay TimeDisplay;
        [HideInInspector] public ResourceDisplay EnergyDisplay;
        [HideInInspector] public ResourceDisplay BoostDisplay;
        [HideInInspector] public ResourceDisplay AmmoDisplay;

        public static readonly float OneFuelUnit = 1 / 10f;
        ShipStatus shipData;

        void Start()
        {
            shipData = GetComponent<ShipStatus>();

            StartCoroutine(LateStart());
        }

        IEnumerator LateStart()
        {
            yield return new WaitForSeconds(.2f);

            Reset();
            
            BoostDisplay?.gameObject.SetActive(usesBoost);
            AmmoDisplay?.gameObject.SetActive(usesAmmo);
            EnergyDisplay?.gameObject.SetActive(usesEnergy);
        }

        void Update()
        {
            if (shipData.ElevatedAmmoGain)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate * 2);
            else if (gainsAmmo)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate.Value);

            if (ChargeLevel != ChargeTestHarness)
                ChargeLevel = ChargeTestHarness;
        }

        public void Reset()
        {
            ResetBoost();
            ResetAmmo();
            ResetEnergy();
        }

        public void ResetBoost()
        {
            CurrentBoost = initialBoost;
        }
        public void ResetAmmo()
        {
            CurrentAmmo = initialAmmo;
        }

        public void ResetEnergy()
        {
            CurrentEnergy = initialEnergy;
        }

        public void ChangeBoostAmount(float amount)
        {
            CurrentBoost = Mathf.Clamp(currentBoost + amount, 0, maxBoost);
        }

        // TODO: Revisit
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
        
        public void ChangeEnergyAmount(float amount)
        {
            CurrentEnergy = Mathf.Clamp(currentEnergy + amount, 0, maxEnergy);
        }



        /********************************/
        /*  ELEMENTAL LEVELS STUFF HERE */
        /********************************/

        public delegate void ElementLevelChange(Element element, int level);
        public event ElementLevelChange OnElementLevelChange;

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
        const int MaxLevel = 10;

        [SerializeField] float ChargeTestHarness;

        public float ChargeLevel
        {
            get => chargeLevel;
            private set
            {
                chargeLevel = value;

                if (ChargeLevelDisplay != null)
                    ChargeLevelDisplay.UpdateDisplay(chargeLevel);

                OnElementLevelChange?.Invoke(Element.Charge, Mathf.FloorToInt(chargeLevel * MaxLevel));
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

                OnElementLevelChange?.Invoke(Element.Mass, Mathf.FloorToInt(massLevel * MaxLevel));
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

                OnElementLevelChange?.Invoke(Element.Space, Mathf.FloorToInt(spaceLevel * MaxLevel));
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

                OnElementLevelChange?.Invoke(Element.Time, Mathf.FloorToInt(timeLevel * MaxLevel));
            }
        }

        public void InitializeElementLevels()
        {
            chargeLevel = InitialChargeLevel;
            massLevel = InitialMassLevel;
            spaceLevel = InitialSpaceLevel;
            timeLevel = InitialTimeLevel;
        }

        public int GetLevel(Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    return Mathf.FloorToInt(chargeLevel);
                case Element.Mass:
                    return Mathf.FloorToInt(massLevel);
                case Element.Space:
                    return Mathf.FloorToInt(spaceLevel);
                case Element.Time:
                    return Mathf.FloorToInt(timeLevel);
            }

            return 0;
        }

        public void IncrementLevel(Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    AdjustChargeLevel(1);
                    break;
                case Element.Mass:
                    AdjustMassLevel(1);
                    break;
                case Element.Space:
                    AdjustSpaceLevel(1);
                    break;
                case Element.Time:
                    AdjustTimeLevel(1);
                    break;
            }
        }

        public void AdjustLevel(Element element, float amount)
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
        void AdjustChargeLevel(float amount)
        {
            chargeLevel = Math.Clamp(chargeLevel + amount, 0, MaxChargeLevel);
        }
        void AdjustMassLevel(float amount)
        {
            massLevel = Math.Clamp(massLevel + amount, 0, MaxMassLevel);
        }
        void AdjustSpaceLevel(float amount)
        {
            spaceLevel = Math.Clamp(spaceLevel + amount, 0, MaxSpaceLevel);
        }
        void AdjustTimeLevel(float amount)
        {
            timeLevel = Math.Clamp(timeLevel + amount, 0, MaxTimeLevel);
        }
    }
}