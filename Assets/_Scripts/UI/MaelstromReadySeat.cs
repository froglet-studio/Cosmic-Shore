using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One pilot's face in the Maelstrom hub's "Pilots ready" row (Docs/MENU_REDESIGN_B.md section
    /// 27): the avatar inside an octagon ring, the name, and Ready / Waiting. The ring takes the
    /// pilot's domain colour once they are ready and the face dims while they are not - the same
    /// reading the in-match Ready plate gives. Humans only: an AI has no button, so a seat for one
    /// could never change (MaelstromLobby.HumanPlayers).
    /// </summary>
    public class MaelstromReadySeat : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image ring;
        [SerializeField] Image avatar;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text stateText;
        [SerializeField] UIThemeSO theme;

        [Header("Words (empty = no line)")]
        [SerializeField] string readyWord = "Ready";
        [SerializeField] string waitingWord = "Waiting";

        [Tooltip("Alpha of a waiting pilot's face.")]
        [SerializeField, Range(0f, 1f)] float waitingAlpha = 0.6f;

        bool _shown;
        bool _ready;

        public void Show(string pilotName, Sprite face, Color domainColor, bool ready)
        {
            bool punch = _shown && ready && !_ready;
            _shown = true;
            _ready = ready;
            KitVisibility.SetShown(group, true);

            if (nameText)
            {
                nameText.text = pilotName;
                nameText.color = UITheme.Resolve(theme, ready ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
            }
            if (stateText)
            {
                stateText.text = ready ? readyWord : waitingWord;
                stateText.color = UITheme.Resolve(theme, ready ? UIColorToken.Cta : UIColorToken.NeutralLightest);
            }
            if (ring) ring.color = ready ? domainColor : UITheme.Resolve(theme, UIColorToken.SurfaceDark);
            if (avatar)
            {
                avatar.sprite = face;
                avatar.enabled = face != null;
                avatar.color = new Color(1f, 1f, 1f, ready ? 1f : waitingAlpha);
            }
            if (punch) CosmicShore.Gameplay.MaelstromTransitions.PulseScale(transform, 0.14f);
        }

        public void Hide()
        {
            _shown = false;
            _ready = false;
            KitVisibility.SetShown(group, false);
        }
    }
}
