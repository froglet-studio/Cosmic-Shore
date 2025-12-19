using System;
using System.Collections;
using CosmicShore.DialogueSystem.Models;
using TMPro;
using UnityEngine;

namespace CosmicShore.DialogueSystem.View
{
    public sealed class MainMenuDialogueView : MonoBehaviour, IDialogueView
    {
        [SerializeField] DialogueUIPrefabRefs prefab;
        [SerializeField] Transform contentParent;

        GameObject _instance;
        DialogueUIPrefabRefs _refs;
        Animator _animator;

        Action _currentOnLineComplete;
        bool _monologuePanelEntered;
        bool _leftPanelEntered;
        bool _rightPanelEntered;

        public void ShowDialogueSet(DialogueSet set)
        {
            if (_instance == null)
            {
                _instance = Instantiate(prefab.gameObject, contentParent);
                _refs = _instance.GetComponent<DialogueUIPrefabRefs>();
                _animator = _instance.GetComponent<Animator>();

                _refs.nextButton.onClick.AddListener(OnNextClicked);
                _refs.skipButton.onClick.AddListener(OnSkipClicked);
            }

            _instance.SetActive(true);
        }

        public void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete)
        {
            _currentOnLineComplete = onLineComplete;
            _refs.nextButton.gameObject.SetActive(false);

            if (set.mode == DialogueModeType.Monologue)
                ShowMonologueLine(set, line);
            else
                ShowDialogueLine(set, line);
        }

        void ShowMonologueLine(DialogueSet set, DialogueLine line)
        {
            _refs.MonologueSpeakerText.text = line.speakerName;
            _refs.MonologuePortrait.sprite = set.portraitSpeaker1;
            _refs.MonologueDialogText.text = "";

            if (!_monologuePanelEntered)
            {
                _refs.OnAnimInComplete = () =>
                {
                    StartTypewriter(_refs.MonologueDialogText, line.text,
                        () => _refs.nextButton.gameObject.SetActive(true));
                    _refs.OnAnimInComplete = null;
                };

                _animator.Play("MonologuePopOut");
                _monologuePanelEntered = true;
            }
            else
            {
                StartTypewriter(_refs.MonologueDialogText, line.text,
                    () => _refs.nextButton.gameObject.SetActive(true));
            }
        }

        void ShowDialogueLine(DialogueSet set, DialogueLine line)
        {
            bool isLeft = ResolveSide(line) == DialogueSide.Left;
            _refs.nextButton.gameObject.SetActive(false);

            if (isLeft)
            {
                _refs.leftSpeakerName.text = line.speakerName;
                _refs.leftPortrait.sprite = set.portraitSpeaker1;
                _refs.leftDialogueText.text = "";
                _refs.leftDialogueText.gameObject.SetActive(true);
                _refs.rightSpeakerRoot.gameObject.SetActive(false);
            }
            else
            {
                _refs.rightSpeakerName.text = line.speakerName;
                _refs.rightPortrait.sprite = set.portraitSpeaker2;
                _refs.rightDialogueText.text = "";
                _refs.rightDialogueText.gameObject.SetActive(true);
                _refs.leftSpeakerRoot.gameObject.SetActive(false);
            }

            if ((isLeft && !_leftPanelEntered) || (!isLeft && !_rightPanelEntered))
            {
                _refs.OnAnimInComplete = () =>
                {
                    var text = isLeft ? _refs.leftDialogueText : _refs.rightDialogueText;
                    StartTypewriter(text, line.text,
                        () => _refs.nextButton.gameObject.SetActive(true));
                    _refs.OnAnimInComplete = null;
                };

                _animator.Play("DialoguePopOut");

                if (isLeft) _leftPanelEntered = true;
                else _rightPanelEntered = true;
            }
            else
            {
                var text = isLeft ? _refs.leftDialogueText : _refs.rightDialogueText;
                StartTypewriter(text, line.text,
                    () => _refs.nextButton.gameObject.SetActive(true));
            }
        }

        DialogueSide ResolveSide(DialogueLine line)
        {
            if (line.side != DialogueSide.Auto)
                return line.side;

            return line.speaker switch
            {
                DialogueSpeaker.Speaker1 => DialogueSide.Left,
                DialogueSpeaker.Speaker2 => DialogueSide.Right,
                _ => DialogueSide.Left
            };
        }

        void OnNextClicked()
        {
            _refs.nextButton.gameObject.SetActive(false);
            _currentOnLineComplete?.Invoke();
            _currentOnLineComplete = null;
        }

        void OnSkipClicked()
        {
            // For now, skip = advance immediately
            _currentOnLineComplete?.Invoke();
            _currentOnLineComplete = null;
        }

        public void Hide(Action onHidden)
        {
            if (_instance == null)
            {
                onHidden?.Invoke();
                return;
            }

            _refs.OnAnimOutComplete = () =>
            {
                _instance.SetActive(false);
                _monologuePanelEntered = false;
                _leftPanelEntered = false;
                _rightPanelEntered = false;
                onHidden?.Invoke();
            };

            bool wasMonologue = true; // For main menu maybe always monologue; adjust if needed.
            _animator.Play(wasMonologue ? "MonologuePopIn" : "DialoguePopIn");
        }

        void StartTypewriter(TMP_Text target, string text, Action onComplete)
        {
            StopAllCoroutines();
            StartCoroutine(TypewriterRoutine(target, text, onComplete));
        }

        IEnumerator TypewriterRoutine(TMP_Text target, string text, Action onComplete)
        {
            target.text = "";
            foreach (char c in text)
            {
                target.text += c;
                yield return new WaitForSecondsRealtime(0.04f);
            }
            onComplete?.Invoke();
        }
    }
}
