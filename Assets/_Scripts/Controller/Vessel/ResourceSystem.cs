using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(IVesselStatus))]
    public class ResourceSystem : ElementalShipComponent
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
                RecoverBaseLevels(Time.deltaTime);

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

        [Header("Elemental Recovery")]
        [Tooltip("Elements passively return to the [0,10] resting band: an overcharge above level 10 " +
                 "drains back down to 10, and a deficit below level 0 fills back up to 0 - symmetric ends. " +
                 "Rate is in normalized units per second (0.1 = one integer level per second). " +
                 "Set 0 to disable the drift.")]
        [SerializeField, Min(0f)] float elementalRecoveryRate = 0.05f;

        public delegate void ElementLevelChange(Element element, int level);
        public event ElementLevelChange OnElementLevelChange;

        const float MinElementalLevel = -0.5f;
        const float MaxElementalLevel = 1.5f;
        const int   LevelScale = 10;

        // Passive recovery pulls each element's BASE level back into the [0,10] resting band.
        // Above the upper bound it drains down to it; below the lower bound it fills up to it;
        // inside the band the level holds so ordinary crystal progress stays stable.
        const float RestingBandLower = 0f; // integer level 0  - deficits recover up to here
        const float RestingBandUpper = 1f; // integer level 10 - overcharge drains down to here

        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Base (persistent) levels - written by crystals, the comeback system, init, etc.
        Dictionary<Element, float> ElementalLevels = new();

        // Temporary, decaying modifiers layered on top of the base levels.
        readonly List<ElementalEffect> _activeEffects = new();
        readonly Dictionary<Element, float> _elementModifiers = new();
        // Comeback bonus layer — composited into the effective level WITHOUT touching the
        // crystal-earned base. (The comeback system previously wrote the base via
        // SetElementLevel every tick, erasing crystal progression within a second.)
        // Single-writer: ElementalComebackSystem.
        readonly Dictionary<Element, float> _comebackModifiers = new();
        // Domain fauna buff layer — the summed elemental value of this vessel's domain's LIVING
        // fauna hearts (each contributes exactly what its dropped crystal would grant on
        // collection). Held while the fauna live, revoked the moment they die — composited like
        // the comeback layer so it never touches crystal-earned base progress.
        // Single-writer: DomainFaunaBuffSystem.
        readonly Dictionary<Element, float> _faunaBuffModifiers = new();
        // Last integer level emitted per element, so OnElementLevelChange only fires on real changes.
        readonly Dictionary<Element, int> _emittedLevels = new();

        public void InitializeElementLevels(ResourceCollection resourceGroup)
        {
            ElementalLevels[Element.Charge] = resourceGroup.Charge;
            ElementalLevels[Element.Mass]   = resourceGroup.Mass;
            ElementalLevels[Element.Space]  = resourceGroup.Space;
            ElementalLevels[Element.Time]   = resourceGroup.Time;
        }

        // The comeback layer may only FILL an element toward integer level 10 (normalized 1.0)
        // - it never lifts anyone past it. Earned progression (crystals, effects) can still
        // reach the overcharge band above 10; charity cannot.
        const float ComebackCeiling = 1.0f;

        /// <summary>Effective level = base + temporary modifiers + domain fauna buff + comeback
        /// bonus, clamped to range. The fauna buff counts as earned power (the comeback
        /// contribution yields to it); the comeback contribution is capped so it can never
        /// raise the effective level above 10.</summary>
        float GetEffectiveLevel(Element element)
        {
            float baseLevel = ElementalLevels.TryGetValue(element, out var b) ? b : 0f;
            float modifier  = _elementModifiers.TryGetValue(element, out var m) ? m : 0f;
            float faunaBuff = _faunaBuffModifiers.TryGetValue(element, out var f) ? f : 0f;
            float comeback  = _comebackModifiers.TryGetValue(element, out var c) ? c : 0f;

            return CompositeEffectiveLevel(baseLevel, modifier, faunaBuff, comeback);
        }

        /// <summary>
        /// Pure layer compositing (edit-mode tested): earned = base + temporary modifier +
        /// domain fauna buff; the comeback bonus only fills toward <see cref="ComebackCeiling"/>
        /// above that sum, and the result clamps to the element range.
        /// </summary>
        public static float CompositeEffectiveLevel(
            float baseLevel, float tempModifier, float faunaBuff, float comebackBonus)
        {
            float earned = baseLevel + tempModifier + faunaBuff;
            float comeback = Mathf.Min(comebackBonus, Mathf.Max(0f, ComebackCeiling - earned));
            return Mathf.Clamp(earned + comeback, MinElementalLevel, MaxElementalLevel);
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
        /// Sets the comeback bonus for an element (normalized units, ≥ 0). Composites into the
        /// effective level on top of the crystal-earned base instead of overwriting it, so
        /// crystal progression and comeback buffs coexist. Pass 0 to remove the bonus.
        /// Single-writer: ElementalComebackSystem.
        /// </summary>
        public void SetComebackModifier(Element element, float normalizedBonus)
        {
            normalizedBonus = Mathf.Max(0f, normalizedBonus);
            _comebackModifiers.TryGetValue(element, out var current);
            if (Mathf.Approximately(current, normalizedBonus)) return;

            if (normalizedBonus == 0f) _comebackModifiers.Remove(element);
            else _comebackModifiers[element] = normalizedBonus;
            EmitElementLevel(element);
        }

        /// <summary>Clears all comeback bonuses (turn/game end).</summary>
        public void ClearComebackModifiers()
        {
            if (_comebackModifiers.Count == 0) return;
            _comebackModifiers.Clear();
            foreach (var element in AllElements)
                EmitElementLevel(element);
        }

        /// <summary>
        /// Sets the domain fauna buff for an element (normalized units, ≥ 0): the value this
        /// vessel's domain draws from its LIVING fauna hearts. Composites into the effective
        /// level without touching the crystal-earned base, so the buff vanishes cleanly when
        /// the fauna die — and their dropped crystals grant the very same value back through
        /// <see cref="AdjustLevel"/> on collection. Pass 0 to remove the buff.
        /// Single-writer: DomainFaunaBuffSystem.
        /// </summary>
        public void SetFaunaBuffModifier(Element element, float normalizedBonus)
        {
            normalizedBonus = Mathf.Max(0f, normalizedBonus);
            _faunaBuffModifiers.TryGetValue(element, out var current);
            if (Mathf.Approximately(current, normalizedBonus)) return;

            if (normalizedBonus == 0f) _faunaBuffModifiers.Remove(element);
            else _faunaBuffModifiers[element] = normalizedBonus;
            EmitElementLevel(element);
        }

        /// <summary>Clears all domain fauna buffs (system teardown).</summary>
        public void ClearFaunaBuffModifiers()
        {
            if (_faunaBuffModifiers.Count == 0) return;
            _faunaBuffModifiers.Clear();
            foreach (var element in AllElements)
                EmitElementLevel(element);
        }

        /// <summary>
        /// Standardized elemental buff/debuff. Positive <paramref name="magnitude"/> buffs the
        /// element, negative debuffs it - the two are fully symmetric.
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

        // Passively pulls each element's persistent base level back toward the [0,10] resting band.
        // Symmetric with the temporary-effect decay: an overcharge above level 10 bleeds back down to
        // 10, and a deficit below level 0 fills back up to 0, both at elementalRecoveryRate per second.
        // Levels already inside the band are left untouched, so ordinary progress is stable - this only
        // removes the excess so parking at the level-15 cap yields no lasting benefit over level 10.
        void RecoverBaseLevels(float dt)
        {
            if (elementalRecoveryRate <= 0f) return;

            float step = elementalRecoveryRate * dt;
            for (int i = 0; i < AllElements.Length; i++)
            {
                var element = AllElements[i];
                // A live test-harness override pins the level - don't fight it.
                if (HarnessValueFor(element) != 0f) continue;
                if (!ElementalLevels.TryGetValue(element, out var baseLevel)) continue;

                float target;
                if (baseLevel > RestingBandUpper) target = RestingBandUpper;
                else if (baseLevel < RestingBandLower) target = RestingBandLower;
                else continue; // already resting inside the band

                float next = Mathf.MoveTowards(baseLevel, target, step);
                if (Mathf.Approximately(next, baseLevel)) continue;

                ElementalLevels[element] = next;
                EmitElementLevel(element);
            }
        }

        float HarnessValueFor(Element element) => element switch
        {
            Element.Charge => ChargeTestHarness,
            Element.Mass   => MassTestHarness,
            Element.Space  => SpaceTestHarness,
            Element.Time   => TimeTestHarness,
            _              => 0f,
        };

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
