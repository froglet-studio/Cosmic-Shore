using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's GAMES tab (rev 3): every game the selected vessel can fly, as wide art cards like the
    /// Arcade's - its card art drifting behind its name, the vessel-only ones tagged - and a press opens
    /// that game's configure window (<see cref="ScreenSwitcher.OpenGame"/>).
    ///
    /// <para>Read off the game LISTS (Arcade + Arena), filtered by <c>SO_ArcadeGame.Vessels</c>, which is
    /// the reliable source: <c>SO_Vessel.Games</c> is stale. A fixed set of tiles is authored; unused
    /// ones hide through their CanvasGroups and leave the layout.</para>
    /// </summary>
    public class HangarGamesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Tile
        {
            public CanvasGroup group;
            public KitButton button;
            public Image art;
            public TMP_Text eyebrowText;
            public TMP_Text titleText;
            public TMP_Text lineText;
        }

        [SerializeField] private Tile[] tiles = new Tile[6];
        [Tooltip("The lists a game can come from, in display order.")]
        [SerializeField] private SO_GameList[] lists = new SO_GameList[2];
        [SerializeField] private ScreenSwitcher screenSwitcher;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("Words")]
        [Tooltip("Header; {vessel} is filled in.")]
        [SerializeField] private string headerTemplate = "Games for the {vessel}";
        [Tooltip("Eyebrow on a game only this vessel flies.")]
        [SerializeField] private string onlyThisTemplate = "{vessel} only";
        [Tooltip("Eyebrow on a game several vessels fly.")]
        [SerializeField] private string anyVesselWord = "Any vessel";

        readonly List<SO_ArcadeGame> _games = new();

        void Awake()
        {
            if (!screenSwitcher) screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            for (int i = 0; i < tiles.Length; i++)
            {
                int index = i;
                if (tiles[i]?.button) tiles[i].button.onClick.AddListener(() => Open(index));
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _games.Clear();
            if (vessel)
            {
                foreach (var list in lists)
                {
                    if (!list || list.Games == null) continue;
                    foreach (var game in list.Games)
                        if (game && game.Vessels != null && game.Vessels.Contains(vessel) && !_games.Contains(game)) _games.Add(game);
                }
                // The vessel's own games first: they are what makes this hull different.
                _games.Sort((a, b) => (a.Vessels.Count == 1 ? 0 : 1).CompareTo(b.Vessels.Count == 1 ? 0 : 1));
            }

            string name = vessel ? vessel.Name : string.Empty;
            if (headerText) headerText.text = headerTemplate.Replace("{vessel}", name);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                bool used = i < _games.Count;
                KitVisibility.SetShown(tile.group, used);
                if (!used) continue;
                var game = _games[i];
                if (tile.art)
                {
                    tile.art.sprite = game.CardBackground ? game.CardBackground : game.IconActive;
                    tile.art.enabled = tile.art.sprite;
                }
                if (tile.titleText) tile.titleText.text = game.DisplayName;
                if (tile.eyebrowText) tile.eyebrowText.text = game.Vessels.Count == 1 ? onlyThisTemplate.Replace("{vessel}", name) : anyVesselWord;
                if (tile.lineText) tile.lineText.text = game.Description;
            }
            KitVisibility.SetShown(emptyGroup, _games.Count == 0);
        }

        public void SetActivePage(bool active) { }

        void Open(int index)
        {
            if (index < 0 || index >= _games.Count || !screenSwitcher) return;
            screenSwitcher.OpenGame(_games[index]);
        }
    }
}
