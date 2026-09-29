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
        readonly Cell cell;

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
            // ChangeTeam BEFORE Cell.AddBlock: Cell.AddBlock reads block.Domain to
            // decide which per-domain countGrids the prism belongs in. If AddBlock
            // ran first, it would see the pooled HealthPrism's stale/Blue domain and
            // bin the prism into the wrong buckets - and the later RemoveBlock,
            // using the now-correct domain, would decrement different buckets,
            // leaving phantom counts that drift the anti-domain answer over time.
            // (§2.3.1 in Docs/DENSITY_PARTITIONING_AUDIT.md.)
            hp.ChangeTeam(domain);
            // HashSet.Add returns true only on a new entry, so forward only once per prism
            // and Cell.LiveBlockCount counts unique prisms (not double-counted re-adds).
            if (healthBlocks.Add(hp) && cell)
                cell.AddBlock(hp);
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

        // Cached predicates. The cell-forwarding lambda captures `this`, so writing it
        // inline allocates a display class AND a Predicate<HealthPrism> on EVERY call —
        // and CleanupDeadRefs runs on every health-prism Remove, i.e. once per flora/
        // fauna prism eaten. The capture is `this`, which never changes, so the delegate
        // is built once per tracker instead of once per death. (The no-cell branch's
        // lambda captures nothing and is already cached by the compiler; it is kept as a
        // field only so both branches read the same way.)
        Predicate<HealthPrism> _dropDeadAndUnbind;
        static readonly Predicate<HealthPrism> s_dropDead = h => !h;

        public void CleanupDeadRefs()
        {
            // Forward each dead ref to the cell before discarding so Cell.LiveBlockCount
            // doesn't drift upward when prisms die outside the normal Damage path
            // (scene unload, parent destruction, AOE chains that bypass HealthPrism).
            if (cell != null)
            {
                _dropDeadAndUnbind ??= h =>
                {
                    if (h) return false;
                    cell.RemoveBlock(h);
                    return true;
                };
                healthBlocks.RemoveWhere(_dropDeadAndUnbind);
            }
            else
            {
                healthBlocks.RemoveWhere(s_dropDead);
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
