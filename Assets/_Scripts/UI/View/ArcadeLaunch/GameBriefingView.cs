using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's text block: the card's description, and — underneath it — one play tip
    /// at a time, cycled on a timer.
    ///
    /// <para>Tips are authored per card (<see cref="SO_ArcadeGame.Tips"/>) and are advice, not
    /// lore: the description says what the mode IS, a tip says what to DO about it. A card with no
    /// tips shows the description alone — the tip line is switched off rather than left showing an
    /// empty "Tip:" prefix, because an empty label reads as a missing string.</para>
    ///
    /// <para>Shared by both launch panels (minigame and Maelstrom) — the briefing is the one part
    /// of the two layouts that is genuinely identical, so it is one component rather than two.</para>
    /// </summary>
    public class GameBriefingView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField, Tooltip("The card's Description. Left alone when the card has none.")]
        TMP_Text descriptionText;

        [SerializeField, Tooltip("The rotating tip line. Its whole GameObject is switched off " +
                                 "when the card authors no tips.")]
        TMP_Text tipText;

        [Header("Tip rotation")]
        [SerializeField, Tooltip("Prefix written in front of every tip. Set empty for none.")]
        string tipPrefix = "Tip: ";

        [SerializeField, Tooltip("Seconds each tip holds before the next one. A single-tip card " +
                                 "never rotates, whatever this says.")]
        [Min(1f)] float tipDwellSeconds = 6f;

        [SerializeField, Tooltip("Seconds the tip takes to fade out and back in between tips. " +
                                 "0 swaps instantly.")]
        [Min(0f)] float tipFadeSeconds = 0.35f;

        readonly List<string> _tips = new();
        int _tipIndex;
        float _tipTimer;
        float _fadeTimer;
        bool _fadingOut;

        /// <summary>Fill the block from a card. Safe to call with null (clears the block).</summary>
        public void Show(SO_ArcadeGame game)
        {
            Show(game ? game.Description : null, game ? game.Tips : null);
        }

        /// <summary>The general form, so a caller with its own copy can use the same block.</summary>
        public void Show(string description, IReadOnlyList<string> tips)
        {
            if (descriptionText)
                descriptionText.text = description ?? string.Empty;

            _tips.Clear();
            if (tips != null)
            {
                foreach (var tip in tips)
                    if (!string.IsNullOrWhiteSpace(tip))
                        _tips.Add(tip.Trim());
            }

            _tipIndex = 0;
            _tipTimer = 0f;
            _fadeTimer = 0f;
            _fadingOut = false;

            if (!tipText) return;

            bool hasTips = _tips.Count > 0;
            tipText.gameObject.SetActive(hasTips);
            if (hasTips)
            {
                SetTipAlpha(1f);
                WriteCurrentTip();
            }
        }

        /// <summary>Clear the block — used when no card is selected.</summary>
        public void Clear() => Show(null, null);

        void OnDisable()
        {
            // A panel that comes back should start on the first tip at full alpha, not resume
            // mid-fade on whatever tip happened to be up when it was hidden.
            _tipIndex = 0;
            _tipTimer = 0f;
            _fadeTimer = 0f;
            _fadingOut = false;
            if (tipText && tipText.gameObject.activeSelf)
            {
                SetTipAlpha(1f);
                WriteCurrentTip();
            }
        }

        void Update()
        {
            if (_tips.Count <= 1 || !tipText || !tipText.gameObject.activeInHierarchy) return;

            // Unscaled: the launch panel can be open while the menu holds timeScale at 0.
            float dt = Time.unscaledDeltaTime;

            if (_fadeTimer > 0f)
            {
                _fadeTimer -= dt;
                float remaining01 = tipFadeSeconds > 0f ? Mathf.Clamp01(_fadeTimer / tipFadeSeconds) : 0f;
                SetTipAlpha(_fadingOut ? remaining01 : 1f - remaining01);

                if (_fadeTimer <= 0f && _fadingOut)
                {
                    // Halfway: the old tip is gone, so swap the string and fade the new one in.
                    _fadingOut = false;
                    _tipIndex = (_tipIndex + 1) % _tips.Count;
                    WriteCurrentTip();
                    _fadeTimer = tipFadeSeconds;
                }
                return;
            }

            _tipTimer += dt;
            if (_tipTimer < tipDwellSeconds) return;

            _tipTimer = 0f;
            if (tipFadeSeconds <= 0f)
            {
                _tipIndex = (_tipIndex + 1) % _tips.Count;
                WriteCurrentTip();
                return;
            }

            _fadingOut = true;
            _fadeTimer = tipFadeSeconds;
        }

        void WriteCurrentTip()
        {
            if (!tipText || _tips.Count == 0) return;
            tipText.text = tipPrefix + _tips[Mathf.Clamp(_tipIndex, 0, _tips.Count - 1)];
        }

        void SetTipAlpha(float alpha)
        {
            if (!tipText) return;
            var c = tipText.color;
            tipText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }
    }
}
