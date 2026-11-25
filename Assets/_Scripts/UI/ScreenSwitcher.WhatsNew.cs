using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The What's new window's part of the switcher (Docs/MENU_REDESIGN_B.md section 216): building the window
    /// and opening it once by itself after an update.</summary>
    public partial class ScreenSwitcher
    {
        /// <summary>
        /// Opens the What's new window by itself once per update (Docs/MENU_REDESIGN_B.md section 216): waits for the menu
        /// to be Ready and the boot world's veil to lift, then opens it if the newest update has not been shown on this
        /// device and nothing else holds the menu (<see cref="WhatsNewRules.ShouldOpenByItself"/>). Gives up quietly after
        /// <see cref="returnToCardTimeoutSeconds"/>.
        /// </summary>
        private IEnumerator OpenWhatsNewAfterUpdateCoroutine()
        {
            if (WebToybox.IsBuild) yield break;
            var data = WhatsNewSO.Load();
            var newestId = data && data.Newest != null ? data.Newest.Id : null;
            var seenId = PlayerPrefs.GetString(WhatsNewRules.SeenPrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(newestId) || newestId == seenId) yield break;

            float giveUpAt = Time.unscaledTime + returnToCardTimeoutSeconds;
            var menu = FindAnyObjectByType<MainMenuController>();
            while ((menu && menu.CurrentState is MainMenuState.None or MainMenuState.Initializing)
                   || EnvironmentLoadVeil.IsCovering)
            {
                if (Time.unscaledTime > giveUpAt) yield break;
                yield return null;
            }
            if (menu && menu.CurrentState != MainMenuState.Ready) yield break;

            if (!WhatsNewRules.ShouldOpenByItself(newestId, seenId,
                    guided: MenuGuide.IsActive || QuestArcadeConstraints.Active,
                    inFreestyle: InFreestyle,
                    windowOpen: HasActiveModal,
                    returningToCard: false))
                yield break;

            OpenModal(ModalWindows.WHATS_NEW);
        }

        /// <summary>
        /// Builds the What's new window (<see cref="WhatsNewModal"/>, Docs/MENU_REDESIGN_B.md section 216) beside the
        /// Quests window when the scene has none, with the Voyage window's kit, as <see cref="EnsureQuestsModal"/> does.
        /// It stands down when a WHATS_NEW modal is already registered, or when Resources holds no What's new words.
        /// </summary>
        private void EnsureWhatsNewModal()
        {
            if (WebToybox.IsBuild) return;
            Modals ??= new List<ModalWindowManager>();
            foreach (var authored in Modals)
                if (authored != null && authored.ModalType == ModalWindows.WHATS_NEW)
                    return;

            var data = WhatsNewSO.Load();
            var style = VoyageBoardStyleSO.Load();
            var lines = VoyageContentSO.Load() is { } content ? content.Lines : null;
            if (!data || !style)
            {
                CSDebug.LogWarning($"[ScreenSwitcher] No What's new words at Resources/{WhatsNewSO.ResourcePath} or no " +
                                   "voyage board style; Settings' What's new button opens nothing.");
                return;
            }
            var mission = FindModal(ModalWindows.MISSION);
            var view = mission ? mission.GetComponentInChildren<VoyageJobsView>(true) : null;

            // In the Voyage window's layer, last in it, so it draws over the window it can open (Set sail).
            Transform parent = mission ? mission.transform.parent : _canvasRect;
            var go = new GameObject("WhatsNewWindow", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            var modal = go.AddComponent<WhatsNewModal>();
            // Hidden from the first frame; the window's own Start keeps it so until it is opened.
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            modal.Setup(new VoyageKit(style, lines, view ? view.FontTemplate : null), data);
            if (_container != null)
                GameObjectInjector.InjectRecursive(go, _container);
            modal.ModalType = ModalWindows.WHATS_NEW;
            modal.AttachScreenSwitcher(this);
            Modals.Add(modal);
        }
    }
}
