using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Urchin's HUD. Beyond the fleet-standard four-icon ability row (which the base class
    /// owns, in charge -> mass -> space -> time order), it shows the two things the Urchin's
    /// loop actually turns on:
    ///
    /// * **Ammo** - the volley spends it and the ride refills it, so it is the meter that ties
    ///   the two halves of the vessel together.
    /// * **Riding** - whether the vessel is latched onto a trail. Deliberately BINARY: the
    ///   Urchin either is on the ribbon or it is not, and a partial fill here would read as a
    ///   meter and invite the question of what "half attached" means.
    ///
    /// Both are driven from the Urchin's OWN components by the controller. Nothing here polls
    /// another vessel's executor by type - that compiles, returns null on every other hull, and
    /// leaves a gauge silently dead (the Squirrel's heat ring lived that way for its whole life).
    /// </summary>
    public class UrchinVesselHUDView : VesselHUDView
    {
        [Header("Ammo")]
        [Tooltip("Radial or bar fill for the spike ammo the volley spends and the ride refills.")]
        [SerializeField] Image ammoFill;
        [SerializeField] Color ammoNormalColor = Color.white;
        [SerializeField] Color ammoFullColor = Color.cyan;

        [Header("Riding")]
        [Tooltip("Lit while attached to a trail. Binary by design - see the class summary.")]
        [SerializeField] Image ridingIndicator;
        [SerializeField] Color ridingOffColor = new(1f, 1f, 1f, 0.25f);
        [SerializeField] Color ridingOnColor = Color.green;

        [Tooltip("Seconds the riding indicator takes to cross between its two states, so the " +
                 "change is visible rather than a snap. Continuity of existence applies to UI too.")]
        [SerializeField, Min(0f)] float ridingBlendSeconds = 0.15f;

        float _ridingBlend;
        bool _riding;

        public override void Initialize()
        {
            if (ammoFill)
            {
                ammoFill.fillAmount = 0f;
                ammoFill.color = ammoNormalColor;
            }

            _riding = false;
            _ridingBlend = 0f;
            if (ridingIndicator) ridingIndicator.color = ridingOffColor;
        }

        /// <param name="normalized">Ammo as a 0..1 fraction of capacity.</param>
        public void SetAmmo(float normalized)
        {
            if (!ammoFill) return;
            normalized = Mathf.Clamp01(normalized);
            ammoFill.fillAmount = normalized;
            ammoFill.color = Color.Lerp(ammoNormalColor, ammoFullColor, normalized);
        }

        public void SetRiding(bool riding) => _riding = riding;

        void Update()
        {
            if (!ridingIndicator) return;

            float target = _riding ? 1f : 0f;
            _ridingBlend = ridingBlendSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_ridingBlend, target, Time.deltaTime / ridingBlendSeconds);

            ridingIndicator.color = Color.Lerp(ridingOffColor, ridingOnColor, _ridingBlend);
        }
    }
}
