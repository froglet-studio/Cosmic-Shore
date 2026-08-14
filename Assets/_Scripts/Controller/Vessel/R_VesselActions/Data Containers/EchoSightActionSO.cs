using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Dolphin's <b>Echo Sight</b> — hold the trigger and every prism standing
    /// inside the next crystal blast's destruction volume lights up. Release and the highlight
    /// fades back out.
    ///
    /// It fires nothing and it moves nothing. The blast still goes off when the Dolphin strikes a
    /// crystal, and the camera is left entirely alone: the sight only reveals the shape the pilot
    /// has been banking with every skim, so they can pick which way to be pointing when they take
    /// the crystal.
    ///
    /// Element → parameter: SPACE owns this ability, together with the blast it previews — Space
    /// already carries the cone further down-range, and the sight is how that reach becomes
    /// legible. Its level-5 upgrade is the blast's existing "Clean Blast" (the cone spares your own
    /// domain), authored on the explosion effect rather than here.
    /// </summary>
    [CreateAssetMenu(fileName = "EchoSightAction", menuName = "ScriptableObjects/Vessel Actions/Echo Sight")]
    public class EchoSightActionSO : ShipActionSO
    {
        [Header("Highlight")]
        [Tooltip("Seconds the highlight takes to fade all the way in, and back out. Nothing pops - " +
                 "continuity of existence covers a targeting overlay as much as it covers mass.")]
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.28f;
        [Tooltip("Peak strength of the prism highlight inside the destruction volume, 0-1.")]
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 1f;

        public float TransitionSeconds => transitionSeconds;
        public float HighlightStrength => highlightStrength;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<EchoSightActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<EchoSightActionExecutor>()?.Release(this, vesselStatus);
    }
}
