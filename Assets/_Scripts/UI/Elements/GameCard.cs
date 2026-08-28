using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class GameCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] SO_GameList AllGames;
        [SerializeField] Sprite StarIconActive;
        [SerializeField] Sprite StarIconInActive;
        [HideInInspector] public ArcadeExploreView ExploreView;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image StarImage;
        [SerializeField] int Index;

        [Header("Mode Vessel")]
        [Tooltip("Shows the vessel this mode is played in. Most arcade modes lock to one hull, " +
                 "so this is card IDENTITY - it is drawn for every card, party or not.")]
        [SerializeField] Image VesselIcon;

        [Header("Party Picks")]
        [Tooltip("Container the interested party members' avatars are laid out in. The FIRST " +
                 "AvatarIcon under it is the authored template every extra chip is cloned from, " +
                 "so the look is authored once in the prefab rather than in code.")]
        [SerializeField] Transform AvatarSpace;
        [Tooltip("Resolves a party member's avatar id to its sprite - the same list the arcade " +
                 "lobby panel uses, so a member wears the same face in both places.")]
        [SerializeField] SO_ProfileIconList ProfileIcons;
        [Tooltip("The card's own Border image, TINTED when the local player is one of the members " +
                 "queuing for this card, so a client can tell their own request apart from a " +
                 "teammate's at a glance.")]
        [SerializeField] Image LocalPickBorder;
        [Tooltip("Colour the border takes while this is the local player's pick. The UNPICKED " +
                 "colour is whatever the prefab authored - captured at Awake rather than " +
                 "restated here, so re-authoring the border cannot leave the two out of step.")]
        [SerializeField] Color localPickBorderColor = new Color(0.45f, 1f, 0.85f, 1f);

        // The border's authored colour, so "not picked" restores exactly what the prefab drew.
        Color _borderRestColor = Color.white;

        // The authored chip, captured before anything is cloned from it. Kept as the pool's
        // element 0 so the prefab's own object is the one that renders in the common case.
        Image _avatarTemplate;
        readonly List<Image> _avatarChips = new();

        [Header("Lock State")]
        [Tooltip("Overlay shown when the game mode is locked")]
        [SerializeField] private GameObject lockOverlay;
        [Tooltip("Tint color applied to the card background when locked")]
        [SerializeField] private Color lockedTintColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private bool _isLocked;
        private Color _originalBgColor = Color.white;

        bool favorited;
        public bool Favorited
        {
            get { return favorited; }
            set
            {
                favorited = value;
                UpdateCardView();
            }
        }

        GameModes gameMode;
        public GameModes GameMode
        {
            get { return gameMode; }
            set
            {
                gameMode = value;
                UpdateCardView();
            }
        }

        void Awake()
        {
            // Captured BEFORE any clone exists, or the template would be re-resolved to a clone
            // on a later refresh and the pool would grow a generation deeper each time.
            if (AvatarSpace && AvatarSpace.childCount > 0)
            {
                _avatarTemplate = AvatarSpace.GetChild(0).GetComponent<Image>();
                if (_avatarTemplate) _avatarChips.Add(_avatarTemplate);
            }

            if (LocalPickBorder) _borderRestColor = LocalPickBorder.color;
        }

        void Start()
        {
            if (gameMode == GameModes.Random)
                gameMode = GameModes.BlockBandit;

            UpdateCardView();
            ShowPartyPicks(null, false);
        }

        void UpdateCardView()
        {
            SO_ArcadeGame game = AllGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
            if (game == null)
            {
                Debug.LogWarning($"GameCard: No SO_ArcadeGame found for mode {gameMode} on {gameObject.name}");
                return;
            }

            GameTitle.text = game.DisplayName;
            BackgroundImage.sprite = game.CardBackground;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;

            UpdateVesselIcon(game);
        }

        /// <summary>
        /// Draws the hull this mode is played in. Arcade modes are overwhelmingly vessel-locked
        /// (SO_ArcadeGame.Vessels), so the FIRST entry is the mode's vessel; a mode that allows
        /// several draws nothing rather than picking one arbitrarily and telling the player
        /// something untrue about what they will fly.
        /// </summary>
        void UpdateVesselIcon(SO_ArcadeGame game)
        {
            if (!VesselIcon) return;

            var vessel = game.Vessels is { Count: 1 } ? game.Vessels[0] : null;
            var sprite = vessel ? vessel.IconActive : null;

            VesselIcon.sprite = sprite;
            VesselIcon.enabled = sprite != null;
        }

        /// <summary>
        /// Shows which party members are asking to play this card. Called by the grid whenever
        /// the replicated pick list changes; <paramref name="avatarIds"/> null or empty clears
        /// the card back to no chips.
        /// </summary>
        public void ShowPartyPicks(IReadOnlyList<int> avatarIds, bool includesLocalPlayer)
        {
            // TINT the border rather than switching an object on: the card's Border is a
            // decorative frame that is drawn on EVERY card, so using its active state as the
            // highlight would strip the border off every card the local player has not picked.
            if (LocalPickBorder)
                LocalPickBorder.color = includesLocalPlayer ? localPickBorderColor : _borderRestColor;

            if (!AvatarSpace || !_avatarTemplate) return;

            int wanted = avatarIds?.Count ?? 0;

            // Grow the pool from the AUTHORED chip, so extra chips inherit its size, anchoring
            // and material without any of that being restated in code.
            while (_avatarChips.Count < wanted)
            {
                var clone = Instantiate(_avatarTemplate, AvatarSpace);
                clone.name = $"{_avatarTemplate.name} ({_avatarChips.Count})";
                _avatarChips.Add(clone);
            }

            for (int i = 0; i < _avatarChips.Count; i++)
            {
                var chip = _avatarChips[i];
                if (!chip) continue;

                bool used = i < wanted;
                chip.gameObject.SetActive(used);
                if (used) chip.sprite = ResolveAvatar(avatarIds[i]);
            }
        }

        /// <summary>
        /// Avatar id to sprite, matching ArcadeLobbyList's resolution exactly - including its
        /// fallback to the first icon, so an unknown id draws a face rather than a hole.
        /// </summary>
        Sprite ResolveAvatar(int avatarId)
        {
            if (!ProfileIcons || ProfileIcons.profileIcons == null) return null;

            foreach (var icon in ProfileIcons.profileIcons)
                if (icon.Id == avatarId)
                    return icon.IconSprite;

            return ProfileIcons.profileIcons.Count > 0 ? ProfileIcons.profileIcons[0].IconSprite : null;
        }

        public void ToggleFavorite()
        {
            Favorited = !Favorited;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
            FavoriteSystem.ToggleFavorite(gameMode);
            ExploreView.PopulateGameSelectionList();
        }

        public void OnCardClicked()
        {
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
            CSDebug.Log($"GameCard - Clicked: Gamemode: {gameMode}");

            SO_ArcadeGame game = AllGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
            if (game != null)
                FTUEEventManager.RaiseCTAClicked(game.CallToActionTargetType);
        }

        /// <summary>
        /// Sets the visual locked state of this card.
        /// Locked cards are greyed out with a lock icon overlay and non-interactable.
        /// </summary>
        public void SetLocked(bool locked)
        {
            if (lockOverlay != null)
                lockOverlay.SetActive(locked);

            if (BackgroundImage != null)
            {
                // Only save the original color when transitioning from unlocked → locked
                // to avoid overwriting it with the tinted color on repeated SetLocked(true) calls
                if (locked && !_isLocked)
                    _originalBgColor = BackgroundImage.color;

                BackgroundImage.color = locked ? lockedTintColor : _originalBgColor;
            }

            _isLocked = locked;

            if (TryGetComponent<Button>(out var btn))
                btn.interactable = !locked;
        }
    }
}
