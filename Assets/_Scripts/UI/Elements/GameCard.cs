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
        [Tooltip("Shows the vessel this mode is played in. Card IDENTITY - drawn from the moment " +
                 "the grid appears, for every card, whether or not there is a party.")]
        [SerializeField] Image VesselIcon;
        [Tooltip("Resolves a hull the Mode Map named as an override but the card itself does not " +
                 "list. Optional: without it the override can only resolve to a vessel the card " +
                 "already lists, which covers every mode except the ones whose Vessels list is " +
                 "empty (Wildlife Blitz).")]
        [SerializeField] SO_VesselList VesselList;

        [Header("Genre Petal")]
        [Tooltip("The element petal saying what KIND of game this is - a race is TIME, making " +
                 "or taking mass is MASS, destroying it is SPACE, working other pilots over is " +
                 "CHARGE. Card IDENTITY like the vessel icon, so it is drawn for every card " +
                 "from the moment the grid appears.\n\n" +
                 "Ships with no sprite and DISABLED: the art and the shape are both resolved at " +
                 "runtime from the mode itself, and an Image left enabled with no sprite draws " +
                 "a white quad.")]
        [SerializeField] Image GenrePetal;

        [Tooltip("The SECOND petal, for a mode that is genuinely two kinds of game at once " +
                 "(Brood Rush lays claim mass and tears the other side's out, so it is MASS and " +
                 "SPACE). Sits UNDER the first rather than beside it, so a single-genre card - " +
                 "which is nearly all of them - draws in exactly the place it always did.\n\n" +
                 "Ships with no sprite and DISABLED, and stays that way on every card whose " +
                 "genre is one element.")]
        [SerializeField] Image GenrePetalSecondary;

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
            UpdateGenrePetal(game);
        }

        /// <summary>
        /// Draws the hull this mode is played in. Unconditional: it is card IDENTITY, so it is
        /// drawn from the moment the grid appears, for every card, party or not.
        ///
        /// WHICH hull is the derivation the Mode Map already shows
        /// (<c>ModeMapWindow.EffectiveHull</c>), reproduced here rather than re-invented: the
        /// <see cref="ModeControlsLibrarySO"/> override if the mode authors one, otherwise the
        /// card's first listed vessel. The override is what makes a card that ALLOWS several
        /// hulls still name the one it is played in - Scurry lists Sparrow, Manta and Squirrel
        /// but is a Squirrel mode, so the list order alone would show the wrong ship.
        ///
        /// Falls back to <c>IconInactive</c> because a vessel can author one icon and not the
        /// other, and hides the image when the vessel has NO icon at all - a content gap rather
        /// than a wiring one: the Scarab currently authors neither, so Scarab Scramble draws no
        /// hull until that art exists.
        /// </summary>
        void UpdateVesselIcon(SO_ArcadeGame game)
        {
            if (!VesselIcon) return;

            var vessel = ResolveModeVessel(game);
            var sprite = vessel ? (vessel.IconActive ? vessel.IconActive : vessel.IconInactive) : null;

            VesselIcon.sprite = sprite;
            VesselIcon.enabled = sprite != null;
        }

        /// <summary>
        /// The hull a mode is played in, by the same rule the Mode Map applies: an authored
        /// override wins, else the card's first listed vessel. An override is resolved against
        /// the card's own list first (no extra asset needed for the common case) and only then
        /// against <see cref="VesselList"/>, which is what lets a card with an EMPTY Vessels
        /// list still name its hull.
        /// </summary>
        SO_Vessel ResolveModeVessel(SO_ArcadeGame game)
        {
            var library = Resources.Load<ModeControlsLibrarySO>(ModeControlsLibrarySO.ResourcePath);
            var over = library ? library.VesselFor(game.Mode) : VesselClassType.Any;

            if (over != VesselClassType.Any)
            {
                if (game.Vessels != null)
                    foreach (var v in game.Vessels)
                        if (v && v.Class == over) return v;

                if (VesselList && VesselList.TryGetVesselByClass(over, out var listed)) return listed;
            }

            if (game.Vessels != null)
                foreach (var v in game.Vessels)
                    if (v) return v;

            return null;
        }

        /// <summary>
        /// Draws the element petal (or two) that say what KIND of game this is. Unconditional
        /// and derived end to end, so a card can never advertise a genre its own end condition
        /// contradicts: <see cref="ModeGenre"/> answers which of the four kinds a mode is, and
        /// the fleet's own <see cref="ElementalBarsConfigSO"/> supplies the petal - the same art
        /// the vessel HUD flowers and the ability lockup's upgrade badge draw, so the card and
        /// the HUD cannot drift apart on what a Mass petal looks like.
        ///
        /// <para>Tinted the lockup's level-5 WHITE rather than a per-element colour, because the
        /// four petals are already told apart by SHAPE - that is what the flower is built on -
        /// and a second channel saying the same thing would only compete with the card art.</para>
        ///
        /// <para>Draws NOTHING when the mode has no genre (Maelstrom, which draws OTHER modes and
        /// so has none of its own). Blank is the honest state here, exactly as it is for a vessel
        /// with no icon - and the SECOND petal is blank on every card but the two-genre ones, so
        /// a card that draws one petal is saying that is the whole answer.</para>
        /// </summary>
        void UpdateGenrePetal(SO_ArcadeGame game)
        {
            ResolveGenrePetals(game.Mode, out var primary, out var secondary, out var tint);
            ApplyPetal(GenrePetal, primary, tint);
            ApplyPetal(GenrePetalSecondary, secondary, tint);
        }

        static void ApplyPetal(Image image, Sprite sprite, Color tint)
        {
            if (!image) return;

            image.sprite = sprite;
            if (sprite) image.color = tint;
            image.enabled = sprite != null;
        }

        /// <summary>
        /// The petal art for a mode's genre - one sprite, two, or none.
        ///
        /// <para>The metric the fallback reads comes from <see cref="ModePreviewLibrarySO"/>,
        /// which is where the launch panel's objective box already reads it: a mode's
        /// <c>ScoringRuleSO</c> lives in its own scene, so the preview definition is the
        /// platform's only pre-scene answer to "what is this mode scored on". Reading it here
        /// rather than adding a second table is what keeps the card, the objective box and the
        /// goal row saying one thing about the modes whose genre IS their metric.</para>
        ///
        /// <para>A mode with an explicit row in <see cref="ModeGenre"/> needs no definition at
        /// all, so a missing one is passed along as a NULL metric rather than short-circuiting -
        /// and null rather than the enum's zero, because <c>ScoringMetric.Crystals</c> is a real
        /// answer several cards give.</para>
        /// </summary>
        void ResolveGenrePetals(GameModes mode, out Sprite primary, out Sprite secondary,
                                out Color tint)
        {
            primary = null;
            secondary = null;
            tint = Color.white;

            if (!_previews) _previews = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            var definition = _previews ? _previews.Resolve(mode) : null;
            ScoringMetric? metric = definition ? definition.ObjectiveMetric : (ScoringMetric?)null;

            if (!ModeGenre.TryElementsFor(mode, metric, out var first, out var second)) return;

            if (!_bars) _bars = Resources.Load<ElementalBarsConfigSO>(ElementalBarsConfigSO.ResourcePath);
            if (!_bars) return;

            tint = _bars.whiteColor;
            primary = _bars.GetPetalSprite(first);
            if (second != Element.None) secondary = _bars.GetPetalSprite(second);
        }

        // Both assets are shipped singletons and the grid rebuilds every card on every refresh
        // (a favourite toggle repopulates the whole list), so the two lookups are cached per
        // session rather than per card - the shape ObjectiveIconSetSO.Load already uses.
        static ModePreviewLibrarySO _previews;
        static ElementalBarsConfigSO _bars;

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

            // The audio is a FLOURISH and the two lines under it are the ACTION, so the audio must
            // not be able to stop them. This is persistent call [0] on every card's favourite star,
            // ahead of the favourite itself - an unguarded deref here eats the toggle, on the one
            // control that was the workaround for the dead-card bug this file's row-clone sibling
            // just fixed. Same class, one component over.
            if (AudioSystem.Instance)
                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);

            FavoriteSystem.ToggleFavorite(gameMode);
            ExploreView.PopulateGameSelectionList();
        }

        public void OnCardClicked()
        {
            if (AudioSystem.Instance)
                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
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
