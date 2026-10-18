using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The settings window's ACCOUNT tab (Docs/MENU_REDESIGN_B.md §54, the approved R9Account boards): who you are (pilot
    /// name and a short player ID), a guest warning while no account is linked, one row per sign-in provider the
    /// platform offers (its mark, its name, "Linked" or a LINK button), Sign out and Delete account. Linking, signing out
    /// and deleting go through <see cref="IPlatformSignIn"/>; until the login branch fills it, no provider is available,
    /// so in a release build the tab shows the pilot, the guest warning and nothing to press. A link that finds the
    /// account already owns another pilot opens <see cref="AccountPromptWindow"/>'s conflict.
    /// The actions are main-menu only (<see cref="SetMenuContext"/>, from GameSettingsPanelController's context lock).
    /// </summary>
    public class AccountSettingsView : MonoBehaviour
    {
        [Serializable]
        public struct AccountRow
        {
            public SignInProvider provider;
            public RectTransform root;
            [Tooltip("The provider's mark; replaced by SignInProvidersSO's mark when that is set.")]
            public Image mark;
            [Tooltip("Under the provider's name: the linked account, \"Not linked\", or what went wrong.")]
            public TMP_Text line;
            public Button linkButton;
            [Tooltip("Shown instead of LINK once linked (its word is authored on the prefab).")]
            public GameObject linkedMark;
        }

        [Header("Config")]
        [SerializeField] SignInProvidersSO providers;

        [Header("Pilot")]
        [SerializeField] TMP_Text pilotName;
        [SerializeField] TMP_Text playerIdText;
        [SerializeField] GameObject guestNotice;

        [Header("Linked accounts")]
        [SerializeField] AccountRow[] rows;
        [Tooltip("Top of the first row and the row pitch, inside the rows' parent; rows the platform does not offer collapse.")]
        [SerializeField] float rowsTop = 34f;
        [SerializeField] float rowPitch = 84f;

        [Header("Actions")]
        [SerializeField] RectTransform actionsRow;
        [SerializeField] float actionsGap = 26f;
        [SerializeField] Button signOutButton;
        [SerializeField] Button deleteButton;

        [Header("Window")]
        [SerializeField] AccountPromptWindow prompt;

        [Header("Words (empty shows nothing)")]
        [SerializeField] string steamName = "Steam";
        [SerializeField] string googleName = "Google";
        [SerializeField] string appleName = "Apple";
        [Tooltip("{id} is the player ID, shortened.")]
        [SerializeField] string playerIdLine = "Player ID {id}";
        [SerializeField] string notLinkedLine = "Not linked";
        [SerializeField] string notReadyLine = "Not ready in this build";
        [SerializeField] string linkingLine = "Linking...";
        [SerializeField] string linkFailedLine = "Did not link. Try again.";
        [Tooltip("{provider} is the provider's name.")]
        [SerializeField] string conflictEyebrow = "Link {provider}";
        [SerializeField] string conflictTitle = "This {provider} account already has a pilot";
        [SerializeField] string conflictBody = "Pilots cannot be merged. Choose which one to fly; the other stays where it is.";
        [SerializeField] string conflictFoot = "Keeping {this} leaves {provider} unlinked. Switching signs this device in as {other}.";
        [SerializeField] string thisPilotCaption = "On this device";
        [SerializeField] string otherPilotCaption = "On {provider}";
        [Tooltip("{name} is the pilot's name.")]
        [SerializeField] string flyOtherLabel = "Fly {name}";
        [SerializeField] string keepThisLabel = "Keep {name}";
        [SerializeField] string cancelLabel = "Cancel";
        [SerializeField] string signOutTitle = "Sign out?";
        [SerializeField] string signOutBody = "You can sign back in with any linked account.";
        [SerializeField] string signOutConfirm = "Sign out";
        [SerializeField] string deleteTitle = "Delete your account?";
        [SerializeField] string deleteBody = "Your pilot, crystals and vessels are deleted from our servers for good. This cannot be undone.";
        [SerializeField] string deleteConfirm = "Delete account";

        [Inject] IPlatformSignIn _signIn;
        [Inject] AuthenticationServiceFacade _auth;
        [Inject] PlayerDataService _playerData;

        bool _menu = true;
        bool _busy;
        CancellationTokenSource _cts;
        IPlatformSignIn _subscribed;

        void Awake()
        {
            foreach (var r in rows)
            {
                var provider = r.provider;
                if (r.linkButton) r.linkButton.onClick.AddListener(() => LinkAsync(provider).Forget());
                var mark = providers ? providers.MarkFor(provider) : null;
                if (r.mark && mark) r.mark.sprite = mark;
            }
            if (signOutButton) signOutButton.onClick.AddListener(ConfirmSignOut);
            if (deleteButton) deleteButton.onClick.AddListener(ConfirmDelete);
        }

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
            Refresh();
        }

        void Start()
        {
            // [Inject] fields land after Awake/OnEnable; draw again with the services.
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void OnDestroy()
        {
            if (_subscribed != null) _subscribed.AccountsChanged -= OnAccountsChanged;
        }

        void Subscribe()
        {
            if (_subscribed != null || _signIn == null) return;
            _subscribed = _signIn;
            _subscribed.AccountsChanged += OnAccountsChanged;
        }

        void OnAccountsChanged() => RefreshOnMainThreadAsync().Forget();

        async UniTaskVoid RefreshOnMainThreadAsync()
        {
            await MainThreadDispatcher.SwitchToMainThreadAsync();
            if (this) Refresh();
        }

        /// <summary>Main menu: the actions work. In a match: they are shown but locked (leaving the game, or losing the
        /// pilot mid-match, belong in the menu).</summary>
        public void SetMenuContext(bool menu)
        {
            _menu = menu;
            Refresh();
        }

        public void Refresh()
        {
            var profile = _playerData != null && _playerData.IsInitialized ? _playerData.CurrentProfile : null;
            Set(pilotName, profile?.Identity.DisplayName ?? string.Empty);
            string id = _auth != null ? _auth.PlayerId : string.Empty;
            Set(playerIdText, string.IsNullOrEmpty(id) ? string.Empty : TemplateText.Fill(playerIdLine, "id", ShortId(id)));

            bool anyLinked = false;
            float y = rowsTop;
            foreach (var r in rows)
            {
                if (!r.root) continue;
                bool linked = _signIn != null && _signIn.IsLinked(r.provider);
                anyLinked |= linked;
                bool shown = linked || (providers && providers.Shows(r.provider, _signIn));
                r.root.gameObject.SetActive(shown);
                if (!shown) continue;
                r.root.anchoredPosition = new Vector2(r.root.anchoredPosition.x, -y);
                y += rowPitch;
                bool available = _signIn != null && _signIn.IsAvailable(r.provider);
                Set(r.line, linked ? LinkedLine(r.provider) : available ? notLinkedLine : notReadyLine);
                if (r.linkedMark) r.linkedMark.SetActive(linked);
                if (r.linkButton)
                {
                    r.linkButton.gameObject.SetActive(!linked);
                    r.linkButton.interactable = _menu && !_busy;
                }
            }
            if (actionsRow) actionsRow.anchoredPosition = new Vector2(actionsRow.anchoredPosition.x, -(y + actionsGap));
            if (guestNotice) guestNotice.SetActive(!anyLinked);

            if (signOutButton)
            {
                signOutButton.gameObject.SetActive(_signIn != null && _signIn.CanSignOut);
                signOutButton.interactable = _menu && !_busy;
            }
            if (deleteButton)
            {
                deleteButton.gameObject.SetActive(_signIn != null && _signIn.CanDeleteAccount);
                deleteButton.interactable = _menu && !_busy;
            }
        }

        string LinkedLine(SignInProvider provider)
        {
            string name = _signIn.LinkedName(provider);
            return string.IsNullOrEmpty(name) ? string.Empty : name;
        }

        async UniTaskVoid LinkAsync(SignInProvider provider)
        {
            if (_busy || !_menu) return;
            var row = Row(provider);
            if (_signIn == null || !_signIn.IsAvailable(provider))
            {
                Set(row.line, notReadyLine);
                return;
            }
            _busy = true;
            Refresh();
            Set(row.line, linkingLine);
            SignInOutcome outcome;
            try
            {
                outcome = await _signIn.LinkAsync(provider, _cts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                outcome = new SignInOutcome(SignInStatus.Failed, ex.Message);
            }
            finally
            {
                _busy = false;
            }
            await MainThreadDispatcher.SwitchToMainThreadAsync();
            if (!this) return;
            Refresh();
            switch (outcome.Status)
            {
                case SignInStatus.Succeeded:
                    break;
                case SignInStatus.AlreadyHasPilot:
                    ShowConflict(provider, outcome);
                    break;
                default:
                    CSDebug.LogWarning($"[Account] Linking {provider} ended {outcome.Status}: {outcome.Reason}");
                    Set(row.line, linkFailedLine);
                    break;
            }
        }

        void ShowConflict(SignInProvider provider, SignInOutcome outcome)
        {
            if (!prompt) return;
            string n = NameOf(provider);
            string self = outcome.ThisPilot.Name, other = outcome.OtherPilot.Name;
            prompt.ShowConflict(
                TemplateText.Fill(conflictEyebrow, "provider", n),
                TemplateText.Fill(conflictTitle, "provider", n),
                TemplateText.Fill(conflictBody, "provider", n),
                TemplateText.Fill(conflictFoot, "provider", n, "this", self, "other", other),
                TemplateText.Fill(thisPilotCaption, "provider", n), outcome.ThisPilot,
                TemplateText.Fill(otherPilotCaption, "provider", n), outcome.OtherPilot,
                TemplateText.Fill(flyOtherLabel, "name", other), () => SwitchAsync(provider).Forget(),
                TemplateText.Fill(keepThisLabel, "name", self), () => { },
                cancelLabel);
        }

        async UniTaskVoid SwitchAsync(SignInProvider provider)
        {
            if (_signIn == null) return;
            var outcome = await _signIn.SwitchToLinkedPilotAsync(provider, _cts?.Token ?? CancellationToken.None);
            await MainThreadDispatcher.SwitchToMainThreadAsync();
            if (!outcome.Succeeded)
                CSDebug.LogWarning($"[Account] Switching to the {provider} pilot ended {outcome.Status}: {outcome.Reason}");
            if (this) Refresh();
        }

        void ConfirmSignOut()
        {
            if (!prompt || _signIn == null || !_menu) return;
            prompt.ShowConfirm(signOutTitle, signOutBody, signOutConfirm, () => _signIn.SignOutAsync(CancellationToken.None).Forget(), cancelLabel);
        }

        void ConfirmDelete()
        {
            if (!prompt || _signIn == null || !_menu) return;
            prompt.ShowConfirm(deleteTitle, deleteBody, deleteConfirm, () => _signIn.DeleteAccountAsync(CancellationToken.None).Forget(), cancelLabel);
        }

        AccountRow Row(SignInProvider provider)
        {
            foreach (var r in rows)
                if (r.provider == provider) return r;
            return default;
        }

        string NameOf(SignInProvider provider) => provider switch
        {
            SignInProvider.Steam => steamName,
            SignInProvider.Google => googleName,
            SignInProvider.Apple => appleName,
            _ => string.Empty,
        };

        static string ShortId(string id) => id.Length <= 10 ? id : id.Substring(0, 4) + "..." + id.Substring(id.Length - 4);

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s ?? string.Empty;
        }

        [ContextMenu("Preview: link conflict")]
        void PreviewConflict() => ShowConflict(SignInProvider.Google, new SignInOutcome(SignInStatus.AlreadyHasPilot, null,
            new PilotSummary("Nova Runner", 1200, 3, 4), new PilotSummary("Shomboss", 9800, 9, 21)));

        [ContextMenu("Preview: delete account")]
        void PreviewDelete()
        {
            if (prompt) prompt.ShowConfirm(deleteTitle, deleteBody, deleteConfirm, () => { }, cancelLabel);
        }
    }
}
