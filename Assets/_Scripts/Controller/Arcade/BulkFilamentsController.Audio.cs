using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void EnsureSingleAudioListener()
        {
            AudioListener keeper = FindEnabledAudioListener();
            if (!keeper)
            {
                if (!_mainCamera)
                    _mainCamera = Camera.main;

                if (_mainCamera)
                {
                    keeper = _mainCamera.GetComponent<AudioListener>();
                    if (!keeper)
                        keeper = _mainCamera.gameObject.AddComponent<AudioListener>();
                }
                else if (_runtimeRoot)
                {
                    keeper = _runtimeRoot.GetComponent<AudioListener>();
                    if (!keeper)
                        keeper = _runtimeRoot.AddComponent<AudioListener>();
                }

                if (keeper)
                    keeper.enabled = true;
            }

            if (!keeper)
                return;

            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener && listener != keeper && listener.enabled)
                    listener.enabled = false;
            }
        }

        static AudioListener FindEnabledAudioListener()
        {
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener && listener.enabled)
                    return listener;
            }

            return null;
        }
    }
}
