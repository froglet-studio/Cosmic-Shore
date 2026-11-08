using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A crew one of whose seats plays held upright (Shombith, 2026-11-08: "the vessels that are more 2D friendly, just
    /// goby and pistol: when you change your orientation the game swaps to pistol's view"). On a phone, while the local
    /// person sits on this crew's hull, the screen may turn upright; turning it upright takes <see cref="UprightSeat"/>,
    /// turning it back to landscape takes the helm. Every other hull keeps the screen landscape and turns nothing.
    /// </summary>
    public interface IUprightSeat
    {
        /// <summary>The seat a phone held upright takes (past the helm, 0).</summary>
        int UprightSeat { get; }
    }

    /// <summary>
    /// The local person's phone turned while they sit on a crew with an <see cref="IUprightSeat"/>: the turn moves them
    /// between the helm and that seat through the crew's own <see cref="ICrewStations.TakeSeat"/>, the way the swap
    /// gesture does. Ticked by <see cref="LocalCrewStation"/>. A turn is an EDGE: the first tick on a hull only takes the
    /// upright seat if the phone is already upright (a person who came from the upright menu into the Goby lands in the
    /// Pistol's view), and a seat changed by the swap gesture or a double tap stays until the phone turns again.
    /// </summary>
    internal sealed class CrewOrientation
    {
        bool _allowing;
        bool? _wasUpright;

        static bool Phone => Application.isMobilePlatform;

        /// <summary>One tick: <paramref name="crew"/> is the person's own crew (or null), <paramref name="localSeat"/>
        /// their seat on it (-1 while no local person is aboard), <paramref name="riding"/> whether they ride another
        /// hull, <paramref name="paused"/> whether the game is paused (a turn then waits for the game to go on).</summary>
        public void Tick(ICrewStations crew, int localSeat, bool riding, bool paused)
        {
            int upright = UprightSeatOf(Phone, crew, localSeat, riding);
            bool allow = upright > 0;
            if (allow != _allowing)
            {
                _allowing = allow;
                _wasUpright = null;
                ScreenOrientationPolicy.Allow(ScreenOrientationPolicy.Source.Seat, allow);
            }
            if (!allow || paused) return;
            bool isUpright = Screen.height > Screen.width;
            int take = SeatFor(isUpright, _wasUpright, localSeat, upright);
            _wasUpright = isUpright;
            if (take >= 0) crew.TakeSeat(take);
        }

        /// <summary>The person left their hull, or it went away: the screen goes back to landscape.</summary>
        public void Release()
        {
            if (!_allowing) return;
            _allowing = false;
            _wasUpright = null;
            ScreenOrientationPolicy.Allow(ScreenOrientationPolicy.Source.Seat, false);
        }

        /// <summary>The upright seat the person's phone may turn into, or -1: a phone, the person's own crew with an
        /// <see cref="IUprightSeat"/>, a seat of theirs on it, and not riding another hull.</summary>
        public static int UprightSeatOf(bool phone, ICrewStations crew, int localSeat, bool riding)
        {
            if (!phone || riding || localSeat < 0 || crew is not IUprightSeat seat) return -1;
            return seat.UprightSeat > 0 && seat.UprightSeat < crew.SeatCount ? seat.UprightSeat : -1;
        }

        /// <summary>The seat a turn takes, or -1 for none. Upright takes <paramref name="uprightSeat"/> on a turn and on
        /// the first tick; back to landscape takes the helm from the upright seat on a turn only.</summary>
        public static int SeatFor(bool upright, bool? wasUpright, int localSeat, int uprightSeat)
        {
            if (wasUpright == upright) return -1;
            if (upright) return localSeat == uprightSeat ? -1 : uprightSeat;
            return wasUpright.HasValue && localSeat == uprightSeat ? 0 : -1;
        }
    }
}
