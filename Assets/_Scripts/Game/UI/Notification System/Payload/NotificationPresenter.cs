using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public sealed class NotificationPresenter : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private ScriptableEventNotificationPayload channel;
        [SerializeField] private NotificationSettingsSO settings;

        [Header("Hierarchy")]
        [SerializeField] private RectTransform parent;       // where prefab is instantiated under HUD
        [SerializeField] private NotificationView viewPrefab; // your top-right anchored prefab

        // runtime
        readonly Queue<NotificationPayload> _queue = new();
        Coroutine _runner;
        NotificationView _viewInstance;
        Sequence _activeSeq;

        void OnEnable()
        {
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            if (channel) channel.OnRaised += Enqueue;
        }

        void OnDisable()
        {
            if (channel) channel.OnRaised -= Enqueue;
            if (_runner != null) { StopCoroutine(_runner); _runner = null; }
            _queue.Clear();
            KillSeq();
        }

        void KillSeq()
        {
            if (_activeSeq != null && _activeSeq.IsActive())
            {
                _activeSeq.Kill();
                _activeSeq = null;
            }
        }

        void EnsureInstance()
        {
            if (_viewInstance != null) return;
            if (!viewPrefab) { Debug.LogError("[NotificationPresenter] Missing viewPrefab"); return; }

            var go = Instantiate(viewPrefab, parent ? parent : transform as RectTransform);
            _viewInstance = go;
            // Ensure bindings
            if (_viewInstance.container == null)
                _viewInstance.container = _viewInstance.GetComponent<RectTransform>();
            if (_viewInstance.fitHelper == null)
                _viewInstance.fitHelper = _viewInstance.GetComponent<CanvasFitHelper>();
            if (_viewInstance.canvasGroup == null)
                _viewInstance.canvasGroup = _viewInstance.GetComponent<CanvasGroup>();
        }

        void Enqueue(NotificationPayload p)
        {
            while (settings && _queue.Count >= settings.maxQueue && _queue.Count > 0)
                _queue.Dequeue();

            _queue.Enqueue(p);
            if (_runner == null) _runner = StartCoroutine(RunQueue());
        }

        IEnumerator RunQueue()
        {
            while (_queue.Count > 0)
            {
                var p = _queue.Dequeue();
                yield return ShowOnce(p);
            }
            _runner = null;
        }

        IEnumerator ShowOnce(NotificationPayload p)
        {
            EnsureInstance();
            if (_viewInstance == null || settings == null) yield break;

            var v  = _viewInstance;
            var rt = v.container;
            var cg = v.canvasGroup;

            // bind text
            v.Bind(p);

            // compute positions using current on-screen pos from the prefab
            var showPos  = rt.anchoredPosition; // designer-defined on-screen position
            var startPos = v.fitHelper
                ? v.fitHelper.GetOffscreenPos(showPos, settings.inFrom, settings.inPadding)
                : showPos;

            var exitPos  = v.fitHelper
                ? v.fitHelper.GetOffscreenPos(showPos, settings.outTo, settings.outPadding)
                : showPos;

            // prepare
            KillSeq();
            rt.anchoredPosition = startPos;

            if (settings.fade && cg) cg.alpha = settings.startAlpha;
            if (settings.scale) v.transform.localScale = settings.startScale;

            // build sequence
            _activeSeq = DOTween.Sequence();
            if (settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            // IN
            _activeSeq.Join(rt.DOAnchorPos(showPos, settings.inDuration).SetEase(settings.inEase));
            if (settings.fade && cg)
                _activeSeq.Join(cg.DOFade(settings.endAlpha, settings.inDuration));
            if (settings.scale)
                _activeSeq.Join(v.transform.DOScale(settings.endScale, settings.inDuration));

            // HOLD
            _activeSeq.AppendInterval(settings.holdDuration);

            // OUT
            _activeSeq.Append(rt.DOAnchorPos(exitPos, settings.outDuration).SetEase(settings.outEase));
            if (settings.fade && cg)
                _activeSeq.Join(cg.DOFade(settings.startAlpha, settings.outDuration));
            if (settings.scale)
                _activeSeq.Join(v.transform.DOScale(settings.startScale, settings.outDuration));

            yield return _activeSeq.WaitForCompletion();
            _activeSeq = null;

            // snap back to showPos so repeated calls always enter from valid baseline
            rt.anchoredPosition = showPos;
        }
    }
}
