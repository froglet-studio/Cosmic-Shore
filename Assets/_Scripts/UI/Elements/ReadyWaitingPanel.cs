using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The state between pressing READY and the countdown, in a multiplayer match
    /// (Docs/MENU_REDESIGN_B.md section 22.2; the HudReady board). Before it existed, a pilot who
    /// pressed Ready saw the button vanish and nothing else until the last pilot pressed - the one
    /// signal was a toast, so "is it broken or are we waiting on somebody?" had no answer.
    ///
    /// <para>The panel names who is still picking: a count ("2 / 3") and one chip per human pilot,
    /// lit in its domain colour once that pilot is ready. Readiness arrives on the same SOAP
    /// channel the match feed reads (<see cref="GameToastSituation.PlayerReady"/>, raised on every
    /// client by the controller's NotifyPlayerReady RPC), so the panel and the feed cannot
    /// disagree.</para>
    ///
    /// <para>Shown by <see cref="MiniGameHUD"/> when the local pilot presses Ready and more than
    /// one human is flying; hidden when the countdown starts (<see cref="Gameplay.CountdownTimer"/>)
    /// or the Ready button comes back for a new round.</para>
    /// </summary>
    public class ReadyWaitingPanel : MonoBehaviour
    {
        public readonly struct Pilot
        {
            public readonly string Name;
            public readonly Sprite Avatar;
            public readonly Color DomainColor;

            public Pilot(string name, Sprite avatar, Color domainColor)
            {
                Name = name;
                Avatar = avatar;
                DomainColor = domainColor;
            }
        }

        [Header("Wiring")]
        [Tooltip("Shows and hides the panel (KitVisibility).")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("'2 / 3' - ready pilots over human pilots.")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("The roster row the chips are cloned into.")]
        [SerializeField] private RectTransform roster;
        [Tooltip("Template chip, cloned once per human pilot. It sits OUTSIDE the roster at alpha 0, " +
                 "so it never takes a layout slot and nothing has to be deactivated.")]
        [SerializeField] private ReadyWaitingChip chipTemplate;
        [Tooltip("Resources/Channels/GameToastChannel - the match feed's channel. PlayerReady " +
                 "raises on it on every client.")]
        [SerializeField] private ScriptableEventGameToastData toastChannel;

        [Header("Look")]
        [SerializeField] private string readyWord = "Ready";
        [SerializeField] private string waitingWord = "Picking";
        [Tooltip("A waiting pilot's ring.")]
        [SerializeField] private UIColorToken waitingRingToken = UIColorToken.SurfaceDark;
        [Tooltip("A ready pilot's state word.")]
        [SerializeField] private UIColorToken readyTextToken = UIColorToken.Cta;
        [Tooltip("A waiting pilot's state word.")]
        [SerializeField] private UIColorToken waitingTextToken = UIColorToken.NeutralLightest;
        [Tooltip("Alpha of a pilot who has not pressed Ready yet.")]
        [Range(0f, 1f)]
        [SerializeField] private float waitingAlpha = 0.5f;

        private readonly List<ReadyWaitingChip> _chips = new();
        private readonly List<Pilot> _pilots = new();
        private readonly HashSet<string> _ready = new();
        private bool _active;

        public bool IsShowing => _active;

        private void Awake() => KitVisibility.SetShown(canvasGroup, false);

        private void OnEnable() => toastChannel.OnRaised += OnToast;

        private void OnDisable() => toastChannel.OnRaised -= OnToast;

        /// <summary>The local pilot pressed Ready: show the roster with them already lit.</summary>
        public void Begin(IReadOnlyList<Pilot> pilots, string localName)
        {
            _pilots.Clear();
            for (int i = 0; i < pilots.Count; i++) _pilots.Add(pilots[i]);
            // A press that arrived before the panel opened (the local pilot's own, or a fast
            // remote one) is kept - the set is cleared only by Hide.
            if (!string.IsNullOrEmpty(localName)) _ready.Add(localName);
            BuildChips();
            _active = true;
            KitVisibility.SetShown(canvasGroup, true);
            Redraw();
        }

        public void MarkReady(string pilotName)
        {
            if (string.IsNullOrEmpty(pilotName) || !_ready.Add(pilotName)) return;
            if (_active) Redraw();
        }

        /// <summary>The countdown started, or a new round brought the Ready button back.</summary>
        public void Hide()
        {
            _active = false;
            _ready.Clear();
            KitVisibility.SetShown(canvasGroup, false);
        }

        private void OnToast(GameToastData data)
        {
            if (data.Situation != GameToastSituation.PlayerReady || data.Args == null || data.Args.Length == 0) return;
            MarkReady(data.Args[0]);
        }

        private void BuildChips()
        {
            if (!chipTemplate || !roster) return;
            while (_chips.Count < _pilots.Count)
                _chips.Add(Instantiate(chipTemplate, roster));
            // The roster only shrinks when a pilot leaves between two presses; drop the spare.
            for (int i = _chips.Count - 1; i >= _pilots.Count; i--)
            {
                if (_chips[i]) Destroy(_chips[i].gameObject);
                _chips.RemoveAt(i);
            }
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].Bind(_pilots[i].Name, _pilots[i].Avatar);
        }

        private void Redraw()
        {
            int ready = 0;
            for (int i = 0; i < _pilots.Count; i++)
            {
                bool isReady = _ready.Contains(_pilots[i].Name);
                if (isReady) ready++;
                if (i >= _chips.Count) continue;
                _chips[i].Draw(isReady,
                    isReady ? _pilots[i].DomainColor : UITheme.Resolve(null, waitingRingToken),
                    UITheme.Resolve(null, isReady ? readyTextToken : waitingTextToken),
                    isReady ? readyWord : waitingWord,
                    isReady ? 1f : waitingAlpha);
            }
            if (countText)
                countText.text = TabularText.Wrap($"{ready} / {_pilots.Count}", countText.font);
        }
    }
}
