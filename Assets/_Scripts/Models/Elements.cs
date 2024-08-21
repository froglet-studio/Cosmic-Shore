using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class Elements : MonoBehaviour
    {
        static Dictionary<Element, SO_Element> dataDictionary = new();
        static bool initialized = false;

        public static void Initialize()
        {
            SO_Element[] allData = Resources.LoadAll<SO_Element>("Element SOs");

            foreach (var data in allData)
            {
                if (!dataDictionary.ContainsKey(data.Element))
                {
                    dataDictionary.Add(data.Element, data);
                }
                else
                {
                    Debug.LogWarning($"Duplicate key found: {data.Element}");
                }
            }

            initialized = true;
        }

        public static SO_Element Get(Element element)
        {
            if (!initialized)
                Initialize();

            if (dataDictionary.TryGetValue(element, out var data))
            {
                return data;
            }

            Debug.LogError($"Data not found: {element}");
            return null;
        }
    }
}
