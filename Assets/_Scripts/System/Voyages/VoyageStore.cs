using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The galaxy as this device knows it (<see cref="VoyageState"/>): kept in PlayerPrefs as JSON until the backend
    /// holds voyage records (Docs/Voyages/VOYAGES.md §8, Shombith's). A voyage left mid-leg loads as given up: the next
    /// embark starts at home with nothing aboard (<see cref="VoyageRules.Embark"/>).
    /// </summary>
    public static class VoyageStore
    {
        const string Key = "voyage.state.v1";

        static VoyageState _state;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _state = null;

        /// <summary>The saved galaxy, or null on a device that has never voyaged (or whose record will not read).</summary>
        public static VoyageState Load()
        {
            if (_state != null) return _state;
            var json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                _state = JsonUtility.FromJson<VoyageState>(json);
                _state?.Normalize();
            }
            catch (System.ArgumentException e)
            {
                CSDebug.LogWarning($"[Voyage] The saved voyage record would not read ({e.Message}); starting a new galaxy.");
                _state = null;
            }
            return _state;
        }

        public static void Save(VoyageState state)
        {
            if (state == null) return;
            _state = state;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        /// <summary>Forget the galaxy (dev tooling): the next voyage starts from the content's seed news.</summary>
        public static void Clear()
        {
            _state = null;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
