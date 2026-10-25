using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's OMNI-CRYSTAL ability, the <b>Surge</b>: collecting an omni crystal fills the
    /// sword's energy, bursts the blade and holds it ENERGIZED for <see cref="surgeSeconds"/> with no
    /// stance and no cost; lends the hull every element for the same length; and lays the Mass
    /// afterimages THROUGHOUT the surge - the ability that otherwise waits for an energize to end.
    /// When the surge's energize goes out, the ordinary afterimage window opens after it.
    /// See <c>R_VesselActions/RHINO_SWORD_AFTERIMAGE.md</c> § "The surge".
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
        [Tooltip("Seconds the surge holds the blade energized (and lays afterimages).")]
        [SerializeField, Min(0.1f)] float surgeSeconds = 6f;

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
