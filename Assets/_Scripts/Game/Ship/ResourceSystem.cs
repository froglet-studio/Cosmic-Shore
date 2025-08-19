using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game;
using CosmicShore.Models.Enums;
using UnityEngine;

namespace CosmicShore.Core
{
    [RequireComponent(typeof(IShipStatus))]
    public class ResourceSystem : ElementalShipComponent
    {
        [SerializeField] public List<Resource> Resources;

        public static readonly float OneFuelUnit = 1 / 10f;

        private void Awake()
        {
            foreach (var r in Resources)
                r.initialResourceGainRate = r.resourceGainRate;
        }

        IEnumerator Start()
        {
            // Push initial values so any HUD subs pick them up immediately
            foreach (var r in Resources)
                r.CurrentAmount = r.InitialAmount;

            // Elemental events (unchanged)
            OnElementLevelChange?.Invoke(Element.Charge, Mathf.FloorToInt(ChargeLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Mass,   Mathf.FloorToInt(MassLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Space,  Mathf.FloorToInt(SpaceLevel * MaxLevel));
            OnElementLevelChange?.Invoke(Element.Time,   Mathf.FloorToInt(TimeLevel * MaxLevel));

            yield return StartCoroutine(GainResourcesCoroutine());
        }

        IEnumerator GainResourcesCoroutine()
        {
            while (true)
            {
                foreach (var r in Resources)
                    r.CurrentAmount = Mathf.Clamp(r.CurrentAmount + r.resourceGainRate, 0, r.MaxAmount);

                yield return new WaitForSeconds(1);
            }
        }

        void Update()
        {
            if (ElementalLevels.Count > 0)
            {
                if (ChargeTestHarness != 0) ElementalLevels[Element.Charge] = ChargeTestHarness;
                if (MassTestHarness   != 0) ElementalLevels[Element.Mass]   = MassTestHarness;
                if (SpaceTestHarness  != 0) ElementalLevels[Element.Space]  = SpaceTestHarness;
                if (TimeTestHarness   != 0) ElementalLevels[Element.Time]   = TimeTestHarness;

                ChargeLevel = ElementalLevels[Element.Charge];
                MassLevel   = ElementalLevels[Element.Mass];
                SpaceLevel  = ElementalLevels[Element.Space];
                TimeLevel   = ElementalLevels[Element.Time];
            }
        }

        public void Reset()
        {
            foreach (var r in Resources)
                r.CurrentAmount = r.InitialAmount;
        }

        public void ResetResource(int index)
        {
            Resources[index].CurrentAmount = Resources[index].InitialAmount;
        }

        public void ChangeResourceAmount(int index, float amount)
        {
            var r = Resources[index];
            r.CurrentAmount = Mathf.Clamp(r.CurrentAmount + amount, 0, r.MaxAmount);
        }

        /********************************/
        /*  ELEMENTAL LEVELS STUFF HERE */
        /********************************/
        [Header("Elemental Levels")]
        [SerializeField, Range(0, 1)] float ChargeTestHarness;
        [SerializeField, Range(0, 1)] float MassTestHarness;
        [SerializeField, Range(0, 1)] float SpaceTestHarness;
        [SerializeField, Range(0, 1)] float TimeTestHarness;

        [field: SerializeField] public float ChargeLevel { get; private set; }
        [field: SerializeField] public float MassLevel   { get; private set; }
        [field: SerializeField] public float SpaceLevel  { get; private set; }
        [field: SerializeField] public float TimeLevel   { get; private set; }

        public delegate void ElementLevelChange(Element element, int level);
        public event ElementLevelChange OnElementLevelChange;

        const float MaxElementalLevel = 1;
        const int   MaxLevel = 10;
        Dictionary<Element, float> ElementalLevels = new();

        public void InitializeElementLevels(ResourceCollection resourceGroup)
        {
            ElementalLevels[Element.Charge] = resourceGroup.Charge;
            ElementalLevels[Element.Mass]   = resourceGroup.Mass;
            ElementalLevels[Element.Space]  = resourceGroup.Space;
            ElementalLevels[Element.Time]   = resourceGroup.Time;
        }

        public int GetLevel(Element element)
            => !ElementalLevels.TryGetValue(element, out var level) ? 0 : Mathf.FloorToInt(level);

        public void IncrementLevel(Element element) => AdjustLevel(element, .1f);

        public bool AdjustLevel(Element element, float amount)
        {
            var previous = ElementalLevels[element];
            ElementalLevels[element] = Math.Clamp(ElementalLevels[element] + amount, 0, MaxElementalLevel);
            if (Mathf.Approximately(previous, ElementalLevels[element])) return false;

            OnElementLevelChange?.Invoke(element, Mathf.FloorToInt(ElementalLevels[element] * MaxLevel));
            return Mathf.FloorToInt(ElementalLevels[element] * MaxLevel) - Mathf.FloorToInt(previous * MaxLevel) >= 1;
        }
    }
}
