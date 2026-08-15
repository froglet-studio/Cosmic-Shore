using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A BLAST turns an omni crystal into a ball (design: R_VesselActions/SCARAB.md §4.1). The
    /// cavitation punch is the Scarab's reach weapon, and until this existed it was the only one
    /// of the vessel's four tools that could not make a payload — you had to physically fly
    /// through a crystal, so a crystal parked behind a wall of your own trail was unreachable.
    ///
    /// The ball is born AT THE CRYSTAL, not at the vessel, and leaves along the blast's own
    /// radial — the blast is the thing doing the forging, so it is the blast that aims. That is
    /// why this takes the crystal's impactor rather than a <see cref="CrystalImpactData"/>: the
    /// vessel path can use the pilot's position because it is touching the crystal at that
    /// instant, and a blast is not.
    ///
    /// AUTHORED PER BLAST, deliberately. This lives on the explosion's own
    /// <c>ExplosionImpactorDataContainerSO</c>, so only blasts that carry it forge — the Scarab's
    /// cavitation punch does, and a Dolphin crystal cone hitting the same crystal does not start
    /// minting Astro League balls.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScarabBallForgeByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Crystal/ScarabBallForgeByExplosionEffectSO")]
    public class ScarabBallForgeByExplosionEffectSO : ExplosionCrystalEffectSO
    {
        [Header("Ball")]
        [Tooltip("The networked ball prefab (AstroLeagueBall). Must also be registered in " +
                 "DefaultNetworkPrefabs or Spawn() throws.")]
        [SerializeField] AstroLeagueBall _ballPrefab;

        [Tooltip("Speed the forged ball leaves at, along the blast's outward radial.")]
        [SerializeField, Min(1f)] float _launchSpeed = 120f;

        [Tooltip("How much of the blast's own impact magnitude is added on top of the base launch " +
                 "speed. 0 makes every blast forge at the same speed; 1 lets a harder-throwing " +
                 "blast fire the payload harder, the way it throws prism debris harder.")]
        [SerializeField, Range(0f, 1f)] float _blastSpeedContribution = 0.35f;

        [Tooltip("Clearance from the crystal along the launch direction, so the new ball does not " +
                 "materialise inside whatever the crystal was sitting against.")]
        [SerializeField, Min(0f)] float _forwardClearance = 20f;

        public override void Execute(ExplosionImpactor impactor, OmniCrystalImpactor crystalImpactee)
        {
            if (impactor == null || crystalImpactee == null || crystalImpactee.Crystal == null) return;

            var source = impactor.SourceVessel;
            var status = source?.VesselStatus;
            if (status == null) return;    // an anonymous blast has no domain owner to forge for

            Vector3 crystalAt = crystalImpactee.transform.position;
            Vector3 blastAt = impactor.transform.position;

            Vector3 radial = crystalAt - blastAt;
            Vector3 course = radial.sqrMagnitude > 1e-4f
                ? radial.normalized
                : (status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : Vector3.forward);

            // The blast's own throw, in the same units it hands prism debris.
            float blastMagnitude = impactor.BlastImpulse.Speed * impactor.BlastImpulse.Inertia;
            float speed = _launchSpeed + blastMagnitude * _blastSpeedContribution;

            Vector3 spawnAt = crystalAt + course * _forwardClearance;
            ScarabBallForge.Request(status, _ballPrefab, spawnAt, course * speed);

            CSDebug.Log($"[ScarabBallForge] {status.PlayerName}'s blast forged a {status.Domain} " +
                        $"ball at {spawnAt} @ {speed:F0} u/s.");
        }
    }
}
