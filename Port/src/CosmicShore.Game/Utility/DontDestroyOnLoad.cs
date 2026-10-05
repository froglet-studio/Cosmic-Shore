// Ported verbatim from Assets/_Scripts/Utility/DontDestroyOnLoad.cs
// (bootstrap arc 2026-07-08). Mechanical substitutions (README):
// UnityEngine → CosmicShore.Engine. FULLY LIVE — the persistence marker
// AppManager.EnsurePersistent stamps onto managers (the engine's
// Object.DontDestroyOnLoad is a no-op until multi-scene lands, so the
// component doubles as the "already marked" flag the stamp checks).

using CosmicShore.Engine;

namespace CosmicShore.Utility
{
    public class DontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
