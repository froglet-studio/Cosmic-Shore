using CosmicShore.Core;
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
    Ship ship;
    string name;

    public ElementalFloat(float value)
    {
        Value = value;
    }

    public string Name
    {
        set { name = value; }
    }

    public Ship Ship
    {
        set
        {
            ship = value;

            if (Enabled)
            {
                ship.BindElementalFloat(name, element);
                ship.ResourceSystem.OnElementLevelChange += ScaleValueWithLevel;
            }
        }
    }

    // TODO: need to convert this to an exponential curve instead of linear
    void ScaleValueWithLevel(Element element, int level)
    {
        Debug.Log($"Elemental Float: Element: {element}, level: {level}");
        if (element == this.element)
            Value = Mathf.Lerp(Min, Max, level / 10f);
    }
}