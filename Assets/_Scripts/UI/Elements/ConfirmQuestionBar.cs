using System;
using CosmicShore.Utility;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A small inline yes/no bar: <see cref="Ask"/> shows a question with accept / cancel and
    /// invokes the callback only on accept. Reusable - it carries no knowledge of what is being
    /// confirmed; the caller supplies the question and the consequence.
    ///
    /// <para>
    /// Animated in and out per the platform's continuity rule (nothing pops in or out): it wipes
    /// open horizontally with its content fading in behind, and closes the same way. The button
    /// icons punch on press. Every tween runs on unscaled time and is <c>SetLink</c>ed to this
    /// object, so a paused timescale or a destroyed panel cannot strand one.
    /// </para>
    ///
    /// <para>
    /// One pending question at a time: a second <see cref="Ask"/> replaces the first rather than
    /// queueing, because a stale question the player has already moved past is worse than no
    /// question. The previous callback is dropped, never invoked.
    /// </para>
    /// </summary>
    public class ConfirmQuestionBar : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField, Tooltip("The question text. Set per-Ask by the caller.")]
        TMP_Text questionLabel;

        [SerializeField] Button acceptButton;
        [SerializeField] Button cancelButton;

        [Header("Layout")]
        [SerializeField, Tooltip("Root scaled/faded by the open-close animation. Defaults to this object.")]
        RectTransform animationRoot;

        [SerializeField, Tooltip("CanvasGroup faded by the animation. Added to the animation root if missing.")]
        CanvasGroup canvasGroup;

        [Header("Feel")]
        [SerializeField, Tooltip("Seconds for the open wipe.")]
        float openSeconds = 0.28f;

        [SerializeField, Tooltip("Seconds for the close wipe.")]
        float closeSeconds = 0.18f;

        [SerializeField, Tooltip("Scale punch applied to a button icon when pressed.")]
        float pressPunch = 0.35f;

        [SerializeField, Tooltip("Seconds for the press punch.")]
        float pressPunchSeconds = 0.25f;

        Action _onAccept;
        Sequence _transition;
        bool _wired;

        /// <summary>True while a question is on screen awaiting an answer.</summary>
        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (animationRoot == null) animationRoot = transform as RectTransform;

            if (canvasGroup == null && animationRoot != null &&
                !animationRoot.TryGetComponent(out canvasGroup))
                canvasGroup = animationRoot.gameObject.AddComponent<CanvasGroup>();

            WireButtons();

            // Author the bar however is convenient in the scene - it closes itself on load
            // rather than requiring the GameObject be left inactive.
            CloseImmediate();
        }

        void OnEnable() => WireButtons();

        void OnDisable()
        {
            if (acceptButton != null) acceptButton.onClick.RemoveListener(HandleAccept);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
            _wired = false;

            _transition?.Kill();
            _transition = null;
        }

        void WireButtons()
        {
            if (_wired) return;

            if (acceptButton != null) acceptButton.onClick.AddListener(HandleAccept);
            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancel);
            _wired = true;
        }

        /// <summary>
        /// Shows <paramref name="question"/> and calls <paramref name="onAccept"/> if the player
        /// accepts. A second call replaces any pending question; the replaced callback is dropped.
        /// </summary>
        public void Ask(string question, Action onAccept)
        {
            _onAccept = onAccept;

            if (questionLabel != null)
                questionLabel.text = question;

            Open();
        }

        /// <summary>Closes without answering. Safe to call when already closed.</summary>
        public void Cancel() => HandleCancel();

        void Open()
        {
            gameObject.SetActive(true);
            IsOpen = true;

            _transition?.Kill();

            if (animationRoot != null)
                animationRoot.localScale = new Vector3(0f, 1f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            _transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (animationRoot != null)
                _transition.Join(animationRoot.DOScaleX(1f, openSeconds).SetEase(Ease.OutBack));
            if (canvasGroup != null)
                _transition.Join(canvasGroup.DOFade(1f, openSeconds * 0.8f).SetEase(Ease.OutQuad));
        }

        void Close()
        {
            if (!IsOpen)
            {
                CloseImmediate();
                return;
            }

            IsOpen = false;
            _transition?.Kill();

            // Stop taking input the instant the answer lands - the close is a flourish, and a
            // flourish must never be able to absorb a second answer.
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (animationRoot != null)
                _transition.Join(animationRoot.DOScaleX(0f, closeSeconds).SetEase(Ease.InBack));
            if (canvasGroup != null)
                _transition.Join(canvasGroup.DOFade(0f, closeSeconds).SetEase(Ease.InQuad));

            _transition.OnComplete(CloseImmediate);
        }

        void CloseImmediate()
        {
            IsOpen = false;
            _onAccept = null;

            if (animationRoot != null)
                animationRoot.localScale = new Vector3(0f, 1f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        void HandleAccept()
        {
            if (!IsOpen) return;

            // Capture BEFORE Close, which clears the field via CloseImmediate.
            var callback = _onAccept;
            Punch(acceptButton);
            Close();

            try
            {
                callback?.Invoke();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ConfirmQuestionBar] Accept handler threw: {e.Message}");
            }
        }

        void HandleCancel()
        {
            if (!IsOpen) return;

            Punch(cancelButton);
            Close();
        }

        void Punch(Button button)
        {
            if (button == null || pressPunch <= 0f) return;

            var target = button.transform;
            target.DOKill();
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * pressPunch, pressPunchSeconds, vibrato: 6, elasticity: 0.6f)
                  .SetUpdate(true)
                  .SetLink(button.gameObject);
        }
    }
}
