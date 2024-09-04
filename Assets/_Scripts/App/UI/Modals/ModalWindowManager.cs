using System.Collections;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Header("Settings")]
        public bool sharpAnimations;

        Animator windowAnimator;
        bool isOn;

        protected virtual void Start()
        {
            windowAnimator = GetComponent<Animator>();
        }

        public void ModalWindowIn()
        {
            gameObject.SetActive(true);

            if (isOn == false)
            {
                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window In", 0.1f);
                else
                    windowAnimator.Play("Window In");

                isOn = true;
            }
        }

        public void ModalWindowOut()
        {
            if (isOn)
            {
                if (sharpAnimations == false)
                    windowAnimator.CrossFade("Window Out", 0.1f);
                else
                    windowAnimator.Play("Window Out");

                isOn = false;
            }

            StartCoroutine(DisableWindow());
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }
    }
}