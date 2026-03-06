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

            // Track score and subscribe
            var roundStats = vesselStatus.Player?.RoundStats;
            if (roundStats != null)
            {
                _previousScore = roundStats.Score;
                roundStats.OnScoreChanged += HandleScoreChanged;
            }
        }

        private void UnsubscribeScorePopup()
        {
            var roundStats = _vesselStatusBase?.Player?.RoundStats;
            if (roundStats != null)
                roundStats.OnScoreChanged -= HandleScoreChanged;
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
    }
}
