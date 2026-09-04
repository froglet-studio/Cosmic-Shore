using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The player's <b>toybox</b>: the registry of freestyle toys plus per-toy unlock state.
    /// Drives the lava-lamp/freestyle "things to play with" surface in Menu_Main.
    ///
    /// Unlock <i>conditions</i> are deferred ("figure out unlock order later") - every toy
    /// currently ships unlocked via <see cref="ToyDefinitionSO.UnlockedByDefault"/>. The
    /// unlock <i>state</i> lives here as a simple id→bool map with a clean
    /// <see cref="SetToyUnlocked"/> hook a future persistence layer can drive (load on
    /// sign-in, subscribe to <see cref="OnToyboxChanged"/> to save) without touching toys.
    /// </summary>
    [CreateAssetMenu(fileName = "Toybox", menuName = "ScriptableObjects/Toys/Toybox")]
    public class ToyboxSO : ScriptableObject
    {
        [SerializeField, Tooltip("Every toy that can appear in the freestyle toybox.")]
        List<ToyDefinitionSO> toys = new();

        readonly Dictionary<string, bool> _unlocked = new();
        bool _seeded;

        /// <summary>Raised whenever unlock state changes (or a toy is added). UI/persistence observe this.</summary>
        public event Action OnToyboxChanged;

        public IReadOnlyList<ToyDefinitionSO> Toys => toys;

        void Seed()
        {
            if (_seeded) return;
            _seeded = true;
            foreach (var t in toys)
            {
                if (!t) continue;
                if (!_unlocked.ContainsKey(t.Id))
                    _unlocked[t.Id] = t.UnlockedByDefault;
            }
        }

        public bool IsUnlocked(ToyDefinitionSO toy)
        {
            if (!toy) return false;
            Seed();
            return _unlocked.TryGetValue(toy.Id, out var u) ? u : toy.UnlockedByDefault;
        }

        /// <summary>Every toy currently in the player's toybox, in registry order.</summary>
        public IEnumerable<ToyDefinitionSO> UnlockedToys()
        {
            Seed();
            foreach (var t in toys)
                if (t && IsUnlocked(t)) yield return t;
        }

        /// <summary>
        /// Unlock or re-lock a toy by id. Persistence is deferred - a future PlayerDataService
        /// hook can call this on load, then subscribe to <see cref="OnToyboxChanged"/> to save.
        /// </summary>
        public void SetToyUnlocked(string toyId, bool unlocked)
        {
            if (string.IsNullOrEmpty(toyId)) return;
            Seed();
            if (_unlocked.TryGetValue(toyId, out var cur) && cur == unlocked) return;
            _unlocked[toyId] = unlocked;
            OnToyboxChanged?.Invoke();
        }

        /// <summary>Register a toy at runtime (used by the code-built default toybox).</summary>
        public void AddToy(ToyDefinitionSO toy)
        {
            if (!toy || toys.Contains(toy)) return;
            toys.Add(toy);
            _seeded = false; // re-seed to pick up the new toy's default-unlock state
            OnToyboxChanged?.Invoke();
        }
    }
}
