using FMODUnity;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Presentation for the swordfish's strike - the <see cref="SharkJawDriver"/> pattern: it
    /// reads <see cref="SwordfishFauna.State"/> and drives the Animator's two bools (`Pursuing`,
    /// `Charging`), so the artist's charge take plays as the creature coils and lunges (tuck ->
    /// hold -> flare), and it fires the two strike sounds. It decides nothing: every state it
    /// mirrors was decided by the fauna's own state machine.
    ///
    /// Perf: one enum compare per frame; animator writes only on a state change.
    ///
    /// Audio law: both events are inspector-exposed and ship EMPTY - an unwired slot is a
    /// visible TODO for the audio owner, a borrowed event is one nobody ever finds.
    ///
    /// Replication note: a puppet (client copy of a networked swordfish) never runs the strike
    /// state machine, so it shows the swim cycle through a charge. The strike state is not on
    /// the wire yet - the same limitation FaunaNetworkSync records for every presentation state.
    /// </summary>
    public class SwordfishChargeDriver : MonoBehaviour
    {
        static readonly int PursuingId = Animator.StringToHash("Pursuing");
        static readonly int ChargingId = Animator.StringToHash("Charging");

        [Header("Animator")]
        [Tooltip("The model's Animator. Found in children when left empty (the nested FBX carries it).")]
        [SerializeField] Animator animator;
        [Tooltip("Assigned to the Animator if it has no controller - the nested model's Animator " +
                 "is authored controller-less, and this is the reference that survives a model re-export.")]
        [SerializeField] RuntimeAnimatorController controller;

        [Header("Audio")]
        [SerializeField, Tooltip("FMOD event played once when the wind-up begins. Leave empty for silence.")]
        EventReference telegraphEvent;
        [SerializeField, Tooltip("FMOD event played once when the lunge begins. Leave empty for silence.")]
        EventReference lungeEvent;

        [Inject] AudioSystem audioSystem;

        SwordfishFauna _fauna;
        SwordfishFauna.StrikeState _shown = SwordfishFauna.StrikeState.Cruise;

        void Awake()
        {
            _fauna = GetComponentInParent<SwordfishFauna>();
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (animator && !animator.runtimeAnimatorController && controller)
                animator.runtimeAnimatorController = controller;
        }

        void Update()
        {
            if (!_fauna) return;
            var state = _fauna.State;
            if (state == _shown) return;
            _shown = state;

            if (animator && animator.runtimeAnimatorController)
            {
                animator.SetBool(PursuingId, _fauna.IsPursuingVessel);
                animator.SetBool(ChargingId, _fauna.IsCharging);
            }

            switch (state)
            {
                case SwordfishFauna.StrikeState.Telegraph:
                    Play(telegraphEvent);
                    break;
                case SwordfishFauna.StrikeState.Lunge:
                    Play(lungeEvent);
                    break;
            }
        }

        void Play(EventReference reference)
        {
            if (reference.IsNull) return;   // silence, never a substitute event
            if (audioSystem) audioSystem.PlaySFXEvent(reference, transform.position);
            else FMODOneShotVolumeHelper.PlaySFXOneShot(reference, transform.position, 1f);
        }
    }
}
