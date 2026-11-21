using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>A crew seat that owns some of the hull's face buttons itself: the press and the release come from
    /// <see cref="CrewButtonActionSO"/>, and the seat decides what a button does in the seat it is in.</summary>
    public interface ICrewButtons
    {
        /// <param name="button">The seat's own numbering (the Goby's: 0 primer, 1 flares).</param>
        void SetButton(int button, bool down);
    }

    /// <summary>
    /// One of a crew seat's own buttons, as the hull's input sees it: the press and the release go to the seat on the hull
    /// (<see cref="ICrewButtons"/>: the Goby's <see cref="GobyPistolDuo"/>). The hull binds a face-button input to it -
    /// a pad's face button, its keyboard key and the seat's phone button all raise that one event - so the seat reads its
    /// ability the way every hull's ability is read: through the hull's bindings, on every device, behind the autopilot
    /// gate (Garrett, 2026-10-21: "each seat has its own ui and controls. no need to use the dpad in a weird way"). The
    /// trigger twin is <see cref="CrewTriggerActionSO"/>. Stateless, like every <see cref="ShipActionSO"/>: the seat holds
    /// the state.
    /// </summary>
    [CreateAssetMenu(fileName = "CrewButtonAction", menuName = "ScriptableObjects/Vessel Actions/Crew Button")]
    public sealed class CrewButtonActionSO : ShipActionSO
    {
        [Tooltip("Which of the seat's buttons this is, in the seat's own numbering (the Goby: 0 primer, 1 flares).")]
        [SerializeField, Range(0, 3)] int button;

        public int Button => button;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, true);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, false);

        void Send(IVesselStatus vesselStatus, bool down)
        {
            var t = vesselStatus?.Transform;
            if (t && t.TryGetComponent(out ICrewButtons seat)) seat.SetButton(button, down);
        }
    }
}
