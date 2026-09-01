using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A "play today's challenge" button that can sit anywhere (a home-screen shortcut, the daily
    /// challenge detail panel) and routes to the same place the arcade card does - the launch
    /// modal, opened with the challenge's terms pinned.
    ///
    /// <para>It resolves the arcade view rather than holding a launch path of its own, so there is
    /// exactly one route into a challenge attempt and a second entry point cannot drift from the
    /// first.</para>
    /// </summary>
    public class DailyChallengePlayButton : MonoBehaviour
    {
        [Tooltip("The arcade view that owns the grid and the launch modal. Left empty it is found " +
                 "in the scene on first use.")]
        [SerializeField] ArcadeExploreView exploreView;

        Button _button;
        DailyChallengeService _subscribedService;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable()
        {
            EnsureSubscribed();
            Refresh();
        }

        void OnDisable()
        {
            if (_subscribedService != null)
            {
                _subscribedService.OnChallengeChanged -= Refresh;
                _subscribedService = null;
            }
        }

        // The service creates itself at AfterSceneLoad, so a button enabled in the first scene can
        // come up before it exists - re-check rather than subscribing once and going quiet.
        void Update() => EnsureSubscribed();

        void EnsureSubscribed()
        {
            var service = DailyChallengeService.Instance;
            if (service == null || service == _subscribedService) return;

            if (_subscribedService != null)
                _subscribedService.OnChallengeChanged -= Refresh;

            _subscribedService = service;
            _subscribedService.OnChallengeChanged += Refresh;
            Refresh();
        }

        void Refresh()
        {
            if (!_button) _button = GetComponent<Button>();
            if (!_button) return;

            var service = DailyChallengeService.Instance;
            _button.interactable = service != null && service.Today.IsValid && service.CanAttempt;
        }

        /// <summary>Wire this to the Button's onClick.</summary>
        public void Play()
        {
            var view = ResolveView();
            if (view == null)
            {
                CosmicShore.Utility.CSDebug.LogWarning(
                    "[DailyChallengePlayButton] No ArcadeExploreView in the scene - wire one on " +
                    "the button, or place this button on the arcade screen.");
                return;
            }

            view.SelectDailyChallenge();
        }

        ArcadeExploreView ResolveView()
        {
            if (exploreView) return exploreView;
            exploreView = FindAnyObjectByType<ArcadeExploreView>(FindObjectsInactive.Include);
            return exploreView;
        }
    }
}
