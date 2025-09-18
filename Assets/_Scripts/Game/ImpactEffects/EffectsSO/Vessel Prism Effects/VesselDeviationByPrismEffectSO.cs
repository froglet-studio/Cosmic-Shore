using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselDeviationByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselDeviationByPrismEffectSO")]
    public class VesselDeviationByPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Deviation")]
        [SerializeField] private float deviationAngle;  
        [Range(0.05f, 0.95f)]
        [SerializeField] private float slerpBlend;     

        [SerializeField] private float cooldownSeconds;

        private static readonly Dictionary<int, float> _lastAppliedAt = new();

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor?.Vessel?.VesselStatus;
            if (status == null || status.IsStationary) return;

            var t = status.ShipTransform;
            if (t == null) return;

            var id = t.GetInstanceID();
            var now = Time.time;
            if (_lastAppliedAt.TryGetValue(id, out float lastTime))
            {
                if (now - lastTime < Mathf.Max(0f, cooldownSeconds)) return;
            }
            _lastAppliedAt[id] = now;

            var sign = (Random.value < 0.5f) ? -1f : 1f;
            var angle = deviationAngle * sign;

            /*if (scaleAngleBySpeed)
            {
                float k = Mathf.Clamp01(speedForFullAngle > 0f ? status.Speed / speedForFullAngle : 1f);
                angle *= k;
            }*/

            var target = Quaternion.AngleAxis(angle, t.up) * t.rotation;

            var blend = Mathf.Clamp01(slerpBlend);
            t.rotation = Quaternion.Slerp(t.rotation, target, blend);
        }
    }
}
