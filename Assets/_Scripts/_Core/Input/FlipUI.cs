using UnityEngine;

namespace StarWriter.Core.Input
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
            if (state)
                // Flip Off
                transform.rotation = Quaternion.identity;
            else 
                // Flip On
                transform.rotation = Quaternion.Euler(0,0,180);
        }
    }
}