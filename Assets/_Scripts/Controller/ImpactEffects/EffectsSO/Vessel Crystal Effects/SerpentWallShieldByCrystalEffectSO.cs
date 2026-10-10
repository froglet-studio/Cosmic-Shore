using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Any crystal (omni or elemental; the effect sits in all five lists of the Serpent's
    /// container) collected by a Serpent re-shields its seed walls: the crystal morphs into
    /// beams, one to every live super-shielded seed the Serpent owns, and a shield ripples
    /// through each wall from the seed outward. With the Mass upgrade active, each wall also
    /// twists and seals its open cells with danger panels (<see cref="SerpentWallAssembler"/>, SERPENT_SEED_WALL.md).
    ///
    /// Crystal effects are broadcast, so this runs on every peer against each peer's own copy of
    /// the walls, the same way the walls themselves are built.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SerpentWallShieldByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/SerpentWallShieldByCrystalEffectSO")]
    public class SerpentWallShieldByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Runtime GameData: its shared ColorSet tints the beams in the Serpent's domain " +
                 "colour, the same source the sniper tracer reads.")]
        [SerializeField] private GameDataSO gameData;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null) return;

            Vector3 from = data.Origin.Valid
                ? data.Origin.Position
                : (status.ShipTransform ? status.ShipTransform.position : Vector3.zero);

            var colorSet = gameData != null ? gameData.ThemeManagerData?.ColorSet : null;
            Color colour = colorSet != null ? colorSet.GetDomainSignalColor(status.Domain) : Color.white;

            SerpentWallAssembler.ReshieldWallsOf(status, from, colour);
        }
    }
}
