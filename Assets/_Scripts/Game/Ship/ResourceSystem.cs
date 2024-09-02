using System;
using UnityEngine;
using System.Collections;
using CosmicShore.Game.UI;
using System.Collections.Generic;
using CosmicShore.Models.Enums;

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

                if (ResourceDisplays?.BoostDisplay != null)
                    ResourceDisplays?.BoostDisplay.UpdateDisplay(currentBoost);
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

                if (ResourceDisplays?.AmmoDisplay != null)
                    ResourceDisplays?.AmmoDisplay.UpdateDisplay(currentAmmo);

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

                if (ResourceDisplays?.EnergyDisplay != null)
                    ResourceDisplays?.EnergyDisplay.UpdateDisplay(currentEnergy);
            }
        }
        public float MaxEnergy { get { return maxEnergy; } }

        public ResourceDisplayGroup ResourceDisplays;

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

            BindElementalFloats(GetComponent<Ship>());

            ResourceDisplays?.BoostDisplay?.gameObject.SetActive(displayBoost);
            ResourceDisplays?.AmmoDisplay?.gameObject.SetActive(displayAmmo);
            ResourceDisplays?.EnergyDisplay?.gameObject.SetActive(displayEnergy);

            // Notify elemental floats of initial elemental levels
            OnElementLevelChange?.Invoke(Element.Charge, Mathf.FloorToInt(ChargeLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Mass, Mathf.FloorToInt(MassLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Space, Mathf.FloorToInt(SpaceLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Time, Mathf.FloorToInt(TimeLevel * MaxLevel));
        }

        void Update()
        {
            if (shipData.ElevatedAmmoGain)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate.Value * 2);
            else if (gainsAmmo)
                ChangeAmmoAmount(Time.deltaTime * ammoGainRate.Value);
            else if (gainsBoost)
                ChangeBoostAmount(Time.deltaTime * boostGainRate);

            // These four fields are serialized for visibility during class creation and tuning
            // Use the test harness assigned value if it's been set, otherwise use the real value
            ChargeLevel = (ChargeTestHarness != 0 && ChargeLevel != ChargeTestHarness) ? ChargeTestHarness : ElementalLevels[Element.Charge];
            MassLevel = (MassTestHarness != 0 && MassLevel != MassTestHarness) ? MassTestHarness : ElementalLevels[Element.Mass];
            SpaceLevel = (SpaceTestHarness != 0 && SpaceLevel != SpaceTestHarness) ? SpaceTestHarness : ElementalLevels[Element.Space];
            TimeLevel = (TimeTestHarness != 0 && TimeLevel != TimeTestHarness) ? TimeTestHarness : ElementalLevels[Element.Time];
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
        [Header("Elemental Levels")]
        [Tooltip("Convience Property for creating and tuning pilot elemental parameters - if set to zero, will not be used")]
        [SerializeField][Range(0, 1)] float ChargeTestHarness;
        [Tooltip("Convience Property for creating and tuning pilot elemental parameters - if set to zero, will not be used")]
        [SerializeField][Range(0, 1)] float MassTestHarness;
        [Tooltip("Convience Property for creating and tuning pilot elemental parameters - if set to zero, will not be used")]
        [SerializeField][Range(0, 1)] float SpaceTestHarness;
        [Tooltip("Convience Property for creating and tuning pilot elemental parameters - if set to zero, will not be used")]
        [SerializeField][Range(0, 1)] float TimeTestHarness;

        [Tooltip("Serialized for debug visibility")]
        [field: SerializeField] public float ChargeLevel { get; private set; }
        [Tooltip("Serialized for debug visibility")]
        [field: SerializeField] public float MassLevel { get; private set; }
        [Tooltip("Serialized for debug visibility")]
        [field: SerializeField] public float SpaceLevel { get; private set; }
        [Tooltip("Serialized for debug visibility")]
        [field: SerializeField] public float TimeLevel { get; private set; }

        public delegate void ElementLevelChange(Element element, int level);
        public event ElementLevelChange OnElementLevelChange;

        const float MaxElementalLevel = 1;
        const int MaxLevel = 10;
        Dictionary<Element, float> ElementalLevels = new();
        Dictionary<Element, ResourceDisplay> ElementalDisplays = new();

        public void InitializeElementLevels(ResourceCollection resourceGroup)
        {
            ElementalLevels[Element.Charge] = resourceGroup.Charge;
            ElementalLevels[Element.Mass] = resourceGroup.Mass;
            ElementalLevels[Element.Space] = resourceGroup.Space;
            ElementalLevels[Element.Time] = resourceGroup.Time;
            ElementalDisplays[Element.Charge] = ResourceDisplays?.ChargeLevelDisplay;
            ElementalDisplays[Element.Mass] = ResourceDisplays?.MassLevelDisplay;
            ElementalDisplays[Element.Space] = ResourceDisplays?.SpaceLevelDisplay;
            ElementalDisplays[Element.Time] = ResourceDisplays?.TimeLevelDisplay;
        }

        public int GetLevel(Element element)
        {
            if (ElementalLevels.ContainsKey(element))
                return Mathf.FloorToInt(ElementalLevels[element]);

            return 0;
        }

        public bool IncrementLevel(Element element)
        {
            return AdjustLevel(element, .1f);
        }

        /// <summary>
        /// Adjust the level of an Ships elemental parameter
        /// </summary>
        /// <param name="element">Element whose level should be adjusted</param>
        /// <param name="amount">Amount to adjust the level by</param>
        /// <returns>Whether or not the adjustment triggered a full level upgrade</returns>
        public bool AdjustLevel(Element element, float amount)
        {
            var previousLevel = ElementalLevels[element];
            ElementalLevels[element] = Math.Clamp(ElementalLevels[element] + amount, 0, MaxElementalLevel);

            // Don't waste cycles updating if there was no change
            if (previousLevel == ElementalLevels[element]) return false;

            if (ElementalDisplays[element] != null)
                ElementalDisplays[element].UpdateDisplay(ElementalLevels[element]);

            OnElementLevelChange?.Invoke(element, Mathf.FloorToInt(ElementalLevels[element] * MaxLevel));

            return (Mathf.FloorToInt(ElementalLevels[element] * MaxLevel) - Mathf.FloorToInt(previousLevel * MaxLevel) >= 1);
        }
    }
}