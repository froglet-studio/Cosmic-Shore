using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class FlipUI : MonoBehaviour
    {
        void OnEnable()
        {
            PhoneFlipDetector.onPhoneFlip += OnPhoneFlip;
            // A flip that happened while this was inactive (a menu screen switched off during
            // freestyle on MobileLow, a modal opened after the flip) would otherwise be missed.
            if (PhoneFlipDetector.HasFlipState) OnPhoneFlip(PhoneFlipDetector.LastFlipState);
        }

        void OnDisable()
        {
            PhoneFlipDetector.onPhoneFlip -= OnPhoneFlip;
        }
        
        void OnPhoneFlip(bool state)
        {
            transform.rotation = state ? Quaternion.Euler(0, 0, 180) /* Flip On */: Quaternion.identity /* Flip Off */; 
        }
    }
}