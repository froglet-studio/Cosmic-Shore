using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore
{
    /// <summary>
    /// Tracks health blocks (HealthPrisms) for a LifeForm.
    /// Extracted from LifeForm to satisfy SRP - health tracking is a single responsibility.
    /// </summary>
    public class HealthBlockTracker
    {
        readonly HashSet<HealthPrism> healthBlocks = new();
        readonly int healthBlocksForMaturity;
        readonly int minHealthBlocks;

        public int Count => healthBlocks.Count;
        public bool IsMature { get; private set; }

        /// <summary>
        /// Fired when health blocks drop to or below <see cref="minHealthBlocks"/>.
        /// Parameter is the killer name (empty if no killer).
        /// </summary>
        public event Action<string> OnLethal;

        public HealthBlockTracker(int healthBlocksForMaturity, int minHealthBlocks)
        {
            this.healthBlocksForMaturity = healthBlocksForMaturity;
            this.minHealthBlocks = minHealthBlocks;
        }

        public void Add(HealthPrism hp, LifeForm owner, Domains domain)
        {
            if (!hp) return;
            healthBlocks.Add(hp);
            hp.ChangeTeam(domain);
            hp.LifeForm = owner;
            hp.ownerID = $"{owner} + {hp} + {healthBlocks.Count}";
            CheckIfMature();
        }

        public void Remove(HealthPrism hp, string killerName = "")
        {
            if (!hp) return;
            healthBlocks.Remove(hp);
            CleanupDeadRefs();
        }

        public bool IsLethal()
        {
            CleanupDeadRefs();
            return healthBlocks.Count <= minHealthBlocks;
        }

        public void CleanupDeadRefs()
        {
            healthBlocks.RemoveWhere(h => !h);
        }

        public void SetTeam(Domains domain)
        {
            foreach (var hp in healthBlocks)
                if (hp) hp.ChangeTeam(domain);
        }

        public void ActivateAllShields()
        {
            foreach (var hp in healthBlocks.ToList())
                if (healthBlocks.Contains(hp) && hp) hp.ActivateShield();
        }

        public void DamageAll(Domains domain)
        {
            foreach (var hp in healthBlocks.ToArray())
            {
                if (!hp) continue;
                hp.Damage(UnityEngine.Random.onUnitSphere, domain, "Guy Fawkes", true);
            }
        }

        public IEnumerable<HealthPrism> All => healthBlocks;

        void CheckIfMature()
        {
            if (!IsMature && healthBlocks.Count >= healthBlocksForMaturity)
                IsMature = true;
        }
    }
}
