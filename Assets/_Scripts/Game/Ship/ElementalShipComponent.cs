using CosmicShore.Core;
using System;
using System.Reflection;
using UnityEngine;

public class ElementalShipComponent : MonoBehaviour
{
    readonly BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public void BindElementalFloats(IShip ship)
    {
        Type thisType = GetType();
        FieldInfo[] fields = thisType.GetFields(bindingFlags);

        // Find all ElementalFloat Fields
        foreach (FieldInfo fieldInfo in fields)
        {
            if (fieldInfo.FieldType == typeof(ElementalFloat))
            {
                // Assign the ElementalFloat fields name and ship properties
                var elementalFloatInstance = thisType.GetField(fieldInfo.Name, bindingFlags).GetValue(this);
                typeof(ElementalFloat).GetProperty("Name").SetValue(elementalFloatInstance, GetType().Name + "." + fieldInfo.Name);
                typeof(ElementalFloat).GetProperty("Ship").SetValue(elementalFloatInstance, ship);
            }
        }
    }
}