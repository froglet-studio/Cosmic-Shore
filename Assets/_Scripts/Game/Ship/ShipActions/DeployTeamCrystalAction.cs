using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    public class DeployTeamCrystalAction : ShipAction
    {
        private static readonly int Opacity = Shader.PropertyToID("_opacity");

        [Header("Setup")]
        [SerializeField] private Crystal crystalPrefab;
        [SerializeField] private float   forwardOffset = 12f;
        [SerializeField] private float   fadeValue     = 0.5f;
        [Tooltip("Optional terrain mask – leave empty to skip the ray-stop check")]
        [SerializeField] private LayerMask rayMask;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 30f; 
        private float _lastUseTime = -Mathf.Infinity;

        private Crystal   _ghostCrystal;
        private Coroutine _followRoutine;

        public override void StartAction()
        {
            // 1️⃣ Check cooldown
            float timeSinceLastUse = Time.time - _lastUseTime;
            if (timeSinceLastUse < cooldown)
            {
                float remaining = cooldown - timeSinceLastUse;
                Debug.Log($"[DeployTeamCrystalAction] Ability on cooldown – {remaining:F1}s left");
                return;
            }

            if (_ghostCrystal != null) return;

            Vector3 pos = GetSpawnPoint();
            Quaternion rot = Quaternion.LookRotation(Ship.Transform.forward, Ship.Transform.up);

            _ghostCrystal = Instantiate(crystalPrefab, pos, rot);

            PrepareGhost(_ghostCrystal);
            _followRoutine = StartCoroutine(FollowShip());
        }

        public override void StopAction()
        {
            if (_ghostCrystal == null) return;

            StopCoroutine(_followRoutine);
            ActivateCrystal(_ghostCrystal);
            _ghostCrystal = null;

            // 2️⃣ Mark ability as used
            _lastUseTime = Time.time;
            Debug.Log($"[DeployTeamCrystalAction] Crystal deployed. Cooldown started ({cooldown}s)");
        }

        Vector3 GetSpawnPoint()
        {
            Vector3 pos = Ship.Transform.position + Ship.Transform.forward * forwardOffset;

            if (rayMask.value != 0 &&
                Physics.Raycast(Ship.Transform.position, Ship.Transform.forward,
                                out RaycastHit hit, forwardOffset, rayMask,
                                QueryTriggerInteraction.Ignore))
            {
                pos = hit.point;
            }
            return pos;
        }

        IEnumerator FollowShip()
        {
            while (_ghostCrystal != null)
            {
                _ghostCrystal.transform.SetPositionAndRotation(
                    GetSpawnPoint(),
                    Quaternion.LookRotation(Ship.Transform.forward, Ship.Transform.up));
                yield return null;
            }
        }

        void PrepareGhost(Crystal cr)
        {
            cr.enabled = false;
            var colliders = cr.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
                col.enabled = false;

            var fadeIns = cr.GetComponentsInChildren<FadeIn>(true);
            foreach (var fade in fadeIns)
            {
                fade.enabled = false;
                fade.gameObject.GetComponent<Renderer>().material.SetFloat(Opacity, fadeValue);
            }
        }

        void ActivateCrystal(Crystal cr)
        {
            cr.OwnTeam = Ship.ShipStatus.Team;

            var colliders = cr.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
                col.enabled = true;

            var fadeIns = cr.GetComponentsInChildren<FadeIn>(true);
            foreach (var fade in fadeIns)
            {
                fade.enabled = true;
                // fade.StartFadeIn();  // optional if you want a fresh fade
            }

            cr.enabled = true;
            cr.ActivateCrystal();
        }
    }
}