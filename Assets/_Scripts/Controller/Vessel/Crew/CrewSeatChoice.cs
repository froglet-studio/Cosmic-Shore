using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The seat a person picked on a crew vessel, remembered on this device per hull class (Garrett, 2026-10-21: "wrasse
    /// and grouper, are two seats. players can select, change seats etc. both can be played on mobile or pc"). Seat 0 is
    /// the hull (the Grouper), seat 1 the crew seat (the Wrasse). A seat writes it whenever the person picks or swaps
    /// (keys 1 / 2, the D-pad, a double tap on the small view), and reads it when the person next takes the hull, so they
    /// start where they last chose to be. A seat picker in a menu (the Vessel Changer's stations, a launch card) writes the
    /// same value (KITE_NAVIGATOR.md, "Two humans, two seats").
    ///
    /// <para>A device preference, not progress: <see cref="PlayerPrefs"/>, never cloud data. Online, it is the seat a
    /// person ASKS for; the server decides who sits where.</para>
    /// </summary>
    public static class CrewSeatChoice
    {
        const string KeyPrefix = "CrewSeat.";

        /// <summary>The seat last picked on <paramref name="hull"/>: 0 the hull (the default), 1 the crew seat.</summary>
        public static int Get(VesselClassType hull) => Mathf.Max(0, PlayerPrefs.GetInt(Key(hull), 0));

        /// <summary>Remember <paramref name="seat"/> as the one picked on <paramref name="hull"/>.</summary>
        public static void Set(VesselClassType hull, int seat)
        {
            seat = Mathf.Max(0, seat);
            if (Get(hull) == seat) return;
            PlayerPrefs.SetInt(Key(hull), seat);
        }

        // only on a pick or a bind, never per frame
        static string Key(VesselClassType hull) => KeyPrefix + hull;
    }
}
