using CosmicShore.Core;
using System;
using UnityEngine;

[Serializable]
public class ElementalFloat
{
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

            if (element != Element.None)
            {
                ship.NotifyElementalFloatBinding(name, element);
                ship.ResourceSystem.OnElementLevelChange += ScaleValueWithLevel;
            }
        }
    }

    void ScaleValueWithLevel(Element element, int level)
    {
        Debug.Log($"Elemental Float: UpdateLevel: element{element}, level: {level}");
        if (element == this.element)
            Value = Mathf.Lerp(Min, Max, level / 10f);
    }
}