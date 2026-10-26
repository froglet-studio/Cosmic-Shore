using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's OMNI-CRYSTAL ability, the <b>Surge</b> (reworked 2026-10-26, Garrett: "zoom out, re
    /// center the camera on the rhino and surround it with swords. they should still be controlled by
    /// the triggers. this can be an 8 second state"): collecting an omni crystal fills the sword's
    /// energy and, for <see cref="surgeSeconds"/>, holds the blade ENERGIZED with no stance and no cost,
    /// raises a ring of copies of the blade round the hull that the triggers swing with it, and pulls
    /// the pilot's camera straight back and onto the hull so the whole ring is on screen
    /// (<see cref="RhinoSurgeSwordRing"/>). It also lends the hull every element for the same length and
    /// lays the Mass afterimages THROUGHOUT the surge. Nothing resizes: the burst that swelled the blade
    /// is gone with the rest of the sword's resizing. When the surge's energize goes out, the ordinary
    /// afterimage window opens after it. See <c>R_VesselActions/RHINO_ENERGY_SWORD.md</c> § "The
    /// omni-crystal Surge".
    ///
    /// <para>Replaces the meter-fill this container used to carry (RhinoVesselChangeResourceByCrystalEffect,
    /// energy to full and nothing else) and the elemental-crystal BURST the sword ran on every petal it
    /// touched - which spent the whole meter on each one, so collecting the levels the Rhino's elements
    /// are made of emptied the meter its blade is made of.</para>
    ///
    /// <para><b>Where it runs.</b> Omni-crystal collection resolves on the server, which runs this, and
    /// replays it on a remote pilot's own machine (<c>OmniCrystalImpactor.ReplayOnRemoteOwner</c>). The
    /// energize half is owner-authoritative (<see cref="IRhinoSwordState.Surge"/> is a no-op off the
    /// owner) and replicates from there; the elemental half lands where every crystal's elemental
    /// effect lands - the server and the owner.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoVesselSurgeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/RhinoVesselSurgeByCrystalEffectSO")]
    public sealed class RhinoVesselSurgeByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Energize")]
        [Tooltip("Seconds the surge lasts: the blade held energized, the sword ring raised round the hull, " +
                 "the camera pulled back onto it, and afterimages laid.")]
        [SerializeField, Min(0.1f)] float surgeSeconds = 8f;

        [Tooltip("The sword's energy meter is raised to at least this (0..1) - the full meter the old " +
                 "omni pickup granted.")]
        [SerializeField, Range(0f, 1f)] float energyFill = 1f;

        [Header("Temporary elements")]
        [Tooltip("Normalized level lent to EACH of the four elements for the surge (0.1 = one level). " +
                 "Temporary: decays to zero over the surge, never touching the base level.")]
        [SerializeField, Range(0f, 0.5f)] float elementBoost = 0.2f;

        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null) return;

            var blade = status.NearFieldSkimmer;
            var sword = blade ? blade.SwordState : null;
            sword?.Surge(surgeSeconds, energyFill);

            var resources = status.ResourceSystem;
            if (!resources || elementBoost <= 0f) return;
            for (int i = 0; i < Elements.Length; i++)
                resources.ApplyElementalEffect(Elements[i], elementBoost, surgeSeconds);
        }
    }
}
