using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
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
        Cell cell;

        public int Count => healthBlocks.Count;
        public bool IsMature { get; private set; }


        public HealthBlockTracker(int healthBlocksForMaturity, int minHealthBlocks, Cell cell = null)
        {
            this.healthBlocksForMaturity = healthBlocksForMaturity;
            this.minHealthBlocks = minHealthBlocks;
            this.cell = cell;
        }

        public void Add(HealthPrism hp, LifeForm owner, Domains domain)
        {
            if (!hp) return;
            // HashSet.Add returns true only if the entry is new; forward to the cell so
            // Cell.LiveBlockCount tracks unique prisms, not double-counted re-adds.
            if (healthBlocks.Add(hp) && cell)
                cell.AddBlock(hp);
            hp.ChangeTeam(domain);
            hp.LifeForm = owner;
            hp.ownerID = $"{owner} + {hp} + {healthBlocks.Count}";
            CheckIfMature();
        }

        public void Remove(HealthPrism hp, string killerName = "")
        {
            if (!hp) return;
            if (healthBlocks.Remove(hp) && cell)
                cell.RemoveBlock(hp);
            CleanupDeadRefs();
        }

        public bool IsLethal()
        {
            CleanupDeadRefs();
            return healthBlocks.Count <= minHealthBlocks;
        }

        public void CleanupDeadRefs()
        {
            // Forward each dead ref to the cell before discarding so Cell.LiveBlockCount
            // doesn't drift upward when prisms are destroyed without going through Remove
            // (e.g., scene unload, parent destruction, AOE chains that bypass Damage).
            if (cell != null)
            {
                healthBlocks.RemoveWhere(h =>
                {
                    if (h) return false;
                    cell.RemoveBlock(h);
                    return true;
                });
            }
            else
            {
                healthBlocks.RemoveWhere(h => !h);
            }
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
