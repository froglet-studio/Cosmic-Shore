using System;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        [Header("Base View (fallback)")]
        [SerializeField] private VesselHUDView baseView;

        [Header("Legacy Silhouette")]
        [SerializeField] private SilhouetteController silhouette;

        [Header("Score Popup")]
        [SerializeField] private ScorePopup scorePopup;

        [Tooltip("Used to check the active game mode for score popup filtering.")]
        [SerializeField] private GameDataSO gameData;

        [Tooltip("Domain color palette for score popup text color.")]
        [SerializeField] private DomainColorPaletteSO domainColorPalette;

        protected R_VesselActionHandler Actions { get; private set; }
        protected VesselHUDView View => baseView;

        private IVesselStatus _vesselStatusBase;
        private float _previousScore;
        private int _previousIntScore;
        private Action _unsubscribeScoreAction;

        // Game modes where the score popup should NOT appear
        static readonly System.Collections.Generic.HashSet<GameModes> _excludedModes = new()
        {
            GameModes.Freestyle,
            GameModes.MultiplayerFreestyle,
            GameModes.WildlifeBlitz,
            GameModes.MultiplayerWildlifeBlitzGame,
        };

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            UnsubscribeScorePopup();
        }

        public virtual void Initialize(IVesselStatus vesselStatus)
        {
            _vesselStatusBase = vesselStatus;
            Actions = vesselStatus.ActionHandler;

            if (!baseView)
                baseView = GetComponentInChildren<VesselHUDView>(true);

            baseView?.Initialize();
            InitializeScorePopup(vesselStatus);
        }

        public void SubscribeToEvents()
        {
            if (!Actions || !baseView) return;
            Actions.OnInputEventStarted += HandleStart;
            Actions.OnInputEventStopped += HandleStop;
        }

        public void UnsubscribeFromEvents()
        {
            if (!Actions) return;
            Actions.OnInputEventStarted -= HandleStart;
            Actions.OnInputEventStopped -= HandleStop;
        }

        public void ShowHUD() => baseView?.Show();
        public void HideHUD() => baseView?.Hide();

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop(InputEvents ev)  => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!baseView) return;

            foreach (var h in baseView.highlights)
            {
                if (h.input == ev && h.image)
                    h.image.enabled = on;
            }
        }

        public void SetBlockPrefab(GameObject prefab)
        {
            if (baseView != null)
                baseView.TrailBlockPrefab = prefab;

            if (silhouette != null)
                silhouette.SetBlockPrefab(prefab);
        }

        // ---------------------------------------------------------------
        // Score Popup
        // ---------------------------------------------------------------

        private void InitializeScorePopup(IVesselStatus vesselStatus)
        {
            if (!scorePopup) return;

            // Only show score popup for local player
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
                return;

            // Skip excluded game modes
            if (gameData && _excludedModes.Contains(gameData.GameMode))
                return;

            scorePopup.Initialize();

            // Set text color to the player's domain color
            if (domainColorPalette)
                scorePopup.SetColor(domainColorPalette.Get(vesselStatus.Domain));

            var roundStats = vesselStatus.Player?.RoundStats;
            if (roundStats == null) return;

            // Subscribe to the correct stat event based on game mode
            var mode = gameData ? gameData.GameMode : GameModes.Random;

            switch (mode)
            {
                case GameModes.MultiplayerJoust:
                    _previousIntScore = roundStats.JoustCollisions;
                    Action<IRoundStats> onJoust = HandleJoustCollisionChanged;
                    roundStats.OnJoustCollisionChanged += onJoust;
                    _unsubscribeScoreAction = () => roundStats.OnJoustCollisionChanged -= onJoust;
                    break;

                case GameModes.HexRace:
                    _previousIntScore = roundStats.OmniCrystalsCollected;
                    Action<IRoundStats> onCrystal = HandleCrystalCollectedChanged;
                    roundStats.OnOmniCrystalsCollectedChanged += onCrystal;
                    _unsubscribeScoreAction = () => roundStats.OnOmniCrystalsCollectedChanged -= onCrystal;
                    break;

                default:
                    _previousScore = roundStats.Score;
                    roundStats.OnScoreChanged += HandleScoreChanged;
                    _unsubscribeScoreAction = () => roundStats.OnScoreChanged -= HandleScoreChanged;
                    break;
            }
        }

        private void UnsubscribeScorePopup()
        {
            _unsubscribeScoreAction?.Invoke();
            _unsubscribeScoreAction = null;
        }

        private void HandleScoreChanged()
        {
            if (!scorePopup || _vesselStatusBase?.Player?.RoundStats == null) return;

            float currentScore = _vesselStatusBase.Player.RoundStats.Score;
            float delta = currentScore - _previousScore;
            _previousScore = currentScore;

            if (delta > 0f)
            {
                int points = Mathf.Max(1, Mathf.RoundToInt(delta));
                scorePopup.ShowScorePoint(points);
            }
        }

        private void HandleJoustCollisionChanged(IRoundStats stats)
        {
            if (!scorePopup) return;

            int current = stats.JoustCollisions;
            int delta = current - _previousIntScore;
            _previousIntScore = current;

            if (delta > 0)
                scorePopup.ShowScorePoint(delta);
        }

        private void HandleCrystalCollectedChanged(IRoundStats stats)
        {
            if (!scorePopup) return;

            int current = stats.OmniCrystalsCollected;
            int delta = current - _previousIntScore;
            _previousIntScore = current;

            if (delta > 0)
                scorePopup.ShowScorePoint(delta);
        }
    }
}
