using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Honeyguide's triggers, as the hull's input sees it: the press and the release go to the crew
    /// (<see cref="HoneyguideCrew.SetTrigger"/>), which decides what the trigger does now - RT's Peck or LT's Flit, or the
    /// upgrade card on it (<c>HoneyguideCrew.Triggers.cs</c>). The Honeyguide prefab maps RightStickAction to the RT asset and
    /// LeftStickAction to the LT asset. Stateless, like every <see cref="ShipActionSO"/>: the crew on the vessel holds the state.
    /// </summary>
    [CreateAssetMenu(fileName = "HoneyguideTriggerAction", menuName = "ScriptableObjects/Vessel Actions/Honeyguide Trigger")]
    public sealed class HoneyguideTriggerActionSO : ShipActionSO
    {
        [Tooltip("0 RT (the attack: Peck), 1 LT (the move: Flit).")]
        [SerializeField, Range(0, 1)] int trigger;

        public int Trigger => trigger;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, true);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) => Send(vesselStatus, false);

        void Send(IVesselStatus vesselStatus, bool down)
        {
            var t = vesselStatus?.Transform;
            if (t && t.TryGetComponent(out HoneyguideCrew crew)) crew.SetTrigger(trigger, down);
        }
    }
}
