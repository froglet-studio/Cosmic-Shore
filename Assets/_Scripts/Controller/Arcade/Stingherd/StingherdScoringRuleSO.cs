using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stingherd scoring - first DOMAIN to the sting target wins, on <see cref="ScoringMetric.CombatPoints"/>. One
    /// STING - a danger creature of a Jacks' herd touching a rival hull - pays the rule's strike points.
    ///
    /// <para><b>Jackstraw's rule, reused (Kelpwood's shape).</b> A sting is already a Strike-class combat hit: the
    /// Jacks' <c>JacksExecutor.UpdateContacts</c> admits it through <c>CombatHitDrain.TryAdmit</c> (one per hull per
    /// <c>strikeCooldown</c>) and raises it on the combat-hit channel, so it is COUNTED (<c>StrikeHitsLanded</c>) in every
    /// mode. <see cref="JackstrawScoringRuleSO"/> pays exactly that - a Strike for its points, every other class nothing -
    /// with every line an authored template, so Stingherd inherits it and authors its own words on its own asset. At
    /// Charge 5 (Attack) every herd creature that touches a rival bites it, also a Strike, and is paid the same.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Stingherd", fileName = "StingherdScoringRule")]
    public class StingherdScoringRuleSO : JackstrawScoringRuleSO
    {
    }
}
