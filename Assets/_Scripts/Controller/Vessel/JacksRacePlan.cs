using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One herd-and-travel cycle for an AI Jacks in a race (Decoy Run, <c>Controller/Arcade/DecoyRun/DECOY_RUN.md</c>), in
    /// the Jacks' own verbs and nothing else: the jacks run on the board to their waypoints, a decoy is laid where a jack
    /// stands (a trigger pull), the jacks take off (the hull jumps to halfway between them, the shipped take-off: a
    /// teleport, so it threads nothing and only puts the hull on a line), and the hull flies fast to one decoy and then
    /// the other (the triggers while netting). A human does exactly this with two thumbs and two triggers; the plan only
    /// says where the thumbs go.
    /// </summary>
    public struct JacksRacePlan
    {
        /// <summary>Jack 0 runs to <see cref="Jack0Via"/> then to <see cref="Jack0To"/>; jack 1 likewise. A jack with nothing
        /// to lay on the way has its via equal to its end. World positions; each is laid onto the board.</summary>
        public Vector3 Jack0Via, Jack0To, Jack1Via, Jack1To;

        /// <summary>The decoy slot each jack lays at its via point (0 the left trigger's, 1 the right's with Time 5), or -1.</summary>
        public int Jack0Lays, Jack1Lays;

        /// <summary>After take-off, the decoy slots the hull flies to, in order (-1 for none).</summary>
        public int FirstTravel, SecondTravel;

        public static JacksRacePlan Jump(Vector3 a, Vector3 b) => new()
        {
            Jack0Via = a, Jack0To = a, Jack1Via = b, Jack1To = b,
            Jack0Lays = -1, Jack1Lays = -1, FirstTravel = -1, SecondTravel = -1,
        };
    }
}
