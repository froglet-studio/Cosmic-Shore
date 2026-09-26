using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's dust on an ALLY lifeform: a creature or plant of the pilot's own domain
    /// whose heart the dust capsule passes through is REFRESHED — <see cref="ILifeFormEntity.Nourish"/>,
    /// the platform's one door for "feeding matters": a creature's starvation clock resets and its
    /// birth counter advances; a plant's growth quota advances toward its next seeding. It is
    /// deliberately a FOOD-WEB event and never a size, so the payout is more of the thing the
    /// pilot protected rather than a bigger individual (<c>Docs/ECOSYSTEM.md §40</c>).
    ///
    /// <para>The sibling of <see cref="SkimmerWitherLifeformByCrystalEffectSO"/>, which takes the
    /// OPPOSING half. The two sit in one container and partition every lifeform by domain, so a
    /// heart is either withered or refreshed, never both.</para>
    ///
    /// <para><b>Rate-limited PER LIFEFORM, not per vessel.</b> A skimmer fires once per ENTRY, and
    /// a pilot toggling Dust mode over a flower re-enters it on every toggle — without a limit
    /// that is a reproduction pump. The ledger is keyed by the lifeform because the limit is a
    /// fact about the creature ("fed recently"), which is also why two Butterflies share it. It
    /// holds instance ids and times only, and prunes itself.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerNourishLifeformByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Lifeform Crystal/SkimmerNourishLifeformByCrystalEffectSO")]
    public class SkimmerNourishLifeformByCrystalEffectSO : SkimmerLifeformCrystalEffectSO
    {
        [Tooltip("Minimum seconds between two refreshes of the SAME lifeform, whoever dusts it.")]
        [SerializeField, Min(0f)] float refreshCooldownSeconds = 5f;

        [Tooltip("Optional: raised with the pilot's name on each lifeform refreshed.")]
        [SerializeField] ScriptableEventString onLifeformRefreshed;

        static readonly Dictionary<int, float> s_lastRefresh = new();
        static float s_nextPrune;

        public override void Execute(SkimmerImpactor impactor, Crystal embeddedCrystal)
        {
            if (!impactor || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            var pilot = impactor.Skimmer != null ? impactor.Skimmer.VesselStatus : null;
            if (pilot == null) return;

            var lifeform = embeddedCrystal.EmbeddedIn;
            if (lifeform == null || lifeform.IsDying) return;
            if (lifeform.Domain != pilot.Domain) return;   // the opposing half is the wither's

            float now = Time.time;
            int id = embeddedCrystal.GetInstanceID();
            // `now >= last`: a static survives a play session when domain reload is off, and a
            // time from the LAST session would otherwise read as permanently recent.
            if (s_lastRefresh.TryGetValue(id, out var last) && now >= last
                && now - last < refreshCooldownSeconds)
                return;
            s_lastRefresh[id] = now;
            Prune(now);

            if (lifeform.Nourish()) onLifeformRefreshed?.Raise(pilot.PlayerName);
        }

        void Prune(float now)
        {
            if (now < s_nextPrune) return;
            s_nextPrune = now + 30f;
            float cutoff = now - refreshCooldownSeconds;
            List<int> stale = null;
            foreach (var kv in s_lastRefresh)
                if (kv.Value < cutoff) (stale ??= new List<int>()).Add(kv.Key);
            if (stale != null) foreach (var k in stale) s_lastRefresh.Remove(k);
        }
    }
}
