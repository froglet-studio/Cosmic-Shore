using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Dolphin's <b>Echo Sight</b> — hold the trigger to drop into a zoomed
    /// first-person view in which every prism standing inside the next crystal blast's destruction
    /// volume lights up. Release and the view eases back out.
    ///
    /// It fires nothing. The blast still goes off when the Dolphin strikes a crystal; the sight
    /// only lets the pilot SEE the shape they have been banking with every skim, so they can pick
    /// which way to be pointing when they take the crystal.
    ///
    /// Element → parameter: SPACE owns this ability, together with the blast it previews — Space
    /// already carries the cone further down-range, and the sight is how that reach becomes
    /// legible. Its level-5 upgrade is the blast's existing "Clean Blast" (the cone spares your own
    /// domain), authored on the explosion effect rather than here.
    /// </summary>
    [CreateAssetMenu(fileName = "EchoSightAction", menuName = "ScriptableObjects/Vessel Actions/Echo Sight")]
    public class EchoSightActionSO : ShipActionSO
    {
        [Header("View")]
        [Tooltip("Camera follow offset while sighting, in vessel-local units. Roughly the cockpit: " +
                 "a small positive Z sits the view at the nose looking down the blast axis.")]
        [SerializeField] private Vector3 sightFollowOffset = new(0f, 1.5f, 4f);
        [Tooltip("HOME field of view while sighting, in degrees. Pushed through VesselSpeedTunnel " +
                 "rather than written onto the camera, so the speed tunnel stays the single FOV " +
                 "writer and still narrows from here as the Dolphin accelerates.")]
        [SerializeField, Range(10f, 90f)] private float sightFieldOfView = 32f;
        [Tooltip("Seconds to ease all the way into the sight, and back out of it. Nothing snaps.")]
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.28f;

        [Header("Highlight")]
        [Tooltip("Peak strength of the prism highlight inside the destruction volume, 0-1.")]
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 1f;

        public Vector3 SightFollowOffset => sightFollowOffset;
        public float SightFieldOfView => sightFieldOfView;
        public float TransitionSeconds => transitionSeconds;
        public float HighlightStrength => highlightStrength;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<EchoSightActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<EchoSightActionExecutor>()?.Release(this, vesselStatus);
    }
}
