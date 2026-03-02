using System;
using CosmicShore.Core;
using CosmicShore.Models;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class QuestItemCard : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Icon")]
        [SerializeField] private Image iconImage;

        [Header("State Visuals")]
        [Tooltip("Shown when the quest is locked (mode not yet unlocked)")]
        [SerializeField] private GameObject lockedOverlay;
        [Tooltip("Shown when the quest is fully complete and claimed")]
        [SerializeField] private GameObject completedOverlay;
        [Tooltip("Button shown when the quest target is met but not yet claimed")]
        [SerializeField] private Button claimButton;

        [Header("Card Background")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color lockedTint = new(0.35f, 0.35f, 0.35f, 1f);

        [Header("Active Quest Glow")]
        [Tooltip("Child with CanvasGroup that pulses on the frontier quest (auto-resolved from 'GlowBorder')")]
        [SerializeField] private CanvasGroup glowBorder;
        [Tooltip("Glow color when the quest is in progress (active frontier)")]
        [SerializeField] private Color activeQuestGlowColor = new(0.4f, 0.7f, 1f, 1f);
        [Tooltip("Glow color when the quest target is met and ready to claim")]
        [SerializeField] private Color readyToClaimGlowColor = new(0.4f, 1f, 0.55f, 1f);

        private Image _glowImage;
        private Color _originalBgColor = Color.white;
        private GameModes _gameMode;
        private SO_GameModeQuestData _questData;
        private QuestItemState _currentState = QuestItemState.Locked;
        private bool _initialStateSet;
        private Tween _pulseTween;
        private Tween _stateTween;

        public GameModes GameMode => _gameMode;
        public bool IsAnimating { get; private set; }

        void Awake() => ResolveReferences();

        void OnDisable()
        {
            _pulseTween?.Kill();
            _stateTween?.Kill();
            _pulseTween = null;
            _stateTween = null;
            IsAnimating = false;
        }

        void ResolveReferences()
        {
            if (lockedOverlay == null)
                lockedOverlay = FindChild("LockedObject") ?? FindChild("LockedIcon");
            if (completedOverlay == null)
                completedOverlay = FindChild("CompletedObject") ?? FindChild("UnlockedObject");
            if (iconImage == null)
            {
                var iconGo = FindChild("Icon");
                if (iconGo != null) iconGo.TryGetComponent(out iconImage);
            }
            if (nameText == null)
                nameText = FindTMP("GameModeText") ?? FindTMP("UnlockableDetail");
            if (descriptionText == null)
                descriptionText = FindTMP("ObjectiveText");
            if (claimButton == null)
            {
                var btnGo = FindChild("ClaimButton") ?? FindChild("CompleteButton");
                if (btnGo != null) btnGo.TryGetComponent(out claimButton);
            }
            if (cardBackground == null)
                TryGetComponent(out cardBackground);
            if (glowBorder == null)
            {
                var glowGo = FindChild("GlowBorder") ?? FindChild("Glow");
                if (glowGo != null) glowGo.TryGetComponent(out glowBorder);
            }
            if (glowBorder != null && _glowImage == null)
                glowBorder.TryGetComponent(out _glowImage);
        }

        GameObject FindChild(string childName)
        {
            var t = transform.Find(childName);
            return t != null ? t.gameObject : null;
        }

        TMP_Text FindTMP(string childName)
        {
            var t = transform.Find(childName);
            return t != null && t.TryGetComponent<TMP_Text>(out var tmp) ? tmp : null;
        }

        public void Configure(SO_GameModeQuestData quest)
        {
            _questData = quest;
            _gameMode = quest.GameMode;
            if (nameText != null) nameText.text = quest.DisplayName;
            if (descriptionText != null) descriptionText.text = quest.IsPlaceholder ? "Coming Soon" : quest.Description;
            if (iconImage != null && quest.Icon != null) iconImage.sprite = quest.Icon;
            if (cardBackground != null) _originalBgColor = cardBackground.color;
        }

        public void SetState(QuestItemState state)
        {
            var prevState = _currentState;
            _currentState = state;
            bool isFirstSet = !_initialStateSet;
            _initialStateSet = true;

            bool isLocked = state == QuestItemState.Locked;
            bool isReadyToClaim = state == QuestItemState.ReadyToClaim ||
                                  (state == QuestItemState.Unlocked && _questData != null && _questData.IsCompleted);
            bool isClaimed = state == QuestItemState.Claimed;

            if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);
            if (completedOverlay != null) completedOverlay.SetActive(isClaimed);
            if (claimButton != null) claimButton.interactable = isReadyToClaim;
            if (cardBackground != null) cardBackground.color = isLocked ? lockedTint : _originalBgColor;
            if (descriptionText != null && isClaimed) descriptionText.text = "Completed";

            // Animate unlock transition (Locked → anything visible), skip on initial spawn
            if (!isFirstSet && prevState == QuestItemState.Locked && !isLocked)
                PlayUnlockAnimation();
        }

        /// <summary>
        /// Starts or stops the pulsing glow border on the active frontier card.
        /// Glow color changes based on quest state: blue when in progress, green when ready to claim.
        /// </summary>
        public void SetActiveFrontier(bool isActive, QuestItemState state = QuestItemState.Unlocked)
        {
            if (glowBorder == null) return;

            if (isActive)
            {
                // Set glow color based on state
                if (_glowImage != null)
                {
                    _glowImage.color = state == QuestItemState.ReadyToClaim
                        ? readyToClaimGlowColor
                        : activeQuestGlowColor;
                }

                glowBorder.gameObject.SetActive(true);
                _pulseTween?.Kill();
                glowBorder.alpha = 1f;
                _pulseTween = glowBorder.DOFade(0.4f, 0.8f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }
            else
            {
                _pulseTween?.Kill();
                _pulseTween = null;
                glowBorder.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Scale-bounce claim animation. Calls onComplete when done.
        /// </summary>
        public void PlayClaimAnimation(Action onComplete)
        {
            _stateTween?.Kill();
            IsAnimating = true;
            var seq = DOTween.Sequence();
            seq.Append(transform.DOScale(1.15f, 0.2f).SetEase(Ease.OutBack));
            seq.Append(transform.DOScale(1f, 0.15f).SetEase(Ease.InOutSine));
            seq.OnComplete(() => { IsAnimating = false; onComplete?.Invoke(); });
            seq.SetUpdate(true);
            _stateTween = seq;
        }

        void PlayUnlockAnimation()
        {
            _stateTween?.Kill();
            IsAnimating = true;
            transform.localScale = Vector3.one * 0.8f;
            _stateTween = transform.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => IsAnimating = false);
        }

        public void SetButtonInteractable(bool interactable)
        {
            if (claimButton != null) claimButton.interactable = interactable;
        }

        public void BindClaimAction(UnityEngine.Events.UnityAction action)
        {
            if (claimButton == null) return;
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(action);
        }
    }

    public enum QuestItemState
    {
        Locked,
        Unlocked,
        ReadyToClaim,
        Claimed,
    }
}
