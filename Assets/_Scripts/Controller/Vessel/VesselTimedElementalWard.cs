using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The EVENT-DRIVEN half of the platform's elemental-debuff immunity — a ward a pickup, an
    /// ability or a mode can hand a pilot for a FIXED NUMBER OF SECONDS.
    ///
    /// <para>Its sibling <see cref="VesselElementalImmunity"/> is the CONDITION-driven half:
    /// "while boosting", "while stopped", "while drifting". Neither can express the other — a
    /// window that opens on an event and closes on a clock has no condition to poll, and a
    /// condition-held ward has no duration to count. They are separate components so a vessel can
    /// carry both (the Sparrow does) and so a caller can find the one it means: grants are keyed
    /// on the granting component, so two wards on one hull compose instead of clearing each
    /// other.</para>
    ///
    /// <para><b>What it wards is authored here; how long is the caller's business.</b> The mask
    /// is this ward's PROMISE (the same split <see cref="VesselElementalImmunity"/> uses), while
    /// the duration is what the thing that granted it paid for. Re-granting REFRESHES rather than
    /// stacking: <see cref="Grant"/> takes the longer of the two remaining times, so collecting
    /// two crystals in quick succession cannot bank sixteen seconds of immunity.</para>
    ///
    /// <para>Wired today: the <b>Sparrow</b>, warding <see cref="ElementalDebuffSources.All"/> on
    /// an omni-crystal pickup. That is deliberately checked against the mono-vessel modes it
    /// flies in (Dog Fight, Salvo, Wildlife Liberation) — none of them scores on an event a
    /// debuff ward can deny, so a warded pilot is still fully scoreable. Anything wired here in
    /// future must re-run that check: a defensive ability is a MODE-level rule in every mode
    /// where its vessel is mandatory, and the comeback system hands level-5 kit to whoever is
    /// LOSING.</para>
    ///
    /// <para>The grant is revoked in <see cref="OnDisable"/>, so a vessel swap, a pool return or
    /// a turn end can never strand an immune vessel.</para>
    /// </summary>
    public class VesselTimedElementalWard : MonoBehaviour
    {
        [Tooltip("WHICH elemental debuffs this ward stops while it is up. Everything = the total " +
                 "ward. Narrow it to promise less — a debuff that names no class counts as Other, " +
                 "so it is stopped only by a ward that covers everything.")]
        [SerializeField] ElementalDebuffSources wardedSources = ElementalDebuffSources.All;

        /// <summary>Seconds of ward left, 0 when nothing is held. For a HUD readout.</summary>
        public float Remaining { get; private set; }

        /// <summary>True while this ward is standing.</summary>
        public bool IsActive => Remaining > 0f;

        /// <summary>What this ward promises to stop — read by a HUD, never by the debuff gate
        /// (that asks <see cref="ResourceSystem.IsImmuneTo"/> about one class).</summary>
        public ElementalDebuffSources WardedSources => wardedSources;

        /// <summary>Raised whenever the remaining time changes state (granted / expired), for
        /// HUD and VFX consumers. Carries the seconds remaining; 0 means the ward just dropped.</summary>
        public event Action<float> OnWardChanged;

        IVesselStatus _status;
        bool _granted;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();

            // Warn and degrade: with no VesselStatus this can only ever resolve to "not immune",
            // which is indistinguishable on screen from a pickup that simply grants nothing.
            if (_status == null)
                CSDebug.LogWarning($"[{nameof(VesselTimedElementalWard)}] {name} has no " +
                    "VesselStatus, so it can never grant a ward. Move this component onto the " +
                    "vessel ROOT (the GameObject carrying VesselStatus).", this);
        }

        /// <summary>
        /// Hold the ward for <paramref name="seconds"/>. REFRESHES rather than stacks — a second
        /// grant while one is standing takes the longer of the two.
        /// </summary>
        public void Grant(float seconds)
        {
            if (_status == null || seconds <= 0f) return;
            if (wardedSources == ElementalDebuffSources.None) return;

            Remaining = Mathf.Max(Remaining, seconds);
            Apply(true);
            OnWardChanged?.Invoke(Remaining);
        }

        /// <summary>Drops the ward immediately. Idempotent.</summary>
        public void Clear()
        {
            if (Remaining <= 0f && !_granted) return;
            Remaining = 0f;
            Apply(false);
            OnWardChanged?.Invoke(0f);
        }

        void OnDisable()
        {
            Remaining = 0f;
            Apply(false);
        }

        void Update()
        {
            if (Remaining <= 0f) return;

            Remaining -= Time.deltaTime;
            if (Remaining > 0f) return;

            Remaining = 0f;
            Apply(false);
            OnWardChanged?.Invoke(0f);
        }

        void Apply(bool immune)
        {
            if (immune == _granted) return;

            var resources = _status?.ResourceSystem;
            if (!resources) return;

            resources.SetElementalDebuffImmunity(this, immune, wardedSources);
            _granted = immune;
        }
    }
}
