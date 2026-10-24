using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.UI
{
    public enum ProfileModalTab
    {
        Avatar = 0,
        DisplayName = 1
    }

    /// <summary>
    /// The profile window (Docs/MENU_REDESIGN_B.md section 15.2): one window, two tabs. The
    /// Avatar tab is the 6 x 3 avatar grid - a pick saves at once; the Display name tab is the
    /// name field with a live character count, the four rules ticking as the player types, the
    /// danger strip for a refusal and a spinning hex while the service checks the name. A preview
    /// column shows the avatar and name the way other pilots see them.
    ///
    /// <para>Pages show and hide through their CanvasGroups (never SetActive), so the window opens
    /// on whichever tab it was asked for and the hidden page takes no focus. Every button is wired
    /// here in code: the window's body is a generated prefab that cannot carry calls into the
    /// scene.</para>
    /// </summary>
    public class ProfileIconSelectView : ModalWindowManager
    {
        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Avatar Layout")]
        [Tooltip("The grid the avatar buttons are spawned into. Its children are cleared on build.")]
        [SerializeField] private GridLayoutGroup iconGrid;
        [SerializeField] private ProfileIconSelectButton iconButtonPrefab;

        [Header("Display Name")]
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button displayNameSaveButton;
        [SerializeField] private Button displayNameCancelButton;

        [Header("Tabs (optional)")]
        [Tooltip("The AVATAR tab.")]
        [SerializeField] private KitTextTab avatarTab;
        [Tooltip("The DISPLAY NAME tab.")]
        [SerializeField] private KitTextTab nameTab;
        [Tooltip("The Avatar page: the grid, its save note and Done.")]
        [SerializeField] private CanvasGroup avatarPage;
        [Tooltip("The Display name page: the field, its rules, the message strip, Submit and Cancel.")]
        [SerializeField] private CanvasGroup namePage;
        [Tooltip("Closes the window from the Avatar page (the pick is already saved).")]
        [SerializeField] private Button doneButton;
        [Tooltip("Plays when the window opens. Optional.")]
        [SerializeField] private KitEntrance windowEntrance;

        [Header("Preview (optional)")]
        [Tooltip("The large avatar in the \"How others see you\" column.")]
        [SerializeField] private Image previewAvatar;
        [Tooltip("The name under it - the typed name while the Display name tab is up.")]
        [SerializeField] private TMP_Text previewName;
        [Tooltip("The party-slot chip's avatar.")]
        [SerializeField] private Image previewSlotAvatar;
        [Tooltip("The party-slot chip's name.")]
        [SerializeField] private TMP_Text previewSlotName;

        [Header("Avatar page (optional)")]
        [Tooltip("\"Your avatar saves as soon as you pick it.\", then \"Saved to your profile.\" after a pick.")]
        [SerializeField] private KitText saveNoteText;

        [Header("Name page (optional)")]
        [Tooltip("The chrome theme the rule marks and the field border resolve against. Unwired falls back to the authored section 11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("\"9 / 25\" in the field's corner.")]
        [SerializeField] private TMP_Text characterCountText;
        [Tooltip("The field's border: ControlActive, Danger while the name is refused.")]
        [SerializeField] private Image fieldBorder;
        [Tooltip("The four rule captions, in DisplayNameValidator.RuleCaptions order.")]
        [SerializeField] private KitText[] ruleLabels = { };
        [Tooltip("The four rule marks: a tick once met, a hollow diamond until then.")]
        [SerializeField] private Image[] ruleMarks = { };
        [SerializeField] private Sprite ruleMetSprite;
        [SerializeField] private Sprite ruleOpenSprite;
        [Tooltip("The danger strip that carries a refusal.")]
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private KitText messageText;
        [Tooltip("\"Checking the name...\" with its spinning hex, shown while the service decides.")]
        [SerializeField] private CanvasGroup checkingGroup;

        [Header("Profile / UGS")]
        [Inject] private PlayerDataService dataService;
        [Inject] private GameDataSO gameData;

        // Needed to inject runtime-instantiated icon buttons - Reflex does not auto-inject
        // prefabs created via Instantiate(), so the button's [Inject] AudioSystem would be null.
        [Inject] private Container _container;

        private const string SaveNoteIdle = "Your avatar saves as soon as you pick it.";
        private const string SaveNoteDone = "Saved to your profile.";

        // Internal state
        private ProfileIconSelectButton _selectedButton;
        private ProfileIcon             _selectedIcon;
        private bool                    _hasSelectedIcon;
        private ProfileModalTab         _tab;
        private bool                    _saving;
        private string                  _refusal;
        private bool                    _wired;

        // The singleton is the fallback for the same reason ProfileScreen uses it: the window can
        // be asked to open before Reflex has populated [Inject] fields.
        private PlayerDataService Service => dataService != null ? dataService : PlayerDataService.Instance;

        // ------------------------------------------------------------------
        // Unity
        // ------------------------------------------------------------------

        protected override void Start()
        {
            base.Start();

            // Wired before any early return: a missing avatar reference used to leave Save and
            // Cancel with no listener at all.
            Wire();
            SwitchTab(_tab);

            if (!profileIcons)
            {
                CSDebug.LogError("[ProfileIconSelectView] Missing SO_ProfileIconList reference.");
                return;
            }

            if (!iconGrid)
            {
                CSDebug.LogError("[ProfileIconSelectView] Missing GridLayoutGroup (iconGrid).");
                return;
            }

            if (!iconButtonPrefab)
            {
                CSDebug.LogError("[ProfileIconSelectView] Missing iconButtonPrefab.");
                return;
            }

            BuildAvatarGrid();
        }

        private void OnEnable()
        {
            OnModalOpened -= HandleOpened;
            OnModalOpened += HandleOpened;
        }

        protected override void OnDisable()
        {
            OnModalOpened -= HandleOpened;
            base.OnDisable();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (displayNameSaveButton)
                displayNameSaveButton.onClick.AddListener(SaveDisplayName);
            if (displayNameCancelButton)
                displayNameCancelButton.onClick.AddListener(CancelDisplayName);
            if (doneButton)
                doneButton.onClick.AddListener(ModalWindowOut);
            if (avatarTab)
                avatarTab.onClick.AddListener(() => SelectTab((int)ProfileModalTab.Avatar));
            if (nameTab)
                nameTab.onClick.AddListener(() => SelectTab((int)ProfileModalTab.DisplayName));

            if (displayNameInput)
            {
                // The limit follows the validator's config rather than a number in the prefab.
                displayNameInput.characterLimit = DisplayNameValidator.Config.MaxLength;
                displayNameInput.onValueChanged.AddListener(HandleNameTyped);
                // Enter submits, as the board's Submit does.
                displayNameInput.onSubmit.AddListener(_ => SaveDisplayName());
            }

            var captions = DisplayNameValidator.RuleCaptions();
            for (int i = 0; i < ruleLabels.Length && i < captions.Length; i++)
                if (ruleLabels[i]) ruleLabels[i].SetText(captions[i]);
        }

        // ------------------------------------------------------------------
        // Public API used by profile screen
        // ------------------------------------------------------------------

        /// <summary>
        /// Called from the profile screen "Change Avatar" button.
        /// </summary>
        public void OpenAvatar()
        {
            Open(ProfileModalTab.Avatar);
        }

        /// <summary>
        /// Called from the profile screen "Edit Name" button.
        /// </summary>
        public void OpenDisplayName()
        {
            Open(ProfileModalTab.DisplayName);
        }

        /// <summary>
        /// General entry point: decide which tab to show and open modal.
        /// </summary>
        public void Open(ProfileModalTab tab)
        {
            Wire();
            SwitchTab(tab);
            ModalWindowIn();
        }

        /// <summary>Show a tab without reopening the window (the tab row; 0 Avatar, 1 Display name).</summary>
        public void SelectTab(int tab)
        {
            SwitchTab((ProfileModalTab)Mathf.Clamp(tab, 0, 1));
        }

        // A bare ModalWindowIn (the lamp, the arcade widget) never chose a tab; the window opens
        // on the last one shown, already drawn, with the profile's current state.
        private void HandleOpened()
        {
            SwitchTab(_tab);
            SyncSelection();
            if (saveNoteText) saveNoteText.SetText(SaveNoteIdle);
            if (saveNoteText) saveNoteText.SetColorToken(UIColorToken.NeutralLightest);
            if (windowEntrance) windowEntrance.Play();
        }

        // ------------------------------------------------------------------
        // Tab switching
        // ------------------------------------------------------------------

        private void SwitchTab(ProfileModalTab tab)
        {
            _tab = tab;
            bool showAvatar = tab == ProfileModalTab.Avatar;

            KitVisibility.SetShown(avatarPage, showAvatar);
            KitVisibility.SetShown(namePage, !showAvatar);
            if (avatarTab) avatarTab.IsSelected = showAvatar;
            if (nameTab) nameTab.IsSelected = !showAvatar;

            if (!showAvatar)
                PopulateDisplayNameFromProfile();
            DrawPreview();
        }

        private void PopulateDisplayNameFromProfile()
        {
            _refusal = null;
            if (!displayNameInput)
            {
                DrawNamePage();
                return;
            }

            var service = Service;
            string existing = service != null && service.CurrentProfile != null
                ? service.CurrentProfile.Identity.DisplayName
                : string.Empty;

            displayNameInput.SetTextWithoutNotify(existing ?? string.Empty);
            DrawNamePage();
        }

        // ------------------------------------------------------------------
        // Preview column
        // ------------------------------------------------------------------

        private void DrawPreview()
        {
            var service = Service;
            var profile = service != null ? service.CurrentProfile : null;

            Sprite avatar = _hasSelectedIcon ? _selectedIcon.IconSprite
                : profile != null && service != null ? service.GetAvatarSprite(profile.Identity.AvatarId)
                : null;
            if (avatar)
            {
                if (previewAvatar) previewAvatar.sprite = avatar;
                if (previewSlotAvatar) previewSlotAvatar.sprite = avatar;
            }

            string current = profile != null ? profile.Identity.DisplayName : string.Empty;
            string typed = _tab == ProfileModalTab.DisplayName && displayNameInput
                ? DisplayNameValidator.Sanitize(displayNameInput.text)
                : string.Empty;
            string shown = string.IsNullOrEmpty(typed) ? current : typed;
            if (previewName) previewName.text = shown;
            if (previewSlotName) previewSlotName.text = shown;
        }

        // ------------------------------------------------------------------
        // Avatar grid build
        // ------------------------------------------------------------------

        private void BuildAvatarGrid()
        {
            int selectedIconId = LoadProfileIconSelection();

            ClearGrid();

            foreach (var profileIcon in profileIcons.profileIcons)
            {
                var buttonInstance = Instantiate(iconButtonPrefab, iconGrid.transform);
                buttonInstance.transform.localScale = Vector3.one;

                // Reflex doesn't auto-inject Instantiate()'d prefabs - inject so the button's
                // [Inject] AudioSystem resolves (otherwise OnClick NREs on the audio call).
                if (_container != null)
                    GameObjectInjector.InjectRecursive(buttonInstance.gameObject, _container);

                buttonInstance.ProfileIcon = profileIcon;
                buttonInstance.IconView    = this;

                bool isSelected = (profileIcon.Id == selectedIconId);
                if (isSelected)
                {
                    _selectedButton  = buttonInstance;
                    _selectedIcon    = profileIcon;
                    _hasSelectedIcon = true;
                }

                buttonInstance.SetSelected(isSelected);
            }

            // If nothing matched the saved id, default to first icon
            if (!_hasSelectedIcon && profileIcons.profileIcons.Count > 0)
            {
                var first = profileIcons.profileIcons[0];

                var allButtons = iconGrid.GetComponentsInChildren<ProfileIconSelectButton>(true);
                var firstButton = allButtons.FirstOrDefault(b => b.ProfileIcon.Id == first.Id);

                if (firstButton != null)
                {
                    _selectedButton  = firstButton;
                    _selectedIcon    = first;
                    _hasSelectedIcon = true;
                    firstButton.SetSelected(true);
                }
            }

            DrawPreview();
        }

        /// <summary>
        /// Re-highlight the profile's avatar. The grid is built once, so an avatar changed from
        /// anywhere else (another device's save arriving, the arcade widget) used to leave the old
        /// one ringed until the menu reloaded.
        /// </summary>
        private void SyncSelection()
        {
            var service = Service;
            if (!iconGrid || service == null || service.CurrentProfile == null)
            {
                DrawPreview();
                return;
            }

            int id = service.CurrentProfile.Identity.AvatarId;
            foreach (var button in iconGrid.GetComponentsInChildren<ProfileIconSelectButton>(true))
            {
                bool match = button.ProfileIcon.Id == id;
                button.SetSelected(match);
                if (!match) continue;
                _selectedButton = button;
                _selectedIcon = button.ProfileIcon;
                _hasSelectedIcon = true;
            }
            DrawPreview();
        }

        private void ClearGrid()
        {
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < iconGrid.transform.childCount; i++)
            {
                toDestroy.Add(iconGrid.transform.GetChild(i).gameObject);
            }

            foreach (var go in toDestroy)
            {
                Destroy(go);
            }
        }

        // ------------------------------------------------------------------
        // Avatar selection API (called by ProfileIconSelectButton)
        // ------------------------------------------------------------------

        public void SelectIcon(ProfileIconSelectButton button, ProfileIcon profileIcon)
        {
            if (_selectedButton != null && _selectedButton != button)
                _selectedButton.SetSelected(false);

            _selectedButton  = button;
            _selectedIcon    = profileIcon;
            _hasSelectedIcon = true;

            _selectedButton.SetSelected(true);

            SaveProfileIconSelection();
            DrawPreview();
            if (saveNoteText)
            {
                saveNoteText.SetText(SaveNoteDone);
                saveNoteText.SetColorToken(UIColorToken.TextLight);
            }
        }

        // ------------------------------------------------------------------
        // Avatar save / load
        // ------------------------------------------------------------------

        private void SaveProfileIconSelection()
        {
            if (!_hasSelectedIcon)
            {
                CSDebug.LogWarning("[ProfileIconSelectView] SaveProfileIconSelection called but no icon is selected.");
                return;
            }

            int id = _selectedIcon.Id;
            CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ProfileIconSelectView] SaveProfileIconSelection - id={id}");

            var service = Service;
            // Cloud save through UGS profile service
            if (service != null && service.IsInitialized)
            {
                service.SetAvatarId(id);      // do CloudSave inside this
            }
            else if (service != null)
            {
                // Not loaded yet (offline boot, or before the cloud profile arrives). The pick used
                // to be dropped entirely - the grid highlighted it while Home, Profile and the
                // in-match avatar kept the old one, under a warning that claimed it was cached.
                // Apply it to this session's profile; it is not written to the cloud.
                service.ApplyAvatarIdLocally(id);
                CSDebug.LogWarning("[ProfileIconSelectView] Profile not loaded yet - avatar applied for this session only, not saved to the cloud.");
            }
            else
            {
                CSDebug.LogWarning("[ProfileIconSelectView] No profile service - avatar not applied.");
            }
        }

        private int LoadProfileIconSelection()
        {
            var service = Service;
            if (service != null && service.IsInitialized && service.CurrentProfile != null)
            {
                int avatarId = service.CurrentProfile.Identity.AvatarId;
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ProfileIconSelectView] Loaded avatar from UGS - id={avatarId}");
                return avatarId;
            }

            return 0;
        }

        // ------------------------------------------------------------------
        // Display name: live rules, save / cancel
        // ------------------------------------------------------------------

        private void HandleNameTyped(string _)
        {
            // A service refusal ("already taken") answers the name that was submitted; typing
            // anything clears it.
            _refusal = null;
            DrawNamePage();
            DrawPreview();
        }

        /// <summary>
        /// The live state of the name page: count, rule ticks, the danger strip, Submit. The strip
        /// speaks once the name is long enough to judge (or empty), as the board does - a two-letter
        /// name in progress is shown by its length rule, not scolded.
        /// </summary>
        private void DrawNamePage()
        {
            string raw = displayNameInput ? displayNameInput.text : string.Empty;
            string sanitized = DisplayNameValidator.Sanitize(raw);
            var config = DisplayNameValidator.Config;
            var rules = DisplayNameValidator.EvaluateRules(raw);
            var check = DisplayNameValidator.Validate(raw);

            if (characterCountText)
                characterCountText.text = $"{sanitized.Length} / {config.MaxLength}";

            for (int i = 0; i < DisplayNameRuleState.Count; i++)
            {
                bool met = rules[i];
                if (i < ruleMarks.Length && ruleMarks[i])
                {
                    var sprite = met ? ruleMetSprite : ruleOpenSprite;
                    if (sprite) ruleMarks[i].sprite = sprite;
                    ruleMarks[i].color = UITheme.Resolve(theme, met ? UIColorToken.Cta : UIColorToken.NeutralLightest);
                }
                if (i < ruleLabels.Length && ruleLabels[i])
                    ruleLabels[i].SetColorToken(met ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
            }

            bool judge = sanitized.Length == 0 || sanitized.Length >= config.MinLength;
            string message = !string.IsNullOrEmpty(_refusal) ? _refusal
                : !check.IsValid && judge ? check.Message
                : null;
            bool refused = !string.IsNullOrEmpty(message) && !_saving;

            KitVisibility.SetShown(messageGroup, refused);
            if (messageText && refused) messageText.SetText(message);
            KitVisibility.SetShown(checkingGroup, _saving);
            if (fieldBorder)
                fieldBorder.color = UITheme.Resolve(theme, refused ? UIColorToken.Danger : UIColorToken.ControlActive);
            if (displayNameSaveButton)
                displayNameSaveButton.interactable = check.IsValid && !_saving && string.IsNullOrEmpty(_refusal);
        }

        private void SaveDisplayName()
        {
            if (!displayNameInput || _saving)
                return;

            SaveDisplayNameAsync(displayNameInput.text).Forget();
        }

        private async UniTaskVoid SaveDisplayNameAsync(string newName)
        {
            try
            {
                // Full rule set (length, characters, profanity, duplicates) lives in
                // PlayerDataService.TrySetDisplayNameAsync. On rejection the window stays open on
                // the strip, so the player can correct the name.
                var localCheck = DisplayNameValidator.Validate(newName);
                if (!localCheck.IsValid)
                {
                    Refuse(localCheck.Message);
                    return;
                }

                _saving = true;
                DrawNamePage();

                var service = Service;
                if (service != null)
                {
                    var result = await service.TrySetDisplayNameAsync(newName);
                    if (!result.IsValid)
                    {
                        _saving = false;
                        Refuse(result.Message);
                        return;
                    }
                }

                _saving = false;
                DrawNamePage();
                ModalWindowOut();
            }
            catch (Exception e)
            {
                _saving = false;
                Refuse("Can't save the name right now. Try again in a moment.");
                CSDebug.LogWarning($"[ProfileIconSelectView] SaveDisplayName failed: {e.Message}");
            }
        }

        private void Refuse(string message)
        {
            _refusal = message;
            DrawNamePage();
            if (displayNameInput) KitShake.Play(displayNameInput.gameObject);
        }

        private void CancelDisplayName()
        {
            // Revert input to current profile name, then close.
            PopulateDisplayNameFromProfile();
            ModalWindowOut();
        }
    }
}
