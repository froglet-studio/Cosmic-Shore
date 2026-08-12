using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shared declarative driver for the general elemental-debuff immunity state
    /// (<see cref="ResourceSystem.SetElementalDebuffImmunity"/> /
    /// <see cref="IVesselStatus.IsElementallyImmune"/>). Drop it on a vessel root, pick the window it
    /// holds immunity in, and optionally gate that on an element's level-5 upgrade. Nothing here is
    /// vessel-specific — it is one component any vessel (or mode) can wire, which is the point: the
    /// immunity is a platform state, not a Sparrow feature.
    ///
    /// Wired today:
    ///   Sparrow — <see cref="Condition.WhileBoosting"/> gated on <see cref="Element.Time"/>
    ///             (the TIME level-5 upgrade: boost is the shield).
    ///   Serpent — <see cref="Condition.WhileTranslationRestricted"/>, ungated
    ///             (stopping to weave is what makes you untouchable — no unlock needed).
    ///
    /// The grant is source-keyed on THIS component, so a second holder (another ability, a game mode)
    /// can hold immunity at the same time without either clearing the other. It is revoked in
    /// OnDisable, so a vessel swap / pool return can never strand an immune vessel.
    ///
    /// Evaluated for AI as well as human pilots: the conditions read replicated/owner state
    /// (<c>IsBoosting</c>, <c>IsTranslationRestricted</c>, <c>IsUpgradeActive</c>) that AI reaches
    /// through the very same executors, so nothing extra is wired for AI.
    /// </summary>
    public class VesselElementalImmunity : MonoBehaviour
    {
        public enum Condition
        {
            /// <summary>Held for as long as the vessel exists (subject to the upgrade gate).</summary>
            Always = 0,

            /// <summary>Held while the boost input is engaged (<c>IVesselStatus.IsBoosting</c>).</summary>
            WhileBoosting = 1,

            /// <summary>Held while the vessel is stopped / translation-restricted (turret + Serpent stances).</summary>
            WhileTranslationRestricted = 2,
        }

        [Header("Immunity window")]
        [Tooltip("When this vessel holds elemental-debuff immunity. The state itself is general — this " +
                 "only picks the window.")]
        [SerializeField] Condition condition = Condition.Always;

        [Tooltip("Optional level-5 gate: immunity only holds while this element's qualitative upgrade " +
                 "is active (ElementalAbilityMapSO). Set to None for an ungated, always-earned state " +
                 "like the Serpent's stopped stance.")]
        [SerializeField] Element upgradeGate = Element.None;

        IVesselStatus _status;
        bool _granted;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();

            // Warn and degrade, never fail silently: with no VesselStatus this component can only
            // ever resolve to "not immune", which looks exactly like a correctly locked upgrade.
            if (_status == null)
                Debug.LogWarning($"[VesselElementalImmunity] {name} has no VesselStatus, so it can " +
                                 "never grant elemental-debuff immunity. Move this component onto " +
                                 "the vessel ROOT (the GameObject carrying VesselStatus).", this);
        }

        void OnDisable() => Grant(false);

        void Update() => Grant(ShouldBeImmune());

        bool ShouldBeImmune()
        {
            if (_status == null) return false;

            // Outcome-affecting unlocks resolve through IsUpgradeActive (replicated unlock bits), never a
            // raw local level read — a local read desyncs the state across peers.
            if (upgradeGate != Element.None && upgradeGate != Element.Omni)
            {
                var abilities = _status.ElementalAbilityHandler;
                if (!abilities || !abilities.IsUpgradeActive(upgradeGate)) return false;
            }

            return condition switch
            {
                Condition.WhileBoosting              => _status.IsBoosting,
                Condition.WhileTranslationRestricted => _status.IsTranslationRestricted,
                Condition.Always                     => true,
                _                                    => false,
            };
        }

        void Grant(bool immune)
        {
            if (immune == _granted) return;

            var resources = _status?.ResourceSystem;
            if (!resources) return;

            resources.SetElementalDebuffImmunity(this, immune);
            _granted = immune;
        }
    }
}
