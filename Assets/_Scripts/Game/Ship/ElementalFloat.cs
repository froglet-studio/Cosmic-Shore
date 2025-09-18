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

    // TODO: need to convert this to an exponential curve instead of linear
    void ScaleValueWithLevel(Element element, int level)
    {
        if (element == this.element)
            Value = Mathf.Lerp(Min, Max, level / 10f);
    }
}