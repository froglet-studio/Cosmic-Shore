using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    // PORT: [RequireComponent(typeof(IVesselStatus))] — restore when the vessel layer ports (Deviations log)
    // PORT: base was ElementalShipComponent — restore when IVessel/ElementalFloat port (Deviations log)
    public class ResourceSystem : MonoBehaviour
    {
        [SerializeField] public List<Resource> Resources;
        public event Action<int, float, float> OnResourceChanged; 
        public static readonly float OneFuelUnit = 1 / 10f;

        private void Awake()
        {
            foreach (var r in Resources)
                r.initialResourceGainRate = r.resourceGainRate;
        }

        IEnumerator Start()
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                var r = Resources[i];
                r.CurrentAmount = r.InitialAmount;
                EmitResourceChanged(i);  
            }
            
            // Emit initial elemental levels
            EmitElementLevel(Element.Charge);
            EmitElementLevel(Element.Mass);
            EmitElementLevel(Element.Space);
            EmitElementLevel(Element.Time);

            yield return StartCoroutine(GainResourcesCoroutine());
        }

        IEnumerator GainResourcesCoroutine()
        {
            while (true)
            {
                for (int i = 0; i < Resources.Count; i++)
                {
                    var r = Resources[i];
                    float prev = r.CurrentAmount;
                    r.CurrentAmount = Mathf.Clamp(prev + r.resourceGainRate, 0, r.MaxAmount);
                    if (!Mathf.Approximately(prev, r.CurrentAmount))
                        EmitResourceChanged(i);
                }
                yield return new WaitForSeconds(1);
            }
        }

        void Update()
        {
            TickElementalEffects();

            if (ElementalLevels.Count > 0)
            {
                if (ChargeTestHarness != 0) ElementalLevels[Element.Charge] = ChargeTestHarness;
                if (MassTestHarness   != 0) ElementalLevels[Element.Mass]   = MassTestHarness;
                if (SpaceTestHarness  != 0) ElementalLevels[Element.Space]  = SpaceTestHarness;
                if (TimeTestHarness   != 0) ElementalLevels[Element.Time]   = TimeTestHarness;

                ChargeLevel = GetEffectiveLevel(Element.Charge);
                MassLevel   = GetEffectiveLevel(Element.Mass);
                SpaceLevel  = GetEffectiveLevel(Element.Space);
                TimeLevel   = GetEffectiveLevel(Element.Time);
            }
        }

        public void Reset()
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                var r = Resources[i];
                r.CurrentAmount = r.InitialAmount;
                EmitResourceChanged(i);
            }
        }

        public void ResetResource(int index)
        {
            Resources[index].CurrentAmount = Resources[index].InitialAmount;
            EmitResourceChanged(index);   
        }
        
        public void ChangeResourceAmount(int index, float amount)
        {
            var r = Resources[index];
            float prev = r.CurrentAmount;
            r.CurrentAmount = Mathf.Clamp(prev + amount, 0, r.MaxAmount);
            if (!Mathf.Approximately(prev, r.CurrentAmount))
                EmitResourceChanged(index);
        }

        public void SetResourceAmount(int index, float amount)
        {
            var r = Resources[index];
            float min = 0f;
            float prev = r.CurrentAmount;
            float max = r.MaxAmount;
            r.CurrentAmount = Mathf.Clamp(amount, min, max);
            if (!Mathf.Approximately(prev, r.CurrentAmount))
                EmitResourceChanged(index);
        }


        /********************************/
        /*  ELEMENTAL LEVELS STUFF HERE */
        /********************************/
        [Header("Elemental Levels")]
        [SerializeField, Range(-0.5f, 1.5f)] float ChargeTestHarness;
        [SerializeField, Range(-0.5f, 1.5f)] float MassTestHarness;
        [SerializeField, Range(-0.5f, 1.5f)] float SpaceTestHarness;
        [SerializeField, Range(-0.5f, 1.5f)] float TimeTestHarness;

        [field: SerializeField] public float ChargeLevel { get; private set; }
        [field: SerializeField] public float MassLevel   { get; private set; }
        [field: SerializeField] public float SpaceLevel  { get; private set; }
        [field: SerializeField] public float TimeLevel   { get; private set; }

        public delegate void ElementLevelChange(Element element, int level);
        public event ElementLevelChange OnElementLevelChange;

        const float MinElementalLevel = -0.5f;
        const float MaxElementalLevel = 1.5f;
        const int   LevelScale = 10;

        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Base (persistent) levels — written by crystals, the comeback system, init, etc.
        Dictionary<Element, float> ElementalLevels = new();

        // Temporary, decaying modifiers layered on top of the base levels.
        readonly List<ElementalEffect> _activeEffects = new();
        readonly Dictionary<Element, float> _elementModifiers = new();
        // Last integer level emitted per element, so OnElementLevelChange only fires on real changes.
        readonly Dictionary<Element, int> _emittedLevels = new();

        public void InitializeElementLevels(ResourceCollection resourceGroup)
        {
            ElementalLevels[Element.Charge] = resourceGroup.Charge;
            ElementalLevels[Element.Mass]   = resourceGroup.Mass;
            ElementalLevels[Element.Space]  = resourceGroup.Space;
            ElementalLevels[Element.Time]   = resourceGroup.Time;
        }

        /// <summary>Effective level = base level + active temporary modifiers, clamped to range.</summary>
        float GetEffectiveLevel(Element element)
        {
            float baseLevel = ElementalLevels.TryGetValue(element, out var b) ? b : 0f;
            float modifier  = _elementModifiers.TryGetValue(element, out var m) ? m : 0f;
            return Mathf.Clamp(baseLevel + modifier, MinElementalLevel, MaxElementalLevel);
        }

        public int GetLevel(Element element)
            => Mathf.FloorToInt(GetEffectiveLevel(element) * LevelScale);

        public float GetNormalizedLevel(Element element)
            => GetEffectiveLevel(element);

        public void IncrementLevel(Element element) => AdjustLevel(element, .1f);

        /// <summary>Permanently adjusts the base level of an element. Returns true if the integer level rose.</summary>
        public bool AdjustLevel(Element element, float amount)
        {
            int previousLevel = GetLevel(element);
            float previousBase = ElementalLevels.TryGetValue(element, out var b) ? b : 0f;
            ElementalLevels[element] = Mathf.Clamp(previousBase + amount, MinElementalLevel, MaxElementalLevel);
            EmitElementLevel(element);
            return GetLevel(element) - previousLevel >= 1;
        }

        /// <summary>Permanently sets the base level of an element.</summary>
        public void SetElementLevel(Element element, float normalizedLevel)
        {
            ElementalLevels[element] = Mathf.Clamp(normalizedLevel, MinElementalLevel, MaxElementalLevel);
            EmitElementLevel(element);
        }

        /// <summary>
        /// Standardized elemental buff/debuff. Positive <paramref name="magnitude"/> buffs the
        /// element, negative debuffs it — the two are fully symmetric.
        /// <para><paramref name="duration"/> &gt; 0 → temporary: applied as a modifier that decays
        /// linearly back to zero over <paramref name="duration"/> seconds, leaving the base level
        /// untouched so persistent progress (crystals, comeback, etc.) is preserved.</para>
        /// <para><paramref name="duration"/> &lt;= 0 → permanent: added straight to the base level.</para>
        /// </summary>
        public void ApplyElementalEffect(Element element, float magnitude, float duration)
        {
            if (duration <= 0f)
            {
                AdjustLevel(element, magnitude);
                return;
            }

            _activeEffects.Add(new ElementalEffect
            {
                Element = element,
                Magnitude = magnitude,
                Duration = duration,
                Elapsed = 0f,
            });
            RecomputeModifiers();
        }

        // Advances active temporary effects, drops expired ones, and refreshes modifier totals.
        void TickElementalEffects()
        {
            if (_activeEffects.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                effect.Elapsed += dt;
                if (effect.Elapsed >= effect.Duration)
                    _activeEffects.RemoveAt(i);
            }

            RecomputeModifiers();
        }

        // Recomputes the per-element modifier total and emits any resulting level changes.
        void RecomputeModifiers()
        {
            for (int i = 0; i < AllElements.Length; i++)
            {
                var element = AllElements[i];
                float sum = 0f;
                for (int j = 0; j < _activeEffects.Count; j++)
                {
                    var effect = _activeEffects[j];
                    if (effect.Element == element)
                        sum += effect.CurrentContribution;
                }
                _elementModifiers[element] = sum;
                EmitElementLevel(element);
            }
        }

        // Fires OnElementLevelChange only when the integer effective level actually changes.
        void EmitElementLevel(Element element)
        {
            int level = GetLevel(element);
            if (_emittedLevels.TryGetValue(element, out var previous) && previous == level)
                return;
            _emittedLevels[element] = level;
            OnElementLevelChange?.Invoke(element, level);
        }
        
        void EmitResourceChanged(int index)
        {
            if ((uint)index >= (uint)Resources.Count) return;
            var r = Resources[index];
            OnResourceChanged?.Invoke(index, r.CurrentAmount, r.MaxAmount);
        }

        // A single temporary elemental effect that decays from full magnitude to zero.
        sealed class ElementalEffect
        {
            public Element Element;
            public float Magnitude;
            public float Duration;
            public float Elapsed;

            public float CurrentContribution => Mathf.Lerp(Magnitude, 0f, Elapsed / Duration);
        }
    }
}
