using CosmicShore.Utility;

﻿using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class PhoneFlipDetector : MonoBehaviour
    {
        [SerializeField] float phoneFlipThreshold = .1f;
        public bool PhoneFlipState;
        public ScreenOrientation currentOrientation;

        public delegate void OnPhoneFlipEvent(bool state);
        public static event OnPhoneFlipEvent onPhoneFlip;

        /// <summary>The last flip state raised, for a listener that was inactive when it changed
        /// (<see cref="FlipUI"/> re-applies it on enable). Meaningful once <see cref="HasFlipState"/>.</summary>
        public static bool LastFlipState { get; private set; }

        /// <summary>True once any flip has been raised this session.</summary>
        public static bool HasFlipState { get; private set; }

        // Enter Play Mode runs without a domain reload: a flip from the last session must not answer.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastFlipState = false;
            HasFlipState = false;
        }

        static void Raise(bool state)
        {
            LastFlipState = state;
            HasFlipState = true;
            onPhoneFlip?.Invoke(state);
        }

        void Update()
        {
            DetectPhoneFlip();
        }

        void DetectPhoneFlip()
        {
            //// We don't want the phone flip to flop like a fish out of water if the phone is mostly parallel to the ground
            if (Mathf.Abs(UnityEngine.Input.acceleration.y) >= phoneFlipThreshold)
            {
                if (UnityEngine.Input.acceleration.y < 0 && PhoneFlipState)
                {
                    PhoneFlipState = false;
                    currentOrientation = ScreenOrientation.LandscapeLeft;
                    Raise(PhoneFlipState);

                    CSDebug.LogVerbose(CSLogChannel.Input, $"[PhoneFlipDetector] Phone flip state change - flipState={PhoneFlipState}");
                }
                else if (UnityEngine.Input.acceleration.y > 0 && !PhoneFlipState)
                {
                    PhoneFlipState = true;
                    currentOrientation = ScreenOrientation.LandscapeRight;
                    Raise(PhoneFlipState);

                    CSDebug.LogVerbose(CSLogChannel.Input, $"[PhoneFlipDetector] Phone flip state change - flipState={PhoneFlipState}");
                }
            }
        }
    }
}