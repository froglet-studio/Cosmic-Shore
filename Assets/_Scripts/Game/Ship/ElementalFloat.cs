using CosmicShore.Core;
using CosmicShore.Game;
using System;
using UnityEngine;

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