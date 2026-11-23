using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How the Thresher's Mass Mode ball trail orients its tiles (<see cref="ThresherExecutor"/>'s ball lay). Its own
    /// file so the executor does not grow (<c>check_agent_first.py</c>). Pure, so it is testable.
    /// </summary>
    public static class ThresherBallTrail
    {
        /// <summary>
        /// The ball trail's up: the last lay's up (already turned by the hull's own rotation since then, so a deliberate
        /// roll twists the ribbon) carried onto this lay's direction (parallel transport), so the flat 8 u tiles stay one
        /// ribbon as the ball swings. It used to aim each tile's up at the ship, which spins with every orbit of the ball,
        /// so neighbouring tiles were laid at unrelated roll angles (Caleb's fly test, 2026-11-18). The first lay, or one
        /// whose path runs along the old up, seeds from the direction to the ship.
        /// </summary>
        public static Vector3 Up(Vector3 previousUp, Vector3 dir, Vector3 towardShip)
        {
            Vector3 up = Vector3.ProjectOnPlane(previousUp, dir);
            if (up.sqrMagnitude > 0.04f) return up.normalized;
            up = Vector3.ProjectOnPlane(towardShip, dir);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return up.normalized;
        }
    }
}
