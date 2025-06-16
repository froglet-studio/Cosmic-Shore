using CosmicShore.DialogueSystem.Models;
using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace CosmicShore.DialogueSystem.View
{
    public class DialogueUIController : MonoBehaviour
    {
        [Header("UI Prefab References")]
        [SerializeField] private DialogueUIPrefabRefs dialogueUIPrefab; // Assign your prefab here
        [SerializeField] private Transform contentParent; // Where to parent the instantiated prefab

        private DialogueUIPrefabRefs _instanceRefs;
        private GameObject _currentInstance;

        // Animation config (can expose in inspector)
        public float speakerOffset = 700f, speakerMoveDur = 0.45f, boxScaleDur = 0.35f, overshoot = 1.05f, overshootDur = 0.12f;
        public Ease speakerEase = Ease.OutQuart, boxEase = Ease.OutBack;

        private bool _monologuePanelEntered = false;
        private bool _leftPanelEntered = false;
        private bool _rightPanelEntered = false;
        private bool _isLeftActive = true;

        public void ShowMonologue(DialogueSet set, DialogueLine line, Action onNextLine)
        {
            CleanupPrefab();

            _currentInstance = Instantiate(dialogueUIPrefab.gameObject, contentParent);
            _instanceRefs = _currentInstance.GetComponent<DialogueUIPrefabRefs>();

            // Hide right side
            _instanceRefs.rightSpeakerRoot.gameObject.SetActive(false);
            _instanceRefs.rightBox.gameObject.SetActive(false);

            // Set left side text/sprite
            _instanceRefs.leftSpeakerName.text = line.speakerName;
            _instanceRefs.leftDialogueText.text = "";
            _instanceRefs.leftPortrait.sprite = set.portraitSpeaker1;
            _instanceRefs.nextButton.onClick.AddListener(OnNextClicked);

            // First line: animate panel from 'from' to 'to', then animate box and typewriter
            if (!_monologuePanelEntered)
            {
                Debug.Log("Panel not already in place, just animate box + typewriter");
                _instanceRefs.leftSpeakerRoot.anchoredPosition = _instanceRefs.moveFromLeft.anchoredPosition;
                _instanceRefs.leftSpeakerRoot.gameObject.SetActive(true);

                _instanceRefs.leftSpeakerRoot.DOAnchorPos(_instanceRefs.moveToLeft.anchoredPosition, speakerMoveDur)
                    .SetEase(speakerEase)
                    .OnComplete(() =>
                    {
                        AnimateBoxAndTypewriter(false);
                        _monologuePanelEntered = true;
                    });
            }
            else
            {
                // 
                Debug.Log("Panel already in place, just animate box + typewriter");
                _instanceRefs.leftSpeakerRoot.anchoredPosition = _instanceRefs.moveToLeft.anchoredPosition;
                _instanceRefs.leftSpeakerRoot.gameObject.SetActive(true);

                AnimateBoxAndTypewriter(true);
            }

            void AnimateBoxAndTypewriter(bool useOnlyTypewriter)
            {
                if (!useOnlyTypewriter)
                {
                    // Animate width (not scale), hide text until done
                    _instanceRefs.leftDialogueText.gameObject.SetActive(false);

                    var box = _instanceRefs.leftBox;
                    float targetWidth = 824f; // Set your desired width

                    // Set box width to 0, then animate to target
                    var size = box.sizeDelta;
                    box.sizeDelta = new Vector2(0f, size.y);
                    box.gameObject.SetActive(true);

                    DOTween.To(
                        () => box.sizeDelta.x,
                        x => box.sizeDelta = new Vector2(x, size.y),
                        targetWidth,
                        boxScaleDur
                    )
                    .SetEase(boxEase)
                    .OnComplete(() =>
                    {
                        _instanceRefs.leftDialogueText.gameObject.SetActive(true);
                        StartTypewriter(_instanceRefs.leftDialogueText, line.text, () => _instanceRefs.nextButton.gameObject.SetActive(true));
                    });
                }
                else
                {
                    // Box already at target width, just typewriter
                    _instanceRefs.leftBox.gameObject.SetActive(true);
                    _instanceRefs.leftDialogueText.gameObject.SetActive(true);
                    StartTypewriter(_instanceRefs.leftDialogueText, line.text, () => _instanceRefs.nextButton.gameObject.SetActive(true));
                }
            }


            _instanceRefs.nextButton.gameObject.SetActive(true);
        }


        public void ShowDialogue(DialogueSet set, DialogueLine line, Action onNextLine, bool isLeft)
        {
            CleanupPrefab();

            _currentInstance = Instantiate(dialogueUIPrefab.gameObject, contentParent);
            _instanceRefs = _currentInstance.GetComponent<DialogueUIPrefabRefs>();

            // Decide side and ensure the other is hidden
            _isLeftActive = isLeft;
            if (isLeft)
            {
                _instanceRefs.rightSpeakerRoot.gameObject.SetActive(false);
                _instanceRefs.rightBox.gameObject.SetActive(false);

                _instanceRefs.leftSpeakerName.text = line.speakerName;
                _instanceRefs.leftDialogueText.text = "";
                _instanceRefs.leftPortrait.sprite = set.portraitSpeaker1;

                // Reset box scale X to 0
                _instanceRefs.leftBox.localScale = new Vector3(0, 1, 1);

                // If it's the first time, animate panel in
                if (!_leftPanelEntered)
                {
                    _instanceRefs.leftSpeakerRoot.anchoredPosition = _instanceRefs.moveFromLeft.anchoredPosition;
                    _instanceRefs.leftSpeakerRoot.gameObject.SetActive(true);

                    _instanceRefs.leftSpeakerRoot.DOAnchorPos(_instanceRefs.moveToLeft.anchoredPosition, speakerMoveDur)
                        .SetEase(speakerEase)
                        .OnComplete(() =>
                        {
                            AnimateBoxAndTypewriter();
                            _leftPanelEntered = true;
                        });
                }
                else
                {
                    _instanceRefs.leftSpeakerRoot.anchoredPosition = _instanceRefs.moveToLeft.anchoredPosition;
                    _instanceRefs.leftSpeakerRoot.gameObject.SetActive(true);
                    AnimateBoxAndTypewriter();
                }

                void AnimateBoxAndTypewriter()
                {
                    _instanceRefs.leftBox.localScale = new Vector3(0, 1, 1);
                    _instanceRefs.leftBox.gameObject.SetActive(true);

                    _instanceRefs.leftBox.DOScaleX(1f, boxScaleDur)
                        .SetEase(boxEase)
                        .OnComplete(() =>
                        {
                            StartTypewriter(_instanceRefs.leftDialogueText, line.text, () => _instanceRefs.nextButton.gameObject.SetActive(true));
                        });
                }
            }
            else // RIGHT
            {
                _instanceRefs.leftSpeakerRoot.gameObject.SetActive(false);
                _instanceRefs.leftBox.gameObject.SetActive(false);

                _instanceRefs.rightSpeakerName.text = line.speakerName;
                _instanceRefs.rightDialogueText.text = "";
                _instanceRefs.rightPortrait.sprite = set.portraitSpeaker2;

                // Reset box scale X to 0
                _instanceRefs.rightBox.localScale = new Vector3(0, 1, 1);

                // If it's the first time, animate panel in
                if (!_rightPanelEntered)
                {
                    _instanceRefs.rightSpeakerRoot.anchoredPosition = _instanceRefs.moveFromRight.anchoredPosition;
                    _instanceRefs.rightSpeakerRoot.gameObject.SetActive(true);

                    _instanceRefs.rightSpeakerRoot.DOAnchorPos(_instanceRefs.moveToRight.anchoredPosition, speakerMoveDur)
                        .SetEase(speakerEase)
                        .OnComplete(() =>
                        {
                            AnimateBoxAndTypewriter();
                            _rightPanelEntered = true;
                        });
                }
                else
                {
                    _instanceRefs.rightSpeakerRoot.anchoredPosition = _instanceRefs.moveToRight.anchoredPosition;
                    _instanceRefs.rightSpeakerRoot.gameObject.SetActive(true);
                    AnimateBoxAndTypewriter();
                }


                void AnimateBoxAndTypewriter()
                {
                    _instanceRefs.rightBox.localScale = new Vector3(0, 1, 1);
                    _instanceRefs.rightBox.gameObject.SetActive(true);

                    _instanceRefs.rightBox.DOScaleX(1f, boxScaleDur)
                        .SetEase(boxEase)
                        .OnComplete(() =>
                        {
                            StartTypewriter(_instanceRefs.rightDialogueText, line.text, () => _instanceRefs.nextButton.gameObject.SetActive(true));
                        });
                }
            }
            _instanceRefs.nextButton.gameObject.SetActive(false);
            _instanceRefs.skipButton.gameObject.SetActive(true);
        }


        public void Hide()
        {
            CleanupPrefab();
        }

        private void CleanupPrefab()
        {
            if (_currentInstance != null)
            {
                Destroy(_currentInstance);
                _currentInstance = null;
                _instanceRefs = null;
            }
            // Reset flags so next PlaySequence starts panel-in again
            _leftPanelEntered = false;
            _rightPanelEntered = false;
        }


        // Plug in your typewriter, or use this stub
        private void StartTypewriter(TMP_Text target, string text, Action onComplete)
        {
            target.text = text;
            StartCoroutine(Typewriter(target, text));
            onComplete?.Invoke();
        }

        private IEnumerator Typewriter(TMP_Text textDisplay, string text)
        {
            textDisplay.text = "";

            //if (typingAudioSource && typingLoopClip)
            //{
            //    typingAudioSource.clip = typingLoopClip;
            //    typingAudioSource.loop = false;
            //    typingAudioSource.Play();
            //}

            foreach (char c in text)
            {
                textDisplay.text += c;
                yield return new WaitForSecondsRealtime(0.04f);
            }

            //StopTypingAudio();
            //_isTyping = false;
        }

        public bool WaitingForNextPressed { get; private set; }

        public void OnNextClicked()
        {
            WaitingForNextPressed = true;
            Debug.Log("On Next Pressed");
        }

        public void ResetWaitingForNext() => WaitingForNextPressed = false;

    }

}
