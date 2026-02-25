using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Tracks spindles (visual components) for a LifeForm.
    /// Extracted from LifeForm to satisfy SRP - spindle tracking is a single responsibility.
    /// </summary>
    public class SpindleTracker
    {
        readonly HashSet<Spindle> spindles = new();

        public int Count => spindles.Count;

        public void Add(Spindle sp, LifeForm owner)
        {
            if (!sp) return;
            spindles.Add(sp);
            sp.LifeForm = owner;
        }

        public Spindle Instantiate(Spindle prefab, Transform parent)
        {
            Spindle newSpindle = Object.Instantiate(prefab, parent.position, parent.rotation, parent);
            return newSpindle;
        }

        public void Remove(Spindle sp)
        {
            if (!sp) return;
            spindles.Remove(sp);
            CleanupDeadRefs();
        }

        public bool IsEmpty()
        {
            CleanupDeadRefs();
            return spindles.Count == 0;
        }

        public void CleanupDeadRefs()
        {
            spindles.RemoveWhere(s => !s);
        }

        public void ForceWitherAll(GameObject root)
        {
            var allSpindles = root.GetComponentsInChildren<Spindle>(true);
            foreach (var sp in allSpindles)
            {
                if (sp) sp.ForceWither();
            }
        }

        public IEnumerable<Spindle> All => spindles;
    }
}
