using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A per-client "gallery" camera used during the vote (and reveal): it takes over the
    /// shared manual replay camera (the AstroLeague goal-replay pattern -
    /// <see cref="CameraManager.BeginManualReplayCamera"/>) and slowly orbits the round's
    /// painting so every player studies the shared creation while casting their answers,
    /// instead of staring at wherever their own vessel happened to stop.
    ///
    /// Local-only presentation - it drives the camera rig transform each frame and restores
    /// the normal gameplay camera on <see cref="Stop"/> (idempotent, also on destroy).
    /// </summary>
    public class FakeArtistGalleryCam : MonoBehaviour
    {
        Transform _rig;
        Vector3 _center;
        float _distance;
        float _angle;
        bool _restored;

        public static FakeArtistGalleryCam Begin(Vector3 center, float radius)
        {
            var cm = CameraManager.Instance;
            if (cm == null) return null;

            var rig = cm.BeginManualReplayCamera();
            if (rig == null) return null;

            var go = new GameObject("FakeArtistGalleryCam");
            var cam = go.AddComponent<FakeArtistGalleryCam>();
            cam._rig = rig;
            cam._center = center;
            cam._distance = Mathf.Max(180f, radius * 2.1f);
            cam._angle = 0f;
            cam.PoseNow(); // frame it immediately (before the first LateUpdate)
            return cam;
        }

        void PoseNow()
        {
            if (_rig == null) return;
            // Look slightly down at the canvas from a slowly-orbiting vantage.
            var dir = Quaternion.Euler(20f, _angle, 0f) * Vector3.back;
            _rig.position = _center + dir * _distance;
            _rig.rotation = Quaternion.LookRotation(_center - _rig.position, Vector3.up);
        }

        void LateUpdate()
        {
            if (_rig == null) return;
            _angle += Time.deltaTime * 7f; // slow orbit for a gallery feel
            PoseNow();
        }

        /// <summary>Restore the normal gameplay camera and tear down (safe to call twice).</summary>
        public void Stop()
        {
            if (!_restored)
            {
                _restored = true;
                CameraManager.Instance?.RestoreGameplayCamera();
            }
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!_restored)
            {
                _restored = true;
                CameraManager.Instance?.RestoreGameplayCamera();
            }
        }
    }
}
