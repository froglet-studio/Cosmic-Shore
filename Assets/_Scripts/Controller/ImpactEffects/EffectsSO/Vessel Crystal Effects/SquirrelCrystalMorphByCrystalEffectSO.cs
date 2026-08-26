using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Squirrel's bespoke omni-crystal retirement: the crystal does not shatter, it BECOMES
    /// the eight shielded prisms of the boost ring the same hit lays.
    /// Record: `_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md`.
    ///
    /// This asset is the authored FEEL; <see cref="SquirrelCrystalMorph"/> is the mechanism and
    /// <c>CrystalMorphMeshBuilder</c> is the geometry. It is wired into the Squirrel's
    /// <c>VesselImpactorDataContainerSO.OmniCrystalRetirement</c>, which is also what tells
    /// <see cref="OmniCrystalImpactor"/> to skip the shared husk spray for this hull.
    ///
    /// It deliberately does NOT lay the ring, and must not: the ring is laid by the sibling
    /// <see cref="VesselExplosionByCrystalEffectSO"/> through the ordinary AOE spawner, and the
    /// morph listens for whatever that lays (`BoostRingBuilder.RingLaid`). One authority for the
    /// ring, and the animation follows any retune of it for free. If the ring never arrives, the
    /// morph fades the crystal's body out instead — continuity of existence holds either way.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SquirrelCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/SquirrelCrystalMorphByCrystalEffectSO")]
    public class SquirrelCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        [Header("Timing")]
        [Tooltip("Seconds from the crystal's last frame to the ring's first. Matched to the " +
                 "platform's crystal-capture beat (CrystalCaptureConfigSO, 0.44s) so a pickup " +
                 "reads the same length whichever vessel took it.")]
        [SerializeField] float duration = 0.45f;

        [Tooltip("How much of the window is spent staggering faces against each other. 0 moves " +
                 "every face together; higher trades travel time for a wave across the shape.")]
        [Range(0f, 0.9f)]
        [SerializeField] float stagger = 0.35f;

        [Tooltip("Fraction of the window the GEOMETRY gets. The remainder is the hand-off: at " +
                 "the boundary the morph's corners, face normals and colours ARE the prisms', so " +
                 "the real ring takes over the surface and the crystal's shells dissolve off the " +
                 "top of it. The morph's stagger is fitted inside this fraction, so the last " +
                 "panel always lands before the hand-off.")]
        [Range(0.5f, 1f)]
        [SerializeField] float morphFraction = 0.85f;

        [Tooltip("How much of the MORPH half is spent carrying the crystal's colour onto the " +
                 "shielded prism's, read off the material the laid prisms actually bound. " +
                 "1 blends across the whole morph; lower arrives at the prism palette early " +
                 "and holds it. It always completes before the hand-off.")]
        [Range(0.1f, 1f)]
        [SerializeField] float colourBlendFraction = 1f;

        [Header("Choreography")]
        [Tooltip("Phase of the crystal's LEFTOVER faces — its struts and panel rims, which have " +
                 "no octahedron face to become and collapse into the shield instead. 0 means " +
                 "they are absorbed first, so nothing is left hanging when the panels land.")]
        [Range(0f, 1f)]
        [SerializeField] float fillerPhase;

        [Tooltip("Phase of the FIRST panel face to land. Phases are spread from here to Panel " +
                 "Phase End across each octahedron's eight faces, so a shield assembles rather " +
                 "than appearing whole.")]
        [Range(0f, 1f)]
        [SerializeField] float panelPhaseStart = 0.55f;

        [Tooltip("Phase of the LAST panel face to land.")]
        [Range(0f, 1f)]
        [SerializeField] float panelPhaseEnd = 1f;

        [Header("Ring")]
        [Tooltip("How long to wait for the boost ring before giving up. The ring is laid by a " +
                 "sibling effect through the AOE spawner, which takes a frame or two; if it " +
                 "never comes the crystal's body fades out instead of hanging there.")]
        [SerializeField] float ringGraceSeconds = 1.5f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (vesselImpactor == null || vesselImpactor.Vessel == null) return;

            var status = vesselImpactor.Vessel.VesselStatus;
            if (status == null) return;

            // The crystal this hit collected, found by the id the impact carried — so it
            // resolves on EVERY peer, not just the machine whose collider fired. Reading it live
            // (rather than re-authoring a look-alike mesh and material set on this asset) is
            // what keeps the morph's first frame identical to the crystal's last, tint and all.
            //
            // Its APPEARANCE is read live; its POSE is not. By now the crystal has usually been
            // respawned somewhere else with its rotation reset, so the pose comes from the impact
            // data, which captured it at the collect (CrystalImpactData.Position).
            var crystal = FindCrystal(data.CrystalId);
            if (crystal == null)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] no live crystal with id {data.CrystalId} " +
                                   $"among {Crystal.Active.Count} active — it was collected and moved " +
                                   "on before this effect ran, so there is nothing to copy. The " +
                                   "pickup retires with no animation.");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] retirement fired for '{status.PlayerName}' on crystal " +
                $"{data.CrystalId} at {data.Position}.");

            SquirrelCrystalMorph.Begin(crystal, new Pose(data.Position, data.Rotation),
                data.Scale == Vector3.zero ? Vector3.one : data.Scale, status.Domain,
                new SquirrelCrystalMorph.Settings
            {
                Duration = Mathf.Max(0.05f, duration),
                Stagger = stagger,
                MorphFraction = morphFraction,
                FillerPhase = fillerPhase,
                PanelPhaseStart = panelPhaseStart,
                PanelPhaseEnd = Mathf.Max(panelPhaseStart, panelPhaseEnd),
                RingGraceSeconds = ringGraceSeconds,
                ColourBlendFraction = colourBlendFraction,
            });
        }

        /// <summary>
        /// The live crystal with this id. Reads <see cref="Crystal.Active"/> — the registry that
        /// exists so systems can enumerate live crystals without a scene scan — rather than a
        /// <c>CellRuntimeDataSO</c> reference, because an effect SO is shared across cells and
        /// must not hold one. Embedded hearts are excluded: they are never omni pickups.
        /// </summary>
        static Crystal FindCrystal(int id)
        {
            var live = Crystal.Active;
            for (int i = 0; i < live.Count; i++)
            {
                var c = live[i];
                if (c && !c.IsEmbedded && c.Id == id) return c;
            }
            return null;
        }
    }
}
