using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ShipElementStatsHandler : MonoBehaviour
    {
        [Serializable]
        public struct ElementStat
        {
            public string StatName;
            public Element Element;

            public ElementStat(string statName, Element element)
            {
                StatName = statName;
                Element = element;
            }
        }

        [Header("Elemental Stats")]
        [SerializeField] protected List<ElementStat> ElementStats = new();

        public virtual void BindElementalFloat(string name, Element element)
        {
            if (ElementStats.TrueForAll(es => es.StatName != name))
                ElementStats.Add(new ElementStat(name, element));
        }
    }
}
