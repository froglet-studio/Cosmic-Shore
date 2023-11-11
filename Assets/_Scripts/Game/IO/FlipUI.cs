using UnityEngine;

namespace CosmicShore.Game.IO
{
    public class FlipUI : MonoBehaviour
    {
        void OnEnable()
        {
            PhoneFlipDetector.onPhoneFlip += OnPhoneFlip;
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