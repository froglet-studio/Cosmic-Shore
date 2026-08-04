using CosmicShore.Data;
using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public class ElementalFloat
    {
        [SerializeField] public bool Enabled;
        [SerializeField] public float Value;
        [SerializeField] float Min;
        [SerializeField] float Max;
        [SerializeField] Element element;
        IVessel vessel;
        string name;

        public ElementalFloat(float value)
        {
            Value = value;
        }

        /// <summary>
        /// Live evaluation against the vessel's CURRENT element level — the unified read
        /// path (same math as ScaleValueWithLevel, no binding or subscription required, so
        /// it works for per-vessel component fields whose event binding was never wired).
        /// Returns the serialized Value when disabled or no ResourceSystem is available.
        /// </summary>
        public float EvaluateLive(IVesselStatus status)
        {
            if (!Enabled) return Value;
            var resources = status?.ResourceSystem;
            if (resources == null) return Value;
            return Mathf.LerpUnclamped(Min, Max, resources.GetLevel(element) / 10f);
        }

        public string Name
        {
            set { name = value; }
        }

        public IVessel Vessel
        {
            set
            {
                vessel = value;

                if (Enabled)
                {
                    vessel.BindElementalFloat(name, element);
                    vessel.VesselStatus.ResourceSystem.OnElementLevelChange += ScaleValueWithLevel;
                }
            }
        }

        // LerpUnclamped allows levels outside 0-10 (e.g. -5 to 15 from comeback system)
        // to extrapolate beyond Min/Max
        void ScaleValueWithLevel(Element element, int level)
        {
            if (element == this.element)
                Value = Mathf.LerpUnclamped(Min, Max, level / 10f);
        }
    }
}
