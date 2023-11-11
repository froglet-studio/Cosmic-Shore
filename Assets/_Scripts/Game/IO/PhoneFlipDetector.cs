using UnityEngine;

namespace CosmicShore.Game.IO
{
    public class PhoneFlipDetector : MonoBehaviour
    {
        [SerializeField] float phoneFlipThreshold = .1f;
        public bool PhoneFlipState;
        public ScreenOrientation currentOrientation;

        public delegate void OnPhoneFlipEvent(bool state);
        public static event OnPhoneFlipEvent onPhoneFlip;

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
                    onPhoneFlip(PhoneFlipState);

                    Debug.Log($"PhoneFlipDetector Phone flip state change detected - new flip state: {PhoneFlipState}");
                }
                else if (UnityEngine.Input.acceleration.y > 0 && !PhoneFlipState)
                {
                    PhoneFlipState = true;
                    currentOrientation = ScreenOrientation.LandscapeRight;
                    onPhoneFlip(PhoneFlipState);

                    Debug.Log($"PhoneFlipDetectorPhone flip state change detected - new flip state: {PhoneFlipState}");
                }
            }
        }
    }
}