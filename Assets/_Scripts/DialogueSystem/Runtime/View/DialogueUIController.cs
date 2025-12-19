using CosmicShore.DialogueSystem.Models;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace CosmicShore.DialogueSystem.View
{
    public class DialogueUIController : MonoBehaviour
    {
        [SerializeField] private DialogueUIPrefabRefs dialogueUIPrefab;
        [SerializeField] private Transform contentParent;

        private GameObject _currentInstance;
        private DialogueUIPrefabRefs _instanceRefs;
        private Animator _animator;
        private Action _onNextLine;

        private bool _monologuePanelEntered;
        private bool _leftPanelEntered;
        private bool _rightPanelEntered;
        private bool _isLeftActive;

        // Called by DialogueManager for each monologue line
        public void ShowMonologue(DialogueSet set, DialogueLine line, Action onNextLine)
        {
            // Instantiate once
            if (_currentInstance == null)
            {
                _currentInstance = Instantiate(dialogueUIPrefab.gameObject, contentParent);
                _instanceRefs = _currentInstance.GetComponent<DialogueUIPrefabRefs>();
                _animator = _currentInstance.GetComponent<Animator>();

                _instanceRefs.OnAnimInComplete += () =>
                {
                    StartTypewriter(_instanceRefs.MonologueDialogText, line.text,
                        () => _instanceRefs.nextButton.gameObject.SetActive(true));
                    _instanceRefs.OnAnimInComplete = null;
                };

                // Wire Next button
                _instanceRefs.nextButton.onClick.AddListener(OnMonologueNextClicked);
            }

            _onNextLine = onNextLine;

            _instanceRefs.MonologueSpeakerText.text = line.speakerName;
            _instanceRefs.MonologuePortrait.sprite = set.portraitSpeaker1;
            _instanceRefs.MonologueDialogText.text = "";

            WireNextButton(onNextLine);

            if (!_monologuePanelEntered)
            {
                _animator.Play("MonologuePopout");
                _monologuePanelEntered = true;
            }
            else
            {
                StartTypewriter(_instanceRefs.MonologueDialogText, line.text,
                    () => _instanceRefs.nextButton.gameObject.SetActive(true));
            }
        }

        private void OnMonologueNextClicked()
        {
            _instanceRefs.nextButton.onClick.RemoveListener(OnMonologueNextClicked);

            WaitingForNextPressed = true;

            _onNextLine?.Invoke();
        }


        // Called by DialogueManager for each dialogue line
        public void ShowDialogue(DialogueSet set, DialogueLine line, Action onNextLine, bool isLeft)
        {
            // Instantiate once
            if (_currentInstance == null)
            {
                _currentInstance = Instantiate(dialogueUIPrefab.gameObject, contentParent);
                _instanceRefs = _currentInstance.GetComponent<DialogueUIPrefabRefs>();
                _animator = _currentInstance.GetComponent<Animator>();

                // Hook Anim-Event ? typewriter
                _instanceRefs.OnAnimInComplete += () =>
                {
                    if (_isLeftActive)
                    {
                        StartTypewriter(_instanceRefs.leftDialogueText, line.text,
                            () => _instanceRefs.nextButton.gameObject.SetActive(true));
                    }
                    else
                    {
                        StartTypewriter(_instanceRefs.rightDialogueText, line.text,
                            () => _instanceRefs.nextButton.gameObject.SetActive(true));
                    }
                    _instanceRefs.OnAnimInComplete = null;
                };

                // Wire Next button
                _instanceRefs.nextButton.onClick.AddListener(OnDialogueNextClicked);
            }


            _onNextLine = onNextLine;
            _isLeftActive = isLeft;

            // Hide opposite side
            if (isLeft)
            {
                _instanceRefs.rightSpeakerRoot.gameObject.SetActive(false);
                _instanceRefs.rightBox.gameObject.SetActive(false);
            }
            else
            {
                _instanceRefs.leftSpeakerRoot.gameObject.SetActive(false);
                _instanceRefs.leftBox.gameObject.SetActive(false);
            }

            // Fill data & hide text until anim-in
            if (isLeft)
            {
                _instanceRefs.leftSpeakerName.text = line.speakerName;
                _instanceRefs.leftPortrait.sprite = set.portraitSpeaker1;
                _instanceRefs.leftDialogueText.text = "";
                _instanceRefs.leftDialogueText.gameObject.SetActive(false);
            }
            else
            {
                _instanceRefs.rightSpeakerName.text = line.speakerName;
                _instanceRefs.rightPortrait.sprite = set.portraitSpeaker2;
                _instanceRefs.rightDialogueText.text = "";
                _instanceRefs.rightDialogueText.gameObject.SetActive(false);
            }

            _instanceRefs.nextButton.gameObject.SetActive(false);
            _instanceRefs.skipButton.gameObject.SetActive(true);

            WireNextButton(onNextLine);
            _instanceRefs.nextButton.gameObject.SetActive(false);

            // First time this side? play PopOut, else start typewriter
            if (isLeft)
            {
                if (!_leftPanelEntered)
                {
                    _animator.Play("DialoguePopOut");
                    _leftPanelEntered = true;
                }
                else
                {
                    StartTypewriter(_instanceRefs.leftDialogueText, line.text,
                        () => _instanceRefs.nextButton.gameObject.SetActive(true));
                }
            }
            else
            {
                if (!_rightPanelEntered)
                {
                    _animator.Play("DialoguePopOut");
                    _rightPanelEntered = true;
                }
                else
                {
                    StartTypewriter(_instanceRefs.rightDialogueText, line.text,
                        () => _instanceRefs.nextButton.gameObject.SetActive(true));
                }
            }
        }

        private void OnDialogueNextClicked()
        {
            _instanceRefs.nextButton.onClick.RemoveListener(OnDialogueNextClicked);

            // ? NEW: let the manager know we clicked Next
            WaitingForNextPressed = true;

            _onNextLine?.Invoke();
        }

        private void WireNextButton(Action onNextLine)
        {
            WaitingForNextPressed = false;
            _instanceRefs.nextButton.onClick.RemoveAllListeners();
            _instanceRefs.nextButton.onClick.AddListener(() =>
            {
                WaitingForNextPressed = true;
                onNextLine?.Invoke();
                _instanceRefs.nextButton.gameObject.SetActive(false);
            });
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

        /// <summary>
        /// Plays the appropriate �pop-in� (exit) animation, then cleans up.
        /// </summary>
        public void HideWithAnimation(bool wasMonologue, Action onHidden)
        {
            // Subscribe to the AnimationEvent callback
            _instanceRefs.OnAnimOutComplete += () =>
            {
                Destroy(_currentInstance);
                _currentInstance = null;
                _instanceRefs = null;

                _monologuePanelEntered = false;
                _leftPanelEntered = false;
                _rightPanelEntered = false;

                onHidden?.Invoke();
            };

            _animator.Play(wasMonologue ? "MonologuePopIn" : "DialoguePopIn");
        }



        // Plug in your typewriter, or use this stub
        private void StartTypewriter(TMP_Text target, string text, Action onComplete)
        {
            target.text = "";
            StartCoroutine(Typewriter(target, text, onComplete));
            //onComplete?.Invoke();
        }

        private IEnumerator Typewriter(TMP_Text textDisplay, string text, Action onComplete)
        {
            textDisplay.text = "";
            foreach (char c in text)
            {
                textDisplay.text += c;
                yield return new WaitForSecondsRealtime(0.04f);
            }
            // Only now, after _all_ characters are drawn:
            onComplete?.Invoke();
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