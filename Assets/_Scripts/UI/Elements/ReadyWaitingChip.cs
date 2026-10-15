using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One pilot in <see cref="ReadyWaitingPanel"/>'s roster: an octagonal avatar inside a ring and
    /// the pilot's state under it ("Ready" / "Picking"). The panel clones it from an inactive
    /// template and owns every colour on it; the chip only holds the references.
    /// </summary>
    public class ReadyWaitingChip : MonoBehaviour
    {
        [Tooltip("The octagonal ring behind the avatar. Wears the pilot's domain colour once ready.")]
        [SerializeField] private Image ring;
        [Tooltip("The pilot's avatar, inside the masked octagon.")]
        [SerializeField] private Image avatar;
        [Tooltip("'Ready' or 'Picking'.")]
        [SerializeField] private TMP_Text state;
        [Tooltip("Dims a pilot who has not pressed Ready yet.")]
        [SerializeField] private CanvasGroup group;

        public string PilotName { get; private set; }

        public void Bind(string pilotName, Sprite avatarSprite)
        {
            PilotName = pilotName;
            if (avatar)
            {
                avatar.sprite = avatarSprite;
                avatar.enabled = avatarSprite;
            }
        }

        public void Draw(bool ready, Color ringColor, Color stateColor, string stateWord, float alpha)
        {
            if (ring) ring.color = ringColor;
            if (state)
            {
                state.text = stateWord;
                state.color = stateColor;
            }
            if (group) group.alpha = alpha;
        }
    }
}
