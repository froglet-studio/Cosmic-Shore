using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's text block: what the mode IS, and — cycling through the same line — what
    /// to DO about it.
    ///
    /// <para><b>One text field, not two.</b> The description and the tips are the same voice
    /// answering the same question at different depths, so they take turns in one place rather than
    /// stacking into a wall of copy beside a preview window. A second line held permanently would
    /// also mean authoring for the worst case: a card with four tips would need four lines of space
    /// that a card with none leaves empty.</para>
    ///
    /// <para>The rotation is the description first — a player who has just opened the card is asking
    /// "what is this?" before "how do I play it?" — then each tip, then back to the description.
    /// A card with no tips never rotates and simply shows its description, which is the correct
    /// resting state rather than a degraded one.</para>
    ///
    /// <para>Shared by both launch panels. The briefing is the one part of the two layouts that is
    /// genuinely identical, so it is one component rather than two.</para>
    /// </summary>
    public class GameBriefingView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField, Tooltip("The ONE line the block cycles through: the card's description, " +
                                 "then each of its tips, then back. There is deliberately no " +
                                 "second field - see the class summary.")]
        TMP_Text bodyText;

        [Header("Rotation")]
        [SerializeField, Tooltip("Prefix written in front of a TIP (never in front of the " +
                                 "description). Empty for none.")]
        string tipPrefix = "Tip: ";

        [SerializeField, Tooltip("Seconds each entry holds before the next. A card with no tips " +
                                 "never rotates, whatever this says.")]
        [Min(1f)] float dwellSeconds = 6f;

        [SerializeField, Tooltip("Seconds of crossfade between entries. 0 swaps instantly.")]
        [Min(0f)] float fadeSeconds = 0.4f;

        [SerializeField, Tooltip("Extra seconds the DESCRIPTION holds over a tip, so the card's " +
                                 "own words are what a player mostly sees. 0 treats it as one " +
                                 "entry among the tips.")]
        [Min(0f)] float descriptionExtraDwell = 3f;

        readonly List<string> _entries = new();
        int _index;
        float _dwellTimer;
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
            _entries.Clear();

            // Index 0 is always the description when there is one - it is what the player is
            // asking for at the moment the card opens.
            if (!string.IsNullOrWhiteSpace(description))
                _entries.Add(description.Trim());

            if (tips != null)
            {
                foreach (var tip in tips)
                    if (!string.IsNullOrWhiteSpace(tip))
                        _entries.Add(tipPrefix + tip.Trim());
            }

            ResetRotation();
        }

        /// <summary>Clear the block — used when no card is selected.</summary>
        public void Clear() => Show(null, null);

        void OnDisable()
        {
            // A panel that comes back starts on the description at full alpha, not resumed
            // mid-fade on whatever entry happened to be up when it was hidden.
            ResetRotation();
        }

        void ResetRotation()
        {
            _index = 0;
            _dwellTimer = 0f;
            _fadeTimer = 0f;
            _fadingOut = false;

            if (!bodyText) return;

            bodyText.gameObject.SetActive(_entries.Count > 0);
            SetAlpha(1f);
            WriteCurrent();
        }

        void Update()
        {
            if (_entries.Count <= 1 || !bodyText || !bodyText.gameObject.activeInHierarchy) return;

            // Unscaled: the launch panel can be open while the menu holds timeScale at 0.
            float dt = Time.unscaledDeltaTime;

            if (_fadeTimer > 0f)
            {
                _fadeTimer -= dt;
                float remaining01 = fadeSeconds > 0f ? Mathf.Clamp01(_fadeTimer / fadeSeconds) : 0f;
                SetAlpha(_fadingOut ? remaining01 : 1f - remaining01);

                if (_fadeTimer <= 0f && _fadingOut)
                {
                    // Halfway: the old entry is gone, so swap the string and fade the new one in.
                    _fadingOut = false;
                    Advance();
                    _fadeTimer = fadeSeconds;
                }
                return;
            }

            _dwellTimer += dt;
            if (_dwellTimer < CurrentDwell) return;

            _dwellTimer = 0f;
            if (fadeSeconds <= 0f)
            {
                Advance();
                return;
            }

            _fadingOut = true;
            _fadeTimer = fadeSeconds;
        }

        /// <summary>The description earns longer than a tip: it is the card's own answer.</summary>
        float CurrentDwell => _index == 0 ? dwellSeconds + descriptionExtraDwell : dwellSeconds;

        void Advance() { _index = (_index + 1) % _entries.Count; WriteCurrent(); }

        void WriteCurrent()
        {
            if (!bodyText || _entries.Count == 0) return;
            bodyText.text = _entries[Mathf.Clamp(_index, 0, _entries.Count - 1)];
        }

        void SetAlpha(float alpha)
        {
            if (!bodyText) return;
            var c = bodyText.color;
            bodyText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }
    }
}
