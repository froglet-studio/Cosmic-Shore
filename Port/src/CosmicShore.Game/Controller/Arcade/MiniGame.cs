// PORT Deviation — type-preserving STATIC SHELL of the legacy MiniGame base class
// (original: Assets/_Scripts/Controller/Arcade/MiniGame.cs, 482 lines — the
// pre-MiniGameControllerBase game driver; the port's controller chain replaced the
// instance side). Landed in Arc F 2b-ii because the arcade views/modal read and
// write its STATIC launch-configuration surface (player count, intensity, vessel
// type, starting resources). Statics are verbatim; the instance machinery stays
// with the legacy class and is NOT ported (upstream still carries it for old
// single-player scenes only).
using CosmicShore.Data;

namespace CosmicShore.Core
{
    public static class MiniGame
    {
        public static int NumberOfPlayers = 1;  // TODO: P1 - support excluding single player games (e.g for elimination)
        public static int IntensityLevel = 1;
        public static bool IsDailyChallenge = false;
        public static bool IsMission = false;
        public static bool IsTraining = false;
        static VesselClassType _playerVesselType = VesselClassType.Dolphin;
        static bool playerShipTypeInitialized;

        public static VesselClassType PlayerVesselType
        {
            get => _playerVesselType;
            set
            {
                _playerVesselType = value;
                playerShipTypeInitialized = true;
            }
        }

        public static ResourceCollection ResourceCollection = new(.5f, .5f, .5f, .5f);
    }
}
