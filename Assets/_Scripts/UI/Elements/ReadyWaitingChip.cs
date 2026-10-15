using DG.Tweening;
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

        [Tooltip("How far the chip swells the moment its pilot readies (the board's tick), then settles.")]
        [SerializeField, Min(0f)] private float readyPunch = 0.2f;

        public string PilotName { get; private set; }
        bool _wasReady;
        Tween _punch;

        public void Bind(string pilotName, Sprite avatarSprite)
        {
            if (PilotName != pilotName) _wasReady = false;
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
            if (ready && !_wasReady && readyPunch > 0f)
            {
                _punch?.Kill(true);
                transform.localScale = Vector3.one;
                _punch = transform.DOPunchScale(Vector3.one * readyPunch, 0.32f, 1, 0f).SetLink(gameObject).SetUpdate(true);
            }
            _wasReady = ready;
        }

        void OnDestroy() => _punch?.Kill();
    }
}
