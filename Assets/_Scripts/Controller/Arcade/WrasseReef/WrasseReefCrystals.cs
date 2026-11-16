using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The reef's elemental crystals: Bloomrush / Tapestry's peer-local scatter (every machine lays the same crystals from
    /// one seed, no network object), weighted toward the game's element, and REGROWN - a taken crystal's slot grows a new
    /// one somewhere else after <see cref="WrasseReefSettingsSO.crystalRegrowSeconds"/>.
    ///
    /// <para><b>Why the weighting is the whole game lever.</b> On the Wrasse a crystal is a petal on the hull, and the
    /// hull's level in an element is how often that element's motes come to the kite (<c>KiteNavigator.PickElement</c>).
    /// A Charge reef therefore fills the kite's board with Charge motes, which is what a Charge game wants, while the
    /// vessel is exactly as it ships.</para>
    ///
    /// <para><b>Deterministic per slot and generation.</b> Slot <c>i</c>'s <c>k</c>-th crystal is drawn from
    /// <c>Hash(seed, i, k)</c>, so two peers that see a crystal taken a frame apart still regrow the same one in the
    /// same place.</para>
    /// </summary>
    public sealed class WrasseReefCrystals
    {
        sealed class Slot
        {
            public Crystal Crystal;
            public int Generation;
            public float RegrowAt = -1f;
        }

        readonly List<Slot> _slots = new();
        WrasseReefSettingsSO _settings;
        ElementalCrystalSetSO _set;
        Vector3 _centre;

        /// <summary>Lays the rung's crystals round <paramref name="centre"/>. Clears any earlier scatter first.</summary>
        public void Lay(WrasseReefSettingsSO settings, int intensity, Vector3 centre)
        {
            Clear();
            _settings = settings;
            _centre = centre;
            if (settings == null) return;
            _set = ElementalCrystalSetSO.Load();
            if (_set == null) return;

            int count = settings.CrystalCountFor(intensity);
            for (int i = 0; i < count; i++)
            {
                var slot = new Slot();
                _slots.Add(slot);
                Grow(slot, i);
            }
        }

        /// <summary>Regrows the slots whose crystal was taken. Call every frame on every peer.</summary>
        public void Tick(float now)
        {
            if (_settings == null || _set == null) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Crystal && slot.Crystal.gameObject.activeInHierarchy) continue;
                if (_settings.crystalRegrowSeconds <= 0f) continue;
                if (slot.RegrowAt < 0f)
                {
                    if (slot.Crystal) Object.Destroy(slot.Crystal.gameObject);
                    slot.Crystal = null;
                    slot.RegrowAt = now + _settings.crystalRegrowSeconds;
                    continue;
                }
                if (now < slot.RegrowAt) continue;
                slot.Generation++;
                Grow(slot, i);
            }
        }

        /// <summary>The nearest standing crystal within <paramref name="reach"/> of <paramref name="from"/>.</summary>
        public bool TryNearest(Vector3 from, float reach, out Vector3 position)
        {
            position = default;
            float best = reach * reach;
            bool found = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var c = _slots[i].Crystal;
                if (!c || !c.gameObject.activeInHierarchy) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = c.transform.position;
                found = true;
            }
            return found;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Crystal) Object.Destroy(_slots[i].Crystal.gameObject);
            _slots.Clear();
        }

        void Grow(Slot slot, int index)
        {
            slot.RegrowAt = -1f;
            slot.Crystal = null;
            var rng = new System.Random(unchecked(_settings.crystalScatterSeed * 7919 + index * 104729 + slot.Generation * 31));

            var element = rng.NextDouble() < _settings.favouredShare && _settings.favouredElement is >= Element.Charge and <= Element.Time
                ? _settings.favouredElement
                : ElementalCrystalSetSO.RandomElementFrom(rng);
            var prefab = _set.GetPrefab(element);
            if (prefab == null) return;

            // Equal-volume scatter through the ball (cube root of a uniform draw), Tapestry's.
            float r = _settings.crystalScatterRadius * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
            float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            var pos = _centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

            var crystal = Object.Instantiate(prefab, pos, Quaternion.identity);
            // One size, one petal, like every elemental crystal (ElementalCrystalSetSO.CrystalWorldScale).
            crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            if (_set.CollectionEffects is { Length: > 0 })
                impactor.SetCollectionEffects(_set.CollectionEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            slot.Crystal = crystal;
        }
    }
}
