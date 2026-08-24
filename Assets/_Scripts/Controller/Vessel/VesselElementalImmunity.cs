using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shared declarative driver for the general elemental-debuff immunity state
    /// (<see cref="ResourceSystem.SetElementalDebuffImmunity"/> /
    /// <see cref="IVesselStatus.IsImmuneToElementalDebuff"/>). Drop it on a vessel root, pick the window it
    /// holds immunity in, and optionally gate that on an element's level-5 upgrade. Nothing here is
    /// vessel-specific — it is one component any vessel (or mode) can wire, which is the point: the
    /// immunity is a platform state, not a Sparrow feature.
    ///
    /// A window is TWO declarations, not one: WHEN the ward holds (<see cref="condition"/> +
    /// <see cref="upgradeGate"/>) and WHAT it wards against (<see cref="wardedSources"/>). The
    /// second exists because "immune to danger prisms" and "immune to an opposing pilot's weapon"
    /// are different promises — see <see cref="ElementalDebuffSources"/>.
    ///
    /// Wired today:
    ///   Sparrow — <see cref="Condition.WhileBoosting"/> gated on <see cref="Element.Time"/>,
    ///             warding <see cref="ElementalDebuffSources.All"/>
    ///             (the TIME level-5 upgrade: boost is the shield).
    ///   Serpent — <see cref="Condition.WhileTranslationRestricted"/>, ungated, warding
    ///             <see cref="ElementalDebuffSources.All"/>
    ///             (stopping to weave is what makes you untouchable — no unlock needed).
    ///   Dolphin — <see cref="Condition.WhileDrifting"/> gated on <see cref="Element.Time"/>,
    ///             warding <see cref="ElementalDebuffSources.DangerPrism"/> ALONE
    ///             (the TIME level-5 upgrade: the drift is the ward — against the ARENA, not
    ///             against other pilots. Unscoped it also cancelled the Dolphin crystal blast,
    ///             which is the whole scoring event of The Bends, a mode in which every pilot is
    ///             a Dolphin: the trailing pilot's comeback buff handed them a hard counter to
    ///             the only way they could be scored on).
    ///
    /// The grant is keyed on THIS component (the grantor — not to be confused with the debuff
    /// SOURCE class it wards), so a second holder (another ability, a game mode) can hold immunity
    /// at the same time without either clearing the other. It is revoked in
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

            /// <summary>Held while the vessel is drifting (<c>IVesselStatus.IsDrifting</c>).</summary>
            WhileDrifting = 3,
        }

        [Header("Immunity window")]
        [Tooltip("When this vessel holds elemental-debuff immunity. The state itself is general — this " +
                 "only picks the window.")]
        [SerializeField] Condition condition = Condition.Always;

        [Tooltip("Optional level-5 gate: immunity only holds while this element's qualitative upgrade " +
                 "is active (ElementalAbilityMapSO). Set to None for an ungated, always-earned state " +
                 "like the Serpent's stopped stance.")]
        [SerializeField] Element upgradeGate = Element.None;

        [Tooltip("WHICH elemental debuffs this window wards off. Everything = the total ward " +
                 "(Sparrow, Serpent). Narrow it to promise less: the Dolphin's Drift Ward is " +
                 "DangerPrism only, so drifting protects it from the arena without cancelling an " +
                 "opposing pilot's blast. A debuff that names no class counts as Other, so it is " +
                 "stopped only by a ward that covers everything.")]
        [SerializeField] ElementalDebuffSources wardedSources = ElementalDebuffSources.All;

        IVesselStatus _status;

        // What is currently granted, so an inspector edit to wardedSources mid-play re-grants
        // rather than leaving the vessel warded against the old set.
        ElementalDebuffSources _granted = ElementalDebuffSources.None;

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

            // A ward against nothing is not a window, it is a disabled component.
            if (wardedSources == ElementalDebuffSources.None) return false;

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
                Condition.WhileDrifting              => _status.IsDrifting,
                Condition.Always                     => true,
                _                                    => false,
            };
        }

        void Grant(bool immune)
        {
            var wanted = immune ? wardedSources : ElementalDebuffSources.None;
            if (wanted == _granted) return;

            var resources = _status?.ResourceSystem;
            if (!resources) return;

            resources.SetElementalDebuffImmunity(this, immune, wanted);
            _granted = wanted;
        }
    }
}
