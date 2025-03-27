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
    IShip ship;
    string name;

    public ElementalFloat(float value)
    {
        Value = value;
    }

    public string Name
    {
        set { name = value; }
    }

    public IShip Ship
    {
        set
        {
            ship = value;

            if (Enabled)
            {
                ship.BindElementalFloat(name, element);
                ship.ShipStatus.ResourceSystem.OnElementLevelChange += ScaleValueWithLevel;
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