using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The style-B Mission window's grid (Docs/MENU_REDESIGN_B.md section 17.2). It draws an
    /// <see cref="SO_MissionList"/> as <see cref="KitMissionCard"/>s.
    ///
    /// <para>It is deliberately NOT an <see cref="ArcadeExploreView"/>. Missions are separate games
    /// (<see cref="SO_Mission"/> derives from <see cref="SO_Game"/>, not <see cref="SO_ArcadeGame"/>),
    /// and the arcade explore view's roster, cards, favourites and configure flow are all typed to
    /// arcade games. The two windows share a look, not a data path.</para>
    ///
    /// <para>The cards ship Locked (their <see cref="MenuAvailabilityView"/>). A press on an Available
    /// card raises <see cref="MissionPicked"/>; the mission launch itself stays where it lives,
    /// <c>Arcade.LaunchMission</c>.</para>
    /// </summary>
    public class MissionExploreView : MonoBehaviour
    {
        [Inject] Container _container;

        [Header("Data")]
        [SerializeField, Tooltip("The missions to draw - MissionGames, the same list Arcade launches from.")]
        SO_MissionList missions;

        [Header("Layout")]
        [SerializeField, Tooltip("The grid the cards are spawned into.")]
        RectTransform grid;
        [SerializeField, Tooltip("The card prefab, one instance per mission.")]
        KitMissionCard cardPrefab;
        [SerializeField, Tooltip("\"1\" beside ALL MISSIONS. Optional.")]
        TMP_Text countText;

        readonly List<KitMissionCard> _cards = new();
        bool _built;

        /// <summary>Raised when an Available mission card is pressed.</summary>
        public event Action<SO_Mission> MissionPicked;

        /// <summary>The spawned cards, in roster order.</summary>
        public IReadOnlyList<KitMissionCard> Cards => _cards;

        void Start() => Build();

        public void Build()
        {
            if (_built || !missions || !grid || !cardPrefab) return;
            _built = true;
            foreach (var mission in missions.Games)
            {
                if (!mission) continue;
                var card = Instantiate(cardPrefab, grid);
                card.name = $"MissionCard_{mission.name}";
                // Spawned at runtime, so the scene's injection pass never saw it: the card's
                // MenuAvailabilityView needs the AudioSystem for its refusal sting.
                if (_container != null) GameObjectInjector.InjectRecursive(card.gameObject, _container);
                card.Draw(mission);
                card.Pressed += OnCardPressed;
                _cards.Add(card);
            }
            if (countText) countText.text = _cards.Count.ToString();
        }

        void OnCardPressed(KitMissionCard card)
        {
            foreach (var c in _cards)
                if (c.Tile) c.Tile.IsSelected = c == card;
            MissionPicked?.Invoke(card.Mission);
        }

        void OnDestroy()
        {
            foreach (var c in _cards)
                if (c) c.Pressed -= OnCardPressed;
        }
    }
}
