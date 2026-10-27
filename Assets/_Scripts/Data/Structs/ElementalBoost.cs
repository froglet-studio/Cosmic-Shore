using System;
using UnityEngine;

namespace CosmicShore.Data
{
    /// <summary>
    /// A pilot's balance lever: extra petals in one element at match start
    /// (Docs/PILOTS_AND_ARTIFACTS.md §2.2). One petal is one element level.
    ///
    /// <para>Pilots are side-grades, so most carry none. When one pilot's kit swap plays weaker than
    /// another's, the fix is a little starting level rather than a bespoke buff, which keeps every
    /// quantitative change inside the element system. It is added on top of the slot's own starting
    /// level through the same seam as <c>SO_ArcadeGame.StartingElements</c>.</para>
    /// </summary>
    [Serializable]
    public struct ElementalBoost
    {
        [Tooltip("The element the boost goes into. Charge, Mass, Space or Time; anything else is no boost.")]
        public Element Element;

        [Tooltip("Petals added at match start. One petal = one element level. Usually 0.")]
        [Min(0)] public int Petals;

        public ElementalBoost(Element element, int petals)
        {
            Element = element;
            Petals = petals;
        }

        /// <summary>True when this adds nothing: no petals, or not one of the four elements.</summary>
        public bool IsNone() =>
            Petals <= 0 || Element is not (Element.Charge or Element.Mass or Element.Space or Element.Time);
    }
}
