using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The Toy Box's memory: for each toy, the PATH to the variant the player last committed
    /// from its detail window - the labels of the branches they opened and the leaf they
    /// pressed Switch / Spawn / Start on (<c>["Fauna", "Shark", "Charge"]</c> for the Lifeform
    /// Matrix, <c>["Atlantis"]</c> for the cell selector).
    ///
    /// <para>A toy's options are built at runtime from live toys and carry no ids, so the LABEL
    /// is the identity - it is the station's own name, and a variant whose name changed is a
    /// different variant to the player too. A path that no longer resolves (a species retired,
    /// a painting renamed) simply stops matching at the first missing label and the window opens
    /// where it would have opened anyway.</para>
    ///
    /// <para>Keyed by the toy DEFINITION asset's name - the one thing about a toy that survives
    /// the cell swaps which rebuild the whole toybox, which is also why the detail window keys
    /// its own re-bind on the definition.</para>
    /// </summary>
    public static class ToyPreferenceStore
    {
        const string SaveFileName = "toy_preferences.data";

        [Serializable]
        public struct ToyPreference
        {
            public string ToyKey;
            public List<string> Path;
        }

        static List<ToyPreference> _records;
        static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _records = null;
            _initialized = false;
        }

        static void Init()
        {
            _records = DataAccessor.Load<List<ToyPreference>>(SaveFileName) ?? new List<ToyPreference>();
            _initialized = true;
        }

        /// <summary>The remembered path for <paramref name="toyKey"/>, or false when the player
        /// has never committed a variant of that toy from the window.</summary>
        public static bool TryGetPath(string toyKey, out IReadOnlyList<string> path)
        {
            path = null;
            if (string.IsNullOrEmpty(toyKey)) return false;
            if (!_initialized) Init();

            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].ToyKey != toyKey) continue;
                if (_records[i].Path is not { Count: > 0 }) return false;
                path = _records[i].Path;
                return true;
            }
            return false;
        }

        /// <summary>Remember that <paramref name="path"/> is the variant the player last committed.</summary>
        public static void SavePath(string toyKey, IList<string> path)
        {
            if (string.IsNullOrEmpty(toyKey) || path is not { Count: > 0 }) return;
            if (!_initialized) Init();

            var record = new ToyPreference { ToyKey = toyKey, Path = new List<string>(path) };

            bool replaced = false;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].ToyKey != toyKey) continue;
                _records[i] = record;
                replaced = true;
                break;
            }
            if (!replaced) _records.Add(record);

            DataAccessor.Save(SaveFileName, _records);

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ToyPreference] Saved {toyKey}: {string.Join(" > ", path)}.");
        }

        /// <summary>Forget one toy's record. Test and tooling hygiene; nothing in the UI calls it.</summary>
        public static void Clear(string toyKey)
        {
            if (!_initialized) Init();
            _records.RemoveAll(r => r.ToyKey == toyKey);
            DataAccessor.Save(SaveFileName, _records);
        }
    }
}
