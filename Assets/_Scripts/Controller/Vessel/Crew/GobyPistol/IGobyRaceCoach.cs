using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A race's coach for an AI Goby (Perch Run, <c>Controller/Arcade/PERCH_RUN.md</c>): where to point the nose and how
    /// long a dart to fire once it points there. The Goby AI turns, cruises and darts by the same calls the person's
    /// buttons make (<see cref="GobyPistolDuo.RaceCoach"/>); the coach only says where.
    /// </summary>
    public interface IGobyRaceCoach
    {
        /// <summary>The point to turn the nose onto, and the dart to fire when it is there (0: cruise, do not dart).
        /// False: no aim (the AI cruises straight on).</summary>
        bool TryRaceAim(GobyPistolDuo goby, out Vector3 aim, out float dartLength);
    }
}
