using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyage window's Saga tab in the picked look (Rev 77 "Saga", Docs/MENU_REDESIGN_B.md sections 150 and 156).
    /// The picked part's art fills the left, muted, with "Saga - part 1 of 2", its name and its forms at the foot. The
    /// right holds the parts on a coverflow (<see cref="VoyageFlowCard"/>, posed as the Voyages tab's cards are), the
    /// picked part's line, and one Lime Set sail. Set sail opens the part's launch window through the ONE configure modal
    /// (<see cref="ArcadeGameConfigureModal.OpenFor"/>), as <see cref="VoyageView"/>'s cards did.
    ///
    /// <para>A part is an ordinary arcade card (<see cref="SO_ArcadeGame"/>): its art, name and description are the
    /// card's, and its forms are a line authored here. Every other word is a serialized line too.
    /// Tools/Build/author_voyage_window.py lays the tab out and authors them.</para>
    /// </summary>
    public class SagaView : MonoBehaviour
    {
        [Serializable]
        public class Part
        {
            [Tooltip("The part's arcade card (Tandava, Tandava: Severing).")]
            public SO_ArcadeGame game;
            [Tooltip("The part's forms, as one line under its name. Empty shows nothing.")]
            public string forms;
        }

        [Header("Saga")]
        [Tooltip("The saga's parts, in order.")]
        [SerializeField] Part[] parts = Array.Empty<Part>();
        [Tooltip("The configure modal that launches a part. Found in the scene when left empty or when it cannot launch the " +
                 "part (the window is a prefab and cannot name the scene's modal).")]
        [SerializeField] ArcadeGameConfigureModal configureModal;
        [Tooltip("The text whose font the tab's words take (the window's title).")]
        [SerializeField] TMP_Text fontFrom;

        [Header("Words")]
        [Tooltip("Over the part's name on the left: {n} the part's number, {total} the parts.")]
        [SerializeField] string partLine = "Saga - part {n} of {total}";
        [Tooltip("A card's name: {n} its number (01), {name} the part's name.")]
        [SerializeField] string cardLine = "{n}  {name}";
        [Tooltip("The call to action.")]
        [SerializeField] string setSailLabel = "Set sail";

        [Header("Look")]
        [Tooltip("Card size, px.")]
        [SerializeField] Vector2 cardSize = new(300f, 280f);
        [Tooltip("The left picture's multiply (the board's saturate .35, brightness .6, as near as a multiply comes).")]
        [SerializeField] Color featureTint = new(0.6f, 0.6f, 0.66f, 1f);
        [Tooltip("The cards' art multiply.")]
        [SerializeField] Color artTint = new(0.7f, 0.7f, 0.76f, 1f);

        readonly List<VoyageFlowCard> _cards = new();
        RectTransform _flow;
        RawImage _feature;
        TMP_Text _part, _title, _forms, _line;
        bool _built;
        int _picked;

        int Count => parts?.Length ?? 0;

        SO_ArcadeGame GameOf(int i) => i >= 0 && i < Count && parts[i] != null ? parts[i].game : null;

        void Awake() => Build();

        void OnEnable() => Refresh();

        void Pick(int index)
        {
            if (index == _picked || index < 0 || index >= Count) return;
            _picked = index;
            Refresh();
        }

        /// <summary>Redraw the picked part and stand the cards about it.</summary>
        public void Refresh()
        {
            if (!_built) Build();
            int count = Count;
            _picked = Mathf.Clamp(_picked, 0, Mathf.Max(0, count - 1));

            while (_cards.Count < count) _cards.Add(FlowUi.Card(_flow, _cards.Count, cardSize, fontFrom, Pick));
            for (int i = 0; i < count; i++)
            {
                var game = GameOf(i);
                _cards[i].Name.text = TemplateText.Fill(cardLine, "n", (i + 1).ToString("00"), "name", game ? game.DisplayName : null);
                FlowUi.Art(_cards[i].Art, game ? game.CardBackground : null, cardSize.x / Mathf.Max(1f, cardSize.y));
                _cards[i].Art.color = artTint;
            }
            FlowUi.Stand(_cards, count, _picked, cardSize.x * 0.82f);

            var picked = GameOf(_picked);
            var rt = _feature.rectTransform.rect;
            FlowUi.Art(_feature, picked ? picked.CardBackground : null, rt.height > 1f ? rt.width / rt.height : 1.2f);
            _feature.color = featureTint;
            _part.text = count > 0 ? TemplateText.Fill(partLine, "n", (_picked + 1).ToString(), "total", count.ToString()) : string.Empty;
            _title.text = picked ? picked.DisplayName ?? string.Empty : string.Empty;
            _forms.text = count > 0 && parts[_picked] != null ? parts[_picked].forms ?? string.Empty : string.Empty;
            _line.text = picked ? picked.Description ?? string.Empty : string.Empty;
        }

        void SetSail()
        {
            var game = GameOf(_picked);
            if (!game) return;
            // Not just any copy: Menu_Main has three, and the Arena and Maelstrom windows' are hosts with no launch panels
            // (claude/tandava-arena-mode, 2026-10-29: opening a card on one bound nothing and Start was dead).
            if (!configureModal || !configureModal.CanOpen(game)) configureModal = FindLauncher(game);
            if (configureModal) configureModal.OpenFor(game);
            else CSDebug.LogWarning($"[SagaView] Set sail found no ArcadeGameConfigureModal that launches '{game.DisplayName}'.");
        }

        static ArcadeGameConfigureModal FindLauncher(SO_ArcadeGame game)
        {
            foreach (var modal in FindObjectsByType<ArcadeGameConfigureModal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (modal && modal.CanOpen(game)) return modal;
            return null;
        }

        void OnRectTransformDimensionsChange()
        {
            // The left picture is cropped to its own shape, which is only known once the page is laid out.
            if (_built && isActiveAndEnabled) Refresh();
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            var root = (RectTransform)transform;

            // The left: the picked part's art, with its words at the foot.
            var left = FlowUi.Node("Feature", root);
            FlowUi.Anchor(left, Vector2.zero, new Vector2(0.56f, 1f), Vector2.zero, Vector2.zero);
            var plate = FlowUi.Fill(left, FlowUi.Deep);
            left.gameObject.AddComponent<RectMask2D>();
            FlowUi.Edge(plate, FlowUi.Line);
            var art = FlowUi.Node("Art", left);
            FlowUi.Anchor(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _feature = art.gameObject.AddComponent<RawImage>();
            _feature.raycastTarget = false;
            _part = FlowUi.Text(left, fontFrom, 14, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(_part.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 124f), new Vector2(-36f, 146f));
            _title = FlowUi.Text(left, fontFrom, 48, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(_title.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 64f), new Vector2(-36f, 120f));
            _forms = FlowUi.Text(left, fontFrom, 18, FlowUi.Light, TextAlignmentOptions.TopLeft);
            _forms.textWrappingMode = TextWrappingModes.Normal;
            FlowUi.Anchor(_forms.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 14f), new Vector2(-36f, 58f));

            // The right: the parts on the coverflow, the picked part's line, and Set sail.
            var right = FlowUi.Node("Parts", root);
            FlowUi.Anchor(right, new Vector2(0.58f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            _flow = FlowUi.Node("Flow", right);
            FlowUi.Anchor(_flow, new Vector2(0f, 0.42f), Vector2.one, Vector2.zero, Vector2.zero);
            _flow.gameObject.AddComponent<RectMask2D>();
            _line = FlowUi.Text(right, fontFrom, 18, FlowUi.Light, TextAlignmentOptions.TopLeft);
            _line.textWrappingMode = TextWrappingModes.Normal;
            _line.overflowMode = TextOverflowModes.Ellipsis;
            FlowUi.Anchor(_line.rectTransform, Vector2.zero, new Vector2(1f, 0.42f), new Vector2(0f, 84f), new Vector2(0f, -20f));
            var sail = FlowUi.Node("SetSail", right);
            FlowUi.Anchor(sail, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 56f));
            var act = FlowUi.Act(sail, fontFrom, SetSail);
            act.label.text = setSailLabel ?? string.Empty;
        }
    }
}
