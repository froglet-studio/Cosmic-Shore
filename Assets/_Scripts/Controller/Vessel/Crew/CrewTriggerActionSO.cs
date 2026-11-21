using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>A crew seat that owns the hull's triggers itself: the press and the release come from
    /// <see cref="CrewTriggerActionSO"/>, and the seat decides what a trigger does in the seat it is in.</summary>
    public interface ICrewTriggers
    {
        /// <param name="trigger">0 RT, 1 LT.</param>
        void SetTrigger(int trigger, bool down);
    }

    /// <summary>
    /// One of a crew hull's triggers, as the hull's input sees it: the press and the release go to the seat on the hull
    /// (<see cref="ICrewTriggers"/>: the Wrasse's <see cref="KiteNavigator"/>, the Orca's <see cref="OrcaCrew"/>). The hull
    /// maps RightStickAction to the RT asset and LeftStickAction to the LT asset - the events the pad's triggers, the
    /// keyboard's shifts AND a phone's thumb lifts all raise (lift the left thumb = RT, the right = LT), so a seat that reads
    /// its trigger here works on every device. A seat that read <c>InputStatus.RightTriggerAnalog</c> instead worked on a
    /// pad and a keyboard only: no phone writes the analog triggers (Garrett, 2026-10-21: "The grouper can not use boost on
    /// mobile"). <see cref="HoneyguideTriggerActionSO"/> is the same shape for the Honeyguide's crew. Stateless, like every
    /// <see cref="ShipActionSO"/>: the seat holds the state.
    /// </summary>
    [CreateAssetMenu(fileName = "CrewTriggerAction", menuName = "ScriptableObjects/Vessel Actions/Crew Trigger")]
    public sealed class CrewTriggerActionSO : ShipActionSO
    {
        [Tooltip("0 RT (a pad's right trigger, right Shift, a phone's left-thumb lift), 1 LT (the left trigger, left " +
                 "Shift, the right-thumb lift).")]
        [SerializeField, Range(0, 1)] int trigger;

        public int Trigger => trigger;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, true);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, false);

        void Send(IVesselStatus vesselStatus, bool down)
        {
            var t = vesselStatus?.Transform;
            if (t && t.TryGetComponent(out ICrewTriggers seat)) seat.SetTrigger(trigger, down);
        }
    }
}
