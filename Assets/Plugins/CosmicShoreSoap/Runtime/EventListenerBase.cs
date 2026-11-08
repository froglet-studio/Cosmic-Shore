using System;
using System.Threading;
using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Base of the listener components: a scene object that runs authored UnityEvent
    /// responses when a channel is raised.
    /// </summary>
    public abstract class EventListenerBase : MonoBehaviour
    {
        protected enum Binding
        {
            UNTIL_DESTROY = 0,
            UNTIL_DISABLE = 1,
        }

        [Tooltip("Until Destroy: subscribes in Awake and keeps listening while disabled, until destroyed.\n" +
                 "Until Disable: listens only while enabled.")]
        [SerializeField] protected Binding _binding = Binding.UNTIL_DESTROY;

        [Tooltip("Switch the GameObject off right after subscribing, so a hidden panel still hears the event that shows it.")]
        [SerializeField] protected bool _disableAfterSubscribing;

        // Only delayed responses need it, so it is made on first use (no allocation per listener).
        CancellationTokenSource _delayed;

        protected abstract void ToggleRegistration(bool register);

        void Awake()
        {
            if (_binding == Binding.UNTIL_DESTROY) ToggleRegistration(true);
            gameObject.SetActive(!_disableAfterSubscribing);
        }

        void OnEnable()
        {
            if (_binding == Binding.UNTIL_DISABLE) ToggleRegistration(true);
        }

        void OnDisable()
        {
            if (_binding != Binding.UNTIL_DISABLE) return;
            ToggleRegistration(false);
            CancelDelayed();
        }

        void OnDestroy()
        {
            if (_binding != Binding.UNTIL_DESTROY) return;
            ToggleRegistration(false);
            CancelDelayed();
        }

        /// <summary>
        /// Run <paramref name="response"/> after <paramref name="delay"/> seconds of scaled
        /// time, even while this object is inactive. Unsubscribing cancels it.
        /// </summary>
        protected void RespondLater(float delay, Action response)
        {
            _delayed ??= new CancellationTokenSource();
            Wait(delay, response, _delayed.Token);
        }

        static async void Wait(float delay, Action response, CancellationToken token)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(delay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            response();
        }

        void CancelDelayed()
        {
            if (_delayed == null) return;
            _delayed.Cancel();
            _delayed.Dispose();
            _delayed = null;
        }
    }
}
