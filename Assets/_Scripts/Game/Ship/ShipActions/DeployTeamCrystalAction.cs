using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// • Hold Button-1 → ghost crystal hovers in front of the ship.<br/>
    /// • Release       → crystal is planted, collider + team become active.
    /// </summary>
    public class DeployTeamCrystalAction : ShipAction
    {
        private static readonly int Opacity = Shader.PropertyToID("_opacity");

        [Header("Setup")]
        [SerializeField]
        private Crystal crystalPrefab;
        [SerializeField] private float forwardOffset = 12f;
        [SerializeField] private float fadeValue = 0.5f;
        [Tooltip("Optional terrain mask – leave empty to skip the ray-stop check")]
        [SerializeField]
        private LayerMask rayMask;

        private Crystal _ghostCrystal;
        private Coroutine _followRoutine;

        public override void StartAction()
        {
            if (_ghostCrystal != null) return;      

            var pos = GetSpawnPoint();
            var rot = Quaternion.LookRotation(Ship.Transform.forward, Ship.Transform.up);

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
        }

        private Vector3 GetSpawnPoint()
        {
            var pos = Ship.Transform.position + Ship.Transform.forward * forwardOffset;

            if (rayMask.value != 0 &&
                Physics.Raycast(Ship.Transform.position, Ship.Transform.forward,
                                out var hit, forwardOffset, rayMask,
                                QueryTriggerInteraction.Ignore))
            {
                pos = hit.point;
            }
            return pos;
        }

        private IEnumerator FollowShip()
        {
            while (_ghostCrystal != null)
            {
                _ghostCrystal.transform.SetPositionAndRotation(
                    GetSpawnPoint(),
                    Quaternion.LookRotation(Ship.Transform.forward, Ship.Transform.up));

                yield return null;
            }
        }

        private void PrepareGhost(Crystal cr)
        {
            cr.enabled = false;
            var colliders = cr.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
                col.enabled = false;
            
            var fadeIns = cr.GetComponentsInChildren<FadeIn>(true);
            foreach (var fade in fadeIns)
            {
                fade.enabled = false;                         
                fade.gameObject.GetComponent<Renderer>()
                    .material.SetFloat(Opacity, fadeValue);
            }
        }

        private void ActivateCrystal(Crystal cr)
        {
            cr.OwnTeam = Ship.ShipStatus.Team;

            var colliders = cr.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
                col.enabled = true;
            
            var fadeIns = cr.GetComponentsInChildren<FadeIn>(true);
            foreach (var fade in fadeIns)
            {
                fade.enabled = true;
                // fade.StartFadeIn();                        
            }

            cr.enabled = true;
            cr.ActivateCrystal();                            
        }
    }
}
