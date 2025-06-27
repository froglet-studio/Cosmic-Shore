using System;
using UnityEngine;
using System.Collections;
using CosmicShore.Game.UI;
using System.Collections.Generic;
using CosmicShore.Models.Enums;

namespace CosmicShore.Core
{
    [Serializable]
    public class Resource
    {
        public delegate void ResourceUpdateDelegate(float currentResource);
        public event ResourceUpdateDelegate OnResourceChange;

        [SerializeField] public string Name;
        [HideInInspector] public ResourceDisplay Display;

        [HideInInspector] public float initialResourceGainRate;

        [SerializeField] public float resourceGainRate;

        [SerializeField][Range(0, 1)] float maxAmount = 1f;
        public float MaxAmount => maxAmount;

        [SerializeField][Range(0, 1)] float initialAmount = 1f;
        public float InitialAmount => initialAmount;
        float currentAmount;
        public float CurrentAmount
        {
            get => currentAmount;
            set
            {
                currentAmount = value;

                if (Display != null)
                    Display.UpdateDisplay(currentAmount);

                OnResourceChange?.Invoke(currentAmount);
            }
        }
    }

    public class ResourceSystem : ElementalShipComponent
    {
        [SerializeField] public List<Resource> Resources;

        public static readonly float OneFuelUnit = 1 / 10f;

        private void Awake()
        {
            foreach (var resource in Resources)
            {
                resource.initialResourceGainRate = resource.resourceGainRate;
            }

       
        }

        IEnumerator Start()
        {
            var ship = GetComponent<Ship>();
            if (ship != null && ship.shipHUD != null)
            {
                IShipHUDView hudView = ship.ShipHUDView;
                if (hudView != null)
                {
                    for (int i = 0; i < Resources.Count; i++)
                    {
                        var resource = Resources[i];
                        var display = hudView.GetResourceDisplay(resource.Name);
                        if (display != null)
                        {
                            RegisterDisplay(i, display);
                        }
                        else
                        {
                            Debug.LogWarning($"No ResourceDisplay found for resource: {resource.Name}");
                        }
                    }
                }
                else
                {
                    Debug.Log($"<color=red>No Ship Hud View Found</color>");
                }
            }

            yield return new WaitForSeconds(.5f);

            foreach (var resource in Resources)
            {
                resource.Display?.gameObject.SetActive(true);
            }

            // Notify elemental floats of initial elemental levels
            OnElementLevelChange?.Invoke(Element.Charge, Mathf.FloorToInt(ChargeLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Mass, Mathf.FloorToInt(MassLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Space, Mathf.FloorToInt(SpaceLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Time, Mathf.FloorToInt(TimeLevel * MaxLevel));

            StartCoroutine(GainResourcesCoroutine());
        }

        IEnumerator GainResourcesCoroutine()
        {
            while (true)
            {
                foreach (var resource in Resources)
                {
                    //Debug.Log("Resource: " + resource.Name + " before gaining Current Amount: " + resource.CurrentAmount); 
                    resource.CurrentAmount = Mathf.Clamp(resource.CurrentAmount + resource.resourceGainRate, 0, resource.MaxAmount);
                    //Debug.Log("Resource: " + resource.Name + " after gaining Current Amount: " + resource.CurrentAmount);
                }
                yield return new WaitForSeconds(1);
            }
        }

        void Update()
        {
            // These four fields are serialized for visibility during class creation and tuning
            // Use the test harness assigned value if it's been set, otherwise use the real value
            if (ElementalLevels.Count > 0)
            {
                if (ChargeTestHarness != 0)
                    ElementalLevels[Element.Charge] = ChargeTestHarness;
                if (MassTestHarness != 0)
                    ElementalLevels[Element.Mass] = MassTestHarness;
                if (SpaceTestHarness != 0)
                    ElementalLevels[Element.Space] = SpaceTestHarness;
                if (TimeTestHarness != 0)
                    ElementalLevels[Element.Time] = TimeTestHarness;

                ChargeLevel = ElementalLevels[Element.Charge];
                MassLevel = ElementalLevels[Element.Mass];
                SpaceLevel = ElementalLevels[Element.Space];
                TimeLevel = ElementalLevels[Element.Time];
            }
        }

        public void Reset()
        {
            foreach (var resource in Resources)
            {
                resource.CurrentAmount = resource.InitialAmount;
            }
        }

        public void ResetResource(int index)
        {
            Resources[index].CurrentAmount = Resources[index].InitialAmount;
        }

        public void ChangeResourceAmount(int index, float amount)
        {
            Resources[index].CurrentAmount = Mathf.Clamp(Resources[index].CurrentAmount + amount, 0, Resources[index].MaxAmount);
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

        public void InitializeElementLevels(ResourceCollection resourceGroup)
        {
            ElementalLevels[Element.Charge] = resourceGroup.Charge;
            ElementalLevels[Element.Mass] = resourceGroup.Mass;
            ElementalLevels[Element.Space] = resourceGroup.Space;
            ElementalLevels[Element.Time] = resourceGroup.Time;
        }

        public int GetLevel(Element element)
        {
            return !ElementalLevels.TryGetValue(element, out var level) ? 0 : Mathf.FloorToInt(level);
        }

        public void IncrementLevel(Element element)
        {
            AdjustLevel(element, .1f);
        }

        /// <summary>
        /// Call this right after you instantiate a ResourceDisplay prefab
        /// so that Resource.CurrentAmount updates drive the UI.
        /// </summary>
        public void RegisterDisplay(int resourceIndex, ResourceDisplay display)
        {
            if (resourceIndex < 0 || resourceIndex >= Resources.Count)
            {
                Debug.LogWarning($"Invalid resource index: {resourceIndex}");
                return;
            }

            var resource = Resources[resourceIndex];
            resource.Display = display;

            // If you want to push the current value immediately:
            display.UpdateDisplay(resource.CurrentAmount);

            // And (optionally) subscribe so you don’t rely on resource.Display inside the setter:
            resource.OnResourceChange += display.UpdateDisplay;

            Debug.Log($"Registerd Display{resource.Name}");
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
            if (Mathf.Approximately(previousLevel, ElementalLevels[element])) return false;

            OnElementLevelChange?.Invoke(element, Mathf.FloorToInt(ElementalLevels[element] * MaxLevel));

            return Mathf.FloorToInt(ElementalLevels[element] * MaxLevel) - Mathf.FloorToInt(previousLevel * MaxLevel) >= 1;
        }
    }
}