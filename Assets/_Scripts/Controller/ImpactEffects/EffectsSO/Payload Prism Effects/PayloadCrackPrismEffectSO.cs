using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The PLAIN payload's effect: cracks super-shielded (and shielded) prisms in the burst
    /// (Docs/Pilots/ARTIFACTS.md §2). The second sanctioned super-shield crack after the Grizzly
    /// sniper's, and the same crack: both run <see cref="SuperShieldCrack.TryCrack"/>, so a
    /// silenced carrier's burst deflects and the carrier's own domain's mass is never touched.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PayloadCrackPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Payload - Prism/PayloadCrackPrismEffectSO")]
    public class PayloadCrackPrismEffectSO : PayloadPrismEffectSO
    {
        [SerializeField, Tooltip("Crack ordinary one-hit shields too, not only super shields.")]
        bool alsoShielded = true;

        [SerializeField, Tooltip("Crack, don't shatter: a prism whose SUPER shield falls keeps a regular " +
                                 "one-hit shield.")]
        bool leaveRegularShield = true;

        public override void Execute(in PayloadRelease release, Prism prism) =>
            SuperShieldCrack.TryCrack(prism, release.CarrierName, release.CarrierDomain,
                alsoShielded, leaveRegularShield);
    }
}
