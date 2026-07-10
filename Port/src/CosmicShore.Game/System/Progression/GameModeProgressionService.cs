// PORT Deviation — type-preserving SHELL of GameModeProgressionService (original:
// Assets/_Scripts/System/Progression/GameModeProgressionService.cs, 788 lines of
// quest-chain progression over Cloud Save). Landed as a shell in Arc F 2b-ii because
// ArcadeExploreView null-guards it and gates card locking through
// IsGameModeUnlocked — the shell unlocks everything, which matches a fresh install
// with progression disabled. The real port is its own future unit (tracked in the
// PORT_PLAN arcade dependency box).
using System;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Core
{
    public class GameModeProgressionService : MonoBehaviour
    {
        public static GameModeProgressionService Instance { get; private set; }

        /// <summary>Raised when the progression data changes (unlock claimed, stat improved).</summary>
        public event Action<GameModeProgressionData> OnProgressionChanged;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shell: every mode is unlocked (progression gating arrives with the real port).</summary>
        public bool IsGameModeUnlocked(GameModes mode) => true;

        /// <summary>Shell: every intensity tier is unlocked (real: Cloud-Save-backed play counts).</summary>
        public int GetMaxUnlockedIntensity(GameModes mode) => 4;

        /// <summary>Shell: mirrors the upstream definition over the shell max (always true).</summary>
        public bool IsIntensityUnlocked(GameModes mode, int intensity) => intensity <= GetMaxUnlockedIntensity(mode);

        /// <summary>
        /// Shell: no quest chain yet — always null. Callers null-guard exactly like the
        /// original (locked-intensity goal toasts simply never fire until the real port).
        /// </summary>
        public SO_GameModeQuestData GetQuestForMode(GameModes mode) => null;

        protected void RaiseProgressionChanged(GameModeProgressionData data) => OnProgressionChanged?.Invoke(data);
    }
}
