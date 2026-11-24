using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One place in the Artifacts room you can touch (Rev 140 A, Docs/MENU_REDESIGN_B.md sections 230 and 231; Shombith,
    /// 2026-11-24: "I cannot click in the image itself that should be what I should be able to do", then "Lets build A"):
    /// an artifact floating over its pedestal in the room's clip, or a dark alcove that waits for an artifact still to
    /// find. The spot is an invisible button laid over that place in the picture, anchored to it as a fraction of the
    /// picture so it stays on it at any aspect; over it, its light: a halo round the artifact and its pedestal's ring.
    ///
    /// <para>The light is dark at rest, half lit while pointed at or focused by the pad (with the name over it), and
    /// fully lit while picked. Each state is an alpha on an authored group, never a colour written here: the theme pass
    /// re-applies every themed graphic's colour (<c>UIThemeLive.RepaintKit</c>).</para>
    ///
    /// <para><see cref="ArkArtifactsPage"/> writes it; the spot only draws and reports a press.</para>
    /// </summary>
    public class ArtifactSpot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("The invisible button over the artifact and its pedestal (or the alcove).")]
        [SerializeField] private Button button;
        [Tooltip("The artifact kind on this pedestal (ArtifactSO.Id, e.g. artifact.hydra). Empty: an alcove, an artifact still to find.")]
        [SerializeField] private string kindId = "";
        [Tooltip("Picked, the artifact's words stand to the LEFT of the room (the pedestals right of the column of light).")]
        [SerializeField] private bool wordsOnLeft;
        [Tooltip("The halo round the artifact and the pedestal's ring: its alpha is the spot's light.")]
        [SerializeField] private CanvasGroup lightGroup;
        [Tooltip("The name over the artifact, shown while pointed at or focused and not picked.")]
        [SerializeField] private CanvasGroup tagGroup;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The light while pointed at or focused.")]
        [Range(0f, 1f)]
        [SerializeField] private float hoverLight = 0.6f;
        [Tooltip("The light while picked.")]
        [Range(0f, 1f)]
        [SerializeField] private float pickedLight = 1f;

        bool _hover;
        bool _picked;
        bool _named = true;

        public Button Button => button;
        public string KindId => kindId;
        public bool IsAlcove => string.IsNullOrEmpty(kindId);
        public bool WordsOnLeft => wordsOnLeft;

        /// <summary>The point the leader line starts from: the top of the artifact, in the spot's own space.</summary>
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Draws the spot: <paramref name="name"/> over it (empty hides the tag), lit while <paramref name="picked"/>.</summary>
        public void Draw(string name, bool picked)
        {
            _picked = picked;
            _named = !string.IsNullOrEmpty(name);
            if (nameText) nameText.text = name ?? string.Empty;
            Apply();
        }

        public void OnPointerEnter(PointerEventData _) => SetHover(true);
        public void OnPointerExit(PointerEventData _) => SetHover(false);
        public void OnSelect(BaseEventData _) => SetHover(true);
        public void OnDeselect(BaseEventData _) => SetHover(false);

        void OnDisable() => SetHover(false);

        void SetHover(bool on)
        {
            _hover = on;
            Apply();
        }

        void Apply()
        {
            if (lightGroup) lightGroup.alpha = _picked ? pickedLight : _hover ? hoverLight : 0f;
            if (tagGroup) tagGroup.alpha = _hover && !_picked && _named ? 1f : 0f;
        }
    }
}
