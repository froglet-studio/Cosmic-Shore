using System;
using UnityEngine;
using System.Collections;
using CosmicShore.Game.UI;

namespace CosmicShore.Core
{
    public class ResourceSystem : ElementalShipComponent
    {
        [Header("Boost")]
        [SerializeField] bool displayBoost;
        [SerializeField] bool gainsBoost;
        [SerializeField] float boostGainRate = .1f;
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

        public delegate void AmmoUpdateDelegate(float currentAmmo);
        public event AmmoUpdateDelegate OnAmmoChange;

        [Header("Ammo")]
        [SerializeField] bool displayAmmo;
        [SerializeField] bool gainsAmmo;
        [SerializeField] ElementalFloat ammoGainRate = new ElementalFloat(0.01f);
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
                {
                    AmmoDisplay.UpdateDisplay(currentAmmo);
                }
                OnAmmoChange?.Invoke(currentAmmo);
            }
        }
        public float MaxAmmo { get { return maxAmmo; } }

        [Header("Energy")]
        [SerializeField] bool displayEnergy;
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
        //[HideInInspector] public ResourceButton BoostDisplay;
        [SerializeField] ResourceDisplay BoostDisplay;
        [SerializeField] ResourceDisplay AmmoDisplay;

        public static readonly float OneFuelUnit = 1 / 10f;
        ShipStatus shipData;

        void Start()
        {
            shipData = GetComponent<ShipStatus>();

            StartCoroutine(LateStart());
        }

        // Give time for components to initialize before notifying of initial resource levels
        IEnumerator LateStart()
        {
            yield return new WaitForSeconds(.5f);

            // TODO: need to assign ship to ammo gain rate or the elemental system wont update its value
            //ammoGainRate.Ship = 

            BoostDisplay?.gameObject.SetActive(displayBoost);
            AmmoDisplay?.gameObject.SetActive(displayAmmo);
            EnergyDisplay?.gameObject.SetActive(displayEnergy);

            // Notify elemental floats of initial elemental levels
            OnElementLevelChange?.Invoke(Element.Charge, Mathf.FloorToInt(chargeLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Mass, Mathf.FloorToInt(massLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Space, Mathf.FloorToInt(spaceLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Time, Mathf.FloorToInt(timeLevel * MaxLevel));
        }

        void Update()
        {
            if (shipData.ElevatedAmmoGain)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate.Value * 2);
            else if (gainsAmmo)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate.Value);
            else if (gainsBoost)
                ChangeBoostAmount(Time.deltaTime * boostGainRate);

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

        public void IncrementLevel(Element element, float amount)
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

        /// <summary>
        /// Adjust the level of an Ships elemental parameter
        /// </summary>
        /// <param name="element">Element whose level should be adjusted</param>
        /// <param name="amount">Amount to adjust the level by</param>
        /// <returns>Whether or not the adjustment triggered a full level upgrade</returns>
        public bool AdjustLevel(Element element, float amount)
        {
            var leveledUp = false;

            switch (element)
            {
                case Element.Charge:
                    leveledUp = AdjustChargeLevel(amount);
                    break;
                case Element.Mass:
                    leveledUp = AdjustMassLevel(amount);
                    break;
                case Element.Space:
                    leveledUp = AdjustSpaceLevel(amount);
                    break;
                case Element.Time:
                    leveledUp = AdjustTimeLevel(amount);
                    break;
            }

            return leveledUp;
        }
        bool AdjustChargeLevel(float amount)
        {
            var previousLevel = chargeLevel;
            chargeLevel = Math.Clamp(chargeLevel + amount, 0, MaxChargeLevel);

            return (Mathf.Floor(chargeLevel) - Mathf.Floor(previousLevel) >= 1);
        }
        bool AdjustMassLevel(float amount)
        {
            var previousLevel = massLevel;
            massLevel = Math.Clamp(massLevel + amount, 0, MaxMassLevel);

            return (Mathf.Floor(massLevel) - Mathf.Floor(previousLevel) >= 1);
        }
        bool AdjustSpaceLevel(float amount)
        {
            var previousLevel = spaceLevel;
            spaceLevel = Math.Clamp(spaceLevel + amount, 0, MaxSpaceLevel);

            return (Mathf.Floor(spaceLevel) - Mathf.Floor(previousLevel) >= 1);
        }
        bool AdjustTimeLevel(float amount)
        {
            var previousLevel = timeLevel;
            timeLevel = Math.Clamp(timeLevel + amount, 0, MaxTimeLevel);

            return (Mathf.Floor(timeLevel) - Mathf.Floor(previousLevel) >= 1);
        }
    }
}