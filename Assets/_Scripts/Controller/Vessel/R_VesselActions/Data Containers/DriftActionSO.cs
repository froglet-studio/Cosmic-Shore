using CosmicShore.Core;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
    public class DriftActionSO : ShipActionSO
    {
        [SerializeField] float Mult = 1.5f;
        [SerializeField] float driftDamping = 0f;
        [SerializeField] bool isSharpDrifting;

        [Tooltip("Plays the drift start/end one-shots. Disable on a secondary drift tier that is " +
                 "bound to the same trigger as the primary tier (e.g. the Squirrel's analog drift " +
                 "stacks single + sharp on one trigger) so the SFX isn't played twice.")]
        [SerializeField] bool playDriftSfx = true;

        [SerializeField] ScriptableEventNoParam OnDriftingStarted;
        [SerializeField] ScriptableEventNoParam OnDoubleDriftingStarted;
        [SerializeField] ScriptableEventNoParam OnDriftEnded;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var t = vesselStatus.VesselTransformer;
            t.BeginDrift(Mult, driftDamping, isSharpDrifting);
            vesselStatus.IsDrifting = true;

            // Drift HUD events are a shared global SOAP channel with no vessel identity,
            // and the action pipeline replays StartAction on every peer - AI pilots drive
            // it too. Raise the HUD event AND play the 2D one-shot SFX only for the
            // locally-owned vessel: an AI (or remote) pilot toggling drift would otherwise
            // spam full-volume start/end cues over the local pilot's own feedback.
            // Remote/AI vessels keep only spatialized per-vessel audio (which is already
            // local-gated, e.g. DriftAudioController). Physics above still runs on all
            // peers for replication.
            if (!vesselStatus.IsLocalUser) return;

            if (playDriftSfx)
                execs.AudioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftStart);

            if (isSharpDrifting)
                OnDoubleDriftingStarted.Raise();
            else
                OnDriftingStarted.Raise();
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var t = vesselStatus.VesselTransformer;
            t.EndDrift(isSharpDrifting);
            vesselStatus.IsDrifting = t.IsDriftActive;

            // See StartAction - only the local owner raises the drift HUD event + SFX.
            if (!vesselStatus.IsLocalUser) return;

            if (playDriftSfx)
                execs.AudioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftEnd);
            OnDriftEnded.Raise();
        }
    }
}
