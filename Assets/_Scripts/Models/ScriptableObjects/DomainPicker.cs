using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game;

namespace CosmicShore
{
    public static class DomainPicker
    {
        static readonly Domains[] All = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        public static Domains PickRandom(SO_CellType.DomainMask mask, Domains? excluded = null)
        {
            List<Domains> candidates = new List<Domains>(4);

            foreach (var d in All)
            {
                if (excluded.HasValue && d == excluded.Value) continue;
                if (IsAllowed(mask, d)) candidates.Add(d);
            }

            // fallback
            if (candidates.Count == 0)
            {
                foreach (var d in All)
                    if (!excluded.HasValue || d != excluded.Value)
                        candidates.Add(d);
            }

            return candidates.Count == 0 ? Domains.Jade : candidates[Random.Range(0, candidates.Count)];
        }

        static bool IsAllowed(SO_CellType.DomainMask mask, Domains d)
        {
            return d switch
            {
                Domains.Jade => (mask & SO_CellType.DomainMask.Jade) != 0,
                Domains.Ruby => (mask & SO_CellType.DomainMask.Ruby) != 0,
                Domains.Gold => (mask & SO_CellType.DomainMask.Gold) != 0,
                Domains.Blue => (mask & SO_CellType.DomainMask.Blue) != 0,
                _ => false
            };
        }
    }
}