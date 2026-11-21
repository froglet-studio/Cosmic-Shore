using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What happened on the kite's board, for its juice (<see cref="KiteNavigatorJuice"/>).</summary>
    internal enum KiteBurstKind { Fizzle = 0, Space = 1, Mass = 2, Time = 3, Charge = 4, Pop = 5, Steal = 6, Bloom = 7 }

    /// <summary>A mote a loop lassoed: where it was on the board, its petals' turn and its element (Element - 1).</summary>
    internal struct KiteBurstMote
    {
        public float X, Z, Spin;
        public int Element;
    }

    /// <summary>
    /// One event the seat raises for the view to turn into juice (Garrett, 2026-10-21: "lets loose as many of the flat ring
    /// effects as we can and juice up each effect"). A loop's burst carries the motes it lassoed, so their own petals become
    /// the effect; Mass's carries where its prisms will be laid. Pooled by <see cref="KiteNavigator"/>: no allocation per pop.
    /// </summary>
    internal sealed class KiteBurst
    {
        public KiteBurstKind Kind;
        public int Element;                // Bloom's element
        public float X, Z, T0, MaxScale;   // where on the board, when; Space's blast scale
        public Vector3 World;              // where in the world (a steal's rival, a pop's prism)
        public readonly List<KiteBurstMote> Motes = new();
        public readonly List<(Vector3 pos, Quaternion rot)> Targets = new();   // Mass: its prisms, in laying order
    }
}
