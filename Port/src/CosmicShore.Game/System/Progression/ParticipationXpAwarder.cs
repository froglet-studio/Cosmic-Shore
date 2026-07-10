// Ported from Assets/_Scripts/System/Progression/ParticipationXpAwarder.cs
// (Progression unit 2026-07-10) — verbatim; UnityEngine → CosmicShore.Engine.
using CosmicShore.ScriptableObjects; // SO_ProgressionConfig
using CosmicShore.UI;        // PlayerDataService
using CosmicShore.Utility;   // GameDataSO, CSDebug
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Awards flat participation XP to the LOCAL player once per game (win or lose).
    /// Listens to the existing GameDataSO.OnMiniGameEnd SOAP channel so it covers every
    /// mode through one decoupled path. Mirrors GameModeProgressionService.
    /// </summary>
    public class ParticipationXpAwarder : MonoBehaviour
    {
        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        [Header("Progression Config")]
        [Tooltip("Optional. When set, its participationXpPerGame overrides the local xpPerGame " +
                 "below so the award is tuned in one shared place.")]
        [SerializeField] private SO_ProgressionConfig progressionConfig;

        [Header("XP Reward")]
        [Tooltip("Participation XP awarded to the local player every game (win or lose). " +
                 "Feeds the menu XP progress bar via PlayerDataService.AddXP. Set 0 to disable. " +
                 "Ignored when a Progression Config is assigned above.")]
        [SerializeField] private int xpPerGame = 25;

        int XpPerGame => progressionConfig != null ? progressionConfig.participationXpPerGame : xpPerGame;

        bool _awardedThisGame;

        void Start()      // matches GameModeProgressionService: subscribe in Start
        {
            if (gameData == null) return;
            gameData.OnSessionStarted.OnRaised += ResetForNewGame;
            gameData.OnMiniGameEnd.OnRaised   += AwardParticipationXp;
        }

        void OnDestroy()
        {
            if (gameData == null) return;
            gameData.OnSessionStarted.OnRaised -= ResetForNewGame;
            gameData.OnMiniGameEnd.OnRaised    -= AwardParticipationXp;
        }

        void ResetForNewGame() => _awardedThisGame = false;

        void AwardParticipationXp()
        {
            int award = XpPerGame;
            if (_awardedThisGame || award <= 0) return;
            var service = PlayerDataService.Instance;
            if (service == null) return;

            _awardedThisGame = true;
            int total = service.AddXP(award);
            CSDebug.Log($"[ParticipationXp] Awarded {award} XP. Total: {total}");
        }
    }
}
