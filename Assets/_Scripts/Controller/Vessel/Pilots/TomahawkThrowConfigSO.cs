using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tomahawk's numbers (Docs/Pilots/PILOTS_AND_ARTIFACTS.md; pilot.rhino.tomahawk): with Tomahawk in the Rhino's seat,
    /// a two-tap string on one trigger THROWS the energy sword to that side instead of playing its flourish. The blade
    /// spins out on a loop beside the hull - out and forward, wide, behind, home - cutting everything it passes through
    /// with the sword's own contact effects (a rival vessel it hits is a scored hit that steals petals, as any sword
    /// hit is), and returns to the fingers. The loop is posed in the hull's frame, so it swings with your turn.
    /// Read by <see cref="ShieldSwipeActionExecutor"/> through <c>VesselPilotLoadout.TuningOf</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "TomahawkThrowConfig", menuName = "ScriptableObjects/Pilots/Tomahawk Throw Config")]
    public class TomahawkThrowConfigSO : ScriptableObject
    {
        [Header("Which strings throw")]
        [Tooltip("The two-tap string that throws to the RIGHT (its flourish is given up while Tomahawk flies).")]
        [SerializeField] string rightSequence = "RR";
        [Tooltip("The two-tap string that throws to the LEFT.")]
        [SerializeField] string leftSequence = "LL";

        [Header("Flight")]
        [Tooltip("Seconds from release to the blade's return to the hull.")]
        [SerializeField, Min(0.2f)] float flightSeconds = 1.1f;
        [Tooltip("How far out to the side the loop reaches, in blade lengths (so it grows with Space, as the blade does).")]
        [SerializeField, Min(0f)] float sideReachBlades = 2.2f;
        [Tooltip("How far ahead (and, on the way home, behind) the loop reaches, in blade lengths.")]
        [SerializeField, Min(0f)] float forwardReachBlades = 1.4f;
        [Tooltip("Full turns a second the blade spins, flat, while it flies.")]
        [SerializeField, Min(0f)] float spinTurnsPerSecond = 3f;
        [Tooltip("Seconds the blade takes to leave the fingers' pose and join the loop.")]
        [SerializeField, Min(0.01f)] float blendInSeconds = 0.08f;
        [Tooltip("Seconds the blade takes to settle back into the fingers' pose once home.")]
        [SerializeField, Min(0.01f)] float returnBlendSeconds = 0.15f;

        [Header("Feedback")]
        [Tooltip("Played on the blade when it is thrown. Shipped empty: an unwired slot is a visible TODO.")]
        [SerializeField] EventReference throwSound;
        [Tooltip("Toasted for the pilot who threw it (the game's toast container, top right). Empty shows nothing.")]
        [SerializeField] string toastLine = "Tomahawk";

        public string RightSequence => rightSequence;
        public string LeftSequence => leftSequence;
        public float FlightSeconds => flightSeconds;
        public float SideReachBlades => sideReachBlades;
        public float ForwardReachBlades => forwardReachBlades;
        public float SpinTurnsPerSecond => spinTurnsPerSecond;
        public float BlendInSeconds => blendInSeconds;
        public float ReturnBlendSeconds => returnBlendSeconds;
        public EventReference ThrowSound => throwSound;
        public string ToastLine => toastLine;

        /// <summary>+1 right, -1 left, 0 when this string does not throw.</summary>
        public int SideOf(string sequence) =>
            sequence == rightSequence ? 1 : sequence == leftSequence ? -1 : 0;

        /// <summary>
        /// The loop's offset from the mount at <paramref name="u"/> (0..1 of the flight), in the hull's frame, for a blade of
        /// <paramref name="bladeLength"/>: out and forward, widest beside the hull at half way, behind, home at 1. Pure, so
        /// every peer draws the same path.
        /// </summary>
        public Vector3 LoopOffset(float u, int side, float bladeLength)
        {
            float theta = 2f * Mathf.PI * Mathf.Clamp01(u);
            float a = sideReachBlades * bladeLength;
            float f = forwardReachBlades * bladeLength;
            return new Vector3(side * a * 0.5f * (1f - Mathf.Cos(theta)), 0f, f * Mathf.Sin(theta));
        }
    }
}
